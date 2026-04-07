using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;

[Transaction(TransactionMode.Manual)]
public class PlaceSleevesAtWallIntersection : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        UIDocument uidoc = commandData.Application.ActiveUIDocument;
        Document doc = uidoc.Document;

        // ── 1. Find sleeve family symbol ────────────────────────────────────────
        FamilySymbol sleeveSymbol = new FilteredElementCollector(doc)
            .OfClass(typeof(FamilySymbol))
            .OfCategory(BuiltInCategory.OST_GenericModel)
            .Cast<FamilySymbol>()
            .FirstOrDefault(x => x.Name.Contains("OPN_Rectangular Cut"))!;

        if (sleeveSymbol == null)
        {
            TaskDialog.Show("Error", "Sleeve family 'C5053-STR-CIRCULAR' not found.");
            return Result.Failed;
        }

        // ── 2. Collect pipes & walls ────────────────────────────────────────────
        List<Pipe> pipes = new FilteredElementCollector(doc)
            .OfClass(typeof(Pipe))
            .Cast<Pipe>()
            .ToList();

        List<Wall> walls = new FilteredElementCollector(doc)
            .OfClass(typeof(Wall))
            .Cast<Wall>()
            .ToList();

        int placed = 0, skipped = 0;

        using (Transaction t = new Transaction(doc, "Place Pipe Sleeves"))
        {
            t.Start();

            if (!sleeveSymbol.IsActive)
                sleeveSymbol.Activate();

            foreach (Pipe pipe in pipes)
            {
                LocationCurve pipeLoc = pipe.Location as LocationCurve;
                if (pipeLoc == null) { skipped++; continue; }

                Curve pipeCurve = pipeLoc.Curve;
                double pipeOD = pipe.get_Parameter(
                    BuiltInParameter.RBS_PIPE_OUTER_DIAMETER)?.AsDouble() ?? 0;

                foreach (Wall wall in walls)
                {
                    try
                    {
                        Solid wallSolid = GetFirstSolid(wall);
                        if (wallSolid == null) continue;

                        SolidCurveIntersection hit =
                            wallSolid.IntersectWithCurve(pipeCurve,
                                new SolidCurveIntersectionOptions());

                        if (hit == null || hit.SegmentCount == 0) continue;

                        // Midpoint of the through-wall segment (centered in wall)
                        XYZ insertPt = hit.GetCurveSegment(0).Evaluate(0.5, true);

                        XYZ wallNorm = GetWallNormal(wall);

                        // Place without level constraint for full XYZ control
                        FamilyInstance sleeve = doc.Create.NewFamilyInstance(
                            insertPt, sleeveSymbol,
                            StructuralType.NonStructural);

                        // ✅ Align to wall normal to be flush with wall face
                        OrientSleeveToWall(doc, sleeve, insertPt, wallNorm);

                        sleeve.LookupParameter("PipeId")?.Set(pipe.Id.ToString());

                        // ✅ Set dimensions based on wall and pipe
                        SetSleeveParameters(sleeve, pipeOD, wall.Width);

                        // ✅ Re-center: move sleeve so its bounding-box center
                        //    aligns with the pipe-wall intersection point
                        doc.Regenerate();
                        BoundingBoxXYZ bb = sleeve.get_BoundingBox(null);
                        if (bb != null)
                        {
                            XYZ bbCenter = (bb.Min + bb.Max) / 2.0;
                            XYZ correction = insertPt - bbCenter;
                            if (!correction.IsAlmostEqualTo(XYZ.Zero))
                                ElementTransformUtils.MoveElement(doc, sleeve.Id, correction);
                        }

                        placed++;
                    }
                    catch (Exception ex)
                    {
                        skipped++;
                        System.Diagnostics.Debug.WriteLine(
                            $"[PlaceSleeves] pipe {pipe.Id} / wall {wall.Id}: {ex.Message}");
                    }
                }

            }

            t.Commit();
        }

        // ── 3. Register the PipeSleeveUpdater so sleeves follow pipe moves ──
        try
        {
            var updater = new PipeSleeveUpdater(commandData.Application.ActiveAddInId);
            if (UpdaterRegistry.IsUpdaterRegistered(updater.GetUpdaterId()))
            {
                UpdaterRegistry.UnregisterUpdater(updater.GetUpdaterId(), doc);
            }
            
            UpdaterRegistry.RegisterUpdater(updater, doc, true);

            // Trigger on any geometry change of ANY Pipe element globally
            UpdaterRegistry.AddTrigger(
                updater.GetUpdaterId(),
                new ElementCategoryFilter(BuiltInCategory.OST_PipeCurves),
                Element.GetChangeTypeGeometry());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[PlaceSleeves] Updater registration failed: {ex.Message}");
        }

        TaskDialog.Show("Done",
            $"✅ Placed : {placed}\n⚠️ Skipped: {skipped}");

        return Result.Succeeded;
    }

    public static void SetSleeveParameters(FamilyInstance sleeve, double pipeOD, double wallWidth)
    {
        // Try setting depth/thickness to wall width
        var thicknessParams = new[] { "Thickness", "Depth", "L", "Length", "Wall Thickness" };
        foreach (var pName in thicknessParams)
        {
            var p = sleeve.LookupParameter(pName);
            if (p != null && !p.IsReadOnly)
            {
                p.Set(wallWidth);
                break;
            }
        }

        // Try setting width/height to pipe diameter + clearance (approx 50mm = 0.164 ft)
        double size = pipeOD + 0.164; 
        var sizeParams = new[] { "Width", "Height", "W", "H", "B", "Opening Width", "Opening Height" };
        foreach (var pName in sizeParams)
        {
            var p = sleeve.LookupParameter(pName);
            if (p != null && !p.IsReadOnly)
            {
                p.Set(size);
            }
        }

        // Also try Diameter if it's a circular one
        var diaParams = new[] { "Diameter", "Opening Diameter", "d", "D" };
        foreach (var pName in diaParams)
        {
            var p = sleeve.LookupParameter(pName);
            if (p != null && !p.IsReadOnly)
            {
                p.Set(size);
                break;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  ORIENT TO WALL FACE
    // ═══════════════════════════════════════════════════════════════════════════
    public static void OrientSleeveToWall(Document doc, FamilyInstance sleeve, XYZ origin, XYZ wallNorm)
    {
        XYZ flatNorm = new XYZ(wallNorm.X, wallNorm.Y, 0.0);
        if (flatNorm.IsZeroLength()) return;
        flatNorm = flatNorm.Normalize();

        XYZ facing = sleeve.FacingOrientation;
        XYZ facingFlat = new XYZ(facing.X, facing.Y, 0.0);
        if (facingFlat.IsZeroLength()) facingFlat = XYZ.BasisX;
        facingFlat = facingFlat.Normalize();

        double angle = facingFlat.AngleTo(flatNorm);
        if (Math.Abs(angle) > 1e-4)
        {
            double sign = facingFlat.X * flatNorm.Y - facingFlat.Y * flatNorm.X;
            if (sign < 0) angle = -angle;

            ElementTransformUtils.RotateElement(
                doc, sleeve.Id,
                Line.CreateUnbound(origin, XYZ.BasisZ),
                angle);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  GEOMETRY HELPERS
    // ═══════════════════════════════════════════════════════════════════════════

    public static Solid GetFirstSolid(Element element)
    {
        Options opt = new Options
        {
            ComputeReferences = true,
            IncludeNonVisibleObjects = false,
            DetailLevel = ViewDetailLevel.Fine
        };

        GeometryElement geo = element.get_Geometry(opt);
        if (geo == null) return null;

        foreach (GeometryObject obj in geo)
        {
            if (obj is Solid s && s.Volume > 1e-6) return s;
            if (obj is GeometryInstance gi)
                foreach (GeometryObject inner in gi.GetInstanceGeometry())
                    if (inner is Solid si && si.Volume > 1e-6) return si;
        }
        return null;
    }

    public static XYZ GetWallNormal(Wall wall)
    {
        if (!(((wall.Location as LocationCurve)?.Curve) is Line line))
            return XYZ.BasisY;

        XYZ axis = (line.GetEndPoint(1) - line.GetEndPoint(0)).Normalize();
        XYZ n = axis.CrossProduct(XYZ.BasisZ).Normalize();
        return wall.Flipped ? -n : n;
    }

    private XYZ GetCurveDirection(Curve curve)
    {
        XYZ d = curve.GetEndPoint(1) - curve.GetEndPoint(0);
        return d.IsZeroLength() ? XYZ.BasisX : d.Normalize();
    }

    private Level GetNearestLevel(Document doc, double z)
    {
        return new FilteredElementCollector(doc)
            .OfClass(typeof(Level))
            .Cast<Level>()
            .OrderBy(lv => Math.Abs(lv.Elevation - z))
            .FirstOrDefault()!;
    }
}