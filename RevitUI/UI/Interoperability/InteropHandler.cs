using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitUI.Command;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RevitUI.UI.Interoperability
{
    public class InteropHandler : IExternalEventHandler
    {
        public void Execute(UIApplication app)
        {
            Document doc = app.ActiveUIDocument.Document;
            
            // Collect all physical elements
            var collector = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .WhereElementIsViewIndependent();

            var stats = collector
                .GroupBy(e => e.Category?.Name ?? "Uncategorized")
                .Where(g => g.Key != "Uncategorized")
                .Select(g => new CategoryStat
                {
                    CategoryName = g.Key,
                    ElementCount = g.Count(),
                    // Logic: Count elements that have an IFC GUID or a Mark
                    CoveragePercent = CalculateCoverage(g.ToList())
                })
                .OrderByDescending(s => s.ElementCount)
                .Take(15) // Show top 15 categories
                .ToList();

            // Update UI
            InteropCommand.Instance?.Dispatcher.Invoke(() =>
            {
                InteropCommand.Instance.UpdateStats(stats);
            });
        }

        private double CalculateCoverage(List<Element> elements)
        {
            if (elements.Count == 0) return 0;
            
            int valid = elements.Count(e => 
                e.LookupParameter("IfcGuid")?.HasValue == true || 
                e.LookupParameter("Mark")?.HasValue == true ||
                e.LookupParameter("Comments")?.HasValue == true);

            return Math.Round((double)valid / elements.Count * 100, 1);
        }

        public string GetName() => "Interoperability Analysis Handler";
    }
}
