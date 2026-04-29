using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using RevitUI.UI.ModelHealth;

namespace RevitUI.ExternalCommand.ModelHealth
{
    public class ModelHealthHandler : IExternalEventHandler
    {
        public ModelHealthDashboard Dashboard { get; set; }

        public void Execute(UIApplication app)
        {
            Document doc = app.ActiveUIDocument.Document;
            
            try
            {
                // ── 1. Warnings ──────────────────────────────────────────────
                var warnings = doc.GetWarnings();
                int warningCount = warnings.Count;

                // ── 2. In-Place Families ─────────────────────────────────────
                var inPlaceFamilies = new FilteredElementCollector(doc)
                    .OfClass(typeof(Family))
                    .Cast<Family>()
                    .Count(f => f.IsInPlace);

                // ── 3. Groups ────────────────────────────────────────────────
                var groupCount = new FilteredElementCollector(doc)
                    .OfClass(typeof(Group))
                    .GetElementCount();

                // ── 4. CAD Imports ──────────────────────────────────────────
                var cadImports = new FilteredElementCollector(doc)
                    .OfClass(typeof(ImportInstance))
                    .GetElementCount();

                // ── 5. Unused View Filters ──────────────────────────────────
                var allFilters = new FilteredElementCollector(doc).OfClass(typeof(ParameterFilterElement)).Cast<ParameterFilterElement>().ToList();
                var usedFilterIds = new HashSet<ElementId>();
                foreach (View v in new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>())
                {
                    try { foreach (var fid in v.GetFilters()) usedFilterIds.Add(fid); } catch { }
                }
                int unusedFilters = allFilters.Count(f => !usedFilterIds.Contains(f.Id));

                // ── 6. Views Not On Sheets ───────────────────────────────────
                var allViews = new FilteredElementCollector(doc).OfClass(typeof(View))
                    .Cast<View>()
                    .Where(v => !v.IsTemplate && v.CanBePrinted && v.ViewType != ViewType.Internal)
                    .ToList();
                
                // Get all views that are currently placed on sheets
                var viewports = new FilteredElementCollector(doc).OfClass(typeof(Viewport)).Cast<Viewport>();
                var viewsOnSheets = new HashSet<ElementId>(viewports.Select(vp => vp.ViewId));
                
                int orphanedViews = allViews.Count(v => !viewsOnSheets.Contains(v.Id));

                // ── 7. Redundant / Not Placed Rooms ──────────────────────────
                var rooms = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Rooms).Cast<Room>();
                int redundantRooms = rooms.Count(r => r.Area <= 0 || r.Location == null);

                // ── 8. Linked Models ─────────────────────────────────────────
                int linkCount = new FilteredElementCollector(doc).OfClass(typeof(RevitLinkInstance)).GetElementCount();

                // ── 9. Purgeable Elements (Approximate) ───────────────────────
                // Note: GetPurgeableElementIds is only available in newer Revit APIs (2024+) or via a complex workaround.
                // We will use a placeholder or count unused types for now.
                int unusedTypes = 0; // Placeholder

                // ── Update UI ────────────────────────────────────────────────
                Dashboard.Dispatcher.Invoke(() =>
                {
                    Dashboard.UpdateMetrics(new ModelHealthData
                    {
                        WarningCount = warningCount,
                        InPlaceCount = inPlaceFamilies,
                        GroupCount = groupCount,
                        CadImportCount = cadImports,
                        UnusedFilterCount = unusedFilters,
                        OrphanedViewCount = orphanedViews,
                        RedundantRoomCount = redundantRooms,
                        LinkCount = linkCount
                    });
                });
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Model Health Error", ex.Message);
            }
        }

        public string GetName() => "Model Health Handler";
    }

    public class ModelHealthData
    {
        public int WarningCount { get; set; }
        public int InPlaceCount { get; set; }
        public int GroupCount { get; set; }
        public int CadImportCount { get; set; }
        public int UnusedFilterCount { get; set; }
        public int OrphanedViewCount { get; set; }
        public int RedundantRoomCount { get; set; }
        public int LinkCount { get; set; }
        
        public double HealthScore
        {
            get
            {
                double score = 100;
                score -= (WarningCount * 0.5);
                score -= (InPlaceCount * 5); // In-place are bad
                score -= (CadImportCount * 10); // Imports are very bad
                score -= (RedundantRoomCount * 2);
                score -= (OrphanedViewCount * 0.2);
                return Math.Max(0, score);
            }
        }
    }
}
