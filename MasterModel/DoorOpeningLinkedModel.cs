using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DataLab;
using DataLab.Extensions;
using System;
using System.Linq;

[Transaction(TransactionMode.Manual)]
public class DoorOpeningLinkedModel : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var uidoc = commandData.Application.ActiveUIDocument;
        var doc = uidoc.Document;

        try
        {
            var opt = new Options()
            {
                ComputeReferences = true,
                DetailLevel = ViewDetailLevel.Fine
            };

            // FIX #1: Use the tuple overload so we have the Transform
            var doors = doc.GetLinkedFamilyWithTransform(BuiltInCategory.OST_Doors);

            doc.DoAction(() =>
            {
                foreach (var entry in doors)
                {
                    var doorInstance = entry.Instance;
                    var transform = entry.Transform;

                    // FIX #2: Get the bounding box from the linked door
                    //         and transform its corners into host coordinates
                    var doorBB = doorInstance.get_BoundingBox(null);
                    if (doorBB == null) continue;

                    var minWorld = transform.OfPoint(doorBB.Min);
                    var maxWorld = transform.OfPoint(doorBB.Max);

                    // Build a solid from the transformed bounding box
                    // to find the intersecting wall in the HOST document
                    var outline = new Outline(minWorld, maxWorld);
                    var bbFilter = new BoundingBoxIntersectsFilter(outline);

                    // FIX #3: Find the host-document wall that the door sits in
                    var hostWalls = new FilteredElementCollector(doc)
                        .OfClass(typeof(Wall))
                        .WhereElementIsNotElementType()
                        .WherePasses(bbFilter)
                        .Cast<Wall>()
                        .ToList();

                    foreach (var hostWall in hostWalls)
                    {
                        // NewOpening(Wall, XYZ, XYZ) — points must be in
                        // host world coordinates, which they now are
                        doc.Create.NewOpening(hostWall, minWorld, maxWorld);
                    }
                }
            });

            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return Result.Failed;
        }
    }
}