using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace RevitUI.ExternalCommand.Loading
{
    public class RoomLoadingExternal : IExternalEventHandler
    {
        public bool IsHostModelOption { get; set; } = true;

        public Action<List<SpatialElement>> OnRoomsCollected { get; set; }

        public void Execute(UIApplication app)
        {
            var doc = app.ActiveUIDocument.Document;
            if (doc == null) return;

            var rooms = new List<SpatialElement>();

            try
            {
                if (IsHostModelOption)
                {
                    rooms.AddRange(new FilteredElementCollector(doc)
                        .OfCategory(BuiltInCategory.OST_Rooms)
                        .WhereElementIsNotElementType()
                        .Cast<SpatialElement>());
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
                            rooms.AddRange(new FilteredElementCollector(linkDoc)
                                .OfCategory(BuiltInCategory.OST_Rooms)
                                .WhereElementIsNotElementType()
                                .Cast<SpatialElement>());
                        }
                    }
                }

                // Invoke the callback on the UI thread or directly
                OnRoomsCollected?.Invoke(rooms);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", ex.Message);
            }
        }

        public string GetName()
        {
            return "Room Collection Handler";
        }
    }
}
