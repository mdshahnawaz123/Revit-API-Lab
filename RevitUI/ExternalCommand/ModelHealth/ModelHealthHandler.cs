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
                var inPlaceFams = new FilteredElementCollector(doc)
                    .OfClass(typeof(Family))
                    .Cast<Family>()
                    .Where(f => f.IsInPlace)
                    .ToList();
                int inPlaceCount = inPlaceFams.Count;
                var inPlaceNames = inPlaceFams.Select(f => f.Name).ToList();

                // ── 3. Groups ────────────────────────────────────────────────
                var groups = new FilteredElementCollector(doc).OfClass(typeof(Group)).Cast<Group>().ToList();
                int groupCount = groups.Count;
                var groupNames = groups.Select(g => g.Name).Distinct().ToList();

                // ── 4. CAD Imports ──────────────────────────────────────────
                var cadImportList = new FilteredElementCollector(doc)
                    .OfClass(typeof(ImportInstance))
                    .Cast<ImportInstance>()
                    .ToList();
                int cadImports = cadImportList.Count;
                var cadNames = cadImportList.Select(i => i.Category?.Name ?? "Unknown CAD").Distinct().ToList();

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
                
                var viewports = new FilteredElementCollector(doc).OfClass(typeof(Viewport)).Cast<Viewport>();
                var viewsOnSheets = new HashSet<ElementId>(viewports.Select(vp => vp.ViewId));
                
                var orphanedViewsList = allViews.Where(v => !viewsOnSheets.Contains(v.Id)).ToList();
                int orphanedViews = orphanedViewsList.Count;
                var orphanedViewNames = orphanedViewsList.Select(v => v.Name).ToList();

                // ── 7. Redundant / Not Placed Rooms ──────────────────────────
                var rooms = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Rooms).Cast<Room>();
                int redundantRooms = rooms.Count(r => r.Area <= 0 || r.Location == null);

                // ── 8. Linked Models ─────────────────────────────────────────
                var linkInstances = new FilteredElementCollector(doc).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>().ToList();
                int linkCount = linkInstances.Count;
                var linkNames = linkInstances.Select(l => l.Name).ToList();

                // ── 9. NEW ADVANCED METRICS ─────────────────────────────────
                
                // File Size
                string fileSize = "N/A";
                try {
                    if (!string.IsNullOrEmpty(doc.PathName) && System.IO.File.Exists(doc.PathName)) {
                        long bytes = new System.IO.FileInfo(doc.PathName).Length;
                        fileSize = (bytes / (1024.0 * 1024.0)).ToString("F1");
                    }
                } catch { }

                // Worksets
                int worksets = 0;
                if (doc.IsWorkshared) {
                    worksets = new FilteredWorksetCollector(doc).OfKind(WorksetKind.UserWorkset).Count();
                }

                // Design Options
                int designOptions = new FilteredElementCollector(doc).OfClass(typeof(DesignOption)).GetElementCount();

                // Images
                int images = new FilteredElementCollector(doc).OfClass(typeof(ImageType)).GetElementCount();

                // View Templates
                int viewWithoutTemplate = allViews.Count(v => v.ViewTemplateId == ElementId.InvalidElementId);

                // Line Styles
                var lineStyleCat = doc.Settings.Categories.get_Item(BuiltInCategory.OST_Lines);
                int lineStyles = lineStyleCat?.SubCategories.Size ?? 0;

                // Materials
                int materials = new FilteredElementCollector(doc).OfClass(typeof(Material)).GetElementCount();

                // Total Elements
                int totalElements = new FilteredElementCollector(doc).WhereElementIsNotElementType().GetElementCount();

                // ── Update UI ────────────────────────────────────────────────
                Dashboard.Dispatcher.Invoke(() =>
                {
                    Dashboard.UpdateMetrics(new ModelHealthData
                    {
                        WarningCount = warningCount,
                        InPlaceCount = inPlaceCount,
                        GroupCount = groupCount,
                        CadImportCount = cadImports,
                        UnusedFilterCount = unusedFilters,
                        OrphanedViewCount = orphanedViews,
                        RedundantRoomCount = redundantRooms,
                        LinkCount = linkCount,
                        
                        FileSizeMb = fileSize,
                        WorksetCount = worksets,
                        DesignOptionCount = designOptions,
                        ViewWithoutTemplateCount = viewWithoutTemplate,
                        ImageCount = images,
                        MaterialCount = materials,
                        LineStyleCount = lineStyles,
                        TotalElementCount = totalElements,

                        InPlaceNames = inPlaceNames,
                        CadNames = cadNames,
                        GroupNames = groupNames,
                        LinkNames = linkNames,
                        OrphanedViewNames = orphanedViewNames
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

        // ── NEW ADVANCED METRICS ───────────────────────────────────────────
        public string FileSizeMb { get; set; } = "0";
        public int WorksetCount { get; set; }
        public int DesignOptionCount { get; set; }
        public int ViewWithoutTemplateCount { get; set; }
        public int ImageCount { get; set; }
        public int MaterialCount { get; set; }
        public int LineStyleCount { get; set; }
        public int TotalElementCount { get; set; }

        // ── DRILL-DOWN DETAILS ──────────────────────────────────────────────
        public List<string> InPlaceNames { get; set; } = new List<string>();
        public List<string> CadNames { get; set; } = new List<string>();
        public List<string> GroupNames { get; set; } = new List<string>();
        public List<string> LinkNames { get; set; } = new List<string>();
        public List<string> OrphanedViewNames { get; set; } = new List<string>();
        
        public double HealthScore
        {
            get
            {
                double score = 100;
                score -= (WarningCount * 0.2); 
                score -= (InPlaceCount * 8);   
                score -= (CadImportCount * 12); 
                score -= (RedundantRoomCount * 2);
                score -= (OrphanedViewCount * 0.1);
                score -= (ImageCount * 1.5);    
                score -= (DesignOptionCount * 2); 
                
                return Math.Max(0, Math.Min(100, score));
            }
        }
    }
}
