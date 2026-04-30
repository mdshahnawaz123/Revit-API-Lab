using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitUI.UI.Slope;
using System;

namespace RevitUI.Command
{
    [Transaction(TransactionMode.Manual)]
    public class SlopeAnalysisCommand : IExternalCommand
    {
        public static SlopeAnalysisDashboard Instance;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                if (Instance != null)
                {
                    Instance.Activate();
                    return Result.Succeeded;
                }

                var handler = new SlopeAnalysisHandler();
                var externalEvent = ExternalEvent.Create(handler);
                
                Instance = new SlopeAnalysisDashboard(externalEvent, handler);
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
