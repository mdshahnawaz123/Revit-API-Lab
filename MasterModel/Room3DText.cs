using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DataLab;
using DataLab.Extensions;
using System;
using System.IO;
using System.Linq;

namespace MasterModel
{
    [Transaction(TransactionMode.Manual)]
    public class Room3DText : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            var doc = uidoc.Document;

            try
            {
                // Collect all rooms
                var rooms = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .WhereElementIsNotElementType()
                    .Cast<SpatialElement>()
                    .OfType<Autodesk.Revit.DB.Architecture.Room>()
                    .Where(r => r.Area > 0)
                    .ToList();

                if (!rooms.Any())
                {
                    TaskDialog.Show("Rooms", "No rooms found.");
                    return Result.Succeeded;
                }

                // Get ModelTextType
                var textType = new FilteredElementCollector(doc)
                    .OfClass(typeof(ModelTextType))
                    .Cast<ModelTextType>()
                    .FirstOrDefault();

                if (textType == null)
                {
                    TaskDialog.Show("Error", "No ModelTextType found.");
                    return Result.Failed;
                }

                using (Transaction tx = new Transaction(doc, "Create Room Model Text"))
                {
                    tx.Start();

                    foreach (var room in rooms)
                    {
                        var loc = room.Location as LocationPoint;
                        if (loc == null) continue;

                        XYZ point = loc.Point;

                        // Get room name
                        string roomName = room.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString();

                        if (string.IsNullOrEmpty(roomName))
                            continue;

                        // Create horizontal sketch plane at room level
                        Plane plane = Plane.CreateByNormalAndOrigin(XYZ.BasisZ, point);
                        SketchPlane sketchPlane = SketchPlane.Create(doc, plane);
                    }

                    tx.Commit();
                }

                TaskDialog.Show("Success", "Model Text created for all rooms.");
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