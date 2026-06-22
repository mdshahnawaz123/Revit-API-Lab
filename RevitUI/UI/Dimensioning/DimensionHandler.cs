using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using RevitUI.UI.Dimensioning;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RevitUI.UI.Dimensioning
{
    public class DimensionHandler : IExternalEventHandler
    {
        public DimMode Mode { get; set; } = DimMode.Grids;
        public double OffsetMm { get; set; } = 300;
        public bool IncludeHost { get; set; } = true;
        public bool IncludeLinked { get; set; } = false;
        public bool SameGroup { get; set; } = false;
        public bool MultiTierGrids { get; set; } = true;
        public bool WallCoreOnly { get; set; } = false;
        /// <summary>When false, room boundaries use finish location; when true, core center.</summary>
        public bool RoomUseCenterBoundary { get; set; } = false;
        public string OpeningDimMode { get; set; } = "Faces"; // "Faces" or "Centers"
        public ElementId DimensionStyleId { get; set; } = ElementId.InvalidElementId;
        public bool UseSelection { get; set; } = false;
        private UIApplication _app;
        private void Log(string msg) { }

        public void Execute(UIApplication app)
        {
            _app = app;
            Document doc = app.ActiveUIDocument.Document;
            View view = doc.ActiveView;
            int createdCount = 0;
            Log($"═══ Dimension Diagnostic Log ═══");
            Log($"Time: {DateTime.Now:HH:mm:ss}");
            Log($"Mode: {Mode} → {GetModeDisplayName(Mode)}");
            Log($"View: '{view.Name}' (Type: {view.ViewType})");
            Log($"OffsetMm: {OffsetMm} | DimStyle: {DimensionStyleId}");
            Log($"IncludeHost: {IncludeHost} | IncludeLinked: {IncludeLinked}");
            Log($"UseSelection: {UseSelection} | SameGroup: {SameGroup}");
            Log($"MultiTierGrids: {MultiTierGrids} | WallCoreOnly: {WallCoreOnly}");
            Log($"OpeningDimMode: {OpeningDimMode}");
            Log("");

            using (Transaction trans = new Transaction(doc, "Dimension Automation"))
            {
                trans.Start();
                try
                {
                    if (!IsSupportedDimensionView(view))
                    {
                        Log($"✗ BLOCKED: View type '{view.ViewType}' is not supported.");
                        Log($"  Supported: FloorPlan, CeilingPlan, EngineeringPlan, AreaPlan, Section, Elevation, Detail");
                        TaskDialog.Show("B-Lab",
                            "Dimension Automation works in floor plans, sections, and elevations.\n" +
                            "Open a 2D view (not a 3D view) and try again.");
                        trans.RollBack();
                        return;
                    }
                    Log($"✓ View type '{view.ViewType}' is supported.");

                    if (view.ViewType == ViewType.ThreeD)
                    {
                        Log($"✗ BLOCKED: 3D view detected.");
                        TaskDialog.Show("B-Lab", "Switch to a plan, section, or elevation view before creating dimensions.");
                        trans.RollBack();
                        return;
                    }

                    if (UseSelection)
                    {
                        var selIds = app.ActiveUIDocument.Selection.GetElementIds();
                        Log($"[Selection] Selection mode ON → {selIds.Count} element(s) selected.");
                        if (selIds.Count == 0)
                        {
                            Log($"✗ BLOCKED: No elements selected.");
                            TaskDialog.Show("B-Lab", "Please select elements before running the tool.");
                            trans.RollBack();
                            return;
                        }
                        foreach (var id in selIds)
                        {
                            var el = doc.GetElement(id);
                            Log($"  • {el?.GetType().Name}: {el?.Name} (Id: {id})");
                        }
                    }
                    else
                    {
                        Log($"[Scope] Host={IncludeHost}, Linked={IncludeLinked}");
                    }
                    Log("");

                    switch (Mode)
                    {
                        case DimMode.Grids:
                            createdCount = DimensionGrids(doc, view);
                            break;
                        case DimMode.WallsFull:
                            createdCount = DimensionWallsFull(doc, view);
                            break;
                        case DimMode.Walls:
                            createdCount = DimensionWalls(doc, view);
                            break;
                        case DimMode.Rooms:
                            createdCount = DimensionRooms(doc, view);
                            break;
                        case DimMode.MEP:
                            createdCount = DimensionMEP(doc, view);
                            break;
                        case DimMode.Columns:
                            createdCount = DimensionColumns(doc, view);
                            break;
                        case DimMode.CurtainWalls:
                            createdCount = DimensionCurtainWalls(doc, view);
                            break;
                    }
                    
                    if (createdCount > 0)
                    {
                        trans.Commit();
                        Log($"\n✓ SUCCESS: {createdCount} dimension(s) created and committed.");
                        TaskDialog.Show("B-Lab", $"Successfully created {createdCount} dimension(s).");
                    }
                    else
                    {
                        trans.RollBack();
                        Log($"\n✗ FAILED: No dimensions created for mode '{GetModeDisplayName(Mode)}'.");
                        TaskDialog.Show("B-Lab", $"No dimensions were created for mode: {GetModeDisplayName(Mode)}.");
                    }
                }
                catch (Exception ex)
                {
                    trans.RollBack();
                    Log($"\n✗ EXCEPTION: {ex.Message}");
                    Log($"StackTrace: {ex.StackTrace}");
                    TaskDialog.Show("Dimension Error", "Process failed: " + ex.Message);
                }
            }
        }

        private int DimensionWalls(Document doc, View view)
        {
            Log($"[Walls] ── Wall Openings Mode ──");
            int count = 0;
            var targets = GetWallDimTargets(doc);
            Log($"[Walls] Found {targets.Count} wall target(s)");
            if (targets.Count == 0) { Log($"[Walls] ✗ No walls found in scope. Aborting."); return 0; }

            foreach (var t in targets)
                Log($"  • Wall: '{t.Wall.Name}' (Id:{t.Wall.Id}) | Source: {(t.Link != null ? "Linked" : "Host")} | Filter: {(t.OpeningFilter != null ? t.OpeningFilter.Count + " ids" : "all")}");

            IEnumerable<IEnumerable<WallDimTarget>> groups = SameGroup
                ? targets.GroupBy(t => t.Wall.GroupId).Select(g => g.AsEnumerable())
                : new[] { targets };

            foreach (var group in groups)
            {
                foreach (var target in group)
                {
                    var insertIds = GetWallInsertIds(target.Wall, target.OpeningFilter);
                    Log($"  Wall '{target.Wall.Name}': {insertIds.Count} insert(s)");
                    if (insertIds.Count == 0)
                    {
                        int r = CreateWallOpeningDimension(doc, view, target.Wall, target.Link, null);
                        Log($"    → Wall-only dim: {(r > 0 ? "✓" : "✗")}");
                        count += r;
                        continue;
                    }

                    foreach (ElementId insertId in insertIds)
                    {
                        int r = CreateWallOpeningDimension(doc, view, target.Wall, target.Link, insertId);
                        Log($"    → Insert {insertId}: {(r > 0 ? "✓" : "✗")}");
                        count += r;
                    }
                }
            }
            Log($"[Walls] Total created: {count}");
            return count;
        }

        private int DimensionWallsFull(Document doc, View view)
        {
            Log($"[WallsFull] ── Wall Full Mode ──");
            int count = 0;
            var targets = GetWallDimTargets(doc);
            Log($"[WallsFull] Found {targets.Count} wall target(s)");
            if (targets.Count == 0) return 0;

            // Track dimension line signatures to avoid creating duplicate dimensions
            // when two walls share the same alignment (e.g., Wall A intersects Wall B
            // and Wall B intersects Wall A → same dimension placed twice).
            var createdDimSignatures = new HashSet<string>();

            // Track dimension line positions to catch collinear walls producing overlapping dims
            var createdDimLines = new List<(XYZ Dir, XYZ Start, XYZ End)>();

            foreach (var target in targets)
            {
                int r = CreateWallFullDimension(doc, view, target.Wall, target.Link, target.OpeningFilter, createdDimSignatures, createdDimLines);
                Log($"    → Wall Full dim: {(r > 0 ? '✓' : '✗')}");
                count += r;
            }
            Log($"[WallsFull] Total created: {count}");
            return count;
        }

        private int CreateWallFullDimension(Document doc, View view, Wall wall, RevitLinkInstance? link, HashSet<ElementId>? filter, HashSet<string> createdDimSignatures, List<(XYZ Dir, XYZ Start, XYZ End)> createdDimLines)
        {
            ReferenceArray refs = new ReferenceArray();

            Curve? wallCurve = (wall.Location as LocationCurve)?.Curve;
            if (wallCurve is not Line line) return 0;

            XYZ dir = line.Direction.Normalize();
            if (link != null) dir = link.GetTotalTransform().OfVector(dir);
            dir = ProjectVectorToView(view, dir).Normalize();
            XYZ perp = GetPlanPerpendicular(dir, view);

            // 1. Add Inserts/Openings
            AddInsertReferences(wall, refs, link, view, line, dir, perp, filter);

            // 2. Add Intersecting Grids
            AddIntersectingGridReferences(doc, view, wall, link, refs, line, dir, perp);

            // 3. Add Intersecting Walls
            AddIntersectingWallReferences(doc, view, wall, link, refs, line, dir, perp);

            // 4. Add Wall Ends (Faces)
            // Get end faces by extracting geometry and looking for faces parallel to 'perp'
            Options opt = new Options { ComputeReferences = true, View = view };
            try
            {
                GeometryElement? ge = wall.get_Geometry(opt);
                if (ge != null)
                {
                    foreach (GeometryObject go in ge)
                    {
                        if (go is Solid solid && solid.Faces.Size > 0)
                        {
                            foreach (Face face in solid.Faces)
                            {
                                if (face is PlanarFace pf && pf.Reference != null)
                                {
                                    XYZ n = pf.FaceNormal.Normalize();
                                    if (link != null) n = link.GetTotalTransform().OfVector(n).Normalize();
                                    if (Math.Abs(n.DotProduct(dir)) > 0.9)
                                    {
                                        Reference r = pf.Reference;
                                        if (link != null) r = r.CreateLinkReference(link);
                                        try { refs.Append(r); } catch { }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            refs = DeduplicateReferences(doc, refs, view, perp);
            if (refs.Size < 2) return 0;

            // ── Compute a signature from the reference set to detect duplicates ──
            // Two walls that share the same set of intersection references would produce
            // the same dimension. Skip if this exact reference combination already exists.
            var stableReps = new List<string>();
            for (int i = 0; i < refs.Size; i++)
            {
                try
                {
                    string stable = refs.get_Item(i).ConvertToStableRepresentation(doc) ?? "";
                    if (!string.IsNullOrEmpty(stable))
                        stableReps.Add(stable);
                }
                catch { }
            }
            stableReps.Sort(StringComparer.Ordinal);
            string signature = string.Join("|", stableReps);

            if (!string.IsNullOrEmpty(signature) && !createdDimSignatures.Add(signature))
            {
                Log($"        [WallFull] ⚠ Duplicate dimension skipped (same refs) for wall '{wall.Name}' (Id:{wall.Id})");
                return 0;
            }

            double offsetFeet = OffsetMm / 304.8;
            Line dimLine = Line.CreateBound(
                FlattenToView(view, line.GetEndPoint(0) + perp * offsetFeet),
                FlattenToView(view, line.GetEndPoint(1) + perp * offsetFeet));

            // ── Spatial dedup: check if a dimension already exists along this same line ──
            // Catches collinear or overlapping wall segments that produce different references
            // but visually identical dimension lines.
            XYZ dimDir = dimLine.Direction.Normalize();
            XYZ dimMid = (dimLine.GetEndPoint(0) + dimLine.GetEndPoint(1)) * 0.5;
            double collinearTol = 0.15; // ~45mm tolerance for "same line" detection

            foreach (var existing in createdDimLines)
            {
                // Check if directions are parallel
                if (Math.Abs(existing.Dir.DotProduct(dimDir)) < 0.95) continue;

                // Check if the midpoints are roughly on the same line
                // (perpendicular distance between the two dim lines)
                XYZ delta = dimMid - existing.Start;
                XYZ crossDir = GetPlanPerpendicular(existing.Dir, view);
                double perpDist = Math.Abs(delta.DotProduct(crossDir));
                if (perpDist > collinearTol) continue;

                // Check if lines overlap (project this dim onto the existing dim's axis)
                double projStart = (dimLine.GetEndPoint(0) - existing.Start).DotProduct(existing.Dir);
                double projEnd = (dimLine.GetEndPoint(1) - existing.Start).DotProduct(existing.Dir);
                double existLen = existing.Start.DistanceTo(existing.End);
                double pMin = Math.Min(projStart, projEnd);
                double pMax = Math.Max(projStart, projEnd);

                // If they overlap significantly along the direction
                if (pMax > 0.1 && pMin < existLen - 0.1)
                {
                    Log($"        [WallFull] ⚠ Duplicate dimension skipped (overlapping line) for wall '{wall.Name}' (Id:{wall.Id})");
                    return 0;
                }
            }

            createdDimLines.Add((dimDir, dimLine.GetEndPoint(0), dimLine.GetEndPoint(1)));

            return TryCreateDimension(doc, view, dimLine, refs) ? 1 : 0;
        }

        private void AddIntersectingGridReferences(Document doc,View view,Wall targetWall,RevitLinkInstance? targetLink,ReferenceArray refs,Line wallLine,XYZ dir,XYZ perp)
        {
            var grids = GetGrids(doc, view);
            Curve transformedWallLine = targetLink != null
                ? wallLine.CreateTransformed(targetLink.GetTotalTransform())
                : wallLine;

            foreach (var gItem in grids)
            {
                Curve? gridCurve = gItem.Element.Curve;
                if (gridCurve == null) continue;

                if (gItem.Link != null)
                    gridCurve = gridCurve.CreateTransformed(gItem.Link.GetTotalTransform());

                // Skip parallel grids
                if (gridCurve is Line gl)
                {
                    XYZ gDir = gl.Direction.Normalize();
                    if (gItem.Link != null)
                        gDir = gItem.Link.GetTotalTransform().OfVector(gDir).Normalize();

                    gDir = ProjectVectorToView(view, gDir).Normalize();

                    if (Math.Abs(gDir.DotProduct(dir)) > 0.9)
                        continue;
                }

#if NET10_0_OR_GREATER
                // Revit 2027+: new API without out parameter
                var cir = transformedWallLine.Intersect(
                    gridCurve,
                    CurveIntersectResultOption.Simple);

                if (cir.Result == SetComparisonResult.Overlap ||
                    cir.Result == SetComparisonResult.Subset)
#else
                // Revit 2024-2026: old API with out parameter
                SetComparisonResult result = transformedWallLine.Intersect(gridCurve, out IntersectionResultArray _);

                if (result == SetComparisonResult.Overlap ||
                    result == SetComparisonResult.Subset)
#endif
                {
                    Reference? gridRef = GetGridReference(gItem.Element, gItem.Link);
                    if (gridRef != null)
                        refs.Append(gridRef);
                }
            }
        }

        private void AddIntersectingWallReferences(Document doc, View view, Wall targetWall, RevitLinkInstance? targetLink, ReferenceArray refs, Line wallLine, XYZ dir, XYZ perp)
        {
            var otherWalls = GetElements<Wall>(doc, BuiltInCategory.OST_Walls);
            Curve transformedWallLine = targetLink != null ? wallLine.CreateTransformed(targetLink.GetTotalTransform()) : wallLine;

            foreach (var wItem in otherWalls)
            {
                if (wItem.Element.Id == targetWall.Id) continue;

                Curve? otherCurve = (wItem.Element.Location as LocationCurve)?.Curve;
                if (otherCurve == null) continue;
                
                if (wItem.Link != null) otherCurve = otherCurve.CreateTransformed(wItem.Link.GetTotalTransform());

#if NET10_0_OR_GREATER
                // Revit 2027+: new API without out parameter
                var cir2 = transformedWallLine.Intersect(otherCurve, CurveIntersectResultOption.Simple);
                if (cir2.Result == SetComparisonResult.Overlap || cir2.Result == SetComparisonResult.Subset)
#else
                // Revit 2024-2026: old API with out parameter
                SetComparisonResult result = transformedWallLine.Intersect(otherCurve, out IntersectionResultArray _);
                if (result == SetComparisonResult.Overlap || result == SetComparisonResult.Subset)
#endif
                {
                    if (otherCurve is Line ol)
                    {
                        XYZ oDir = ol.Direction.Normalize();
                        if (wItem.Link != null) oDir = wItem.Link.GetTotalTransform().OfVector(oDir).Normalize();
                        oDir = ProjectVectorToView(view, oDir).Normalize();
                        
                        if (Math.Abs(oDir.DotProduct(dir)) > 0.9) continue;
                    }

                    try
                    {
                        // Only add the NEAREST face of the intersecting wall to avoid
                        // creating "0" dimension segments between exterior and interior faces.
                        Reference? bestFaceRef = null;
                        double bestDist = double.MaxValue;
                        XYZ wallMid = (transformedWallLine.GetEndPoint(0) + transformedWallLine.GetEndPoint(1)) * 0.5;

                        foreach (ShellLayerType layerType in new[] { ShellLayerType.Exterior, ShellLayerType.Interior })
                        {
                            IList<Reference> faces = HostObjectUtils.GetSideFaces(wItem.Element, layerType);
                            foreach (Reference faceRef in faces)
                            {
                                if (faceRef == null) continue;
                                
                                // Validate face normal
                                GeometryObject? geoObj = wItem.Element.GetGeometryObjectFromReference(faceRef);
                                if (geoObj is PlanarFace pf)
                                {
                                    XYZ n = pf.FaceNormal.Normalize();
                                    if (wItem.Link != null) n = wItem.Link.GetTotalTransform().OfVector(n).Normalize();
                                    if (Math.Abs(n.DotProduct(dir)) < 0.99) continue; // Must be parallel to dim direction

                                    // Calculate distance from the face origin to the target wall centerline
                                    XYZ faceOrigin = pf.Origin;
                                    if (wItem.Link != null) faceOrigin = wItem.Link.GetTotalTransform().OfPoint(faceOrigin);
                                    double dist = wallMid.DistanceTo(faceOrigin);
                                    if (dist < bestDist)
                                    {
                                        bestDist = dist;
                                        bestFaceRef = wItem.Link != null ? faceRef.CreateLinkReference(wItem.Link) : faceRef;
                                    }
                                }
                            }
                        }

                        if (bestFaceRef != null)
                            try { refs.Append(bestFaceRef); } catch { }
                    }
                    catch { }
                }
            }
        }

        private sealed class WallDimTarget
        {
            public Wall Wall = null!;
            public RevitLinkInstance? Link;
            /// <summary>null = all inserts on wall; otherwise only these insert/opening ids.</summary>
            public HashSet<ElementId>? OpeningFilter;
        }

        private List<WallDimTarget> GetWallDimTargets(Document doc)
        {
            var results = new List<WallDimTarget>();
            if (UseSelection)
            {
                var selIds = _app.ActiveUIDocument.Selection.GetElementIds();
                var byWall = new Dictionary<ElementId, HashSet<ElementId>?>();

                foreach (ElementId selId in selIds)
                {
                    Element? e = doc.GetElement(selId);
                    if (e == null) continue;

                    if (e is Wall w)
                    {
                        if (!byWall.ContainsKey(w.Id))
                            byWall[w.Id] = null;
                    }
                    else if (e is Opening op && GetOpeningHostWallId(op) is ElementId hostWallId)
                    {
                        if (!byWall.TryGetValue(hostWallId, out HashSet<ElementId>? set) || set == null)
                        {
                            set = new HashSet<ElementId>();
                            byWall[hostWallId] = set;
                        }
                        set.Add(op.Id);
                    }
                    else if (e is FamilyInstance fi && fi.Host is Wall hostWall)
                    {
                        if (!byWall.ContainsKey(hostWall.Id))
                            byWall[hostWall.Id] = null;
                    }
                }

                foreach (var kv in byWall)
                {
                    if (doc.GetElement(kv.Key) is Wall wall)
                        results.Add(new WallDimTarget { Wall = wall, Link = null, OpeningFilter = kv.Value });
                }
                return results;
            }

            foreach (var item in GetElements<Wall>(doc, BuiltInCategory.OST_Walls))
                results.Add(new WallDimTarget { Wall = item.Element, Link = item.Link, OpeningFilter = null });

            return results;
        }

        private static ElementId? GetOpeningHostWallId(Opening opening)
        {
            try
            {
                if (opening.Host is Element host && host.Id != ElementId.InvalidElementId)
                    return host.Id;
            }
            catch { }
            return null;
        }

        private static List<ElementId> GetWallInsertIds(Wall wall, HashSet<ElementId>? filter)
        {
            var ids = new List<ElementId>();
            Document doc = wall.Document;
            foreach (ElementId id in wall.FindInserts(true, true, true, true))
            {
                Element? e = doc.GetElement(id);
                if (e is Opening || e is FamilyInstance)
                {
                    if (filter == null || filter.Contains(id))
                        ids.Add(id);
                }
            }

            if (filter != null)
            {
                foreach (ElementId id in filter)
                {
                    if (!ids.Contains(id) && doc.GetElement(id) is Opening)
                        ids.Add(id);
                }
            }
            return ids;
        }

        private int CreateWallOpeningDimension(Document doc, View view, Wall wall, RevitLinkInstance? link, ElementId? singleInsertId)
        {
            LocationCurve? locCurve = wall.Location as LocationCurve;
            if (locCurve?.Curve is not Line wallLine) return 0;

            Curve curve = wallLine;
            if (link != null) curve = curve.CreateTransformed(link.GetTotalTransform());

            if (curve is not Line line) return 0;

            XYZ dir = line.Direction.Normalize();
            XYZ perp = GetPlanPerpendicular(dir, view);

            ReferenceArray refs = new ReferenceArray();

            if (WallCoreOnly)
            {
                AppendWallSideFaceReferences(wall, refs, link, perp);
            }
            else if (singleInsertId != null)
            {
                AddInsertReferences(wall, refs, link, view, line, dir, perp, new HashSet<ElementId> { singleInsertId });
            }
            else
            {
                AddInsertReferences(wall, refs, link, view, line, dir, perp, null);
            }

            if (refs.Size < 2 && singleInsertId != null &&
                wall.Document.GetElement(singleInsertId) is Opening opening)
            {
                Curve wallCurve = line;
                if (link != null) wallCurve = line.CreateTransformed(link.GetTotalTransform());
                if (TryGetOpeningSpanOnWall(opening, view, wallCurve, link, out double pMin, out double pMax))
                    AppendWallJambFacesInSpan(wall, refs, link, view, wallCurve, perp, pMin, pMax);
            }

            if (refs.Size < 2)
                AppendWallSideFaceReferences(wall, refs, link, perp);

            refs = DeduplicateReferences(doc, refs, view, perp);
            if (refs.Size < 2) return 0;

            double offsetFeet = OffsetMm / 304.8;
            Line dimLine = Line.CreateBound(
                FlattenToView(view, line.GetEndPoint(0) + perp * offsetFeet),
                FlattenToView(view, line.GetEndPoint(1) + perp * offsetFeet));

            return TryCreateDimension(doc, view, dimLine, refs) ? 1 : 0;
        }

        private void AddInsertReferences(Wall wall, ReferenceArray refs, RevitLinkInstance? link, View view, Line wallLine,
            XYZ dir, XYZ perp, HashSet<ElementId>? filter)
        {
            Document doc = wall.Document;
            foreach (ElementId id in GetWallInsertIds(wall, filter))
            {
                Element? e = doc.GetElement(id);
                if (e is FamilyInstance fi)
                {
                    if (OpeningDimMode == "Centers")
                        AddFamilyInstanceCenterRef(fi, refs, link);
                    else
                        AddFamilyInstanceFaceRefs(fi, refs, link);
                }
                else if (e is Opening opening)
                {
                    if (OpeningDimMode == "Centers")
                        AddNativeOpeningCenterRef(opening, refs, link, view, dir, perp);
                    else
                        AddNativeOpeningFaceRefs(opening, wall, refs, link, view, wallLine, dir, perp);
                }
            }
        }

        private void AddFamilyInstanceFaceRefs(FamilyInstance fi, ReferenceArray refs, RevitLinkInstance? link)
        {
            foreach (FamilyInstanceReferenceType refType in new[]
            {
                FamilyInstanceReferenceType.Left,
                FamilyInstanceReferenceType.Right,
                FamilyInstanceReferenceType.Front,
                FamilyInstanceReferenceType.Back
            })
            {
                IList<Reference> faceRefs = fi.GetReferences(refType);
                if (faceRefs == null || faceRefs.Count == 0) continue;
                Reference r = faceRefs.First();
                if (link != null) r = r.CreateLinkReference(link);
                try { refs.Append(r); } catch { }
            }
        }

        private void AddFamilyInstanceCenterRef(FamilyInstance fi, ReferenceArray refs, RevitLinkInstance? link)
        {
            var centerRefs = fi.GetReferences(FamilyInstanceReferenceType.CenterLeftRight);
            if (centerRefs.Count == 0) centerRefs = fi.GetReferences(FamilyInstanceReferenceType.CenterFrontBack);
            if (centerRefs.Count == 0) return;
            Reference r = centerRefs.First();
            if (link != null) r = r.CreateLinkReference(link);
            try { refs.Append(r); } catch { }
        }

        private void AddNativeOpeningCenterRef(Opening opening, ReferenceArray refs, RevitLinkInstance? link, View view, XYZ dir, XYZ perp)
        {
            BoundingBoxXYZ? bb = opening.get_BoundingBox(view) ?? opening.get_BoundingBox(null);
            if (bb == null) return;

            XYZ center = (bb.Min + bb.Max) * 0.5;
            if (link != null) center = link.GetTotalTransform().OfPoint(center);

            // Use wall side faces near opening center via geometry on opening if any
            Options opt = new Options { ComputeReferences = true, View = view };
            try
            {
                GeometryElement? ge = opening.get_Geometry(opt);
                if (ge != null)
                    CollectOpeningFacesParallelTo(ge, perp, refs, link);
            }
            catch { }

            if (refs.Size > 0) return;

            // Fallback: dimension between bbox edges along wall using reference planes from wall faces at center
            // (handled by caller fallback to side faces)
        }

        private void AddNativeOpeningFaceRefs(Opening opening, Wall wall, ReferenceArray refs, RevitLinkInstance? link,
            View view, Line? wallLine, XYZ dir, XYZ perp)
        {
            int before = refs.Size;
            Options opt = new Options { ComputeReferences = true, View = view };
            try
            {
                GeometryElement? ge = opening.get_Geometry(opt);
                if (ge != null)
                    CollectOpeningFacesParallelTo(ge, perp, refs, link);
            }
            catch { }

            if (refs.Size > before) return;

            if (wallLine == null && wall.Location is LocationCurve lc)
                wallLine = lc.Curve as Line;
            if (wallLine == null) return;

            Curve wallCurve = wallLine;
            Transform linkT = link?.GetTotalTransform() ?? Transform.Identity;
            if (link != null) wallCurve = wallLine.CreateTransformed(linkT);

            if (!TryGetOpeningSpanOnWall(opening, view, wallCurve, link, out double paramMin, out double paramMax))
                return;

            AppendWallJambFacesInSpan(wall, refs, link, view, wallCurve, perp, paramMin, paramMax);
        }

        private static void CollectOpeningFacesParallelTo(GeometryElement ge, XYZ perp, ReferenceArray refs, RevitLinkInstance? link)
        {
            foreach (GeometryObject obj in ge)
            {
                if (obj is Solid solid)
                {
                    foreach (Face face in solid.Faces)
                    {
                        if (face is PlanarFace pf && pf.Reference != null &&
                            Math.Abs(pf.FaceNormal.Normalize().DotProduct(perp.Normalize())) > 0.75)
                        {
                            Reference r = pf.Reference;
                            if (link != null) r = r.CreateLinkReference(link);
                            try { refs.Append(r); } catch { }
                        }
                    }
                }
                else if (obj is GeometryInstance inst)
                {
                    CollectOpeningFacesParallelTo(inst.GetInstanceGeometry(), perp, refs, link);
                }
            }
        }

        private static bool TryGetOpeningSpanOnWall(Opening opening, View view, Curve wallCurve, RevitLinkInstance? link,
            out double paramMin, out double paramMax)
        {
            paramMin = double.MaxValue;
            paramMax = double.MinValue;
            BoundingBoxXYZ? bb = opening.get_BoundingBox(view) ?? opening.get_BoundingBox(null);
            if (bb == null) return false;

            Transform t = link?.GetTotalTransform() ?? Transform.Identity;
            foreach (XYZ corner in GetBoundingBoxCorners(bb, t))
            {
                IntersectionResult? ir = wallCurve.Project(corner);
                if (ir == null) continue;
                paramMin = Math.Min(paramMin, ir.Parameter);
                paramMax = Math.Max(paramMax, ir.Parameter);
            }
            return paramMax > paramMin;
        }

        private static IEnumerable<XYZ> GetBoundingBoxCorners(BoundingBoxXYZ bb, Transform t)
        {
            XYZ min = t.OfPoint(bb.Min);
            XYZ max = t.OfPoint(bb.Max);
            yield return new XYZ(min.X, min.Y, min.Z);
            yield return new XYZ(max.X, min.Y, min.Z);
            yield return new XYZ(min.X, max.Y, min.Z);
            yield return new XYZ(max.X, max.Y, min.Z);
            yield return new XYZ(min.X, min.Y, max.Z);
            yield return new XYZ(max.X, min.Y, max.Z);
            yield return new XYZ(min.X, max.Y, max.Z);
            yield return new XYZ(max.X, max.Y, max.Z);
        }

        private void AppendWallJambFacesInSpan(Wall wall, ReferenceArray refs, RevitLinkInstance? link, View view,
            Curve wallCurve, XYZ perp, double paramMin, double paramMax)
        {
            var candidates = new List<(Reference Ref, double Param)>();
            Options opt = new Options { ComputeReferences = true, View = view };
            GeometryElement? ge = wall.get_Geometry(opt);
            if (ge == null) return;

            void Visit(GeometryElement geometry)
            {
                foreach (GeometryObject obj in geometry)
                {
                    if (obj is Solid solid)
                    {
                        foreach (Face face in solid.Faces)
                        {
                            if (face is not PlanarFace pf || pf.Reference == null) continue;
                            if (Math.Abs(pf.FaceNormal.Normalize().DotProduct(perp.Normalize())) < 0.75) continue;

                            IntersectionResult? ir = wallCurve.Project(pf.Origin);
                            if (ir == null) continue;
                            if (ir.Parameter < paramMin - 0.5 || ir.Parameter > paramMax + 0.5) continue;

                            Reference r = pf.Reference;
                            if (link != null) r = r.CreateLinkReference(link);
                            candidates.Add((r, ir.Parameter));
                        }
                    }
                    else if (obj is GeometryInstance inst)
                    {
                        Visit(inst.GetInstanceGeometry());
                    }
                }
            }

            Visit(ge);
            if (candidates.Count == 0) return;

            foreach (var item in candidates.OrderBy(c => c.Param).Take(1))
                try { refs.Append(item.Ref); } catch { }
            foreach (var item in candidates.OrderByDescending(c => c.Param).Take(1))
                try { refs.Append(item.Ref); } catch { }
        }

        private void ProcessGeometry(GeometryElement geo, Curve transformedCurve, ReferenceArray refs, RevitLinkInstance link, XYZ perp)
        {
            foreach (var obj in geo)
            {
                if (obj is Solid solid && solid.Faces.Size > 0)
                {
                    AddWallFaces(solid, transformedCurve, refs, link, perp);
                }
                else if (obj is GeometryInstance inst)
                {
                    GeometryElement instGeo = inst.GetInstanceGeometry();
                    ProcessGeometry(instGeo, transformedCurve, refs, link, perp);
                }
            }
        }

        private void AppendWallSideFaceReferences(Wall wall, ReferenceArray refs, RevitLinkInstance link, XYZ perp)
        {
            try
            {
                IList<Reference> faces = HostObjectUtils.GetSideFaces(wall, ShellLayerType.Exterior);
                foreach (Reference faceRef in faces)
                {
                    if (faceRef == null) continue;
                    Reference r = link != null ? faceRef.CreateLinkReference(link) : faceRef;
                    try { refs.Append(r); } catch { }
                }
            }
            catch { }
        }
            
        private void AddWallFaces(Solid solid, Curve transformedCurve, ReferenceArray refs, RevitLinkInstance link, XYZ perp)
        {
            Transform t = link?.GetTotalTransform() ?? Transform.Identity;
            foreach (Face face in solid.Faces)
            {
                if (face is PlanarFace planar && planar.Reference != null)
                {
                    XYZ normal = t.OfVector(planar.FaceNormal).Normalize();
                    if (Math.Abs(normal.DotProduct(perp.Normalize())) > 0.85)
                    {
                        Reference r = planar.Reference;
                        if (link != null) r = r.CreateLinkReference(link);
                        try { refs.Append(r); } catch { }
                    }
                }
            }
        }



        private int DimensionRooms(Document doc, View view)
        {
            Log($"[Rooms] ── Room Boundary Mode ──");
            int count = 0;
            var roomData = GetElements<SpatialElement>(doc, BuiltInCategory.OST_Rooms)
                .Where(x => x.Element is Room)
                .ToList();

            Log($"[Rooms] Found {roomData.Count} room(s)");
            if (roomData.Count == 0) { Log($"[Rooms] ✗ No rooms found. Aborting."); return count; }

            var boundaryOptions = new SpatialElementBoundaryOptions
            {
                SpatialElementBoundaryLocation = RoomUseCenterBoundary
                    ? SpatialElementBoundaryLocation.Center
                    : SpatialElementBoundaryLocation.Finish
            };
            Log($"[Rooms] Boundary location: {boundaryOptions.SpatialElementBoundaryLocation}");

            foreach (var item in roomData)
            {
                Room room = (Room)item.Element;
                RevitLinkInstance link = item.Link;
                Transform linkT = link?.GetTotalTransform() ?? Transform.Identity;
                Log($"  Room: '{room.Name}' (Id:{room.Id}) | Source: {(link != null ? "Linked" : "Host")}");

                IList<IList<BoundarySegment>> loops;
                try { loops = room.GetBoundarySegments(boundaryOptions); }
                catch (Exception ex) { Log($"    ⚠ GetBoundarySegments threw: {ex.Message}"); continue; }
                if (loops == null || loops.Count == 0) { Log($"    ⚠ No boundary loops found"); continue; }
                Log($"    Boundary loops: {loops.Count}");

                var boundaryRefs = new List<(Reference Ref, XYZ Point, XYZ Normal)>();
                int nullRefCount = 0;
                foreach (IList<BoundarySegment> loop in loops)
                {
                    foreach (BoundarySegment seg in loop)
                    {
                        Reference? r = GetBoundarySegmentReference(room, seg, link, view);
                        if (r == null) { nullRefCount++; continue; }

                        Curve c = seg.GetCurve();
                        if (c == null) continue;
                        if (link != null) c = c.CreateTransformed(linkT);

                        if (c is Line segLine)
                        {
                            XYZ segDir = segLine.Direction.Normalize();
                            XYZ normal = GetPlanPerpendicular(segDir, view);
                            XYZ mid = FlattenToView(view, (segLine.GetEndPoint(0) + segLine.GetEndPoint(1)) * 0.5);
                            boundaryRefs.Add((r, mid, normal));
                        }
                    }
                }
                Log($"    Boundary refs: {boundaryRefs.Count} valid, {nullRefCount} null");

                if (boundaryRefs.Count < 2) { Log($"    ⚠ Not enough boundary refs (<2). Skipping room."); continue; }

                var directionGroups = boundaryRefs.GroupBy(b =>
                {
                    XYZ n = b.Normal.Normalize();
                    if (n.X < 0 || (Math.Abs(n.X) < 0.001 && n.Y < 0)) n = -n;
                    return $"{Math.Round(n.X, 3)}_{Math.Round(n.Y, 3)}";
                });

                foreach (var group in directionGroups)
                {
                    var items = group.ToList();
                    if (items.Count < 2) { Log($"    Dir group '{group.Key}': {items.Count} ref(s) → skipped (need ≥2)"); continue; }
                    Log($"    Dir group '{group.Key}': {items.Count} ref(s)");

                    ReferenceArray refs = new ReferenceArray();
                    foreach (var b in items)
                        refs.Append(b.Ref);

                    refs = DeduplicateReferences(doc, refs, view, items.First().Normal);
                    if (refs.Size < 2) { Log($"      ⚠ After dedup: {refs.Size} refs → skipped"); continue; }
                    Log($"      After dedup: {refs.Size} refs");

                    XYZ dimDir = items.First().Normal.Normalize();
                    double minPos = items.Min(b => b.Point.DotProduct(dimDir));
                    double maxPos = items.Max(b => b.Point.DotProduct(dimDir));
                    double dist = Math.Max(5.0, maxPos - minPos);
                    XYZ center = XYZ.Zero;
                    foreach (var b in items) center += b.Point;
                    center /= items.Count;

                    Line dimLine = Line.CreateBound(
                        FlattenToView(view, center - dimDir * dist),
                        FlattenToView(view, center + dimDir * dist));

                    bool ok = TryCreateDimension(doc, view, dimLine, refs);
                    Log($"      → Dimension: {(ok ? "✓" : "✗")} (line length: {dimLine.Length:F3} ft)");
                    if (ok) count++;
                }
            }
            Log($"[Rooms] Total created: {count}");
            return count;
        }

        /// <summary>
        /// Room boundary references must attach to bounding wall <b>faces</b> (finish/interior),
        /// not <c>new Reference(wall)</c> which Revit treats as the wall location / centerline.
        /// </summary>
        private static Reference? GetBoundarySegmentReference(Room room, BoundarySegment seg, RevitLinkInstance? link, View view)
        {
            try
            {
                Curve? c = seg.GetCurve();
                if (c == null) return null;

                ElementId id = seg.ElementId;
                if (id == ElementId.InvalidElementId) return null;

                Document sourceDoc = link?.GetLinkDocument() ?? room.Document;
                Element? elem = sourceDoc.GetElement(id);
                if (elem == null) return null;

                if (elem is Wall wall)
                {
                    Reference? wallRef = GetWallFaceReferenceForRoomBoundary(wall, room, c, link, view);
                    if (wallRef != null) return wallRef;
                }

                if (elem is ModelCurve mc && mc.GeometryCurve?.Reference != null)
                {
                    Reference r = mc.GeometryCurve.Reference;
                    return link != null ? r.CreateLinkReference(link) : r;
                }

                Options opt = new Options { ComputeReferences = true };
                GeometryElement? ge = elem.get_Geometry(opt);
                if (ge != null)
                {
                    foreach (GeometryObject obj in ge)
                    {
                        if (obj is Curve geomCurve && geomCurve.Reference != null)
                        {
                            Reference r = geomCurve.Reference;
                            return link != null ? r.CreateLinkReference(link) : r;
                        }
                    }
                }

                if (c.Reference != null)
                {
                    Reference r = c.Reference;
                    return link != null ? r.CreateLinkReference(link) : r;
                }

                // Do not return `new Reference(elem)` as it is not a geometric reference and will crash NewDimension.
                return null;
            }
            catch
            {
                return null;
            }
        }

        private static Reference? GetWallFaceReferenceForRoomBoundary(Wall wall, Room room, Curve boundaryCurve, RevitLinkInstance? link, View view)
        {
            Document hostDoc = link?.Document ?? room.Document;
            XYZ roomCenter = GetRoomCenter(room);
            Transform toHost = link?.GetTotalTransform() ?? Transform.Identity;
            Transform toWallDoc = toHost.Inverse;

            XYZ midHost = toHost.OfPoint((boundaryCurve.GetEndPoint(0) + boundaryCurve.GetEndPoint(1)) * 0.5);
            XYZ roomCenterHost = link != null ? toHost.OfPoint(roomCenter) : roomCenter;
            XYZ intoRoomHost = roomCenterHost - midHost;
            if (intoRoomHost.GetLength() > 1e-9)
                intoRoomHost = intoRoomHost.Normalize();
            else
                return null;

            XYZ midWall = toWallDoc.OfPoint(midHost);
            XYZ intoRoomWall = toWallDoc.OfVector(intoRoomHost);

            Reference? rayFace = TryFindRoomBoundaryFaceByRay(hostDoc, wall, midHost, intoRoomHost, link, view);
            if (rayFace != null) return rayFace;

            Reference? geomFace = PickWallFaceFromGeometry(wall, boundaryCurve, midWall, intoRoomWall, link, view);
            if (geomFace != null) return geomFace;

            return TryPickSideFaceReference(wall, midWall, intoRoomWall, link, boundaryCurve);
        }

        private static Reference? TryFindRoomBoundaryFaceByRay(Document hostDoc, Wall wall, XYZ midHost, XYZ intoRoomHost, RevitLinkInstance? link, View view)
        {
            View3D? view3d = Get3DView(hostDoc);
            if (view3d == null) return null;

            try
            {
                double probeFt = Math.Max(0.1, UnitUtils.ConvertToInternalUnits(150, UnitTypeId.Millimeters));
                XYZ origin = midHost + intoRoomHost * probeFt;
                XYZ direction = -intoRoomHost;

                var filter = new ElementClassFilter(typeof(Wall));
                var intersector = new ReferenceIntersector(filter, FindReferenceTarget.Face, view3d);

                ReferenceWithContext? hit = intersector.FindNearest(origin, direction);
                if (hit == null) return null;

                Reference r = hit.GetReference();
                Element? hitElem = hostDoc.GetElement(r.ElementId);
                if (hitElem == null) return null;

                if (link != null)
                {
                    if (hitElem.Id == link.Id)
                    {
                        Reference? linkRef = TryGetLinkedWallFaceReference(link, wall, r);
                        return linkRef;
                    }
                    if (hitElem.Id != wall.Id)
                        return null;
                    return r.CreateLinkReference(link);
                }

                if (hitElem.Id != wall.Id) return null;
                return r;
            }
            catch
            {
                return null;
            }
        }

        private static Reference? TryGetLinkedWallFaceReference(RevitLinkInstance link, Wall wall, Reference hostRef)
        {
            try { return hostRef.CreateLinkReference(link); }
            catch { return null; }
        }

        private static Reference? TryPickSideFaceReference(Wall wall, XYZ pointOnBoundary, XYZ towardRoom, RevitLinkInstance? link, Curve boundaryCurve)
        {
            Reference? best = null;
            double bestDist = double.MaxValue;
            double maxDist = Math.Max(wall.Width * 2.0, 3.0);

            foreach (ShellLayerType layer in new[] { ShellLayerType.Interior, ShellLayerType.Exterior })
            {
                IList<Reference> faceRefs;
                try { faceRefs = HostObjectUtils.GetSideFaces(wall, layer); }
                catch { continue; }
                if (faceRefs == null || faceRefs.Count == 0) continue;

                foreach (Reference faceRef in faceRefs)
                {
                    if (faceRef == null) continue;
                    try
                    {
                        GeometryObject? geo = wall.GetGeometryObjectFromReference(faceRef);
                        if (geo is not PlanarFace pf) continue;

                        if (pf.FaceNormal.DotProduct(towardRoom) < 0.25) continue;
                        if (!IsFaceAlignedWithBoundary(pf, boundaryCurve)) continue;

                        double dist = DistancePointToBoundaryCurve(pointOnBoundary, boundaryCurve, pf);
                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            best = link != null ? faceRef.CreateLinkReference(link) : faceRef;
                        }
                    }
                    catch { }
                }
            }

            return bestDist < maxDist ? best : null;
        }

        private static bool IsFaceAlignedWithBoundary(PlanarFace pf, Curve boundaryCurve)
        {
            if (boundaryCurve is not Line boundaryLine) return true;
            XYZ boundaryDir = boundaryLine.Direction.Normalize();
            return Math.Abs(pf.FaceNormal.DotProduct(boundaryDir)) < 0.2;
        }

        private static double DistancePointToBoundaryCurve(XYZ point, Curve boundaryCurve, PlanarFace pf)
        {
            double planeDist = Math.Abs((point - pf.Origin).DotProduct(pf.FaceNormal));
            if (boundaryCurve is not Line line) return planeDist;

            XYZ a = line.GetEndPoint(0);
            XYZ b = line.GetEndPoint(1);
            XYZ ab = b - a;
            double lenSq = ab.DotProduct(ab);
            if (lenSq < 1e-12) return planeDist;

            double t = Math.Max(0, Math.Min(1, (point - a).DotProduct(ab) / lenSq));
            XYZ closest = a + ab * t;
            return planeDist + closest.DistanceTo(point) * 0.01;
        }

        private static Reference? PickWallFaceFromGeometry(Wall wall, Curve boundaryCurve, XYZ pointOnBoundary, XYZ towardRoom, RevitLinkInstance? link, View view)
        {
            Options opt = new Options
            {
                ComputeReferences = true,
                View = view
            };

            GeometryElement? ge = wall.get_Geometry(opt);
            if (ge == null) return null;

            Reference? best = null;
            double bestScore = double.MaxValue;

            void Visit(GeometryElement geometry)
            {
                foreach (GeometryObject obj in geometry)
                {
                    if (obj is Solid solid && solid.Faces.Size > 0)
                    {
                        foreach (Face face in solid.Faces)
                        {
                            if (face is not PlanarFace pf || pf.Reference == null) continue;

                            double facing = pf.FaceNormal.DotProduct(towardRoom);
                            if (facing < 0.25) continue;
                            if (!IsFaceAlignedWithBoundary(pf, boundaryCurve)) continue;

                            double dist = DistancePointToBoundaryCurve(pointOnBoundary, boundaryCurve, pf);
                            double score = dist - facing * 0.1;
                            if (score < bestScore)
                            {
                                bestScore = score;
                                Reference r = pf.Reference;
                                best = link != null ? r.CreateLinkReference(link) : r;
                            }
                        }
                    }
                    else if (obj is GeometryInstance inst)
                    {
                        Visit(inst.GetInstanceGeometry());
                    }
                }
            }

            Visit(ge);
            double maxDist = Math.Max(wall.Width * 2.0, 3.0);
            return bestScore < maxDist ? best : null;
        }

        private static View3D? Get3DView(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(View3D))
                .Cast<View3D>()
                .FirstOrDefault(v => !v.IsTemplate && v.Name.IndexOf("{3D}", StringComparison.OrdinalIgnoreCase) >= 0)
                ?? new FilteredElementCollector(doc)
                    .OfClass(typeof(View3D))
                    .Cast<View3D>()
                    .FirstOrDefault(v => !v.IsTemplate);
        }

        private static XYZ GetRoomCenter(Room room)
        {
            if (room.Location is LocationPoint lp)
                return lp.Point;

            BoundingBoxXYZ? bb = room.get_BoundingBox(null);
            return bb == null ? XYZ.Zero : (bb.Min + bb.Max) * 0.5;
        }

        private static string GetModeDisplayName(DimMode mode) => mode switch
        {
            DimMode.Grids => "Grid to Grid",
            DimMode.Walls => "Wall Openings",
            DimMode.Rooms => "Room Boundary",
            DimMode.MEP => "MEP Sleeves",
            DimMode.Columns => "Grid to Column",
            DimMode.CurtainWalls => "Curtain Walls",
            _ => mode.ToString()
        };

        private int DimensionGrids(Document doc, View view)
        {
            Log($"[Grids] ── Grid to Grid Mode ──");
            int count = 0;
            var gridData = GetGrids(doc, view);
            Log($"[Grids] Found {gridData.Count} grid(s) in scope");
            foreach (var g in gridData)
                Log($"  • Grid: '{g.Element.Name}' (Id:{g.Element.Id}) | CurveType: {g.Element.Curve?.GetType().Name} | Source: {(g.Link != null ? "Linked (" + g.Link.Name + ")" : "Host")}");

            if (gridData.Count < 2)
            {
                Log($"[Grids] ✗ Need at least 2 grids, found {gridData.Count}. Aborting.");
                return count;
            }

            // Group grids by direction (to handle angled grids)
            var directionGroups = gridData.GroupBy(x => GetDirectionKey(x.Element, x.Link, view)).ToList();
            Log($"[Grids] Direction groups: {directionGroups.Count}");

            foreach (var group in directionGroups)
            {
                var sortedGrids = group.OrderBy(x => GetPositionAlongNormal(x.Element, x.Link, view)).ToList();
                Log($"  Group '{group.Key}': {sortedGrids.Count} grid(s) → [{string.Join(", ", sortedGrids.Select(g => g.Element.Name))}]");
                if (sortedGrids.Count > 1)
                {
                    int dimCount = CreateGridDimension(doc, view, sortedGrids);
                    Log($"    → Created {dimCount} dimension(s)");
                    count += dimCount;
                }
                else
                {
                    Log($"    → Skipped (need ≥2 grids in same direction)");
                }
            }
            Log($"[Grids] Total created: {count}");
            return count;
        }

        private string GetDirectionKey(Grid g, RevitLinkInstance? link, View view)
        {
            if (g.Curve is Line l)
            {
                XYZ dir = l.Direction.Normalize();
                if (link != null) dir = link.GetTotalTransform().OfVector(dir);
                dir = ProjectVectorToView(view, dir).Normalize();
                if (dir.X < 0 || (Math.Abs(dir.X) < 0.001 && dir.Y < 0)) dir = -dir;
                return $"{Math.Round(dir.X, 3)}_{Math.Round(dir.Y, 3)}_{Math.Round(dir.Z, 3)}";
            }
            return "unknown";
        }

        private double GetPositionAlongNormal(Grid g, RevitLinkInstance? link, View view)
        {
            if (g.Curve is Line l)
            {
                XYZ p = FlattenToView(view, GetGridMidpoint(g, link));
                XYZ dir = l.Direction.Normalize();
                if (link != null) dir = link.GetTotalTransform().OfVector(dir);
                dir = ProjectVectorToView(view, dir).Normalize();
                if (dir.X < 0 || (Math.Abs(dir.X) < 0.001 && dir.Y < 0)) dir = -dir;
                XYZ normal = GetPlanPerpendicular(dir, view);
                return p.DotProduct(normal);
            }
            return 0;
        }

        private int DimensionCurtainWalls(Document doc, View view)
        {
            Log($"[CurtainWalls] ── Curtain Walls Mode ──");
            int count = 0;
            var walls = GetElements<Wall>(doc, BuiltInCategory.OST_Walls)
                .Where(x => x.Element.WallType.Kind == WallKind.Curtain)
                .ToList();

            Log($"[CurtainWalls] Found {walls.Count} curtain wall(s)");
            if (walls.Count == 0) { Log($"[CurtainWalls] ✗ No curtain walls found. Aborting."); return 0; }

            foreach (var item in walls)
            {
                Wall wall = item.Element;
                CurtainGrid? grid = wall.CurtainGrid;
                Log($"  Wall: '{wall.Name}' (Id:{wall.Id}) | CurtainGrid: {(grid != null ? "present" : "null")}");
                if (grid == null) { Log($"    ⚠ No curtain grid. Skipping."); continue; }

                var uGridIds = grid.GetUGridLineIds();
                var vGridIds = grid.GetVGridLineIds();
                Log($"    U-GridLines: {uGridIds.Count} | V-GridLines: {vGridIds.Count}");

                if (uGridIds.Count > 1)
                {
                    int r = CreateCurtainGridDim(doc, view, wall, uGridIds, item.Link);
                    Log($"    U-Grid dim: {(r > 0 ? "✓" : "✗")}");
                    count += r;
                }

                if (vGridIds.Count > 1)
                {
                    int r = CreateCurtainGridDim(doc, view, wall, vGridIds, item.Link);
                    Log($"    V-Grid dim: {(r > 0 ? "✓" : "✗")}");
                    count += r;
                }
            }
            Log($"[CurtainWalls] Total created: {count}");
            return count;
        }

        private int CreateCurtainGridDim(Document doc, View view, Wall wall, ICollection<ElementId> gridIds, RevitLinkInstance? link)
        {
            var refPoints = new List<(Reference Ref, XYZ Point)>();
            foreach (ElementId id in gridIds)
            {
                Reference? r = GetCurtainGridLineReference(doc, id, view, link);
                if (r == null) continue;

                XYZ? pt = GetReferencePoint(r, view);
                if (pt == null)
                {
                    Element? e = doc.GetElement(id);
                    if (e != null) pt = FlattenToView(view, GetElementCenter(e, link));
                }
                if (pt != null) refPoints.Add((r, pt));
            }

            if (refPoints.Count < 2) return 0;

            refPoints = refPoints
                .OrderBy(p => p.Point.DotProduct(ProjectVectorToView(view, view.RightDirection)))
                .ThenBy(p => p.Point.DotProduct(ProjectVectorToView(view, view.UpDirection)))
                .ToList();

            ReferenceArray refs = new ReferenceArray();
            foreach (var rp in refPoints)
                refs.Append(rp.Ref);

            LocationCurve? lc = wall.Location as LocationCurve;
            if (lc?.Curve is not Line wallLine) return 0;

            Transform t = link?.GetTotalTransform() ?? Transform.Identity;
            XYZ p0 = FlattenToView(view, t.OfPoint(wallLine.GetEndPoint(0)));
            XYZ p1 = FlattenToView(view, t.OfPoint(wallLine.GetEndPoint(1)));
            XYZ wallDir = (p1 - p0).Normalize();
            XYZ perp = GetPlanPerpendicular(wallDir, view);
            double offsetFeet = Math.Max(OffsetMm / 304.8, 2.0);

            // Mullion/grid refs are spaced along the direction perpendicular to the wall run in plan.
            XYZ anchor = refPoints[0].Point + perp * offsetFeet;
            XYZ spanDir = (refPoints.Last().Point - refPoints.First().Point);
            if (spanDir.GetLength() < 1e-6)
                spanDir = perp;
            else
                spanDir = spanDir.Normalize();

            double spanLen = Math.Max(refPoints.First().Point.DistanceTo(refPoints.Last().Point), 5.0);
            Line dimLine = Line.CreateBound(anchor, anchor + spanDir * spanLen);

            return TryCreateDimension(doc, view, dimLine, refs) ? 1 : 0;
        }

        private static Reference? GetCurtainGridLineReference(Document doc, ElementId gridLineId, View view, RevitLinkInstance? link)
        {
            Element? elem = doc.GetElement(gridLineId);
            if (elem == null) return null;

            Options opt = new Options { ComputeReferences = true, IncludeNonVisibleObjects = true };
            try
            {
                GeometryElement? ge = elem.get_Geometry(opt);
                if (ge != null)
                {
                    foreach (GeometryObject go in ge)
                    {
                        if (go is Line ln && ln.Reference != null)
                        {
                            Reference r = ln.Reference;
                            return link != null ? r.CreateLinkReference(link) : r;
                        }
                        if (go is Curve cv && cv.Reference != null)
                        {
                            Reference r = cv.Reference;
                            return link != null ? r.CreateLinkReference(link) : r;
                        }
                    }
                }
            }
            catch { }

            if (elem is Mullion mullion)
            {
                try
                {
                    Reference r = new Reference(mullion);
                    return link != null ? r.CreateLinkReference(link) : r;
                }
                catch { }
            }

            try
            {
                Reference r = new Reference(elem);
                return link != null ? r.CreateLinkReference(link) : r;
            }
            catch
            {
                return null;
            }
        }

        private static bool IsGridMoreVerticalInView(Grid g, RevitLinkInstance? link, View view)
        {
            if (g.Curve is not Line l) return false;
            XYZ dir = l.Direction.Normalize();
            if (link != null) dir = link.GetTotalTransform().OfVector(dir);
            dir = ProjectVectorToView(view, dir).Normalize();
            XYZ right = ProjectVectorToView(view, view.RightDirection).Normalize();
            XYZ up = ProjectVectorToView(view, view.UpDirection).Normalize();
            return Math.Abs(dir.DotProduct(up)) >= Math.Abs(dir.DotProduct(right));
        }

        private static double GridScalarInView(Grid g, RevitLinkInstance? link, View view, XYZ axis)
        {
            return FlattenToView(view, GetGridMidpoint(g, link)).DotProduct(axis.Normalize());
        }

        private int CreateGridDimension(Document doc, View view, List<(Grid Element, RevitLinkInstance? Link)> sortedGrids)
        {
            int count = 0;
            ReferenceArray refs = new ReferenceArray();
            int nullRefCount = 0;
            foreach (var item in sortedGrids)
            {
                Reference? r = GetGridReference(item.Element, item.Link);
                if (r == null) { nullRefCount++; Log($"      ⚠ GetGridReference returned null for grid '{item.Element.Name}'"); continue; }
                try { refs.Append(r); } catch (Exception ex) { Log($"      ⚠ Append ref failed for '{item.Element.Name}': {ex.Message}"); }
            }
            Log($"      Refs collected: {refs.Size} valid, {nullRefCount} null");

            if (refs.Size < 2) { Log($"      ✗ Not enough references ({refs.Size} < 2)"); return 0; }

            double offsetFeet = OffsetMm / 304.8;
            Log($"      Offset: {OffsetMm} mm = {offsetFeet:F4} ft");
            if (!TryBuildGridDimensionLine(sortedGrids, view, offsetFeet, out Line chainLine, out _, out _))
            {
                Log($"      ⚠ TryBuildGridDimensionLine failed, trying fallback...");
                if (!TryBuildGridDimensionLineFromExtents(sortedGrids, view, offsetFeet, out chainLine))
                {
                    Log($"      ✗ Fallback also failed. Cannot build dimension line.");
                    return 0;
                }
                Log($"      ✓ Fallback dimension line succeeded (length: {chainLine.Length:F4} ft)");
            }
            else
            {
                Log($"      ✓ Dimension line built (length: {chainLine.Length:F4} ft)");
            }

            bool chainOk = TryCreateDimension(doc, view, chainLine, refs);
            Log($"      Chain dimension: {(chainOk ? "✓ created" : "✗ failed")}");
            if (chainOk) count++;

            if (MultiTierGrids && sortedGrids.Count > 2)
            {
                Log($"      Multi-tier: attempting overall dimension ({sortedGrids.First().Element.Name} ↔ {sortedGrids.Last().Element.Name})");
                double extraOffsetFeet = 500.0 / 304.8;
                if (TryBuildGridDimensionLine(sortedGrids, view, OffsetMm / 304.8 + extraOffsetFeet,
                        out Line overallLine, out _, out _))
                {
                    ReferenceArray overallRefs = new ReferenceArray();
                    Reference? rStart = GetGridReference(sortedGrids.First().Element, sortedGrids.First().Link);
                    Reference? rEnd = GetGridReference(sortedGrids.Last().Element, sortedGrids.Last().Link);
                    Log($"      Overall refs: start={rStart != null}, end={rEnd != null}");
                    if (rStart != null && rEnd != null)
                    {
                        overallRefs.Append(rStart);
                        overallRefs.Append(rEnd);
                        bool overallOk = TryCreateDimension(doc, view, overallLine, overallRefs);
                        Log($"      Overall dimension: {(overallOk ? "✓ created" : "✗ failed")}");
                        if (overallOk) count++;
                    }
                }
                else
                {
                    Log($"      ⚠ Multi-tier dimension line build failed");
                }
            }
            return count;
        }

        /// <summary>
        /// Grid chain dimension: line runs along spacing direction (normal), anchored at first grid start + offset along grid direction.
        /// </summary>
        private static bool TryBuildGridDimensionLine(
            List<(Grid Element, RevitLinkInstance? Link)> sortedGrids,
            View view,
            double offsetFeet,
            out Line dimensionLine,
            out XYZ gridDir,
            out XYZ spacingDir)
        {
            dimensionLine = null!;
            gridDir = XYZ.BasisX;
            spacingDir = XYZ.BasisY;

            var first = sortedGrids.First();
            if (first.Element.Curve is not Line firstLine) return false;

            Transform t = first.Link?.GetTotalTransform() ?? Transform.Identity;
            gridDir = firstLine.Direction.Normalize();
            if (first.Link != null) gridDir = t.OfVector(gridDir);
            gridDir = ProjectVectorToView(view, gridDir).Normalize();
            if (gridDir.X < 0 || (Math.Abs(gridDir.X) < 0.001 && gridDir.Y < 0)) gridDir = -gridDir;

            spacingDir = GetPlanPerpendicular(gridDir, view);

            double posFirst = GetGridPositionAlongSpacing(sortedGrids.First().Element, sortedGrids.First().Link, view, spacingDir);
            double posLast = GetGridPositionAlongSpacing(sortedGrids.Last().Element, sortedGrids.Last().Link, view, spacingDir);
            double span = posLast - posFirst;
            if (Math.Abs(span) < 1e-6)
            {
                double minP = double.MaxValue;
                double maxP = double.MinValue;
                foreach (var item in sortedGrids)
                {
                    double p = GetGridPositionAlongSpacing(item.Element, item.Link, view, spacingDir);
                    minP = Math.Min(minP, p);
                    maxP = Math.Max(maxP, p);
                }
                span = maxP - minP;
            }
            if (Math.Abs(span) < 1e-6) return false;
            if (span < 0)
            {
                spacingDir = -spacingDir;
                span = -span;
            }

            XYZ anchor = GetGridLineStart(first.Element, first.Link, view);
            anchor = anchor + gridDir * offsetFeet;

            XYZ dimStart = FlattenToView(view, anchor - spacingDir * 1.0);
            XYZ dimEnd = FlattenToView(view, anchor + spacingDir * (span + 1.0));
            if (dimStart.DistanceTo(dimEnd) < 0.01) return false;

            dimensionLine = Line.CreateBound(dimStart, dimEnd);
            return true;
        }

        /// <summary>Fallback when grid-line start anchor fails: span between first/last grid midpoints.</summary>
        private static bool TryBuildGridDimensionLineFromExtents(
            List<(Grid Element, RevitLinkInstance? Link)> sortedGrids,
            View view,
            double offsetFeet,
            out Line dimensionLine)
        {
            dimensionLine = null!;
            var first = sortedGrids.First();
            var last = sortedGrids.Last();
            if (first.Element.Curve is not Line firstLine) return false;

            Transform t = first.Link?.GetTotalTransform() ?? Transform.Identity;
            XYZ gridDir = firstLine.Direction.Normalize();
            if (first.Link != null) gridDir = t.OfVector(gridDir);
            gridDir = ProjectVectorToView(view, gridDir).Normalize();
            XYZ spacingDir = GetPlanPerpendicular(gridDir, view);

            XYZ mid0 = FlattenToView(view, GetGridMidpoint(first.Element, first.Link));
            XYZ mid1 = FlattenToView(view, GetGridMidpoint(last.Element, last.Link));
            double span = Math.Abs(mid1.DotProduct(spacingDir) - mid0.DotProduct(spacingDir));
            if (span < 1e-6)
                span = Math.Max(mid0.DistanceTo(mid1), 1.0);

            XYZ anchor = mid0 + gridDir * offsetFeet;
            XYZ dimStart = FlattenToView(view, anchor);
            XYZ dimEnd = FlattenToView(view, anchor + spacingDir * span);
            if (dimStart.DistanceTo(dimEnd) < 0.01) return false;

            dimensionLine = Line.CreateBound(dimStart, dimEnd);
            return true;
        }

        private static double GetGridPositionAlongSpacing(Grid g, RevitLinkInstance? link, View view, XYZ spacingDir)
        {
            return FlattenToView(view, GetGridMidpoint(g, link)).DotProduct(spacingDir);
        }

        private static XYZ GetGridLineStart(Grid g, RevitLinkInstance? link, View view)
        {
            if (g.Curve is Line line)
            {
                XYZ p = line.GetEndPoint(0);
                if (link != null) p = link.GetTotalTransform().OfPoint(p);
                return FlattenToView(view, p);
            }
            return FlattenToView(view, GetGridMidpoint(g, link));
        }

        private static Reference? GetGridReference(Grid g, RevitLinkInstance? link)
        {
            try
            {
                Reference elemRef = new Reference(g);
                return link != null ? elemRef.CreateLinkReference(link) : elemRef;
            }
            catch
            {
                return null;
            }
        }

        private static XYZ GetGridMidpoint(Grid g, RevitLinkInstance? link)
        {
            if (g.Curve is not Line l) return XYZ.Zero;
            XYZ mid = (l.GetEndPoint(0) + l.GetEndPoint(1)) * 0.5;
            return link != null ? link.GetTotalTransform().OfPoint(mid) : mid;
        }

        private int DimensionColumns(Document doc, View view)
        {
            Log($"[Columns] ── Grid to Column Mode ──");
            int count = 0;
            var columnData = GetElements<FamilyInstance>(doc, BuiltInCategory.OST_StructuralColumns);
            int colCount = columnData.Count;
            columnData.AddRange(GetElements<FamilyInstance>(doc, BuiltInCategory.OST_StructuralFraming));
            Log($"[Columns] Found {colCount} structural column(s) + {columnData.Count - colCount} framing = {columnData.Count} total");

            if (columnData.Count == 0) { Log($"[Columns] ✗ No columns/framing found. Aborting."); return 0; }

            var grids = GetGrids(doc, view);
            Log($"[Columns] Found {grids.Count} grid(s)");
            if (grids.Count == 0) { Log($"[Columns] ✗ No grids found. Aborting."); return 0; }

            int vGrids = grids.Count(g => IsGridMoreVerticalInView(g.Element, g.Link, view));
            int hGrids = grids.Count - vGrids;
            Log($"[Columns] Vertical grids: {vGrids} | Horizontal grids: {hGrids}");

            XYZ viewRight = ProjectVectorToView(view, view.RightDirection).Normalize();
            XYZ viewUp = ProjectVectorToView(view, view.UpDirection).Normalize();

            foreach (var item in columnData)
            {
                XYZ center = FlattenToView(view, GetElementCenter(item.Element, item.Link));
                Log($"  Column: '{item.Element.Name}' (Id:{item.Element.Id})");

                var nearestV = grids
                    .Where(g => IsGridMoreVerticalInView(g.Element, g.Link, view))
                    .OrderBy(g => Math.Abs(GridScalarInView(g.Element, g.Link, view, viewRight) - center.DotProduct(viewRight)))
                    .FirstOrDefault();
                var nearestH = grids
                    .Where(g => !IsGridMoreVerticalInView(g.Element, g.Link, view))
                    .OrderBy(g => Math.Abs(GridScalarInView(g.Element, g.Link, view, viewUp) - center.DotProduct(viewUp)))
                    .FirstOrDefault();

                Log($"    Nearest V-grid: {nearestV.Element?.Name ?? "none"} | Nearest H-grid: {nearestH.Element?.Name ?? "none"}");
                if (nearestV.Element != null) { int r = CreateGridToElementDim(doc, view, nearestV.Element, nearestV.Link, item.Element, item.Link, true); Log($"    V-dim: {(r > 0 ? "✓" : "✗")}"); count += r; }
                if (nearestH.Element != null) { int r = CreateGridToElementDim(doc, view, nearestH.Element, nearestH.Link, item.Element, item.Link, false); Log($"    H-dim: {(r > 0 ? "✓" : "✗")}"); count += r; }
            }
            Log($"[Columns] Total created: {count}");
            return count;
        }

        private int DimensionMEP(Document doc, View view)
        {
            Log($"[MEP] ── MEP Sleeves Mode ──");
            int count = 0;
            var allGeneric = GetElements<FamilyInstance>(doc, BuiltInCategory.OST_GenericModel);
            Log($"[MEP] Generic models in scope: {allGeneric.Count}");
            var openingData = allGeneric
                .Where(x => x.Element.Symbol.FamilyName.Contains("Opening") || x.Element.Symbol.FamilyName.Contains("Sleeve"))
                .ToList();

            Log($"[MEP] Opening/Sleeve families found: {openingData.Count}");
            if (openingData.Count == 0)
            {
                Log($"[MEP] ✗ No families with 'Opening' or 'Sleeve' in name. Aborting.");
                if (allGeneric.Count > 0)
                {
                    Log($"[MEP] Available generic model families:");
                    foreach (var name in allGeneric.Select(x => x.Element.Symbol.FamilyName).Distinct().Take(15))
                        Log($"  • '{name}'");
                }
                return 0;
            }
            foreach (var o in openingData)
                Log($"  • '{o.Element.Symbol.FamilyName}' / '{o.Element.Name}' (Id:{o.Element.Id})");

            var grids = GetGrids(doc, view);
            var columns = GetElements<FamilyInstance>(doc, BuiltInCategory.OST_StructuralColumns);
            Log($"[MEP] Grids: {grids.Count} | Columns: {columns.Count}");

            XYZ viewRight = ProjectVectorToView(view, view.RightDirection).Normalize();
            XYZ viewUp = ProjectVectorToView(view, view.UpDirection).Normalize();

            foreach (var item in openingData)
            {
                XYZ center = FlattenToView(view, GetElementCenter(item.Element, item.Link));
                Log($"  Sleeve '{item.Element.Name}' (Id:{item.Element.Id}):");

                var nearestV = grids
                    .Where(g => IsGridMoreVerticalInView(g.Element, g.Link, view))
                    .OrderBy(g => Math.Abs(GridScalarInView(g.Element, g.Link, view, viewRight) - center.DotProduct(viewRight)))
                    .FirstOrDefault();
                var nearestH = grids
                    .Where(g => !IsGridMoreVerticalInView(g.Element, g.Link, view))
                    .OrderBy(g => Math.Abs(GridScalarInView(g.Element, g.Link, view, viewUp) - center.DotProduct(viewUp)))
                    .FirstOrDefault();

                Log($"    Nearest V-grid: {nearestV.Element?.Name ?? "none"} | H-grid: {nearestH.Element?.Name ?? "none"}");
                if (nearestV.Element != null) { int r = CreateGridToElementDim(doc, view, nearestV.Element, nearestV.Link, item.Element, item.Link, true); Log($"    V-dim: {(r > 0 ? "✓" : "✗")}"); count += r; }
                if (nearestH.Element != null) { int r = CreateGridToElementDim(doc, view, nearestH.Element, nearestH.Link, item.Element, item.Link, false); Log($"    H-dim: {(r > 0 ? "✓" : "✗")}"); count += r; }

                var nearestCol = columns.OrderBy(c => {
                    XYZ cCenter = GetElementCenter(c.Element, c.Link);
                    return cCenter.DistanceTo(center);
                }).FirstOrDefault();
                
                if (nearestCol.Element != null)
                {
                    XYZ cCenter = GetElementCenter(nearestCol.Element, nearestCol.Link);
                    double dist = cCenter.DistanceTo(center);
                    Log($"    Nearest column: '{nearestCol.Element.Name}' at {dist:F2} ft {(dist < 5.0 ? "(within range)" : "(too far, >5ft)")}");
                    if (dist < 5.0)
                    {
                        int r = CreateGridToElementDim(doc, view, nearestCol.Element, nearestCol.Link, item.Element, item.Link, true);
                        Log($"    Col-dim: {(r > 0 ? "✓" : "✗")}");
                        count += r;
                    }
                }
            }
            Log($"[MEP] Total created: {count}");
            return count;
        }

        /// <summary>Dimension from grid to column/opening. elementA is usually the grid.</summary>
        private int CreateGridToElementDim(Document doc, View view, Element gridElement, RevitLinkInstance? gridLink,
            Element targetElement, RevitLinkInstance? targetLink, bool horizontal)
        {
            ReferenceArray refs = new ReferenceArray();

            Reference? gridRef = GetGridReference(gridElement, gridLink);
            XYZ pGrid = FlattenToView(view, GetElementCenter(gridElement, gridLink));
            Reference? targetRef = GetBestReference(targetElement, horizontal, view, targetLink, pGrid);

            if (gridRef == null || targetRef == null) return 0;

            refs.Append(gridRef);
            refs.Append(targetRef);

            pGrid = GetReferencePoint(gridRef, view) ?? pGrid;
            XYZ pTarget = GetReferencePoint(targetRef, view) ?? FlattenToView(view, GetElementCenter(targetElement, targetLink));

            XYZ measureDir = horizontal ? view.RightDirection : view.UpDirection;
            measureDir = ProjectVectorToView(view, measureDir).Normalize();
            XYZ offsetDir = GetPlanPerpendicular(measureDir, view);

            double offsetFeet = OffsetMm / 304.8;
            double padFeet = 100.0 / 304.8; // 100 mm past each reference along the dimension string

            // Place the dimension line near the target element (column/sleeve), not the grid midpoint.
            XYZ dimOrigin = pTarget + offsetDir * offsetFeet;
            
            // Find the span along the measurement direction from the target back to the grid.
            double gridSpan = (pGrid - pTarget).DotProduct(measureDir);
            
            // To ensure the line has valid length even if column is exactly on the grid
            if (Math.Abs(gridSpan) < 1e-9)
                gridSpan = padFeet;

            XYZ pEnd = dimOrigin + measureDir * gridSpan;

            XYZ dir1to2 = (pEnd - dimOrigin).Normalize();
            if (dir1to2.IsAlmostEqualTo(XYZ.Zero)) dir1to2 = measureDir;

            XYZ lineStart = dimOrigin - dir1to2 * padFeet;
            XYZ lineEnd = pEnd + dir1to2 * padFeet;
            Line line = Line.CreateBound(
                FlattenToView(view, lineStart),
                FlattenToView(view, lineEnd));

            return TryCreateDimension(doc, view, line, refs) ? 1 : 0;
        }

        private static Reference? GetGridReference(Element e, RevitLinkInstance? link)
        {
            return e is Grid g ? GetGridReference(g, link) : null;
        }

        private Reference? GetBestReference(Element e, bool horizontal, View view, RevitLinkInstance? link, XYZ? towardPoint = null)
        {
            if (e is Grid g)
                return GetGridReference(g, link);

            if (e is FamilyInstance fi)
            {
                if (!WallCoreOnly)
                    return GetColumnFaceReference(fi, horizontal, view, link, towardPoint);

                if (horizontal)
                {
                    var centerRefs = fi.GetReferences(FamilyInstanceReferenceType.CenterLeftRight);
                    if (centerRefs.Count > 0) return WrapLinkReference(centerRefs.First(), link);
                }
                else
                {
                    var centerRefs = fi.GetReferences(FamilyInstanceReferenceType.CenterFrontBack);
                    if (centerRefs.Count > 0) return WrapLinkReference(centerRefs.First(), link);
                }
            }

            return null;
        }

        private static Reference? WrapLinkReference(Reference r, RevitLinkInstance? link)
        {
            if (link == null) return r;
            try { return r.CreateLinkReference(link); }
            catch { return r; }
        }

        private Reference? GetColumnFaceReference(FamilyInstance column, bool horizontal, View view, RevitLinkInstance? link, XYZ? towardPoint)
        {
            FamilyInstanceReferenceType[] faceTypes = horizontal
                ? new[] { FamilyInstanceReferenceType.Left, FamilyInstanceReferenceType.Right }
                : new[] { FamilyInstanceReferenceType.Front, FamilyInstanceReferenceType.Back };

            Reference? best = null;
            double bestDist = double.MaxValue;

            foreach (FamilyInstanceReferenceType refType in faceTypes)
            {
                IList<Reference> list = column.GetReferences(refType);
                if (list == null || list.Count == 0) continue;

                foreach (Reference candidate in list)
                {
                    Reference wrapped = WrapLinkReference(candidate, link) ?? candidate;
                    if (towardPoint == null)
                        return wrapped;

                    XYZ? pt = GetReferencePoint(wrapped, view);
                    double dist = pt == null ? bestDist : pt.DistanceTo(towardPoint);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        best = wrapped;
                    }
                }
            }

            if (best != null) return best;
            return PickStructuralFaceFromGeometry(column, horizontal, view, link);
        }

        private static Reference? PickStructuralFaceFromGeometry(FamilyInstance column, bool horizontal, View view, RevitLinkInstance? link)
        {
            Options opt = new Options { ComputeReferences = true, View = view };
            GeometryElement? ge = column.get_Geometry(opt);
            if (ge == null) return null;

            XYZ targetNormal = horizontal ? view.RightDirection : view.UpDirection;
            targetNormal = ProjectVectorToView(view, targetNormal).Normalize();

            Reference? best = null;
            double bestAlign = -1;

            void Visit(GeometryElement geometry)
            {
                foreach (GeometryObject obj in geometry)
                {
                    if (obj is Solid solid)
                    {
                        foreach (Face face in solid.Faces)
                        {
                            if (face is not PlanarFace pf || pf.Reference == null) continue;
                            XYZ n = pf.FaceNormal.Normalize();
                            double align = Math.Abs(n.DotProduct(targetNormal));
                            if (align > bestAlign)
                            {
                                bestAlign = align;
                                Reference r = pf.Reference;
                                best = link != null ? r.CreateLinkReference(link) : r;
                            }
                        }
                    }
                    else if (obj is GeometryInstance inst)
                    {
                        Visit(inst.GetInstanceGeometry());
                    }
                }
            }

            Visit(ge);
            return bestAlign > 0.7 ? best : null;
        }

        private static XYZ? GetReferencePoint(Reference reference, View view)
        {
            try
            {
                XYZ p = reference.GlobalPoint;
                return FlattenToView(view, p);
            }
            catch
            {
                return null;
            }
        }

        private static XYZ GetElementCenter(Element e, RevitLinkInstance? link = null)
        {
            XYZ center;
            if (e.Location is LocationPoint lp) center = lp.Point;
            else if (e.Location is LocationCurve lc) center = (lc.Curve.GetEndPoint(0) + lc.Curve.GetEndPoint(1)) * 0.5;
            else
            {
                BoundingBoxXYZ? bbox = e.get_BoundingBox(null);
                center = bbox == null ? XYZ.Zero : (bbox.Min + bbox.Max) * 0.5;
            }
            return link != null ? link.GetTotalTransform().OfPoint(center) : center;
        }

        private bool TryCreateDimension(Document doc, View view, Line dimLine, ReferenceArray refs)
        {
            if (refs == null || refs.Size < 2 || dimLine == null)
            {
                Log($"        [TryCreate] ✗ Pre-check fail: refs={refs?.Size}, dimLine={dimLine != null}");
                return false;
            }
            if (dimLine.Length < 0.01)
            {
                Log($"        [TryCreate] ✗ Dimension line too short: {dimLine.Length:F6} ft");
                return false;
            }

            try
            {
                Dimension? dim;
                if (DimensionStyleId != ElementId.InvalidElementId &&
                    doc.GetElement(DimensionStyleId) is DimensionType dimType)
                {
                    dim = doc.Create.NewDimension(view, dimLine, refs, dimType);
                    Log($"        [TryCreate] NewDimension with style '{dimType.Name}' → {(dim != null && dim.IsValidObject ? "✓" : "✗ null/invalid")}");
                }
                else
                {
                    dim = doc.Create.NewDimension(view, dimLine, refs);
                    Log($"        [TryCreate] NewDimension (default style) → {(dim != null && dim.IsValidObject ? "✓" : "✗ null/invalid")}");
                }

                return dim != null && dim.IsValidObject;
            }
            catch (Exception ex)
            {
                Log($"        [TryCreate] ✗ EXCEPTION: {ex.Message}");
                return false;
            }
        }

        private static bool IsSupportedDimensionView(View view)
        {
            return view.ViewType is ViewType.FloorPlan or ViewType.CeilingPlan or ViewType.EngineeringPlan
                or ViewType.AreaPlan or ViewType.Section or ViewType.Elevation or ViewType.Detail;
        }

        private static XYZ FlattenToView(View view, XYZ point)
        {
            if (view is ViewPlan plan && plan.GenLevel != null)
                return new XYZ(point.X, point.Y, plan.GenLevel.Elevation);

            XYZ origin = view.Origin;
            XYZ normal = view.ViewDirection.Normalize();
            double dist = (point - origin).DotProduct(normal);
            return point - normal * dist;
        }

        private static XYZ ProjectVectorToView(View view, XYZ vector)
        {
            XYZ n = view.ViewDirection.Normalize();
            return (vector - n * vector.DotProduct(n)).Normalize();
        }

        private static XYZ GetPlanPerpendicular(XYZ direction, View view)
        {
            XYZ dir = ProjectVectorToView(view, direction).Normalize();
            XYZ perp = dir.CrossProduct(view.ViewDirection).Normalize();
            if (perp.IsAlmostEqualTo(XYZ.Zero))
                perp = view.UpDirection.Normalize();
            return perp;
        }

        private static double? GetCoordinateOfReference(Document doc, Reference r, View view, XYZ axis)
        {
            try
            {
                XYZ pt = r.GlobalPoint;
                if (pt != null)
                {
                    return FlattenToView(view, pt).DotProduct(axis);
                }
            }
            catch { }

            try
            {
                Element el = doc.GetElement(r.ElementId);
                if (el != null)
                {
                    RevitLinkInstance? linkInst = el as RevitLinkInstance;
                    Element? targetEl = el;
                    Transform t = Transform.Identity;

                    if (linkInst != null)
                    {
                        t = linkInst.GetTotalTransform();
                        Document linkDoc = linkInst.GetLinkDocument();
                        if (linkDoc != null && r.LinkedElementId != ElementId.InvalidElementId)
                        {
                            targetEl = linkDoc.GetElement(r.LinkedElementId);
                        }
                    }

                    if (targetEl != null)
                    {
                        if (targetEl is FamilyInstance fi)
                        {
                            t = t.Multiply(fi.GetTransform());
                        }

                        if (targetEl is Grid grid)
                        {
                            Curve curve = grid.Curve;
                            Curve transformed = t.IsIdentity ? curve : curve.CreateTransformed(t);
                            XYZ pt = transformed.Evaluate(0.5, true);
                            return FlattenToView(view, pt).DotProduct(axis);
                        }

                        GeometryObject geo = targetEl.GetGeometryObjectFromReference(r);
                        if (geo != null)
                        {
                            if (geo is PlanarFace pf)
                            {
                                XYZ pt = t.IsIdentity ? pf.Origin : t.OfPoint(pf.Origin);
                                return FlattenToView(view, pt).DotProduct(axis);
                            }
                            else if (geo is Edge edge)
                            {
                                XYZ pt = edge.Evaluate(0.5);
                                XYZ transformedPt = t.IsIdentity ? pt : t.OfPoint(pt);
                                return FlattenToView(view, transformedPt).DotProduct(axis);
                            }
                            else if (geo is Autodesk.Revit.DB.Point point)
                            {
                                XYZ pt = point.Coord;
                                XYZ transformedPt = t.IsIdentity ? pt : t.OfPoint(pt);
                                return FlattenToView(view, transformedPt).DotProduct(axis);
                            }
                        }
                    }
                }
            }
            catch { }

            return null;
        }

        private static ReferenceArray DeduplicateReferences(Document doc, ReferenceArray refs, View view, XYZ measureDirection)
        {
            var unique = new List<(Reference Ref, double Position, string Stable)>();
            XYZ axis = measureDirection.Normalize();

            for (int i = 0; i < refs.Size; i++)
            {
                Reference r = refs.get_Item(i);
                if (r == null) continue;

                double pos;
                double? calculatedPos = GetCoordinateOfReference(doc, r, view, axis);
                if (calculatedPos.HasValue)
                {
                    pos = calculatedPos.Value;
                }
                else
                {
                    pos = -99999.0 + i;
                }

                string stable = "";
                try { stable = r.ConvertToStableRepresentation(doc) ?? ""; } catch { }

                bool exists = unique.Any(u =>
                {
                    if (stable != "" && u.Stable == stable)
                        return true;

                    if (pos > -90000.0 && u.Position > -90000.0)
                    {
                        return Math.Abs(u.Position - pos) < 0.05; // ~15mm tolerance
                    }
                    return false;
                });

                if (!exists)
                    unique.Add((r, pos, stable));
            }

            unique.Sort((a, b) => a.Position.CompareTo(b.Position));
            var result = new ReferenceArray();
            foreach (var item in unique)
                result.Append(item.Ref);
            return result;
        }

        private List<(T Element, RevitLinkInstance Link)> GetElements<T>(Document doc, BuiltInCategory category) where T : Element
        {
            var results = new List<(T Element, RevitLinkInstance Link)>();
            
            if (UseSelection)
            {
                var selIds = _app.ActiveUIDocument.Selection.GetElementIds();
                if (selIds.Count > 0)
                {
                    var elements = new FilteredElementCollector(doc, selIds)
                        .OfCategory(category)
                        .WhereElementIsNotElementType()
                        .Cast<T>();
                    foreach (var e in elements) results.Add((e, null));
                }
                return results;
            }

            if (IncludeHost)
            {
                var elements = new FilteredElementCollector(doc, doc.ActiveView.Id)
                    .OfCategory(category)
                    .WhereElementIsNotElementType()
                    .Cast<T>();
                foreach (var e in elements) results.Add((e, null));
            }

            if (IncludeLinked)
            {
                var links = new FilteredElementCollector(doc).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>();
                foreach (var link in links)
                {
                    Document linkDoc = link.GetLinkDocument();
                    if (linkDoc == null) continue;
                    var elements = new FilteredElementCollector(linkDoc)
                        .OfCategory(category)
                        .WhereElementIsNotElementType()
                        .Cast<T>();
                    foreach (var e in elements) results.Add((e, link));
                }
            }

            return results;
        }

        private List<(Grid Element, RevitLinkInstance? Link)> GetGrids(Document doc, View view)
        {
            var results = new List<(Grid Element, RevitLinkInstance? Link)>();
            if (UseSelection)
            {
                var selIds = _app.ActiveUIDocument.Selection.GetElementIds();
                if (selIds.Count > 0)
                {
                    var grids = new FilteredElementCollector(doc, selIds).OfClass(typeof(Grid)).Cast<Grid>();
                    foreach (var g in grids) results.Add((g, null));
                }
                return results;
            }

            if (IncludeHost)
            {
                var viewGrids = new FilteredElementCollector(doc, view.Id)
                    .OfClass(typeof(Grid))
                    .Cast<Grid>()
                    .ToList();
                IEnumerable<Grid> hostGrids = viewGrids.Count >= 2
                    ? viewGrids
                    : new FilteredElementCollector(doc).OfClass(typeof(Grid)).Cast<Grid>();
                foreach (var g in hostGrids)
                    results.Add((g, null));
            }

            if (IncludeLinked)
            {
                var links = new FilteredElementCollector(doc).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>();
                foreach (var link in links)
                {
                    Document linkDoc = link.GetLinkDocument();
                    if (linkDoc == null) continue;
                    var grids = new FilteredElementCollector(linkDoc).OfClass(typeof(Grid)).Cast<Grid>();
                    foreach (var g in grids) results.Add((g, link));
                }
            }
            return results;
        }

        public string GetName() => "Dimension Automation Handler";
    }
}
