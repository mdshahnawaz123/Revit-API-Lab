using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DataLab;
using System;

namespace MasterModel
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ViewCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            var doc = uidoc.Document;

            try
            {
                var sectionView = doc.GetElementByName<ViewSection>("Section 1");

                if (sectionView == null)
                {
                    message = "Section 1 not found.";
                    return Result.Failed;
                }

                doc.DoAction(() =>
                {
                    sectionView.GetViewTransform().Visualize(doc, 10);

                },"This is for View Visulize");

                TaskDialog.Show("M", "This is Best for Test");
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }

            return Result.Succeeded;
        }
    }
}