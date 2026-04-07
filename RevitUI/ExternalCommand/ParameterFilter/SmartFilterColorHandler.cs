using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RevitUI.ExternalCommand.ParameterFilter
{
    public class SmartFilterColorHandler : IExternalEventHandler
    {
        public ElementId CategoryId;
        public List<FilterRuleData> Rules = new();
        public bool UseAndLogic = true;
        public Color RevitColor;

        public int ScopeMode = 0;

        public class FilterRuleData
        {
            public ElementId ParameterId;
            public string ParameterName;
            public string Operator;
            public string Value;
            public StorageType StorageType;
        }

        public void Execute(UIApplication app)
        {
            var uidoc = app.ActiveUIDocument;
            if (uidoc == null) return;

            var doc = uidoc.Document;
            var view = doc.ActiveView;

            var elementFilters = new List<ElementFilter>();

            foreach (var ruleData in Rules)
            {
                FilterRule rule = null;

                try
                {
                    switch (ruleData.StorageType)
                    {
                        // ✅ STRING RULES (SAFE FOR ALL REVIT VERSIONS)
                        case StorageType.String:

                            var provider = new ParameterValueProvider(ruleData.ParameterId);

                            FilterStringRuleEvaluator evaluator = ruleData.Operator switch
                            {
                                "Equals" => new FilterStringEquals(),
                                "Contains" => new FilterStringContains(),
                                "Begins With" => new FilterStringBeginsWith(),
                                "Ends With" => new FilterStringEndsWith(),
                                _ => null
                            };

                            if (evaluator != null)
                            {
                                rule = new FilterStringRule(
                                    provider,
                                    evaluator,
                                    ruleData.Value);
                            }

                            break;

                        // ✅ INTEGER RULES
                        case StorageType.Integer:

                            if (int.TryParse(ruleData.Value, out int iVal))
                            {
                                rule = ruleData.Operator switch
                                {
                                    "Equals" => ParameterFilterRuleFactory.CreateEqualsRule(ruleData.ParameterId, iVal),
                                    "Greater Than" => ParameterFilterRuleFactory.CreateGreaterRule(ruleData.ParameterId, iVal),
                                    "Less Than" => ParameterFilterRuleFactory.CreateLessRule(ruleData.ParameterId, iVal),
                                    _ => null
                                };
                            }

                            break;

                        // ✅ DOUBLE RULES
                        case StorageType.Double:

                            if (double.TryParse(ruleData.Value, out double dVal))
                            {
                                rule = ruleData.Operator switch
                                {
                                    "Equals" => ParameterFilterRuleFactory.CreateEqualsRule(ruleData.ParameterId, dVal, 0.0001),
                                    "Greater Than" => ParameterFilterRuleFactory.CreateGreaterRule(ruleData.ParameterId, dVal, 0.0001),
                                    "Less Than" => ParameterFilterRuleFactory.CreateLessRule(ruleData.ParameterId, dVal, 0.0001),
                                    _ => null
                                };
                            }

                            break;

                        // ✅ ELEMENT ID RULE
                        case StorageType.ElementId:

                            if (int.TryParse(ruleData.Value, out int idVal))
                            {
                                rule = ParameterFilterRuleFactory.CreateEqualsRule(
                                    ruleData.ParameterId,
                                    new ElementId(idVal));
                            }

                            break;
                    }
                }
                catch
                {
                    continue; // skip invalid rule safely
                }

                if (rule != null)
                {
                    elementFilters.Add(new ElementParameterFilter(rule));
                }
            }

            if (!elementFilters.Any()) return;

            ElementFilter finalFilter = UseAndLogic
                ? new LogicalAndFilter(elementFilters)
                : new LogicalOrFilter(elementFilters);

            // Scope Options:
            // 0 = Active View (Both) - Use View Filters
            // 1 = Host Only - Use Element Overrides
            // 2 = Link Only - Inform user of API limitations for direct link element colors

            if (ScopeMode == 1) // Host Only
            {
                using (Transaction t = new Transaction(doc, "Host Element Color"))
                {
                    t.Start();
                    var hostElements = new FilteredElementCollector(doc, view.Id)
                        .OfCategoryId(CategoryId)
                        .WhereElementIsNotElementType()
                        .WherePasses(finalFilter)
                        .ToElementIds();

                    var solidFill = new FilteredElementCollector(doc)
                        .OfClass(typeof(FillPatternElement))
                        .Cast<FillPatternElement>()
                        .First(x => x.GetFillPattern().IsSolidFill);

                    OverrideGraphicSettings ogs = new OverrideGraphicSettings();
                    ogs.SetSurfaceTransparency(0);
                    ogs.SetProjectionLineColor(RevitColor);
                    ogs.SetSurfaceForegroundPatternId(solidFill.Id);
                    ogs.SetSurfaceForegroundPatternColor(RevitColor);
                    ogs.SetSurfaceBackgroundPatternId(solidFill.Id);
                    ogs.SetSurfaceBackgroundPatternColor(RevitColor);
                    ogs.SetCutLineColor(RevitColor);
                    ogs.SetCutForegroundPatternId(solidFill.Id);
                    ogs.SetCutForegroundPatternColor(RevitColor);
                    ogs.SetCutBackgroundPatternId(solidFill.Id);
                    ogs.SetCutBackgroundPatternColor(RevitColor);

                    foreach (var id in hostElements)
                    {
                        view.SetElementOverrides(id, ogs);
                    }
                    t.Commit();
                }
                return;
            }
            
            if (ScopeMode == 2) // Link Only
            {
                TaskDialog.Show("Revit API Limitation", "The Revit API currently does not support applying distinct color overrides to individual elements inside a Linked Model. Please use 'Active View (Both)' to recolor Link elements via View Filters.");
                return;
            }

            // ScopeMode == 0 (Both) -> Use View Filters
            var firstRule = Rules.FirstOrDefault();
            string catName = "Element";
            try 
            {
                var cat = Category.GetCategory(doc, CategoryId);
                if(cat != null) catName = cat.Name;
            } 
            catch { }

            string filterName = firstRule != null ? $"{catName}_{firstRule.ParameterName}-{firstRule.Value}" : $"{Guid.NewGuid().ToString("N").Substring(0, 6)}";

            using (Transaction t = new Transaction(doc, "Smart Filter + Color"))
            {
                t.Start();

                var existingFilter = new FilteredElementCollector(doc)
                    .OfClass(typeof(ParameterFilterElement))
                    .Cast<ParameterFilterElement>()
                    .FirstOrDefault(f => f.Name == filterName);

                ParameterFilterElement filter;

                if (existingFilter != null)
                {
                    filter = existingFilter;
                    
                    var categories = filter.GetCategories();
                    if (!categories.Contains(CategoryId))
                    {
                        categories.Add(CategoryId);
                        try { filter.SetCategories(categories); } catch { }
                    }
                    
                    filter.SetElementFilter(finalFilter);
                }
                else
                {
                    filter = ParameterFilterElement.Create(
                        doc,
                        filterName,
                        new List<ElementId> { CategoryId });

                    filter.SetElementFilter(finalFilter);
                }

                // ✅ Add filter to view
                if (!view.GetFilters().Contains(filter.Id))
                    view.AddFilter(filter.Id);

                // ✅ Get solid fill
                var solidFill = new FilteredElementCollector(doc)
                    .OfClass(typeof(FillPatternElement))
                    .Cast<FillPatternElement>()
                    .First(x => x.GetFillPattern().IsSolidFill);

                // ✅ Apply surface color override (Aggressive settings for full solid fill)
                OverrideGraphicSettings ogs = new OverrideGraphicSettings();
                ogs.SetSurfaceTransparency(0); // Force opaque

                ogs.SetProjectionLineColor(RevitColor);
                ogs.SetSurfaceForegroundPatternId(solidFill.Id);
                ogs.SetSurfaceForegroundPatternColor(RevitColor);
                ogs.SetSurfaceBackgroundPatternId(solidFill.Id);
                ogs.SetSurfaceBackgroundPatternColor(RevitColor);

                // ✅ Add Cut overrides for section views
                ogs.SetCutLineColor(RevitColor);
                ogs.SetCutForegroundPatternId(solidFill.Id);
                ogs.SetCutForegroundPatternColor(RevitColor);
                ogs.SetCutBackgroundPatternId(solidFill.Id);
                ogs.SetCutBackgroundPatternColor(RevitColor);

                view.SetFilterOverrides(filter.Id, ogs);

                t.Commit();
            }
        }

        public string GetName() => "Smart Filter + Color";
    }
}