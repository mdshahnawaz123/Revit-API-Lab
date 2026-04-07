using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitUI.ExternalCommand.ParameterFilter
{
    public class ClearColorHandler : IExternalEventHandler
    {
        public ElementId? CategoryId;
        public int ScopeMode = 0;

        public void Execute(UIApplication app)
        {
            var doc = app.ActiveUIDocument.Document;

            var elements = new FilteredElementCollector(doc)
                .OfCategoryId(CategoryId)
                .WhereElementIsNotElementType()
                .ToList();

            using (Transaction t = new Transaction(doc, "Clear View Filters & Isolate"))
            {
                t.Start();

                var view = doc.ActiveView;

                if (ScopeMode == 0) // Both -> clear everything
                {
                    if (view.IsTemporaryHideIsolateActive())
                        view.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);

                    var filters = view.GetFilters().ToList();
                    foreach (var fId in filters)
                    {
                        var filterObj = doc.GetElement(fId);
                        if (filterObj != null && (filterObj.Name.StartsWith("SmartFilter_") || filterObj.Name.StartsWith("TempIsolate_") || filterObj.Name.StartsWith("IsolateHack_")))
                        {
                            view.RemoveFilter(fId);
                        }
                    }
                }

                // Always clear Host element inline overrides
                foreach (var e in elements)
                {
                    view.SetElementOverrides(e.Id, new OverrideGraphicSettings());
                }

                t.Commit();
            }
        }

        public string GetName() => "Clear Color";
    }
}
