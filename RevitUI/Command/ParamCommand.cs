using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevitUI.Command
{
    [Transaction(TransactionMode.Manual)]
    public class ParamCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiapp = commandData.Application;
            var uidoc = uiapp.ActiveUIDocument;
            var doc = uidoc.Document;

            // LOGIN CHECK
            if (!RevitUI.UI.LoginGuard.IsAuthorized())
                return Result.Cancelled;

            try
            {
                RevitUI.UI.ParamFilter.GetOrCreate(doc, uidoc);
            }
            catch (System.Exception ex)
            {
                TaskDialog.Show("Error", ex.Message);
            }

            return Result.Succeeded;
        }
    }
}