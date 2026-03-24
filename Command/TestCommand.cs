using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace B_Lab.Command
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class TestCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            var doc = uidoc.Document;

            try
            {
                //Lets test for parameter filter:

                var provider = new ParameterValueProvider(new ElementId(BuiltInParameter.ALL_MODEL_MARK));
                var evaluator = new FilterStringContains();
                var rule = new FilterStringRule(provider,evaluator, "CW02e");

                var paramFilter = new ElementParameterFilter(rule);

                //This is for Host Model

                var walls = new FilteredElementCollector(doc)
                    .OfClass(typeof(Wall))
                    .WherePasses(paramFilter)
                    .Cast<Wall>()
                    .ToList();

                foreach(var wall in walls)
                {
                    var wallId = wall.Id;
                    uidoc.ShowElements(wallId);
                }

                //Let's Check for Linked Model:

                var LinkedModel = new FilteredElementCollector(doc)
                    .OfClass(typeof(RevitLinkInstance))
                    .WhereElementIsNotElementType()
                    .Cast<RevitLinkInstance>()
                    .ToList();

                foreach(var ele in LinkedModel)
                {
                    var linkdoc = ele.GetLinkDocument();
                    var wallElement = new FilteredElementCollector(linkdoc)
                        .OfClass(typeof(Wall))
                        .WherePasses(paramFilter)
                        .Cast<Wall>()
                        .ToList();

                    foreach (var wall in wallElement)
                    {
                        var wallId = wall.Id;
                        uidoc.ShowElements(wallId);
                    }
                }



            }
            catch(Exception ex)
            {
                message = ex.Message;
            }

            return Result.Succeeded;
        }
    }
}
