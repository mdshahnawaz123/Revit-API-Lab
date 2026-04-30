using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitUI.UI.Purge;
using System;

namespace RevitUI.Command
{
    [Transaction(TransactionMode.Manual)]
    public class PurgeCommand : IExternalCommand
    {
        public static PurgeDashboard Instance;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                if (Instance != null)
                {
                    Instance.Activate();
                    return Result.Succeeded;
                }

                var handler = new PurgeHandler();
                var externalEvent = ExternalEvent.Create(handler);
                
                Instance = new PurgeDashboard(externalEvent, handler);
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
