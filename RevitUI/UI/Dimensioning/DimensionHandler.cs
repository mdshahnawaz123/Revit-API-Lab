using Autodesk.Revit.DB;
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
        public double OffsetMm { get; set; } = 1000;
        public bool IncludeHost { get; set; } = true;
        public bool IncludeLinked { get; set; } = false;
        public bool SameGroup { get; set; } = false;
        public bool MultiTierGrids { get; set; } = true;
        public bool WallCoreOnly { get; set; } = false;
        public string OpeningDimMode { get; set; } = "Faces"; // "Faces" or "Centers"
        public ElementId DimensionStyleId { get; set; } = ElementId.InvalidElementId;
        public bool UseSelection { get; set; } = false;
        private UIApplication _app;

        public void Execute(UIApplication app)
        {
            _app = app;
            Document doc = app.ActiveUIDocument.Document;
            int createdCount = 0;

            using (Transaction trans = new Transaction(doc, "Dimension Automation"))
            {
                trans.Start();
                try
                {
                    if (UseSelection)
                    {
                        var selIds = app.ActiveUIDocument.Selection.GetElementIds();
                        if (selIds.Count == 0)
                        {
                            TaskDialog.Show("B-Lab", "Please select elements before running the tool.");
                            trans.RollBack();
                            return;
                        }
                    }

                    switch (Mode)
                    {
                        case DimMode.Grids:
                            createdCount = DimensionGrids(doc);
                            break;
                        case DimMode.Walls:
                            createdCount = DimensionWalls(doc);
                            break;
                        case DimMode.Rooms:
                            createdCount = DimensionRooms(doc);
                            break;
                        case DimMode.MEP:
                            createdCount = DimensionMEP(doc);
                            break;
                        case DimMode.Columns:
                            createdCount = DimensionColumns(doc);
                            break;
                        case DimMode.CurtainWalls:
                            createdCount = DimensionCurtainWalls(doc);
                            break;
                    }
                    
                    if (createdCount > 0)
                    {
                        trans.Commit();
                        TaskDialog.Show("B-Lab", $"Successfully created {createdCount} dimension(s).");
                    }
                    else
                    {
                        trans.RollBack();
                        TaskDialog.Show("B-Lab", "Process failed. No dimensions were created. Please check your settings, view requirements, or element geometry.");
                    }
                }
                catch (Exception ex)
                {
                    trans.RollBack();
                    TaskDialog.Show("Dimension Error", "Process failed: " + ex.Message);
                }
            }
        }

        private int DimensionWalls(Document doc)
        {
            int count = 0;
            var wallData = GetElements<Wall>(doc, BuiltInCategory.OST_Walls);

            // Implement "Same Group" logic
            IEnumerable<IEnumerable<(Wall Element, RevitLinkInstance Link)>> wallGroups;
            if (SameGroup)
            {
                wallGroups = wallData.GroupBy(x => x.Element.GroupId).Select(g => g.AsEnumerable());
            }
            else
            {
                wallGroups = new[] { wallData };
            }

            foreach (var group in wallGroups)
            {
                foreach (var item in group)
                {
                    Wall wall = item.Element;
                    RevitLinkInstance link = item.Link;

                    LocationCurve locCurve = wall.Location as LocationCurve;
                    if (locCurve == null) continue;
                    Curve curve = locCurve.Curve;
                    if (link != null) curve = curve.CreateTransformed(link.GetTotalTransform());

                    ReferenceArray refs = new ReferenceArray();
                    
                    if (WallCoreOnly)
                    {
                        // Dimension to Centerline
                        try {
                            // Trying to get the centerline reference. 
                            // Note: For some elements, the direct reference works as centerline.
                            refs.Append(new Reference(wall)); 
                        } catch { }
                    }
                    else
                    {
                        Options opt = new Options { ComputeReferences = true, DetailLevel = ViewDetailLevel.Fine };
                        GeometryElement geo = wall.get_Geometry(opt);
                        ProcessGeometry(geo, curve, refs, link);
                    }

                    // Add Openings if requested
                    if (OpeningDimMode == "Centers")
                    {
                        AddOpeningCenters(wall, refs, link);
                    }

                    if (refs.Size >= 2)
                    {
                        if (curve is Line line)
                        {
                            XYZ dir = line.Direction.Normalize();
                            XYZ normal = new XYZ(-dir.Y, dir.X, 0);
                            
                            double offsetFeet = OffsetMm / 304.8;
                            Line dimLine = Line.CreateBound(line.GetEndPoint(0) + normal * offsetFeet, line.GetEndPoint(1) + normal * offsetFeet);
                            try { doc.Create.NewDimension(doc.ActiveView, dimLine, refs); count++; } catch { }
                        }
                    }
                }
            }
            return count;
        }

        private void ProcessGeometry(GeometryElement geo, Curve transformedCurve, ReferenceArray refs, RevitLinkInstance link)
        {
            foreach (var obj in geo)
            {
                if (obj is Solid solid && solid.Faces.Size > 0)
                {
                    AddWallFaces(solid, transformedCurve, refs, link);
                }
                else if (obj is GeometryInstance inst)
                {
                    GeometryElement instGeo = inst.GetInstanceGeometry();
                    ProcessGeometry(instGeo, transformedCurve, refs, link);
                }
            }
        }
            
        private void AddOpeningCenters(Wall wall, ReferenceArray refs, RevitLinkInstance link)
        {
            var doc = wall.Document;
            var inserts = wall.FindInserts(true, true, true, true);
            foreach (var id in inserts)
            {
                var e = doc.GetElement(id);
                if (e is FamilyInstance fi)
                {
                    // For wall-hosted items, we want the center reference parallel to the wall
                    var centerRefs = fi.GetReferences(FamilyInstanceReferenceType.CenterLeftRight);
                    if (centerRefs.Count == 0) centerRefs = fi.GetReferences(FamilyInstanceReferenceType.CenterFrontBack);
                    
                    if (centerRefs.Count > 0)
                    {
                        Reference r = centerRefs.First();
                        if (link != null) r = r.CreateLinkReference(link);
                        try { refs.Append(r); } catch { }
                    }
                }
            }
        }

        private void AddWallFaces(Solid solid, Curve transformedCurve, ReferenceArray refs, RevitLinkInstance link)
        {
            Transform t = link?.GetTotalTransform() ?? Transform.Identity;
            foreach (Face face in solid.Faces)
            {
                if (face is PlanarFace planar)
                {
                    XYZ normal = t.OfVector(planar.FaceNormal);
                    if (transformedCurve is Line line)
                    {
                        XYZ dir = line.Direction.Normalize();
                        if (Math.Abs(normal.DotProduct(dir)) < 0.01)
                        {
                            if (planar.Reference != null)
                            {
                                Reference r = planar.Reference;
                                if (link != null) r = r.CreateLinkReference(link);
                                try { refs.Append(r); } catch { }
                            }
                        }
                    }
                }
            }
        }



        private int DimensionRooms(Document doc)
        {
            int count = 0;
            var roomData = GetElements<SpatialElement>(doc, BuiltInCategory.OST_Rooms);

            if (roomData.Count == 0) return count;

            Options geomOptions = new Options { ComputeReferences = true };

            foreach (var item in roomData)
            {
                SpatialElement room = item.Element;
                RevitLinkInstance link = item.Link;
                XYZ center = GetElementCenter(room);
                if (link != null) center = link.GetTotalTransform().OfPoint(center);
                
                GeometryElement geomElem = room.get_Geometry(geomOptions);
                if (geomElem == null) continue;

                List<PlanarFace> verticalFaces = new List<PlanarFace>();

                foreach (GeometryObject geomObj in geomElem)
                {
                    if (geomObj is Solid solid && solid.Faces.Size > 0)
                    {
                        foreach (Face face in solid.Faces)
                        {
                            if (face is PlanarFace pf)
                            {
                                // Check if the face is vertical (normal has Z ≈ 0)
                                XYZ normal = pf.FaceNormal;
                                if (link != null) normal = link.GetTotalTransform().OfVector(normal);
                                
                                if (Math.Abs(normal.Z) < 0.001)
                                {
                                    verticalFaces.Add(pf);
                                }
                            }
                        }
                    }
                }

                if (verticalFaces.Count == 0) continue;

                // Group vertical faces by their normal direction
                var directionGroups = verticalFaces.GroupBy(f => {
                    XYZ normal = f.FaceNormal;
                    if (link != null) normal = link.GetTotalTransform().OfVector(normal);
                    normal = normal.Normalize();
                    if (normal.X < 0 || (Math.Abs(normal.X) < 0.001 && normal.Y < 0)) normal = -normal;
                    return $"{Math.Round(normal.X, 3)}_{Math.Round(normal.Y, 3)}";
                }).ToList();

                foreach (var group in directionGroups)
                {
                    if (group.Count() < 2) continue;

                    ReferenceArray refs = new ReferenceArray();
                    foreach (var f in group)
                    {
                        if (f.Reference != null)
                        {
                            Reference r = f.Reference;
                            if (link != null) r = r.CreateLinkReference(link);
                            refs.Append(r);
                        }
                    }

                    if (refs.Size < 2) continue;

                    // The dimension line direction must be parallel to the face normal
                    XYZ faceNormal = group.First().FaceNormal;
                    if (link != null) faceNormal = link.GetTotalTransform().OfVector(faceNormal);
                    XYZ dimDir = faceNormal.Normalize();
                    if (dimDir.X < 0 || (Math.Abs(dimDir.X) < 0.001 && dimDir.Y < 0)) dimDir = -dimDir;

                    // Project all face origins onto the dimension direction to find the min and max bounds
                    double minPos = double.MaxValue;
                    double maxPos = double.MinValue;
                    foreach (var f in group)
                    {
                        XYZ p = f.Origin;
                        if (link != null) p = link.GetTotalTransform().OfPoint(p);
                        double pos = p.DotProduct(dimDir);
                        if (pos < minPos) minPos = pos;
                        if (pos > maxPos) maxPos = pos;
                    }

                    // Create the dimension line passing through the room center
                    double dist = Math.Max(10.0, maxPos - minPos);
                    XYZ dimStart = center - dimDir * dist;
                    XYZ dimEnd = center + dimDir * dist;

                    // Force Z = 0 to ensure it's flat on the view plane
                    dimStart = new XYZ(dimStart.X, dimStart.Y, 0);
                    dimEnd = new XYZ(dimEnd.X, dimEnd.Y, 0);

                    if (dimStart.DistanceTo(dimEnd) < 0.01) continue;

                    Line dimLine = Line.CreateBound(dimStart, dimEnd);

                    try 
                    { 
                        if (DimensionStyleId != ElementId.InvalidElementId)
                            doc.Create.NewDimension(doc.ActiveView, dimLine, refs, doc.GetElement(DimensionStyleId) as DimensionType);
                        else
                            doc.Create.NewDimension(doc.ActiveView, dimLine, refs);
                        count++; 
                    } 
                    catch { }
                }
            }
            return count;
        }

        private int DimensionGrids(Document doc)
        {
            int count = 0;
            var gridData = GetGrids(doc);

            if (gridData.Count < 2) return count;

            // Group grids by direction (to handle angled grids)
            var directionGroups = gridData.GroupBy(x => GetDirectionKey(x.Element, x.Link)).ToList();

            foreach (var group in directionGroups)
            {
                var sortedGrids = group.OrderBy(x => GetPositionAlongNormal(x.Element, x.Link)).ToList();
                if (sortedGrids.Count > 1)
                {
                    count += CreateGridDimension(doc, sortedGrids);
                }
            }
            return count;
        }

        private string GetDirectionKey(Grid g, RevitLinkInstance link)
        {
            if (g.Curve is Line l)
            {
                XYZ dir = l.Direction.Normalize();
                if (link != null) dir = link.GetTotalTransform().OfVector(dir);
                // Standardize direction (e.g., always pointing positive X or positive Y)
                if (dir.X < 0 || (Math.Abs(dir.X) < 0.001 && dir.Y < 0)) dir = -dir;
                return $"{Math.Round(dir.X, 3)}_{Math.Round(dir.Y, 3)}_{Math.Round(dir.Z, 3)}";
            }
            return "unknown";
        }

        private double GetPositionAlongNormal(Grid g, RevitLinkInstance link)
        {
            if (g.Curve is Line l)
            {
                XYZ p = l.GetEndPoint(0);
                if (link != null) p = link.GetTotalTransform().OfPoint(p);
                XYZ dir = l.Direction.Normalize();
                if (link != null) dir = link.GetTotalTransform().OfVector(dir);
                if (dir.X < 0 || (Math.Abs(dir.X) < 0.001 && dir.Y < 0)) dir = -dir;
                XYZ normal = new XYZ(-dir.Y, dir.X, 0).Normalize();
                return p.DotProduct(normal);
            }
            return 0;
        }

        private int DimensionCurtainWalls(Document doc)
        {
            int count = 0;
            var walls = new FilteredElementCollector(doc, doc.ActiveView.Id)
                .OfClass(typeof(Wall))
                .Cast<Wall>()
                .Where(w => w.WallType.Kind == WallKind.Curtain)
                .ToList();

            foreach (var wall in walls)
            {
                var grid = wall.CurtainGrid;
                if (grid == null) continue;

                // Dimension U-Grids
                var uGridIds = grid.GetUGridLineIds();
                if (uGridIds.Count > 1) count += CreateCurtainGridDim(doc, wall, uGridIds);

                // Dimension V-Grids
                var vGridIds = grid.GetVGridLineIds();
                if (vGridIds.Count > 1) count += CreateCurtainGridDim(doc, wall, vGridIds);
            }
            return count;
        }

        private int CreateCurtainGridDim(Document doc, Wall wall, ICollection<ElementId> gridIds)
        {
            ReferenceArray refs = new ReferenceArray();
            foreach (var id in gridIds)
            {
                var gridLine = doc.GetElement(id) as CurtainGridLine;
                if (gridLine != null)
                {
                    var r = gridLine.get_Geometry(new Options { ComputeReferences = true }).Cast<GeometryObject>()
                        .Select(x => (x as Curve)?.Reference).FirstOrDefault(x => x != null);
                    if (r != null) refs.Append(r);
                }
            }

            if (refs.Size < 2) return 0;

            LocationCurve lc = wall.Location as LocationCurve;
            if (lc == null) return 0;
            Curve curve = lc.Curve;
            XYZ dir = (curve.GetEndPoint(1) - curve.GetEndPoint(0)).Normalize();
            XYZ normal = new XYZ(-dir.Y, dir.X, 0);
            Line dimLine = Line.CreateBound(curve.GetEndPoint(0) + normal * 2, curve.GetEndPoint(1) + normal * 2);

            try { doc.Create.NewDimension(doc.ActiveView, dimLine, refs); return 1; } catch { return 0; }
        }

        private bool IsVertical(Grid g, RevitLinkInstance link)
        {
            if (g.Curve is Line l)
            {
                XYZ dir = l.Direction;
                if (link != null) dir = link.GetTotalTransform().OfVector(dir);
                return Math.Abs(dir.X) < 0.01;
            }
            return false;
        }

        private double GetX(Grid g, RevitLinkInstance link)
        {
            if (g.Curve is Line l)
            {
                XYZ p = l.GetEndPoint(0);
                if (link != null) p = link.GetTotalTransform().OfPoint(p);
                return p.X;
            }
            return 0;
        }

        private double GetY(Grid g, RevitLinkInstance link)
        {
            if (g.Curve is Line l)
            {
                XYZ p = l.GetEndPoint(0);
                if (link != null) p = link.GetTotalTransform().OfPoint(p);
                return p.Y;
            }
            return 0;
        }

        private int CreateGridDimension(Document doc, List<(Grid Element, RevitLinkInstance Link)> sortedGrids)
        {
            int count = 0;
            ReferenceArray refs = new ReferenceArray();
            foreach (var item in sortedGrids)
            {
                Reference r = new Reference(item.Element);
                if (item.Link != null) r = r.CreateLinkReference(item.Link);
                refs.Append(r);
            }

            if (refs.Size < 2) return 0;

            Line firstLine = sortedGrids.First().Element.Curve as Line;
            XYZ dir = firstLine.Direction.Normalize();
            if (sortedGrids.First().Link != null) dir = sortedGrids.First().Link.GetTotalTransform().OfVector(dir);
            if (dir.X < 0 || (Math.Abs(dir.X) < 0.001 && dir.Y < 0)) dir = -dir;
            XYZ normal = new XYZ(-dir.Y, dir.X, 0).Normalize();

            double offsetFeet = OffsetMm / 304.8;
            
            // Find average center of all grids to ensure the dimension line crosses them geometrically
            XYZ avgCenter = XYZ.Zero;
            foreach (var item in sortedGrids)
            {
                Line l = item.Element.Curve as Line;
                XYZ p = (l.GetEndPoint(0) + l.GetEndPoint(1)) * 0.5;
                if (item.Link != null) p = item.Link.GetTotalTransform().OfPoint(p);
                avgCenter += p;
            }
            avgCenter /= sortedGrids.Count;

            double avgPosAlongDir = avgCenter.DotProduct(dir);
            double posFirst = GetPositionAlongNormal(sortedGrids.First().Element, sortedGrids.First().Link);
            double posLast = GetPositionAlongNormal(sortedGrids.Last().Element, sortedGrids.Last().Link);
            
            // Construct dimension line points using orthonormal basis
            XYZ dimStart = dir * (avgPosAlongDir + offsetFeet) + normal * posFirst;
            XYZ dimEnd = dir * (avgPosAlongDir + offsetFeet) + normal * posLast;

            dimStart = new XYZ(dimStart.X, dimStart.Y, 0);
            dimEnd = new XYZ(dimEnd.X, dimEnd.Y, 0);

            if (dimStart.DistanceTo(dimEnd) < 0.01) return 0;

            Line line = Line.CreateBound(dimStart, dimEnd);

            try {
                Dimension dim;
                if (DimensionStyleId != ElementId.InvalidElementId)
                    dim = doc.Create.NewDimension(doc.ActiveView, line, refs, doc.GetElement(DimensionStyleId) as DimensionType);
                else
                    dim = doc.Create.NewDimension(doc.ActiveView, line, refs);
                count++;
            } catch { }

            if (MultiTierGrids && sortedGrids.Count > 2)
            {
                ReferenceArray overallRefs = new ReferenceArray();
                var rStart = new Reference(sortedGrids.First().Element);
                var rEnd = new Reference(sortedGrids.Last().Element);
                if (sortedGrids.First().Link != null) rStart = rStart.CreateLinkReference(sortedGrids.First().Link);
                if (sortedGrids.Last().Link != null) rEnd = rEnd.CreateLinkReference(sortedGrids.Last().Link);
                overallRefs.Append(rStart);
                overallRefs.Append(rEnd);

                double extraOffset = 500 / 304.8;
                XYZ overallStart = dir * (avgPosAlongDir + offsetFeet + extraOffset) + normal * posFirst;
                XYZ overallEnd = dir * (avgPosAlongDir + offsetFeet + extraOffset) + normal * posLast;
                overallStart = new XYZ(overallStart.X, overallStart.Y, 0);
                overallEnd = new XYZ(overallEnd.X, overallEnd.Y, 0);

                Line overallLine = Line.CreateBound(overallStart, overallEnd);

                try {
                    if (DimensionStyleId != ElementId.InvalidElementId)
                        doc.Create.NewDimension(doc.ActiveView, overallLine, overallRefs, doc.GetElement(DimensionStyleId) as DimensionType);
                    else
                        doc.Create.NewDimension(doc.ActiveView, overallLine, overallRefs);
                    count++;
                } catch { }
            }
            return count;
        }

        private int DimensionColumns(Document doc)
        {
            int count = 0;
            var columnData = GetElements<FamilyInstance>(doc, BuiltInCategory.OST_StructuralColumns);

            if (columnData.Count == 0) return 0;

            var grids = GetGrids(doc);
            if (grids.Count == 0) return 0;

            foreach (var item in columnData)
            {
                XYZ center = GetElementCenter(item.Element);
                if (item.Link != null) center = item.Link.GetTotalTransform().OfPoint(center);
                
                var nearestV = grids.Where(g => IsVertical(g.Element, g.Link)).OrderBy(g => Math.Abs(GetX(g.Element, g.Link) - center.X)).FirstOrDefault();
                var nearestH = grids.Where(g => !IsVertical(g.Element, g.Link)).OrderBy(g => Math.Abs(GetY(g.Element, g.Link) - center.Y)).FirstOrDefault();

                if (nearestV.Element != null) count += CreateSingleDim(doc, item.Element, item.Link, nearestV.Element, nearestV.Link, true);
                if (nearestH.Element != null) count += CreateSingleDim(doc, item.Element, item.Link, nearestH.Element, nearestH.Link, false);
            }
            return count;
        }

        private int DimensionMEP(Document doc)
        {
            int count = 0;
            var openingData = GetElements<FamilyInstance>(doc, BuiltInCategory.OST_GenericModel)
                .Where(x => x.Element.Symbol.FamilyName.Contains("Opening") || x.Element.Symbol.FamilyName.Contains("Sleeve"))
                .ToList();

            if (openingData.Count == 0) return 0;

            var grids = GetGrids(doc);
            var columns = GetElements<FamilyInstance>(doc, BuiltInCategory.OST_StructuralColumns);

            foreach (var item in openingData)
            {
                XYZ center = GetElementCenter(item.Element);
                if (item.Link != null) center = item.Link.GetTotalTransform().OfPoint(center);
                
                var nearestV = grids.Where(g => IsVertical(g.Element, g.Link)).OrderBy(g => Math.Abs(GetX(g.Element, g.Link) - center.X)).FirstOrDefault();
                var nearestH = grids.Where(g => !IsVertical(g.Element, g.Link)).OrderBy(g => Math.Abs(GetY(g.Element, g.Link) - center.Y)).FirstOrDefault();

                if (nearestV.Element != null) count += CreateSingleDim(doc, item.Element, item.Link, nearestV.Element, nearestV.Link, true);
                if (nearestH.Element != null) count += CreateSingleDim(doc, item.Element, item.Link, nearestH.Element, nearestH.Link, false);

                var nearestCol = columns.OrderBy(c => {
                    XYZ cCenter = GetElementCenter(c.Element);
                    if (c.Link != null) cCenter = c.Link.GetTotalTransform().OfPoint(cCenter);
                    return cCenter.DistanceTo(center);
                }).FirstOrDefault();
                
                if (nearestCol.Element != null)
                {
                    XYZ cCenter = GetElementCenter(nearestCol.Element);
                    if (nearestCol.Link != null) cCenter = nearestCol.Link.GetTotalTransform().OfPoint(cCenter);
                    if (cCenter.DistanceTo(center) < 5.0)
                    {
                        count += CreateSingleDim(doc, item.Element, item.Link, nearestCol.Element, nearestCol.Link, true);
                    }
                }
            }
            return count;
        }

        private int CreateSingleDim(Document doc, Element e1, RevitLinkInstance l1, Element e2, RevitLinkInstance l2, bool horizontal)
        {
            ReferenceArray refs = new ReferenceArray();
            
            Reference r1 = GetBestReference(e1, horizontal);
            Reference r2 = GetBestReference(e2, horizontal);

            if (r1 == null || r2 == null) return 0;

            if (l1 != null) r1 = r1.CreateLinkReference(l1);
            if (l2 != null) r2 = r2.CreateLinkReference(l2);

            refs.Append(r1);
            refs.Append(r2);

            XYZ p1 = GetElementCenter(e1);
            if (l1 != null) p1 = l1.GetTotalTransform().OfPoint(p1);
            XYZ p2 = GetElementCenter(e2);
            if (l2 != null) p2 = l2.GetTotalTransform().OfPoint(p2);
            
            Line line;
            if (horizontal)
                line = Line.CreateBound(new XYZ(p1.X, p1.Y, 0), new XYZ(p2.X, p1.Y, 0));
            else
                line = Line.CreateBound(new XYZ(p1.X, p1.Y, 0), new XYZ(p1.X, p2.Y, 0));

            try { doc.Create.NewDimension(doc.ActiveView, line, refs); return 1; } catch { return 0; }
        }

        private Reference GetBestReference(Element e, bool horizontal)
        {
            if (e is Grid g) return new Reference(g);
            
            if (e is FamilyInstance fi)
            {
                if (horizontal)
                {
                    var refs = fi.GetReferences(FamilyInstanceReferenceType.CenterLeftRight);
                    if (refs.Count > 0) return refs.First();
                }
                else
                {
                    var refs = fi.GetReferences(FamilyInstanceReferenceType.CenterFrontBack);
                    if (refs.Count > 0) return refs.First();
                }

                var anyRefs = fi.GetReferences(FamilyInstanceReferenceType.CenterLeftRight);
                if (anyRefs.Count > 0) return anyRefs.First();
                anyRefs = fi.GetReferences(FamilyInstanceReferenceType.CenterFrontBack);
                if (anyRefs.Count > 0) return anyRefs.First();
            }

            return null;
        }

        private XYZ GetElementCenter(Element e)
        {
            if (e.Location is LocationPoint lp) return lp.Point;
            if (e.Location is LocationCurve lc) return (lc.Curve.GetEndPoint(0) + lc.Curve.GetEndPoint(1)) * 0.5;
            
            BoundingBoxXYZ bbox = e.get_BoundingBox(null);
            if (bbox == null) return XYZ.Zero;
            return (bbox.Min + bbox.Max) * 0.5;
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

        private List<(Grid Element, RevitLinkInstance Link)> GetGrids(Document doc)
        {
            var results = new List<(Grid Element, RevitLinkInstance Link)>();
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
                var grids = new FilteredElementCollector(doc, doc.ActiveView.Id).OfClass(typeof(Grid)).Cast<Grid>();
                foreach (var g in grids) results.Add((g, null));
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
