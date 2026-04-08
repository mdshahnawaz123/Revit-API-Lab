using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitUI.ExternalCommand.Opening;
using System;

namespace RevitUI.UI
{
    public class SyncSleeveHandler : IExternalEventHandler
    {
        public Action<string>? OnComplete { get; set; }
        public double ClearanceFeet { get; set; }

        public void Execute(UIApplication app)
        {
            Document doc = app.ActiveUIDocument.Document;
            int count = 0;

            using (Transaction t = new Transaction(doc, "Sync All Sleeves"))
            {
                t.Start();
                try
                {
                    count = MepSleeveUpdater.ProcessAllSleeves(doc, ClearanceFeet);
                    t.Commit();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SyncSleeveHandler] {ex.Message}");
                    t.RollBack();
                    OnComplete?.Invoke($"Error: {ex.Message}");
                    return;
                }
            }
            OnComplete?.Invoke($"Sync complete. {count} sleeve(s) relocated.");
        }

        public string GetName() => "Sync All Sleeves";
    }
}
