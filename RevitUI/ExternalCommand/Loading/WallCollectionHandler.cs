using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RevitUI.ExternalCommand.Loading
{
    public class WallCollectionHandler : IExternalEventHandler
    {
        public bool IsHostModelOption { get; set; } = true;

        public Action<List<Wall>> OnWallsCollected { get; set; }

        public void Execute(UIApplication app)
        {
            var doc = app.ActiveUIDocument.Document;
            if (doc == null) return;
            var activeView = doc.ActiveView;

            var walls = new List<Wall>();

            try
            {
                if (IsHostModelOption)
                {
                    walls.AddRange(new FilteredElementCollector(doc, activeView.Id)
                        .OfClass(typeof(Wall))
                        .WhereElementIsNotElementType()
                        .Cast<Wall>());
                }
                else
                {
                    var linkedModel = new FilteredElementCollector(doc)
                        .OfClass(typeof(RevitLinkInstance))
                        .WhereElementIsNotElementType()
                        .Cast<RevitLinkInstance>();

                    foreach (var link in linkedModel)
                    {
                        var linkDoc = link.GetLinkDocument();
                        if (linkDoc != null)
                        {
                            walls.AddRange(new FilteredElementCollector(linkDoc, activeView.Id)
                                .OfClass(typeof(Wall))
                                .WhereElementIsNotElementType()
                                .Cast<Wall>());
                        }
                    }
                }

                OnWallsCollected?.Invoke(walls);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", ex.Message);
            }
        }

        public string GetName()
        {
            return "Wall Collection Handler";
        }
    }
}
