using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitUI.ExternalCommand.Opening;
using RevitUI.UI;
using System;

namespace RevitUI.Command
{
    [Transaction(TransactionMode.Manual)]
    public class LinkedDoorOpening : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            var doc = uidoc.Document;

            // ── LOGIN CHECK ───────────────────────────────────────────────────
            if (!RevitUI.UI.LoginGuard.IsAuthorized()) return Result.Cancelled;

            try
            {
                var scanHandler = new LinkedDoorScanHandler();
                var scanEvent = ExternalEvent.Create(scanHandler);

                // Opening creation handler
                var openingHandler = new LinkedDoorWindowOpeningHandler();
                var openingEvent = ExternalEvent.Create(openingHandler);

                // Pass both into the window using the singleton helper
                WindowExtensions.ShowSingleton(() => new DoorOpening(scanEvent, scanHandler, openingEvent, openingHandler));
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