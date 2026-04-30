using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitUI.UI.SharedParam;
using System;

namespace RevitUI.Command
{
    [Transaction(TransactionMode.Manual)]
    public class SharedParamCommand : IExternalCommand
    {
        public static SharedParamDashboard Instance;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                if (Instance != null)
                {
                    Instance.Activate();
                    return Result.Succeeded;
                }

                var dashboard = new SharedParamDashboard(null, null);
                var handler = new SharedParamHandler(dashboard);
                var externalEvent = ExternalEvent.Create(handler);

                // Re-create with proper references
                dashboard.Close();
                Instance = new SharedParamDashboard(externalEvent, handler);
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
