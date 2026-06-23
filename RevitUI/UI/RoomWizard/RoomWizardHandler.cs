using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RevitUI.UI.RoomWizard
{
    public class RoomWizardHandler : IExternalEventHandler
    {
        public List<ElementUpdateData> Updates { get; set; } = new List<ElementUpdateData>();

        public void Execute(UIApplication app)
        {
            if (Updates == null || !Updates.Any())
                return;

            Document doc = app.ActiveUIDocument.Document;

            using (Transaction trans = new Transaction(doc, "Room Wizard Update"))
            {
                trans.Start();
                int successCount = 0;
                int failCount = 0;

                foreach (var update in Updates)
                {
                    try
                    {
                        if (update.ElementId == ElementId.InvalidElementId) continue;

                        Element elem = doc.GetElement(update.ElementId);
                        if (elem == null) continue;

                        foreach (var kvp in update.Parameters)
                        {
                            Parameter param = elem.LookupParameter(kvp.Key);
                            if (param != null && !param.IsReadOnly)
                            {
                                switch (param.StorageType)
                                {
                                    case StorageType.String:
                                        param.Set(kvp.Value);
                                        break;
                                    case StorageType.Integer:
                                        if (int.TryParse(kvp.Value, out int intVal))
                                            param.Set(intVal);
                                        break;
                                    case StorageType.Double:
                                        if (double.TryParse(kvp.Value, out double doubleVal))
                                            param.Set(doubleVal);
                                        break;
                                    case StorageType.ElementId:
                                        // Complex to handle mapping of strings to ElementIds via CSV
                                        break;
                                }
                            }
                        }
                        successCount++;
                    }
                    catch
                    {
                        failCount++;
                    }
                }

                trans.Commit();
                
                TaskDialog.Show("Room Wizard", $"Update Complete!\nSuccessfully updated: {successCount} elements.\nFailed: {failCount} elements.");
            }

            // Clear updates after processing
            Updates.Clear();
        }

        public string GetName()
        {
            return "RoomWizardHandler";
        }
    }

    public class ElementUpdateData
    {
        public ElementId ElementId { get; set; }
        public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();
    }
}
