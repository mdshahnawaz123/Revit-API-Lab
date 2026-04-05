using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace MasterModel
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CreateDetailsLine : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            var doc = uidoc.Document;

            try
            {
                //let's create circle


                var c1 = Arc.Create(new XYZ(10, 0, 0), 20, 0, 70, XYZ.BasisX, XYZ.BasisY);


                using(var tx = new Transaction(doc,"Create Details Curve"))
                {
                    tx.Start();
                    var line = Line.CreateBound(new XYZ(10, 0, 0), new XYZ(30, 0, 0));
                    var geometryCurveArray = new CurveArray();

                    geometryCurveArray.Append(line);
                    geometryCurveArray.Append(c1);



                    doc.Create.NewDetailCurveArray(doc.ActiveView, geometryCurveArray);
                    tx.Commit();
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
