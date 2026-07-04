using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitUI.UI;
using System;
using System.Diagnostics;

namespace RevitUI.Command
{
    [Transaction(TransactionMode.Manual)]
    public class IfcViewerCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (!LoginGuard.IsAuthorized())
                return Result.Cancelled;

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://advanced-ifc-viewer-bdd.onrender.com",
                    UseShellExecute = true
                });
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
