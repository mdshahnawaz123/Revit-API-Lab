using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace MasterModel
{
    public class PramFilterOverrides : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            var doc = uidoc.Document;
            try
            {
                

            }
            catch (Exception ex)
            {
                TaskDialog.Show("Parameter Filter Overrides", ex.Message);
                return Result.Failed;
            }
            return Result.Succeeded;
        }
    }
}
