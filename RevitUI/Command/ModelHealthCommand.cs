using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitUI.ExternalCommand.ModelHealth;
using RevitUI.UI.ModelHealth;

namespace RevitUI.Command
{
    [Transaction(TransactionMode.Manual)]
    public class ModelHealthCommand : IExternalCommand
    {
        public static ModelHealthDashboard? Instance;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                if (Instance != null)
                {
                    Instance.Activate();
                    if (Instance.WindowState == System.Windows.WindowState.Minimized)
                        Instance.WindowState = System.Windows.WindowState.Normal;
                    return Result.Succeeded;
                }

                var handler = new ModelHealthHandler();
                var externalEvent = ExternalEvent.Create(handler);
                
                Instance = new ModelHealthDashboard(externalEvent, handler);
                handler.Dashboard = Instance;
                
                // Safety: Reset instance on close
                Instance.Closed += (s, e) => { Instance = null; };
                
                Instance.Show();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Model Health Error", "Failed to open dashboard: " + ex.Message);
                Instance = null;
                return Result.Failed;
            }
        }
    }
}
