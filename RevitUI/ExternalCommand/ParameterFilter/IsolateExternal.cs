using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RevitUI.ExternalCommand.ParameterFilter
{
    public class IsolateExternal : IExternalEventHandler
    {
        // Same data contract as ParamExternal — set these from the UI before Raise()
        public ElementId? ParameterElementId { get; set; }
        public ElementId? CategoryId { get; set; }
        public string? FilterValue { get; set; }
        public string? RuleOperator { get; set; }

        public int ScopeMode { get; set; } = 0;

        public void Execute(UIApplication app)
        {
            var uidoc = app.ActiveUIDocument;

            if (uidoc == null)
            {
                TaskDialog.Show("Error", "No active document.");
                return;
            }

            var doc = uidoc.Document;
            var view = uidoc.ActiveView;

            // ── Validate ────────────────────────────────────────────────────
            if (ParameterElementId == null)
            { TaskDialog.Show("Validation", "Please select a Parameter."); return; }

            if (CategoryId == null)
            { TaskDialog.Show("Validation", "Please select a Category."); return; }

            if (string.IsNullOrWhiteSpace(RuleOperator))
            { TaskDialog.Show("Validation", "Please select an Operator."); return; }

            if (string.IsNullOrWhiteSpace(FilterValue))
            { TaskDialog.Show("Validation", "Please enter a filter value."); return; }

            // ── Build filter ─────────────────────────────────────────────────
            var paramFilter = BuildFilter(ParameterElementId, RuleOperator!, FilterValue!);
            if (paramFilter == null)
            {
                TaskDialog.Show("Error", $"Could not build filter for operator '{RuleOperator}'.");
                return;
            }

            try
            {
                using var tx = new Transaction(doc, "Smart Isolate");
                tx.Start();

                if (ScopeMode == 1) // Host Only
                {
                    var matchingIds = new FilteredElementCollector(doc, view.Id)
                        .OfCategoryId(CategoryId)
                        .WhereElementIsNotElementType()
                        .WherePasses(paramFilter)
                        .ToElementIds()
                        .ToList();

                    if (matchingIds.Count == 0)
                    {
                        TaskDialog.Show("Isolate", "No visible host elements matched the filter.");
                        return;
                    }

                    if (view.IsTemporaryHideIsolateActive())
                        view.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);

                    view.IsolateElementsTemporary(matchingIds);
                    tx.Commit();
                    TaskDialog.Show("Isolate", $"{matchingIds.Count} host element(s) isolated.");
                    return;
                }
                else if (ScopeMode == 2) // Link Only
                {
                    TaskDialog.Show("Revit API Limitation", "Direct element isolation exclusively inside a Link is not supported cleanly by the Revit API. Please use 'Active View (Both)' scope.");
                    return;
                }

                // ScopeMode == 0 (Both) -> Smart Isolate Hack
                if (view.IsTemporaryHideIsolateActive())
                    view.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);

                // We CANNOT use IsolateCategoriesTemporary because isolating "Walls" hides "RvtLinks",
                // meaning the ENTIRE linked model disappears. 
                // Fix: 1. Create a View Filter to hide ALL other filterable categories except the target and the link.
                var filterableCats = ParameterFilterUtilities.GetAllFilterableCategories()
                    .Where(id => id != CategoryId && id.Value != (int)BuiltInCategory.OST_RvtLinks)
                    .ToList();

                if (filterableCats.Any())
                {
                    string hideOthersName = "TempIsolate_Others_" + Guid.NewGuid().ToString("N").Substring(0, 6);
                    var hideOthersFilter = ParameterFilterElement.Create(doc, hideOthersName, filterableCats);
                    // Leaving rules empty means it targets ALL elements in these categories.
                    view.AddFilter(hideOthersFilter.Id);
                    view.SetFilterVisibility(hideOthersFilter.Id, false);
                }

                // Fix 2: Create a View Filter for the Target Category that hides elements that DO NOT match your criteria.
                var provider = new ParameterValueProvider(ParameterElementId);
                FilterRule rule = RuleOperator switch
                {
                    "Equals" => new FilterStringRule(provider, new FilterStringEquals(), FilterValue!),
                    "Contains" => new FilterStringRule(provider, new FilterStringContains(), FilterValue!),
                    "Begins With" => new FilterStringRule(provider, new FilterStringBeginsWith(), FilterValue!),
                    "Ends With" => new FilterStringRule(provider, new FilterStringEndsWith(), FilterValue!),
                    "Greater Than" => double.TryParse(FilterValue, out var gt) ? new FilterDoubleRule(provider, new FilterNumericGreater(), gt, 1e-6) : null!,
                    "Less Than" => double.TryParse(FilterValue, out var lt) ? new FilterDoubleRule(provider, new FilterNumericLess(), lt, 1e-6) : null!,
                    _ => null!
                };

                if (rule != null)
                {
                    FilterRule inverseRule = new FilterInverseRule(rule);
                    ElementFilter inverseElementFilter = new ElementParameterFilter(inverseRule);

                    string filterName = "TempIsolate_Inverse_" + Guid.NewGuid().ToString("N").Substring(0, 6);
                    var inverseFilterElement = ParameterFilterElement.Create(doc, filterName, new List<ElementId> { CategoryId });
                    inverseFilterElement.SetElementFilter(inverseElementFilter);

                    view.AddFilter(inverseFilterElement.Id);
                    view.SetFilterVisibility(inverseFilterElement.Id, false);
                }

                tx.Commit();

                TaskDialog.Show("Isolate", "Smart Isolate Applied!\nMatching elements in BOTH Host and Link are now isolated effectively.");
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", ex.Message);
            }
        }

        // ── Shared filter builder ────────────────────────────────────────────
        //    Identical to the one in ParamExternal. Consider extracting both
        //    into a static FilterRuleBuilder helper class to avoid duplication.
        private static ElementParameterFilter? BuildFilter(
            ElementId parameterId,
            string ruleOperator,
            string value)
        {
            var provider = new ParameterValueProvider(parameterId);

            FilterRule rule = ruleOperator switch
            {
                "Equals" => new FilterStringRule(provider, new FilterStringEquals(), value),
                "Contains" => new FilterStringRule(provider, new FilterStringContains(), value),
                "Begins With" => new FilterStringRule(provider, new FilterStringBeginsWith(), value),
                "Ends With" => new FilterStringRule(provider, new FilterStringEndsWith(), value),

                "Greater Than" => double.TryParse(value, out var gt)
                    ? new FilterDoubleRule(provider, new FilterNumericGreater(), gt, 1e-6)
                    : null!,

                "Less Than" => double.TryParse(value, out var lt)
                    ? new FilterDoubleRule(provider, new FilterNumericLess(), lt, 1e-6)
                    : null!,

                _ => null!
            };

            return rule is null ? null : new ElementParameterFilter(rule);
        }

        public string GetName() => "Isolate Elements by Parameter";
    }
}