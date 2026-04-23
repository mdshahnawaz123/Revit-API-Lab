using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DataLab.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;

namespace RevitUI.ExternalCommand.Room3DTag
{
    public class ExistingTagDelete : IExternalEventHandler
    {
        // Set by RoomTag constructor — handler reads IsChecked at Execute time
        public CheckBox? ActiveViewCheckBox { get; set; }
        public CheckBox? AllModelsCheckBox { get; set; }

        private const string TagFamilyName = "3D Room Tag (BDD)";

        public void Execute(UIApplication app)
        {
            var uidoc = app.ActiveUIDocument;
            var doc = uidoc.Document;

            try
            {
                // Read checkbox state on the Revit API thread
                // (CheckBox.IsChecked is safe to read cross-thread in WPF)
                bool activeViewOnly = ActiveViewCheckBox?.IsChecked == true;
                bool allModels = AllModelsCheckBox?.IsChecked == true;

                // Guard — UI already checks this, but defensive check here too
                if (!activeViewOnly && !allModels)
                {
                    TaskDialog.Show("Info",
                        "Please select at least one option:\n• Active View\n• All Models");
                    return;
                }

                var idsToDelete = new List<ElementId>();
                var sb = new StringBuilder();

                // ── Option 1: Active View scope ──────────────────────────────────────
                if (activeViewOnly)
                {
                    var tagsInView = new FilteredElementCollector(doc, uidoc.ActiveView.Id)
                        .OfClass(typeof(FamilyInstance))
                        .OfCategory(BuiltInCategory.OST_GenericModel)
                        .Cast<FamilyInstance>()
                        .Where(fi => fi.Symbol?.Family?.Name == TagFamilyName)
                        .ToList();

                    if (!tagsInView.Any())
                        sb.AppendLine("• Active View: No tags found.");
                    else
                    {
                        sb.AppendLine($"• Active View: {tagsInView.Count} tag(s) found.");
                        foreach (var tag in tagsInView)
                            LogAndCollect(tag, idsToDelete, sb);
                    }
                }

                // ── Option 2: Entire document scope ──────────────────────────────────
                if (allModels)
                {
                    var tagsInDoc = new FilteredElementCollector(doc)
                        .OfClass(typeof(FamilyInstance))
                        .OfCategory(BuiltInCategory.OST_GenericModel)
                        .Cast<FamilyInstance>()
                        .Where(fi => fi.Symbol?.Family?.Name == TagFamilyName)
                        .ToList();

                    if (!tagsInDoc.Any())
                        sb.AppendLine("• All Models: No tags found.");
                    else
                    {
                        sb.AppendLine($"• All Models: {tagsInDoc.Count} tag(s) found.");
                        foreach (var tag in tagsInDoc)
                            LogAndCollect(tag, idsToDelete, sb);
                    }
                }

                // ── Deduplicate in case both scopes overlap ───────────────────────────
                var uniqueIds = idsToDelete
                    .Distinct()
                    .ToList();

                if (!uniqueIds.Any())
                {
                    TaskDialog.Show("Info", $"No tags found to delete.\n\n{sb}");
                    return;
                }

                // ── Confirm ───────────────────────────────────────────────────────────
                var confirm = TaskDialog.Show(
                    "Confirm Delete",
                    $"About to delete {uniqueIds.Count} tag(s):\n\n{sb}\nProceed?",
                    TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);

                if (confirm != TaskDialogResult.Yes) return;

                // ── Delete in a single transaction ────────────────────────────────────
                using (Transaction tx = new Transaction(doc, "Delete Existing 3D Room Tags"))
                {
                    tx.Start();
                    doc.Delete(uniqueIds);
                    tx.Commit();
                }

                TaskDialog.Show("Deleted Successfully",
                    $"Deleted {uniqueIds.Count} tag(s).\n\n{sb}");
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", $"{ex.Message}\n\n{ex.StackTrace}");
            }
        }

        // ── Helper ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Reads room name/number from the tag instance and adds its ID to the
        /// delete list. Skips silently if already collected (dedup).
        /// </summary>
        private static void LogAndCollect(
            FamilyInstance tag,
            List<ElementId> ids,
            StringBuilder sb)
        {
            if (ids.Contains(tag.Id)) return;

            var name = tag.LookupParameter("Name")?.AsString() ?? "(no name)";
            var number = tag.LookupParameter("Number")?.AsString() ?? "(no number)";

            sb.AppendLine($"  - {number} | {name}");
            ids.Add(tag.Id);
        }

        public string GetName() => "Delete Existing Room Tags";
    }
}