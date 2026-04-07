using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.DB.Structure;

/// <summary>
/// Keeps sleeves snapped to the wall/pipe intersection whenever a pipe is moved.
/// Tracks pipe movement delta and applies the same delta to associated sleeves.
/// </summary>
public class PipeSleeveUpdater : IUpdater
{
    private readonly UpdaterId _updaterId;

    public PipeSleeveUpdater(AddInId addInId)
    {
        _updaterId = new UpdaterId(addInId,
            new Guid("ABCDEF12-3456-7890-ABCD-123456789000"));
    }

    // ─── IUpdater ────────────────────────────────────────────────────────────

    public UpdaterId GetUpdaterId() => _updaterId;
    public string GetUpdaterName() => "Pipe Sleeve Updater";
    public string GetAdditionalInformation() => "Moves sleeves when their host pipe is repositioned.";
    public ChangePriority GetChangePriority() => ChangePriority.MEPFixtures;

    public void Execute(UpdaterData data)
    {
        Document doc = data.GetDocument();

        // Collect all sleeve instances once
        List<FamilyInstance> allSleeves = new FilteredElementCollector(doc)
            .OfClass(typeof(FamilyInstance))
            .Cast<FamilyInstance>()
            .Where(fi => fi.LookupParameter("PipeId") != null)
            .ToList();

        foreach (ElementId pipeId in data.GetModifiedElementIds())
        {
            try
            {
                Pipe pipe = doc.GetElement(pipeId) as Pipe;
                if (pipe == null) continue;

                LocationCurve pipeLoc = pipe.Location as LocationCurve;
                if (pipeLoc == null) continue;

                Curve pipeCurve = pipeLoc.Curve;
                string pipeIdStr = pipeId.ToString();

                // ── Find sleeves that belong to this pipe ──────────────────
                List<FamilyInstance> sleeves = allSleeves
                    .Where(s => s.LookupParameter("PipeId")?.AsString() == pipeIdStr)
                    .ToList();

                // ── Compute true pipe axis as unbounded line ───────────────
                Line pipeLine = pipeCurve as Line;
                if (pipeLine == null) continue; // only support straight segments

                XYZ p0 = pipeLine.GetEndPoint(0);
                XYZ p1 = pipeLine.GetEndPoint(1);
                XYZ dir = (p1 - p0).Normalize();
                if (dir.IsZeroLength()) continue;
                
                Line unboundLine = Line.CreateUnbound(p0, dir);

                // ── Align every sleeve to the pipe axis ────────────────────
                foreach (FamilyInstance sleeve in sleeves)
                {
                    LocationPoint loc = sleeve.Location as LocationPoint;
                    if (loc == null) continue;

                    XYZ origin = loc.Point;

                    // IUpdater runs mid-regeneration, so BoundingBox is unreliable.
                    // We must calculate the visual center manually from the origin.
                    // For this family, origin is at the bottom, so center is UP by Height/2.
                    double height = 0;
                    
                    // Comprehensive list of possible size parameters
                    string[] sizeParams = { "Height", "H", "Opening Height", "Diameter", "Opening Diameter", "d", "D", "Size", "Width", "W", "B", "Opening Width" };
                    foreach (string paramName in sizeParams)
                    {
                        // Check instance parameters
                        Parameter p = sleeve.LookupParameter(paramName);
                        if (p != null && p.HasValue)
                        {
                            height = p.AsDouble();
                            break;
                        }
                        
                        // Check type parameters
                        ElementType type = doc.GetElement(sleeve.GetTypeId()) as ElementType;
                        if (type != null)
                        {
                            Parameter pt = type.LookupParameter(paramName);
                            if (pt != null && pt.HasValue)
                            {
                                height = pt.AsDouble();
                                break;
                            }
                        }
                    }

                    // Up vector is global Z
                    XYZ visualCenter = origin + XYZ.BasisZ * (height / 2.0);

                    // Project the true center onto the pipe line
                    IntersectionResult result = unboundLine.Project(visualCenter);
                    if (result == null) continue;

                    XYZ closestPointOnPipe = result.XYZPoint;
                    XYZ translation = closestPointOnPipe - visualCenter;

                    // Only move if significantly misaligned (to prevent endless loops)
                    if (translation.GetLength() > 0.001) // 0.001 ft = ~0.3mm
                    {
                        ElementTransformUtils.MoveElement(doc, sleeve.Id, translation);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PipeSleeveUpdater] {ex.Message}");
            }
        }
    }
}