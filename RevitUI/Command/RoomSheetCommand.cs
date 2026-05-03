using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitUI.UI;
using RevitUI.UI.RoomSheet;
using System;

namespace RevitUI.Command
{
    [Transaction(TransactionMode.Manual)]
    public class RoomSheetCommand : IExternalCommand
    {
        public static RoomSheetDashboard? Instance;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // LOGIN CHECK
            if (!RevitUI.UI.LoginGuard.IsAuthorized())
                return Result.Cancelled;

            try
            {
                if (Instance == null)
                {
                    var handler = new RoomSheetHandler();
                    var externalEvent = ExternalEvent.Create(handler);
                    
                    Instance = new RoomSheetDashboard(externalEvent, handler, commandData.Application);
                    Instance.Show();
                }
                else
                {
                    Instance.Activate();
                    if (Instance.WindowState == System.Windows.WindowState.Minimized)
                        Instance.WindowState = System.Windows.WindowState.Normal;
                }

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
