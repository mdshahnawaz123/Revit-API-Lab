using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitUI.UI.BatchFamilyUpgrader;
using RevitUI.ExternalCommand.BatchFamilyUpgrader;
using System;

namespace RevitUI.Command
{
    [Transaction(TransactionMode.Manual)]
    public class BatchFamilyUpgraderCommand : IExternalCommand
    {
        public static BatchFamilyUpgraderWindow Instance;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                if (!UI.LoginGuard.IsAuthorized())
                {
                    return Result.Cancelled;
                }

                if (Instance != null)
                {
                    Instance.Activate();
                    return Result.Succeeded;
                }

                var handler = new BatchFamilyUpgraderHandler();
                var externalEvent = ExternalEvent.Create(handler);

                var fetchHandler = new CloudModelFetchHandler();
                var fetchEvent = ExternalEvent.Create(fetchHandler);
                
                Instance = new BatchFamilyUpgraderWindow(externalEvent, handler, fetchEvent, fetchHandler);
                Instance.Closed += (s, e) => Instance = null; // Reset on close
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
