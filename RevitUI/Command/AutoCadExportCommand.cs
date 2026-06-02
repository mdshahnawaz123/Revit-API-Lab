using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitUI.UI;
using RevitUI.UI.Export;
using System;

namespace RevitUI.Command
{
    [Transaction(TransactionMode.Manual)]
    public class AutoCadExportCommand : IExternalCommand
    {
        public static ExportDashboard Instance;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (!LoginGuard.IsAuthorized())
                return Result.Cancelled;

            try
            {
                if (Instance != null)
                {
                    Instance.Activate();
                    return Result.Succeeded;
                }

                var handler = new ExportHandler();
                var externalEvent = ExternalEvent.Create(handler);
                
                Document doc = commandData.Application.ActiveUIDocument.Document;
                Instance = new ExportDashboard(doc, externalEvent, handler);
                Instance.Show();

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
