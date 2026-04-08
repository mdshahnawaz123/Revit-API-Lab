using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Windows.Media;

namespace RevitUI.ExternalCommand.ParameterFilter
{
    public class PickColorHandler : IExternalEventHandler
    {
        private Color _selectedColor = Colors.White;

        public void Execute(UIApplication app)
        {
            var uidoc = app.ActiveUIDocument;
            try
            {
                var dialog = new ColorDialog();

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    _selectedColor = Color.FromArgb(
                        dialog.Color.A,
                        dialog.Color.R,
                        dialog.Color.G,
                        dialog.Color.B);
                }

            }
            catch (Exception ex)
            {
                Autodesk.Revit.UI.TaskDialog.Show("Error", $"An error occurred: {ex.Message}");
            }
        }

        public string GetName()
        {
            return "Colour Picked";
        }
    }
}
