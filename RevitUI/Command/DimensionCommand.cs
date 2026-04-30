using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitUI.UI.Dimensioning;
using System;

namespace RevitUI.Command
{
    [Transaction(TransactionMode.Manual)]
    public class DimensionCommand : IExternalCommand
    {
        public static DimensionDashboard Instance;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                if (Instance != null)
                {
                    Instance.Activate();
                    Instance.WindowState = System.Windows.WindowState.Normal;
                    return Result.Succeeded;
                }

                var handler = new DimensionHandler();
                var externalEvent = ExternalEvent.Create(handler);
                
                Instance = new DimensionDashboard(externalEvent, handler);
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
