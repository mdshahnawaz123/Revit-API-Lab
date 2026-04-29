using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitUI.ExternalCommand.AutoCadExport;
using RevitUI.UI.AutoCadExport;

namespace RevitUI.Command
{
    [Transaction(TransactionMode.Manual)]
    public class AutoCadExportCommand : IExternalCommand
    {
        public static AutoCadExportUI? Instance;
        private static ExternalEvent? _externalEvent;
        private static AutoCadExportHandler? _handler;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (Instance == null)
            {
                _handler = new AutoCadExportHandler();
                _externalEvent = ExternalEvent.Create(_handler);
                
                Instance = new AutoCadExportUI(commandData.Application, _externalEvent, _handler);
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
