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

            using (Transaction trans = new Transaction(doc, "Dimension Automation"))
            {
                trans.Start();
                try
                {
                    if (UseSelection)
                    {
                        // Logic to filter selection by mode...
                    }

                    switch (Mode)
                    {
                        case DimMode.Grids:
                            DimensionGrids(doc);
                            break;
                        case DimMode.Walls:
                            DimensionWalls(doc);
                            break;
                        case DimMode.Rooms:
                            DimensionRooms(doc);
                            break;
                        case DimMode.MEP:
                            DimensionMEP(doc);
                            break;
                        case DimMode.Columns:
                            DimensionColumns(doc);
                            break;
                        case DimMode.CurtainWalls:
                            DimensionCurtainWalls(doc);
                            break;
                    }
                    trans.Commit();
                }
                catch (Exception ex)
                {
                    trans.RollBack();
                    TaskDialog.Show("Dimension Error", ex.Message);
                }
            }
        }

        private void DimensionWalls(Document doc)
        {
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
                            try { doc.Create.NewDimension(doc.ActiveView, dimLine, refs); } catch { }
                        }
                    }
                }
            }
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



        private void DimensionRooms(Document doc)
        {
            var roomData = GetElements<SpatialElement>(doc, BuiltInCategory.OST_Rooms);

            if (roomData.Count == 0)
            {
                TaskDialog.Show("B-Lab", "No rooms found in the active view.");
                return;
            }

            var grids = new FilteredElementCollector(doc, doc.ActiveView.Id).OfClass(typeof(Grid)).Cast<Grid>().ToList();
            if (grids.Count == 0)
            {
                TaskDialog.Show("B-Lab", "No grids found in the active view to dimension against.");
                return;
            }

            foreach (var item in roomData)
            {
                SpatialElement room = item.Element;
                RevitLinkInstance link = item.Link;
                XYZ center = GetElementCenter(room);
                
                // If the room is from a link, transform its center to host
                if (link != null) center = link.GetTotalTransform().OfPoint(center);
                
                // Dimension to nearest vertical grids
                var vGrids = grids.Where(g => IsVertical(g, null)).OrderBy(g => Math.Abs(GetX(g, null) - center.X)).ToList();
                if (vGrids.Count >= 2)
                {
                    var gLeft = vGrids.Where(g => GetX(g, null) < center.X).OrderByDescending(g => GetX(g, null)).FirstOrDefault();
                    var gRight = vGrids.Where(g => GetX(g, null) >= center.X).OrderBy(g => GetX(g, null)).FirstOrDefault();
                    if (gLeft != null && gRight != null) CreateRoomDimBetweenGrids(doc, center, gLeft, gRight, true);
                }

                // Dimension to nearest horizontal grids
                var hGrids = grids.Where(g => !IsVertical(g, null)).OrderBy(g => Math.Abs(GetY(g, null) - center.Y)).ToList();
                if (hGrids.Count >= 2)
                {
                    var gBot = hGrids.Where(g => GetY(g, null) < center.Y).OrderByDescending(g => GetY(g, null)).FirstOrDefault();
                    var gTop = hGrids.Where(g => GetY(g, null) >= center.Y).OrderBy(g => GetY(g, null)).FirstOrDefault();
                    if (gBot != null && gTop != null) CreateRoomDimBetweenGrids(doc, center, gBot, gTop, false);
                }
            }
        }

        private void CreateRoomDimBetweenGrids(Document doc, XYZ roomCenter, Grid g1, Grid g2, bool vertical)
        {
            ReferenceArray roomRefs = new ReferenceArray();
            roomRefs.Append(new Reference(g1));
            roomRefs.Append(new Reference(g2));

            Line line;
            if (vertical)
                line = Line.CreateBound(new XYZ(GetX(g1, null), roomCenter.Y, 0), new XYZ(GetX(g2, null), roomCenter.Y, 0));
            else
                line = Line.CreateBound(new XYZ(roomCenter.X, GetY(g1, null), 0), new XYZ(roomCenter.X, GetY(g2, null), 0));

            try { doc.Create.NewDimension(doc.ActiveView, line, roomRefs); } catch { }
        }

        private void CreateRoomDim(Document doc, SpatialElement room, XYZ roomCenter, Grid grid, bool vertical)
        {
            // Fallback for single grid dimensioning if needed
            ReferenceArray refs = new ReferenceArray();
        }

        private void DimensionGrids(Document doc)
        {
            var gridData = GetGrids(doc);

            if (gridData.Count < 2) return;

            // Group grids by direction (to handle angled grids)
            var directionGroups = gridData.GroupBy(x => GetDirectionKey(x.Element, x.Link)).ToList();

            foreach (var group in directionGroups)
            {
                var sortedGrids = group.OrderBy(x => GetPositionAlongNormal(x.Element, x.Link)).ToList();
                if (sortedGrids.Count > 1)
                {
                    CreateGridDimension(doc, sortedGrids);
                }
            }
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
                XYZ normal = new XYZ(-dir.Y, dir.X, 0).Normalize();
                return p.DotProduct(normal);
            }
            return 0;
        }

        private void DimensionCurtainWalls(Document doc)
        {
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
                if (uGridIds.Count > 1) CreateCurtainGridDim(doc, wall, uGridIds);

                // Dimension V-Grids
                var vGridIds = grid.GetVGridLineIds();
                if (vGridIds.Count > 1) CreateCurtainGridDim(doc, wall, vGridIds);
            }
        }

        private void CreateCurtainGridDim(Document doc, Wall wall, ICollection<ElementId> gridIds)
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

            if (refs.Size < 2) return;

            LocationCurve lc = wall.Location as LocationCurve;
            if (lc == null) return;
            Curve curve = lc.Curve;
            XYZ dir = (curve.GetEndPoint(1) - curve.GetEndPoint(0)).Normalize();
            XYZ normal = new XYZ(-dir.Y, dir.X, 0);
            Line dimLine = Line.CreateBound(curve.GetEndPoint(0) + normal * 2, curve.GetEndPoint(1) + normal * 2);

            try { doc.Create.NewDimension(doc.ActiveView, dimLine, refs); } catch { }
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

        private void CreateGridDimension(Document doc, List<(Grid Element, RevitLinkInstance Link)> sortedGrids)
        {
            ReferenceArray refs = new ReferenceArray();
            foreach (var item in sortedGrids)
            {
                Reference r = new Reference(item.Element);
                if (item.Link != null) r = r.CreateLinkReference(item.Link);
                refs.Append(r);
            }

            if (refs.Size < 2) return;

            // Calculate dimension line direction and position
            Line firstLine = sortedGrids.First().Element.Curve as Line;
            XYZ dir = firstLine.Direction.Normalize();
            if (sortedGrids.First().Link != null) dir = sortedGrids.First().Link.GetTotalTransform().OfVector(dir);
            XYZ normal = new XYZ(-dir.Y, dir.X, 0).Normalize();

            double offsetFeet = OffsetMm / 304.8;
            
            // Find extreme points to place dimension line
            var points = sortedGrids.Select(x => {
                Line l = x.Element.Curve as Line;
                XYZ p1 = l.GetEndPoint(0);
                if (x.Link != null) p1 = x.Link.GetTotalTransform().OfPoint(p1);
                return p1;
            }).ToList();

            XYZ start = points.First();
            XYZ end = points.Last();
            
            // Adjust points based on normal and offset
            XYZ dimStart = start + normal * offsetFeet;
            XYZ dimEnd = end + normal * offsetFeet;
            Line line = Line.CreateBound(dimStart, dimEnd);

            Dimension dim;
            if (DimensionStyleId != ElementId.InvalidElementId)
                dim = doc.Create.NewDimension(doc.ActiveView, line, refs, doc.GetElement(DimensionStyleId) as DimensionType);
            else
                dim = doc.Create.NewDimension(doc.ActiveView, line, refs);

            // Create Overall Dimension if Multi-Tier is enabled
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
                Line overallLine = Line.CreateBound(dimStart + normal * extraOffset, dimEnd + normal * extraOffset);

                if (DimensionStyleId != ElementId.InvalidElementId)
                    doc.Create.NewDimension(doc.ActiveView, overallLine, overallRefs, doc.GetElement(DimensionStyleId) as DimensionType);
                else
                    doc.Create.NewDimension(doc.ActiveView, overallLine, overallRefs);
            }
        }

        private void DimensionColumns(Document doc)
        {
            var columnData = GetElements<FamilyInstance>(doc, BuiltInCategory.OST_StructuralColumns);

            if (columnData.Count == 0) return;

            var grids = new FilteredElementCollector(doc, doc.ActiveView.Id).OfClass(typeof(Grid)).Cast<Grid>().ToList();
            if (grids.Count == 0) return;

            foreach (var item in columnData)
            {
                XYZ center = GetElementCenter(item.Element);
                if (item.Link != null) center = item.Link.GetTotalTransform().OfPoint(center);
                
                var nearestV = grids.Where(g => IsVertical(g, null)).OrderBy(g => Math.Abs(GetX(g, null) - center.X)).FirstOrDefault();
                var nearestH = grids.Where(g => !IsVertical(g, null)).OrderBy(g => Math.Abs(GetY(g, null) - center.Y)).FirstOrDefault();

                if (nearestV != null) CreateSingleDim(doc, item.Element, item.Link, nearestV, null, true);
                if (nearestH != null) CreateSingleDim(doc, item.Element, item.Link, nearestH, null, false);
            }
        }

        private void DimensionMEP(Document doc)
        {
            var openingData = GetElements<FamilyInstance>(doc, BuiltInCategory.OST_GenericModel)
                .Where(x => x.Element.Symbol.FamilyName.Contains("Opening") || x.Element.Symbol.FamilyName.Contains("Sleeve"))
                .ToList();

            if (openingData.Count == 0) return;

            var grids = new FilteredElementCollector(doc, doc.ActiveView.Id).OfClass(typeof(Grid)).Cast<Grid>().ToList();
            var columns = new FilteredElementCollector(doc, doc.ActiveView.Id).OfClass(typeof(FamilyInstance))
                .OfCategory(BuiltInCategory.OST_StructuralColumns).Cast<FamilyInstance>().ToList();

            foreach (var item in openingData)
            {
                XYZ center = GetElementCenter(item.Element);
                if (item.Link != null) center = item.Link.GetTotalTransform().OfPoint(center);
                
                var nearestV = grids.Where(g => IsVertical(g, null)).OrderBy(g => Math.Abs(GetX(g, null) - center.X)).FirstOrDefault();
                var nearestH = grids.Where(g => !IsVertical(g, null)).OrderBy(g => Math.Abs(GetY(g, null) - center.Y)).FirstOrDefault();

                if (nearestV != null) CreateSingleDim(doc, item.Element, item.Link, nearestV, null, true);
                if (nearestH != null) CreateSingleDim(doc, item.Element, item.Link, nearestH, null, false);

                var nearestCol = columns.OrderBy(c => GetElementCenter(c).DistanceTo(center)).FirstOrDefault();
                if (nearestCol != null && GetElementCenter(nearestCol).DistanceTo(center) < 5.0)
                {
                    CreateSingleDim(doc, item.Element, item.Link, nearestCol, null, true);
                }
            }
        }

        private void CreateSingleDim(Document doc, Element e1, RevitLinkInstance l1, Element e2, RevitLinkInstance l2, bool horizontal)
        {
            ReferenceArray refs = new ReferenceArray();
            
            Reference r1 = GetBestReference(e1, horizontal);
            Reference r2 = GetBestReference(e2, horizontal);

            if (r1 == null || r2 == null) return;

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

            try { doc.Create.NewDimension(doc.ActiveView, line, refs); } catch { }
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
                    return results;
                }
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
                    return results;
                }
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
