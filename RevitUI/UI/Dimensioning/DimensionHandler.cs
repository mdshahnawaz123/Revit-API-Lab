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

        public void Execute(UIApplication app)
        {
            Document doc = app.ActiveUIDocument.Document;

            using (Transaction trans = new Transaction(doc, "Dimension Automation"))
            {
                trans.Start();
                try
                {
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
            var walls = new FilteredElementCollector(doc, doc.ActiveView.Id)
                .OfClass(typeof(Wall))
                .Cast<Wall>()
                .ToList();

            foreach (var wall in walls)
            {
                LocationCurve locCurve = wall.Location as LocationCurve;
                if (locCurve == null) continue;
                Curve curve = locCurve.Curve;

                ReferenceArray refs = new ReferenceArray();
                Options opt = new Options { ComputeReferences = true, DetailLevel = ViewDetailLevel.Fine };
                GeometryElement geo = wall.get_Geometry(opt);
                
                foreach (var obj in geo)
                {
                    if (obj is Solid solid && solid.Faces.Size > 0)
                    {
                        AddWallFaces(solid, curve, refs);
                    }
                    else if (obj is GeometryInstance inst)
                    {
                        GeometryElement instGeo = inst.GetInstanceGeometry();
                        foreach (var instObj in instGeo)
                        {
                            if (instObj is Solid instSolid && instSolid.Faces.Size > 0)
                            {
                                AddWallFaces(instSolid, curve, refs);
                            }
                        }
                    }
                }

                if (refs.Size >= 2)
                {
                    if (curve is Line line)
                    {
                        XYZ dir = line.Direction.Normalize();
                        XYZ normal = new XYZ(-dir.Y, dir.X, 0);
                        Line dimLine = Line.CreateBound(line.GetEndPoint(0) + normal * 2, line.GetEndPoint(1) + normal * 2);
                        try { doc.Create.NewDimension(doc.ActiveView, dimLine, refs); } catch { }
                    }
                }
            }
        }

        private void AddWallFaces(Solid solid, Curve curve, ReferenceArray refs)
        {
            foreach (Face face in solid.Faces)
            {
                if (face is PlanarFace planar && IsFaceParallelToCurve(planar, curve))
                {
                    if (planar.Reference != null)
                    {
                        try { refs.Append(planar.Reference); } catch { }
                    }
                }
            }
        }

        private bool IsFaceParallelToCurve(PlanarFace face, Curve curve)
        {
            if (curve is Line line)
            {
                XYZ dir = line.Direction.Normalize();
                return Math.Abs(face.FaceNormal.DotProduct(dir)) < 0.01;
            }
            return false;
        }

        private void DimensionRooms(Document doc)
        {
            List<SpatialElement> rooms = new List<SpatialElement>();

            // 1. Host Rooms
            if (IncludeHost)
            {
                rooms.AddRange(new FilteredElementCollector(doc, doc.ActiveView.Id)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .Cast<SpatialElement>()
                    .Where(r => r.Location != null));
            }

            // 2. Linked Rooms
            if (IncludeLinked)
            {
                var links = new FilteredElementCollector(doc).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>();
                foreach (var link in links)
                {
                    Document linkDoc = link.GetLinkDocument();
                    if (linkDoc == null) continue;

                    var linkedRooms = new FilteredElementCollector(linkDoc)
                        .OfCategory(BuiltInCategory.OST_Rooms)
                        .Cast<SpatialElement>()
                        .Where(r => r.Location != null);
                    
                    foreach(var lr in linkedRooms)
                    {
                        rooms.Add(lr);
                    }
                }
            }

            if (rooms.Count == 0)
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

            foreach (var room in rooms)
            {
                XYZ center = GetElementCenter(room);
                
                // If the room is from a link, transform its center to host
                if (room.Document.IsLinked)
                {
                    var link = new FilteredElementCollector(doc).OfClass(typeof(RevitLinkInstance))
                        .Cast<RevitLinkInstance>()
                        .FirstOrDefault(l => l.GetLinkDocument()?.PathName == room.Document.PathName);
                    
                    if (link != null) center = link.GetTotalTransform().OfPoint(center);
                }
                
                // Dimension to nearest vertical grids
                var vGrids = grids.Where(IsVertical).OrderBy(g => Math.Abs(GetX(g) - center.X)).ToList();
                if (vGrids.Count >= 2)
                {
                    var gLeft = vGrids.Where(g => GetX(g) < center.X).OrderByDescending(GetX).FirstOrDefault();
                    var gRight = vGrids.Where(g => GetX(g) >= center.X).OrderBy(GetX).FirstOrDefault();
                    if (gLeft != null && gRight != null) CreateRoomDimBetweenGrids(doc, center, gLeft, gRight, true);
                }

                // Dimension to nearest horizontal grids
                var hGrids = grids.Where(g => !IsVertical(g)).OrderBy(g => Math.Abs(GetY(g) - center.Y)).ToList();
                if (hGrids.Count >= 2)
                {
                    var gBot = hGrids.Where(g => GetY(g) < center.Y).OrderByDescending(GetY).FirstOrDefault();
                    var gTop = hGrids.Where(g => GetY(g) >= center.Y).OrderBy(GetY).FirstOrDefault();
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
                line = Line.CreateBound(new XYZ(GetX(g1), roomCenter.Y, 0), new XYZ(GetX(g2), roomCenter.Y, 0));
            else
                line = Line.CreateBound(new XYZ(roomCenter.X, GetY(g1), 0), new XYZ(roomCenter.X, GetY(g2), 0));

            try { doc.Create.NewDimension(doc.ActiveView, line, roomRefs); } catch { }
        }

        private void CreateRoomDim(Document doc, SpatialElement room, XYZ roomCenter, Grid grid, bool vertical)
        {
            // Fallback for single grid dimensioning if needed
            ReferenceArray refs = new ReferenceArray();
        }

        private void DimensionGrids(Document doc)
        {
            var grids = new FilteredElementCollector(doc, doc.ActiveView.Id)
                .OfClass(typeof(Grid))
                .Cast<Grid>()
                .ToList();

            if (grids.Count < 2) return;

            var verticalGrids = grids.Where(IsVertical).OrderBy(GetX).ToList();
            var horizontalGrids = grids.Where(g => !IsVertical(g)).OrderBy(GetY).ToList();

            if (verticalGrids.Count > 1) CreateGridDimension(doc, verticalGrids, true);
            if (horizontalGrids.Count > 1) CreateGridDimension(doc, horizontalGrids, false);
        }

        private void DimensionColumns(Document doc)
        {
            var columns = new FilteredElementCollector(doc, doc.ActiveView.Id)
                .OfCategory(BuiltInCategory.OST_StructuralColumns)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .ToList();

            if (columns.Count == 0) return;

            var grids = new FilteredElementCollector(doc, doc.ActiveView.Id).OfClass(typeof(Grid)).Cast<Grid>().ToList();
            if (grids.Count == 0) return;

            foreach (var col in columns)
            {
                XYZ center = GetElementCenter(col);
                
                var nearestV = grids.Where(IsVertical).OrderBy(g => Math.Abs(GetX(g) - center.X)).FirstOrDefault();
                var nearestH = grids.Where(g => !IsVertical(g)).OrderBy(g => Math.Abs(GetY(g) - center.Y)).FirstOrDefault();

                if (nearestV != null) CreateSingleDim(doc, col, nearestV, true);
                if (nearestH != null) CreateSingleDim(doc, col, nearestH, false);
            }
        }

        private void DimensionMEP(Document doc)
        {
            var openings = new FilteredElementCollector(doc, doc.ActiveView.Id)
                .OfClass(typeof(FamilyInstance))
                .OfCategory(BuiltInCategory.OST_GenericModel)
                .Cast<FamilyInstance>()
                .Where(fi => fi.Symbol.FamilyName.Contains("Opening") || fi.Symbol.FamilyName.Contains("Sleeve"))
                .ToList();

            if (openings.Count == 0) return;

            var grids = new FilteredElementCollector(doc, doc.ActiveView.Id).OfClass(typeof(Grid)).Cast<Grid>().ToList();
            var columns = new FilteredElementCollector(doc, doc.ActiveView.Id).OfClass(typeof(FamilyInstance))
                .OfCategory(BuiltInCategory.OST_StructuralColumns).Cast<FamilyInstance>().ToList();

            foreach (var opening in openings)
            {
                XYZ center = GetElementCenter(opening);
                
                var nearestV = grids.Where(IsVertical).OrderBy(g => Math.Abs(GetX(g) - center.X)).FirstOrDefault();
                var nearestH = grids.Where(g => !IsVertical(g)).OrderBy(g => Math.Abs(GetY(g) - center.Y)).FirstOrDefault();

                if (nearestV != null) CreateSingleDim(doc, opening, nearestV, true);
                if (nearestH != null) CreateSingleDim(doc, opening, nearestH, false);

                var nearestCol = columns.OrderBy(c => GetElementCenter(c).DistanceTo(center)).FirstOrDefault();
                if (nearestCol != null && GetElementCenter(nearestCol).DistanceTo(center) < 5.0)
                {
                    CreateSingleDim(doc, opening, nearestCol, true);
                }
            }
        }

        private void CreateGridDimension(Document doc, List<Grid> sortedGrids, bool isVerticalGroup)
        {
            ReferenceArray refs = new ReferenceArray();
            foreach (var grid in sortedGrids) refs.Append(new Reference(grid));

            Line line;
            double offsetFeet = OffsetMm / 304.8;

            if (isVerticalGroup)
            {
                double minY = sortedGrids.Min(GetMinY) - offsetFeet;
                line = Line.CreateBound(new XYZ(GetX(sortedGrids.First()), minY, 0), new XYZ(GetX(sortedGrids.Last()), minY, 0));
            }
            else
            {
                double minX = sortedGrids.Min(GetMinX) - offsetFeet;
                line = Line.CreateBound(new XYZ(minX, GetY(sortedGrids.First()), 0), new XYZ(minX, GetY(sortedGrids.Last()), 0));
            }

            doc.Create.NewDimension(doc.ActiveView, line, refs);
        }

        private void CreateSingleDim(Document doc, Element e1, Element e2, bool horizontal)
        {
            ReferenceArray refs = new ReferenceArray();
            
            // For columns/families, we must pick the reference plane parallel to the grid
            Reference r1 = GetBestReference(e1, horizontal);
            Reference r2 = GetBestReference(e2, horizontal);

            if (r1 == null || r2 == null) return;

            refs.Append(r1);
            refs.Append(r2);

            XYZ p1 = GetElementCenter(e1);
            XYZ p2 = GetElementCenter(e2);
            
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
                // For Horizontal Dimension (X-distance), we need the Vertical reference (Left/Right)
                // For Vertical Dimension (Y-distance), we need the Horizontal reference (Front/Back)
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

                // Fallback to any center reference
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

        private bool IsVertical(Grid g) {
            if (g.Curve is Line l) return Math.Abs(l.Direction.X) < 0.01;
            return false;
        }
        private double GetX(Grid g) => g.Curve is Line l ? l.GetEndPoint(0).X : 0;
        private double GetY(Grid g) => g.Curve is Line l ? l.GetEndPoint(0).Y : 0;
        private double GetMinY(Grid g) => g.Curve is Line l ? Math.Min(l.GetEndPoint(0).Y, l.GetEndPoint(1).Y) : 0;
        private double GetMinX(Grid g) => g.Curve is Line l ? Math.Min(l.GetEndPoint(0).X, l.GetEndPoint(1).X) : 0;

        public string GetName() => "Dimension Automation Handler";
    }
}
