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
            
            // Collect elements and filter for Model categories only
            var elements = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .WhereElementIsViewIndependent()
                .Where(e => e.Category != null && 
                            e.Category.CategoryType == CategoryType.Model && 
                            !e.Category.Name.Contains("<") && // Exclude <Sketch>, <Internal>, etc.
                            !e.Category.Name.Contains("Automatic")) // Exclude Automatic Sketch Dimensions
                .ToList();

            var stats = elements
                .GroupBy(e => e.Category.Name)
                .Select(g => new CategoryStat
                {
                    CategoryName = g.Key,
                    ElementCount = g.Count(),
                    // Logic: Count elements that have interop data
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
            
            // Define interop-critical parameters
            string[] interopParams = { "IfcGuid", "Mark", "Comments", "Identity Data", "BIM_ID" };

            int valid = elements.Count(e => 
                interopParams.Any(p => e.LookupParameter(p)?.HasValue == true) ||
                !string.IsNullOrEmpty(e.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.AsString()) ||
                !string.IsNullOrEmpty(e.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString())
            );

            return Math.Round((double)valid / elements.Count * 100, 1);
        }

        public string GetName() => "Interoperability Analysis Handler";
    }
}
