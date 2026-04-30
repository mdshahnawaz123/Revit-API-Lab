using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RevitUI.UI.Purge
{
    public class PurgeHandler : IExternalEventHandler
    {
        public bool PurgeTemplates { get; set; }
        public bool PurgeFilters { get; set; }
        public bool PurgeStyles { get; set; }
        public bool PurgeRooms { get; set; }
        public bool PurgeEmptyViews { get; set; }

        public void Execute(UIApplication app)
        {
            Document doc = app.ActiveUIDocument.Document;
            int deletedCount = 0;

            using (Transaction trans = new Transaction(doc, "Model Purge Plus"))
            {
                trans.Start();
                try
                {
                    if (PurgeRooms) deletedCount += PurgeRedundantRooms(doc);
                    if (PurgeTemplates) deletedCount += PurgeUnusedTemplates(doc);
                    if (PurgeFilters) deletedCount += PurgeUnusedFilters(doc);
                    if (PurgeStyles) deletedCount += PurgeImportedStyles(doc);
                    if (PurgeEmptyViews) deletedCount += PurgeEmptyViewsLogic(doc);

                    trans.Commit();
                    TaskDialog.Show("B-Lab", $"Deep Clean Complete!\nRemoved {deletedCount} items from the model.");
                }
                catch (Exception ex)
                {
                    trans.RollBack();
                    TaskDialog.Show("Purge Error", ex.Message);
                }
            }
        }

        private int PurgeRedundantRooms(Document doc)
        {
            var rooms = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType()
                .Cast<Room>()
                .Where(r => r.Area <= 0)
                .Select(r => r.Id)
                .ToList();

            if (rooms.Count > 0) doc.Delete(rooms);
            return rooms.Count;
        }

        private int PurgeUnusedTemplates(Document doc)
        {
            var templates = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => v.IsTemplate)
                .ToList();

            var appliedTemplates = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Select(v => v.ViewTemplateId)
                .Distinct()
                .ToList();

            var unused = templates.Where(t => !appliedTemplates.Contains(t.Id)).Select(t => t.Id).ToList();
            if (unused.Count > 0) doc.Delete(unused);
            return unused.Count;
        }

        private int PurgeUnusedFilters(Document doc)
        {
            var allFilters = new FilteredElementCollector(doc).OfClass(typeof(ParameterFilterElement)).Select(f => f.Id).ToList();
            var appliedFilters = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .SelectMany(v => v.GetFilters())
                .Distinct()
                .ToList();

            var unused = allFilters.Except(appliedFilters).ToList();
            if (unused.Count > 0) doc.Delete(unused);
            return unused.Count;
        }

        private int PurgeImportedStyles(Document doc)
        {
            var styles = new FilteredElementCollector(doc)
                .OfClass(typeof(GraphicsStyle))
                .Cast<GraphicsStyle>()
                .Where(s => s.Name.Contains(".dwg") || s.Name.Contains("Imported"))
                .Select(s => s.Id)
                .ToList();

            if (styles.Count > 0) doc.Delete(styles);
            return styles.Count;
        }

        private int PurgeEmptyViewsLogic(Document doc)
        {
            var views = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !v.IsTemplate && v.ViewType == ViewType.Section || v.ViewType == ViewType.Elevation)
                .Where(v => !new FilteredElementCollector(doc, v.Id).Any())
                .Select(v => v.Id)
                .ToList();

            if (views.Count > 0) doc.Delete(views);
            return views.Count;
        }

        public string GetName() => "Model Purge Plus Handler";
    }
}
