using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DataLab;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitUI.ExternalCommand.Loading
{
    public class WallLineStyleHandler : IExternalEventHandler
    {
        public bool IsHostModelOption { get; set; } = true;
        
        // Property to receive the walls selected in the UI
        public List<Element> SelectedWalls { get; set; } = new List<Element>();

        public void Execute(UIApplication app)
        {
            var doc = app.ActiveUIDocument.Document;
            try
            {
                var wallLineStyle = doc.GetDetailLines(); // Assuming this is an extension method you have
                
                // Do whatever you need with SelectedWalls here!
                // Example: var ids = SelectedWalls.Select(w => w.Id).ToList();
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", ex.Message);
            }     
        }

        public string GetName()
        {
            return "WallLineStyleHandler";
        }
    }
}
