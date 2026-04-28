using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitUI.Command
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class LoadingDigram : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            var doc = uidoc.Document;

            // ── LOGIN CHECK ───────────────────────────────────────────────────
            if (!RevitUI.UI.LoginGuard.IsAuthorized()) return Result.Cancelled;

            try
            {
                RevitUI.UI.WindowExtensions.ShowSingleton(() => new RevitUI.UI.LoadingDigram.StructuralLoading(doc, uidoc), hideIcon: true);
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
