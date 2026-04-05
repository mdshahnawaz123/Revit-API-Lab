using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace MasterModel
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class SectionCutter : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            var doc = uidoc.Document;

            try
            {
                // 1. Pick wall
                var reference = uidoc.Selection.PickObject(ObjectType.Element, "Select a Wall");
                var wall = doc.GetElement(reference) as Wall;

                if (wall == null)
                {
                    message = "Selected element is not a Wall.";
                    return Result.Failed;
                }

                // 2. Get Section ViewFamilyType
                var sectionTypeId = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewFamilyType))
                    .Cast<ViewFamilyType>()
                    .FirstOrDefault(x => x.ViewFamily == ViewFamily.Section)?.Id;

                if (sectionTypeId == null)
                {
                    message = "Section ViewFamilyType not found.";
                    return Result.Failed;
                }

                // 3. Get wall geometry
                var location = wall.Location as LocationCurve;
                if (location == null)
                {
                    message = "Wall has no location curve.";
                    return Result.Failed;
                }

                var curve = location.Curve;
                var midPoint = curve.Evaluate(0.5, true);
                var start = curve.GetEndPoint(0);
                var end = curve.GetEndPoint(1);

                // 4. ✅ Build orientation vectors
                var wallDir = (end - start).Normalize();       // along the wall (X axis)
                var upDir = XYZ.BasisZ;                      // up (Y axis)
                var viewDir = wallDir.CrossProduct(upDir).Normalize(); // into wall (Z axis = camera)

                // 5. ✅ Build Transform — positions AND orients the section
                var transform = Transform.Identity;
                transform.Origin = midPoint;
                transform.BasisX = wallDir;   // horizontal in view
                transform.BasisY = upDir;     // vertical in view
                transform.BasisZ = viewDir;   // view depth direction

                // 6. ✅ Build BoundingBoxXYZ with Transform
                double width = 10; // half-width left & right of midpoint (feet)
                double height = 10; // view height (feet)
                double depth = 5;  // view depth (feet)

                var bb = new BoundingBoxXYZ
                {
                    Transform = transform,
                    Min = new XYZ(-width, 0, -depth),
                    Max = new XYZ(width, height, depth)
                };

                // 7. Duplicate name check
                var sectionName = $"Section_{wall.Id}";
                bool alreadyExists = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewSection))
                    .Any(x => x.Name == sectionName);

                if (alreadyExists)
                {
                    TaskDialog.Show("Skipped", $"'{sectionName}' already exists.");
                    return Result.Succeeded;
                }

                // 8. Create section
                using (var tx = new Transaction(doc, "Create Wall Cross-Section"))
                {
                    tx.Start();

                    var newSection = ViewSection.CreateSection(doc, sectionTypeId, bb);
                    
                    newSection.Name = sectionName;

                    tx.Commit();
                }

                TaskDialog.Show("Done", $"Section '{sectionName}' created successfully.");
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