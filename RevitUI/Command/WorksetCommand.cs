using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitUI.UI.Worksets;
using RevitUI.UI;
using System;

namespace RevitUI.Command
{
    [Transaction(TransactionMode.Manual)]
    public class WorksetCommand : IExternalCommand
    {
        public static WorksetDashboard Instance;

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

                var handler = new WorksetHandler();
                var externalEvent = ExternalEvent.Create(handler);
                
                Instance = new WorksetDashboard(externalEvent, handler);
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
