using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitUI.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace B_Lab.Command
{
    [Transaction(TransactionMode.Manual)]
    public class ParamCommand : IExternalCommand
    {
        private static ParamFilter frm;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiapp = commandData.Application;
            var app = uiapp.Application;
            var uidoc = uiapp.ActiveUIDocument;
            var doc = uidoc.Document;

            try
            {
                if(doc != null)
                {
                    if(frm == null || !frm.IsVisible)
                    {
                        frm = new ParamFilter(doc, uidoc);
                        frm.Show();
                    }
                    else
                    {
                        frm.Activate();
                    }
                }

            }
            catch(Exception ex)
            {
                TaskDialog.Show("M",ex.Message);
            }
            return Result.Succeeded;
        }
    }
}
