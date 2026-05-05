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
        private static ExternalEvent? _externalEvent;
        private static ModelHealthHandler? _handler;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // LOGIN CHECK
            if (!RevitUI.UI.LoginGuard.IsAuthorized()) return Result.Cancelled;

            try
            {
                RevitUI.UI.WindowExtensions.ShowSingleton(() =>
                {
                    _handler = new ModelHealthHandler();
                    _externalEvent = ExternalEvent.Create(_handler);
                    var dashboard = new ModelHealthDashboard(_externalEvent, _handler);
                    _handler.Dashboard = dashboard;
                    return dashboard;
                }, hideIcon: true);
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }

            return Result.Succeeded;
        }
    }
}
