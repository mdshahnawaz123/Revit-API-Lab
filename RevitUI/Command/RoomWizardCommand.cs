using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitUI.UI;
using RevitUI.UI.RoomWizard;
using System;

namespace RevitUI.Command
{
    [Transaction(TransactionMode.Manual)]
    public class RoomWizardCommand : IExternalCommand
    {
        public static RoomWizardDashboard Instance;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (!LoginGuard.IsAuthorized())
                return Result.Cancelled;

            try
            {
                if (Instance != null)
                {
                    Instance.Activate();
                    Instance.WindowState = System.Windows.WindowState.Normal;
                    return Result.Succeeded;
                }

                var handler = new RoomWizardHandler();
                var externalEvent = ExternalEvent.Create(handler);
                
                Instance = new RoomWizardDashboard(externalEvent, handler, commandData.Application.ActiveUIDocument);
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
