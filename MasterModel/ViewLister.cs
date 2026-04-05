using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Text;

namespace MasterModel
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ViewLister : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            var doc = uidoc.Document;

            var sb = new StringBuilder();

            try
            {

                var viewFilter = new FilteredElementCollector(doc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .Where(x=>!x.IsTemplate)
                    .ToList();
                viewFilter.ForEach(x =>
                {
                //1.Display NAme 2.ViewType 3.Scale

                var name = x.Name;
                var viewType = x.ViewType;
                var scale = x.Scale;

                });
                

            }
            catch(Exception ex)
            {
                message = ex.Message;
            }

            return Result.Succeeded;
        }
    }
}
