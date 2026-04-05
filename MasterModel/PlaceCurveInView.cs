using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DataLab.Extensions;

namespace MasterModel
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class PlaceCurveInView : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                // ✅ Select detail elements
                IList<Element> selectedElements =
                    uidoc.Selection.PickElementsByRectangle("Select Detail Lines");

                // ✅ Filter only DetailCurves
                List<DetailCurve> detailCurves = selectedElements
                    .OfType<DetailCurve>()
                    .ToList();

                if (!detailCurves.Any())
                {
                    TaskDialog.Show("Info", "No detail curves selected.");
                    return Result.Cancelled;
                }

                // ✅ Get target view
                ViewSection selectedView = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewSection))
                    .Cast<ViewSection>()
                    .FirstOrDefault(x => x.Name == "Section 1")!;

                if (selectedView == null)
                {
                    TaskDialog.Show("Error", "Section view not found.");
                    return Result.Failed;
                }

                // ✅ Your custom extension method
                Transform viewTransform = selectedView.GetViewTransform();

                using (Transaction tx = new Transaction(doc, "Place Curves In View"))
                {
                    tx.Start();

                    foreach (DetailCurve dc in detailCurves)
                    {
                        Curve originalCurve = dc.GeometryCurve;

                        // ✅ Transform curve
                        Curve transformedCurve = originalCurve.CreateTransformed(viewTransform);

                        try
                        {
                            // ✅ Create new curve in target view
                            doc.Create.NewDetailCurve(selectedView, transformedCurve);
                        }
                        catch (Exception ex)
                        {
                            // ⚠️ Some curves may fail if not in plane
                            TaskDialog.Show("Warning", $"Curve skipped: {ex.Message}");
                        }
                    }

                    tx.Commit();
                }

                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}