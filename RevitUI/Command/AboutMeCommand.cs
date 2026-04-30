using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitUI.UI;
using System;

namespace RevitUI.Command
{
    [Transaction(TransactionMode.Manual)]
    public class AboutMeCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                // Use the singleton helper to show the window
                WindowExtensions.ShowSingleton(() => new AboutMe(), hideIcon: false);
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
