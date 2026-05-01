using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RevitUI.UI.Worksets
{
    public class WorksetHandler : IExternalEventHandler
    {
        public enum RequestTypeEnum
        {
            None,
            FetchInitialData,
            FetchTypes,
            FetchElements,
            AssignWorkset
        }

        public RequestTypeEnum RequestType { get; set; } = RequestTypeEnum.None;
        public string SelectedCategory { get; set; }
        public string SelectedType { get; set; }
        public string TargetWorkset { get; set; }
        public bool AssignToAll { get; set; }

        public void Execute(UIApplication app)
        {
            Document doc = app.ActiveUIDocument.Document;
            if (!doc.IsWorkshared)
            {
                TaskDialog.Show("Error", "This model is not workshared.");
                return;
            }

            try
            {
                switch (RequestType)
                {
                    case RequestTypeEnum.FetchInitialData:
                        FetchInitialData(doc);
                        break;
                    case RequestTypeEnum.FetchTypes:
                        FetchTypes(doc);
                        break;
                    case RequestTypeEnum.FetchElements:
                        FetchElements(doc);
                        break;
                    case RequestTypeEnum.AssignWorkset:
                        AssignWorkset(doc);
                        break;
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", "Handler Error: " + ex.Message);
            }
            finally
            {
                RequestType = RequestTypeEnum.None;
            }
        }

        private void FetchInitialData(Document doc)
        {
            var worksets = new FilteredWorksetCollector(doc)
                .OfKind(WorksetKind.UserWorkset)
                .Select(w => w.Name)
                .OrderBy(n => n)
                .ToList();

            var categories = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .Where(e => e.Category != null)
                .Select(e => e.Category.Name)
                .Distinct()
                .OrderBy(n => n)
                .ToList();

            RevitUI.Command.WorksetCommand.Instance?.Dispatcher.Invoke(() =>
            {
                RevitUI.Command.WorksetCommand.Instance.UpdateInitialData(categories, worksets);
            });
        }

        private void FetchTypes(Document doc)
        {
            if (string.IsNullOrEmpty(SelectedCategory)) return;

            var types = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .Where(e => e.Category?.Name == SelectedCategory)
                .Select(e => doc.GetElement(e.GetTypeId())?.Name)
                .Where(n => n != null)
                .Distinct()
                .OrderBy(n => n)
                .ToList();

            RevitUI.Command.WorksetCommand.Instance?.Dispatcher.Invoke(() =>
            {
                RevitUI.Command.WorksetCommand.Instance.UpdateElementTypes(types);
            });
        }

        private void FetchElements(Document doc)
        {
            if (string.IsNullOrEmpty(SelectedCategory) || string.IsNullOrEmpty(SelectedType)) return;

            var elements = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .Where(e => e.Category?.Name == SelectedCategory && doc.GetElement(e.GetTypeId())?.Name == SelectedType)
                .Select(e => new ElementData
                {
                    Id = e.Id.ToString(),
                    TypeName = doc.GetElement(e.GetTypeId())?.Name,
                    WorksetName = doc.GetWorksetTable().GetWorkset(e.WorksetId).Name
                })
                .ToList();

            RevitUI.Command.WorksetCommand.Instance?.Dispatcher.Invoke(() =>
            {
                RevitUI.Command.WorksetCommand.Instance.UpdateElements(elements);
            });
        }

        private void AssignWorkset(Document doc)
        {
            var workset = new FilteredWorksetCollector(doc)
                .OfKind(WorksetKind.UserWorkset)
                .FirstOrDefault(w => w.Name == TargetWorkset);

            if (workset == null) return;

            var collector = new FilteredElementCollector(doc).WhereElementIsNotElementType();
            
            if (AssignToAll)
            {
                collector.Where(e => e.Category?.Name == SelectedCategory);
            }
            else
            {
                collector.Where(e => e.Category?.Name == SelectedCategory && doc.GetElement(e.GetTypeId())?.Name == SelectedType);
            }

            var elements = collector.ToList();

            using (Transaction t = new Transaction(doc, "Assign Workset"))
            {
                t.Start();
                int count = 0;
                foreach (var e in elements)
                {
                    Parameter p = e.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);
                    if (p != null && !p.IsReadOnly)
                    {
                        p.Set(workset.Id.IntegerValue);
                        count++;
                    }
                }
                t.Commit();
                TaskDialog.Show("Success", $"Assigned {count} elements to workset '{TargetWorkset}'.");
            }

            // Refresh the element list in UI
            FetchElements(doc);
        }

        public string GetName() => "Workset Police Handler";
    }
}

