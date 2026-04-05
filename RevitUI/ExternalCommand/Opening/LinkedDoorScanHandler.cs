using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RevitUI.ExternalCommand.Opening
{
    public class OpeningData
    {
        public string ElementId { get; set; }
        public string ElementLevel { get; set; }
        public string ElementType { get; set; }
        public string LinkSource { get; set; }
        public string Status { get; set; }
    }

    public class LinkedDoorScanHandler : IExternalEventHandler
    {
        public bool DoorCheckBox { get; set; }
        public bool WindowCheckBox { get; set; }
        public List<OpeningData> Results { get; private set; } = new List<OpeningData>();

        // ← ADD THIS
        public bool IsComplete { get; set; } = false;

        public void Execute(UIApplication app)
        {
            IsComplete = false; // reset at start
            Results.Clear();

            var doc = app.ActiveUIDocument.Document;

            try
            {
                if (!DoorCheckBox && !WindowCheckBox)
                {
                    TaskDialog.Show("Warning", "Please select at least Door or Window.");
                    IsComplete = true;
                    return;
                }

                var linkInstances = new FilteredElementCollector(doc)
                    .OfClass(typeof(RevitLinkInstance))
                    .WhereElementIsNotElementType()
                    .Cast<RevitLinkInstance>()
                    .ToList();

                if (!linkInstances.Any())
                {
                    TaskDialog.Show("Info", "No linked models found in the current document.");
                    IsComplete = true;
                    return;
                }

                foreach (var link in linkInstances)
                {
                    var linkedDoc = link.GetLinkDocument();
                    if (linkedDoc == null) continue;

                    string linkName = link.Name;

                    if (DoorCheckBox)
                        CollectElements(linkedDoc, BuiltInCategory.OST_Doors, linkName);

                    if (WindowCheckBox)
                        CollectElements(linkedDoc, BuiltInCategory.OST_Windows, linkName);
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", ex.Message);
            }
            finally
            {
                IsComplete = true; // always set true when done — even on exception
            }
        }

        private void CollectElements(Document linkedDoc, BuiltInCategory category, string linkName)
        {
            var elements = new FilteredElementCollector(linkedDoc)
                .OfCategory(category)
                .WhereElementIsNotElementType()
                .ToElements();

            foreach (var elem in elements)
            {
                string elemId = elem.Id.Value.ToString();

                string typeName = "Unknown";
                var elemType = linkedDoc.GetElement(elem.GetTypeId());
                if (elemType != null)
                    typeName = elemType.Name;

                string levelName = "Unknown";
                var levelParam = elem.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM)
                              ?? elem.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM);

                if (levelParam != null && levelParam.AsElementId() != ElementId.InvalidElementId)
                {
                    var level = linkedDoc.GetElement(levelParam.AsElementId()) as Level;
                    if (level != null)
                        levelName = level.Name;
                }

                string categoryLabel = category == BuiltInCategory.OST_Doors ? "Door" : "Window";

                Results.Add(new OpeningData
                {
                    ElementId = elemId,
                    ElementType = typeName,
                    ElementLevel = levelName,
                    LinkSource = linkName,
                    Status = $"{categoryLabel} - Pending"
                });
            }
        }

        public string GetName() => "Door Scan Handler";
    }
}