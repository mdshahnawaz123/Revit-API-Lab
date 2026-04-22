using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitUI.ExternalCommand.Room3DTag
{
    public class Room3DTagExternal : IExternalEventHandler
    {
        public void Execute(UIApplication app)
        {
            var uidoc = app.ActiveUIDocument;
            var doc = uidoc.Document;
            try
            {
                TaskDialog.Show("Room 3D Tag", "This is a placeholder for the Room 3D Tag functionality.");
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", ex.Message);
            }
        }

        public string GetName()
        {
            return "Room 3D Tag";
        }
    }
}
