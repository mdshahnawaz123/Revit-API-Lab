using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;

namespace RevitUI.Command
{
    [Transaction(TransactionMode.Manual)]
    public class OpeningCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            var doc = uidoc.Document;
            
            // ── LOGIN CHECK ───────────────────────────────────────────────────
            if (!RevitUI.UI.LoginGuard.IsAuthorized()) return Result.Cancelled;

            try
            {
                RevitUI.ExternalCommand.Opening.MepSleeveUpdater.Register(commandData.Application.ActiveAddInId);
                RevitUI.UI.MasterOpening.GetOrCreate(doc, uidoc, commandData.Application.ActiveAddInId);
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