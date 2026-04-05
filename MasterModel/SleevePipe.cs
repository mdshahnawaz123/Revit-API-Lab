using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RevitUI
{
    [Transaction(TransactionMode.Manual)]
    public class SleevePipe : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                // Collect all pipes
                var pipes = new FilteredElementCollector(doc)
                    .OfClass(typeof(Pipe))
                    .WhereElementIsNotElementType()
                    .Cast<Pipe>()
                    .ToList();

                List<ElementId> closePipes = new List<ElementId>();
                List<string> report = new List<string>();

                // Loop through pipes
                for (int i = 0; i < pipes.Count; i++)
                {
                    Pipe pipe1 = pipes[i];
                    LocationCurve loc1 = pipe1.Location as LocationCurve;
                    if (loc1 == null) continue;

                    Curve curve1 = loc1.Curve;

                    // Get diameter of pipe1
                    double dia1 = pipe1.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).AsDouble();

                    for (int j = i + 1; j < pipes.Count; j++)
                    {
                        Pipe pipe2 = pipes[j];
                        LocationCurve loc2 = pipe2.Location as LocationCurve;
                        if (loc2 == null) continue;

                        Curve curve2 = loc2.Curve;

                        // Get diameter of pipe2
                        double dia2 = pipe2.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).AsDouble();

                        // Find closest points between curves
                        IList<ClosestPointsPairBetweenTwoCurves> results;

                        curve1.ComputeClosestPoints(
                            curve2,
                            true,
                            true,
                            false,
                            out results
                        );

                        if (results == null || results.Count == 0) continue;

                        XYZ pt1 = results[0].XYZPointOnFirstCurve;
                        XYZ pt2 = results[0].XYZPointOnSecondCurve;

                        if (results == null || results.Count == 0) continue;


                        double centerDist = pt1.DistanceTo(pt2);

                        // Convert to mm
                        double centerDistMM = UnitUtils.ConvertFromInternalUnits(centerDist, UnitTypeId.Millimeters);

                        // Convert diameters to mm
                        double dia1MM = UnitUtils.ConvertFromInternalUnits(dia1, UnitTypeId.Millimeters);
                        double dia2MM = UnitUtils.ConvertFromInternalUnits(dia2, UnitTypeId.Millimeters);

                        // Calculate actual gap (surface-to-surface)
                        double gap = centerDistMM - (dia1MM / 2.0 + dia2MM / 2.0);

                        // Check condition
                        if (gap < 150)
                        {
                            closePipes.Add(pipe1.Id);
                            closePipes.Add(pipe2.Id);

                            report.Add(
                                $"Pipe {pipe1.Id.Value} & Pipe {pipe2.Id.Value} → Gap: {gap:F2} mm"
                            );
                        }
                    }
                }

                // Highlight elements in Revit
                uidoc.Selection.SetElementIds(closePipes.Distinct().ToList());

                // Show result
                TaskDialog.Show("Pipe Sleeve Check",
                    report.Count > 0
                    ? string.Join("\n", report)
                    : "No pipes found with gap less than 150 mm");

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}