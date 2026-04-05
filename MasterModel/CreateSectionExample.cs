using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Linq;

namespace RevitUI
{
    [Transaction(TransactionMode.Manual)]
    public class CreateSectionExample : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                using (Transaction t = new Transaction(doc, "Create Section"))
                {
                    t.Start();

                    // 🔹 Define two points (your section line)
                    XYZ start = new XYZ(0, 0, 0);
                    XYZ end = new XYZ(40, 0, 0);

                    // 🔹 Direction of section line
                    XYZ direction = (end - start).Normalize();

                    // 🔹 Up direction
                    XYZ up = XYZ.BasisZ;

                    // 🔹 Right direction (important!)
                    XYZ right = direction.CrossProduct(up);

                    // 🔹 Midpoint of section
                    XYZ mid = (start + end) * 0.5;

                    // 🔹 Create transform (VERY IMPORTANT)
                    Transform transform = Transform.Identity;
                    transform.Origin = mid;
                    transform.BasisX = right;
                    transform.BasisY = up;
                    transform.BasisZ = direction;

                    // 🔹 Define section box size
                    double width = 40;   // along section line
                    double height = 10;  // vertical
                    double depth = 20;   // view depth

                    BoundingBoxXYZ box = new BoundingBoxXYZ();
                    box.Transform = transform;

                    box.Min = new XYZ(-width / 2, -height / 2, 0);
                    box.Max = new XYZ(width / 2, height / 2, depth);

                    // 🔹 Get Section View Type
                    ViewFamilyType sectionType = new FilteredElementCollector(doc)
                        .OfClass(typeof(ViewFamilyType))
                        .Cast<ViewFamilyType>()
                        .FirstOrDefault(x => x.ViewFamily == ViewFamily.Section)!;

                    if (sectionType == null)
                    {
                        message = "No section view type found!";
                        return Result.Failed;
                    }

                    // 🔹 Create Section
                    ViewSection section = ViewSection.CreateSection(doc, sectionType.Id, box);

                    t.Commit();

                    // 🔹 Open the section view
                    uidoc.ActiveView = section;
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}