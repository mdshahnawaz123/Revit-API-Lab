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
                            TaskDialog.Show("B-Lab", "Room Boundary automation coming soon!");
                            break;
                        case DimMode.MEP:
                            DimensionMEP(doc);
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
                    if (obj is Solid solid)
                    {
                        foreach (Face face in solid.Faces)
                        {
                            if (face is PlanarFace planar && IsFaceParallelToCurve(planar, curve))
                            {
                                try { refs.Append(planar.Reference); } catch { }
                            }
                        }
                    }
                }

                if (refs.Size >= 2)
                {
                    XYZ dir = (curve.GetEndPoint(1) - curve.GetEndPoint(0)).Normalize();
                    XYZ normal = new XYZ(-dir.Y, dir.X, 0);
                    Line line = Line.CreateBound(curve.GetEndPoint(0) + normal * 2, curve.GetEndPoint(1) + normal * 2);
                    try { doc.Create.NewDimension(doc.ActiveView, line, refs); } catch { }
                }
            }
        }

        private bool IsFaceParallelToCurve(PlanarFace face, Curve curve)
        {
            XYZ dir = (curve.GetEndPoint(1) - curve.GetEndPoint(0)).Normalize();
            return Math.Abs(face.FaceNormal.DotProduct(dir)) < 0.01;
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
            refs.Append(new Reference(e1));
            refs.Append(new Reference(e2));

            XYZ p1 = GetElementCenter(e1);
            XYZ p2 = GetElementCenter(e2);
            
            Line line;
            if (horizontal)
                line = Line.CreateBound(new XYZ(p1.X, p1.Y, 0), new XYZ(p2.X, p1.Y, 0));
            else
                line = Line.CreateBound(new XYZ(p1.X, p1.Y, 0), new XYZ(p1.X, p2.Y, 0));

            try { doc.Create.NewDimension(doc.ActiveView, line, refs); } catch { }
        }

        private XYZ GetElementCenter(Element e)
        {
            if (e is FamilyInstance fi && fi.Location is LocationPoint lp) return lp.Point;
            BoundingBoxXYZ bbox = e.get_BoundingBox(null);
            if (bbox == null) return XYZ.Zero;
            return (bbox.Min + bbox.Max) * 0.5;
        }

        private bool IsVertical(Grid g) {
            if (g.Curve is Line l) return Math.Abs(l.Direction.X) < 0.01;
            return false;
        }
        private double GetX(Grid g) => ((Line)g.Curve).GetEndPoint(0).X;
        private double GetY(Grid g) => ((Line)g.Curve).GetEndPoint(0).Y;
        private double GetMinY(Grid g) => Math.Min(((Line)g.Curve).GetEndPoint(0).Y, ((Line)g.Curve).GetEndPoint(1).Y);
        private double GetMinX(Grid g) => Math.Min(((Line)g.Curve).GetEndPoint(0).X, ((Line)g.Curve).GetEndPoint(1).X);

        public string GetName() => "Dimension Automation Handler";
    }
}
