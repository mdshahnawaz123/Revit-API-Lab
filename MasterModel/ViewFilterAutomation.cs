using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Text;
using static System.Net.WebRequestMethods;

namespace MasterModel
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ViewFilterAutomationrCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            var doc = uidoc.Document;
            var sb = new StringBuilder();

            try
            {
                //Automatically create a parameter filter on all floor plan views that highlights Exterior walls in red. Hold for Now will check later:

                using(var tx = new Transaction(doc,"Filter Wall in Floor Plan"))
                {

                    //lets Filter the Wall based on FloorPlan 

                    var walls = new FilteredElementCollector(doc)
                        .OfCategory(BuiltInCategory.OST_Walls)
                        .WhereElementIsElementType()
                        .ToList();

                    foreach(var wall in walls)
                    {
                        var wallType = wall as WallType;

                        var extWall = wallType.Function == WallFunction.Exterior;
                    }
                }

            }
            catch (Exception ex)
            {
                message = ex.Message;
            }

            return Result.Succeeded;
        }
    }
}
