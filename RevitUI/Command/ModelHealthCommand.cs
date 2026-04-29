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
        private static ExternalEvent? _externalEvent;
        private static ModelHealthHandler? _handler;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (Instance == null)
            {
                _handler = new ModelHealthHandler();
                _externalEvent = ExternalEvent.Create(_handler);
                
                Instance = new ModelHealthDashboard(_externalEvent, _handler);
                _handler.Dashboard = Instance;
                Instance.Show();
            }
            else
            {
                Instance.Activate();
            }

            return Result.Succeeded;
        }
    }
}
