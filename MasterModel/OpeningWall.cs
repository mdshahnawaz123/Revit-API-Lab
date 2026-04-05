using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using DataLab.Extensions;

namespace RevitUI
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class OpeningWall : IExternalCommand
    {
        
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            var doc = uidoc.Document;

            try
            {
                var pipes = new FilteredElementCollector(doc)
                    .OfClass(typeof(Pipe))
                    .WhereElementIsNotElementType()
                    .Cast<Pipe>()
                    .ToList();

                foreach (var pipe in pipes)
                {
                    // Get walls intersecting THIS pipe
                    var intersectingWalls = new FilteredElementCollector(doc)
                        .OfClass(typeof(Wall))
                        .WherePasses(new ElementIntersectsElementFilter(pipe))
                        .WhereElementIsNotElementType()
                        .Cast<Wall>()
                        .ToList();

                    foreach (var wall in intersectingWalls)
                    {
                        Options opt = new Options();
                        opt.ComputeReferences = true;

                        GeometryElement pipeGeo = pipe.get_Geometry(opt);
                        GeometryElement wallGeo = wall.get_Geometry(opt);

                        foreach (GeometryObject pObj in pipeGeo)
                        {
                            Solid pipeSolid = pObj as Solid;
                            if (pipeSolid == null || pipeSolid.Volume == 0) continue;

                            foreach (GeometryObject wObj in wallGeo)
                            {
                                Solid wallSolid = wObj as Solid;
                                if (wallSolid == null || wallSolid.Volume == 0) continue;

                                Solid intersection = BooleanOperationsUtils.ExecuteBooleanOperation(
                                    pipeSolid, wallSolid, BooleanOperationsType.Intersect);

                                if (intersection != null && intersection.Volume > 0)
                                {
                                    XYZ center = intersection.ComputeCentroid();

                                    var min = center - new XYZ(1.0,1.0,1.0);
                                    var max = center + new XYZ(1.0, 1.0, 1.0);
                                    doc.DoAction(() =>
                                        {
                                            var op = doc.Create.NewOpening(wall, min, max);

                                        },"Create Wall Opening");


                                    TaskDialog.Show("Accurate Intersection",
                                        $"X: {center.X:F2}, Y: {center.Y:F2}, Z: {center.Z:F2}");
                                }
                            }
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }

            return Result.Succeeded;
        }
    }
}
