using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RevitUI.UI.Worksets
{
    public class WorksetHandler : IExternalEventHandler
    {
        public bool ApplyMapping { get; set; } = false;

        public void Execute(UIApplication app)
        {
            Document doc = app.ActiveUIDocument.Document;
            if (!doc.IsWorkshared)
            {
                TaskDialog.Show("Error", "This model is not workshared.");
                return;
            }

            if (ApplyMapping)
            {
                ProcessMapping(doc);
            }
            else
            {
                FetchProjectData(doc);
            }
        }

        private void FetchProjectData(Document doc)
        {
            // 1. Get all project worksets (User created only)
            var worksets = new FilteredWorksetCollector(doc)
                .OfKind(WorksetKind.UserWorkset)
                .Select(w => w.Name)
                .OrderBy(n => n)
                .ToList();

            // 2. Get all categories present in the model
            var categories = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .Where(e => e.Category != null)
                .Select(e => e.Category.Name)
                .Distinct()
                .OrderBy(n => n)
                .Select(n => new WorksetMapping { CategoryName = n, TargetWorkset = "No Change" })
                .ToList();

            worksets.Insert(0, "No Change");

            // Update UI
            RevitUI.Command.WorksetCommand.Instance?.Dispatcher.Invoke(() =>
            {
                RevitUI.Command.WorksetCommand.Instance.LoadData(categories, worksets);
            });
        }

        private void ProcessMapping(Document doc)
        {
            var ui = RevitUI.Command.WorksetCommand.Instance;
            if (ui == null) return;

            var mappings = ui.Mappings.Where(m => m.TargetWorkset != "No Change").ToList();
            if (mappings.Count == 0) return;

            using (Transaction trans = new Transaction(doc, "Smart Workset Automator"))
            {
                trans.Start();
                try
                {
                    int totalChanged = 0;
                    foreach (var map in mappings)
                    {
                        var targetWorkset = new FilteredWorksetCollector(doc)
                            .OfKind(WorksetKind.UserWorkset)
                            .FirstOrDefault(w => w.Name == map.TargetWorkset);

                        if (targetWorkset == null) continue;

                        var elements = new FilteredElementCollector(doc)
                            .WhereElementIsNotElementType()
                            .Where(e => e.Category?.Name == map.CategoryName)
                            .ToList();

                        foreach (var elem in elements)
                        {
                            Parameter param = elem.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);
                            if (param != null && !param.IsReadOnly)
                            {
                                param.Set(targetWorkset.Id.IntegerValue);
                                totalChanged++;
                            }
                        }
                    }
                    trans.Commit();
                    TaskDialog.Show("B-Lab", $"Successfully moved {totalChanged} elements to their target worksets.");
                    ApplyMapping = false;
                }
                catch (Exception ex)
                {
                    trans.RollBack();
                    TaskDialog.Show("Error", ex.Message);
                }
            }
        }

        public string GetName() => "Smart Workset Handler";
    }
}
