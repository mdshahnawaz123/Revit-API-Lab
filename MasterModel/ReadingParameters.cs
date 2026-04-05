using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DataLab;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitUI
{
    [Transaction(TransactionMode.Manual)]
    public class ReadingParameters : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiapp = commandData.Application;
            var app = uiapp.Application;
            var uidoc = uiapp.ActiveUIDocument;
            var doc = uidoc.Document;

            try
            {
                var fi = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilyInstance))
                    .OfCategory(BuiltInCategory.OST_Doors)
                    .FirstOrDefault();

                StringBuilder sb = new StringBuilder();

                var allParams = fi.GetElementParameter(); // Using the extension method to get all parameters

                var notSharedParams = fi.GetElementParameter()
                    .Where(p => !p.IsShared)
                    .ToList();

                doc.DoAction(() =>
                {
                    foreach(var param in notSharedParams )
                    {
                        
                        var markPara = param.Definition.Name == "Mark" ? param : null;



                        sb.AppendLine(markPara.AsValueString());
                    }
                    
                },"This is for Setting Parametera");

                TaskDialog.Show("Shared Parameters", sb.ToString());

            }
            catch (Exception ex)
            {
                message = ex.Message;
            }
            return Result.Succeeded;
        }
    }
}
