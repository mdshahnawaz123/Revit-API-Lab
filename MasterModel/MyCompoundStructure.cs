using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace MasterModel
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class MyCompoundStructure : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            var doc = uidoc.Document;

            try
            {

                var wall = new FilteredElementCollector(doc)
                    .OfClass(typeof(Wall))
                    .Cast<Wall>()
                    .FirstOrDefault();

                if (wall != null)
                {
                    // ✅ Get WallType correctly
                    WallType wt = wall.WallType;

                    var cs = wt.GetCompoundStructure();

                    if (cs != null)
                    {
                        int count = cs.LayerCount;

                        TaskDialog.Show("Layers", count.ToString());
                    }
                }

            }
            catch(Exception ex)
            {
                message = ex.Message;
            }

            return Result.Succeeded;
        }
    }
}
