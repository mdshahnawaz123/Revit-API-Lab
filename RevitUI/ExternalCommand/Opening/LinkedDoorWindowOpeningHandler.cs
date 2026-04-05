using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Linq;

namespace RevitUI.ExternalCommand.Opening
{
    public class LinkedDoorWindowOpeningHandler : IExternalEventHandler
    {
        public bool DoorCheckBox { get; set; }
        public bool WindowCheckBox { get; set; }

        // UI reads these AFTER event completes
        public int CreatedCount { get; private set; }
        public int SkippedCount { get; private set; }
        public string ResultMessage { get; private set; }

        public void Execute(UIApplication app)
        {
            CreatedCount = 0;
            SkippedCount = 0;
            ResultMessage = "";

            var doc = app.ActiveUIDocument.Document;

            try
            {
                if (!DoorCheckBox && !WindowCheckBox)
                {
                    ResultMessage = "Please select at least Door or Window.";
                    return;
                }

                // ── Explicit transaction (not DoAction wrapper) ──────────────
                using (var tx = new Transaction(doc, "Create Door/Window Openings"))
                {
                    tx.Start();

                    if (DoorCheckBox)
                        ProcessCategory(doc, BuiltInCategory.OST_Doors);

                    if (WindowCheckBox)
                        ProcessCategory(doc, BuiltInCategory.OST_Windows);

                    tx.Commit();
                }

                ResultMessage = $"Done: {CreatedCount} opening(s) created, {SkippedCount} skipped.";
            }
            catch (Exception ex)
            {
                ResultMessage = $"Error: {ex.Message}";
                TaskDialog.Show("Error", ex.Message);
            }
        }

        private void ProcessCategory(Document doc, BuiltInCategory category)
        {
            var elements = doc.GetLinkedFamilyWithTransform(category);
            if (elements == null || !elements.Any()) return;

            foreach (var entry in elements)
            {
                try
                {
                    var bb = entry.Instance.get_BoundingBox(null);
                    if (bb == null) { SkippedCount++; continue; }

                    XYZ minWorld = entry.Transform.OfPoint(bb.Min);
                    XYZ maxWorld = entry.Transform.OfPoint(bb.Max);

                    var outline = new Outline(minWorld, maxWorld);
                    var bbFilter = new BoundingBoxIntersectsFilter(outline);

                    var hostWalls = new FilteredElementCollector(doc)
                        .OfClass(typeof(Wall))
                        .WhereElementIsNotElementType()
                        .WherePasses(bbFilter)
                        .Cast<Wall>()
                        .ToList();

                    if (!hostWalls.Any()) { SkippedCount++; continue; }

                    foreach (var wall in hostWalls)
                    {
                        doc.Create.NewOpening(wall, minWorld, maxWorld);
                        CreatedCount++;
                    }
                }
                catch
                {
                    SkippedCount++;
                }
            }
        }

        public string GetName() => "Door Window Opening Handler";
    }
}