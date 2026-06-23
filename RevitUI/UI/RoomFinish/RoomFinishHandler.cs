using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RevitUI.UI.RoomFinish
{
    public class RoomFinishHandler : IExternalEventHandler
    {
        public bool CreateFloorFinish { get; set; } = true;
        public bool CreateWallFinish { get; set; } = true;
        public ElementId FloorTypeId { get; set; } = ElementId.InvalidElementId;
        public ElementId WallTypeId { get; set; } = ElementId.InvalidElementId;
        public bool FindCeilingHost { get; set; } = true;
        public bool FindCeilingLinked { get; set; } = true;
        public double HeightOverrideMm { get; set; } = -1;
        public RoomScope Scope { get; set; } = RoomScope.Selection;
        public bool IsSyncMode { get; set; } = false;
        public ElementId SelectedRoomId { get; set; } = ElementId.InvalidElementId;

        private UIApplication _app;
        private Document _doc;

        private const string FinishMarker = "BLabFinish";

        public void Execute(UIApplication app)
        {
            _app = app;
            _doc = app.ActiveUIDocument.Document;

            using (Transaction t = new Transaction(_doc, IsSyncMode ? "Sync Room Finishes" : "Generate Room Finishes"))
            {
                t.Start();

                try
                {
                    var rooms = GetRooms();
                    if (!rooms.Any())
                    {
                        TaskDialog.Show("Room Finish", "No valid rooms found based on the selected scope.");
                        t.RollBack();
                        return;
                    }

                    int floorCount = 0;
                    int wallCount = 0;

                    foreach (var item in rooms)
                    {
                        Room room = item.Room;
                        RevitLinkInstance link = item.Link;

                        if (IsSyncMode)
                        {
                            SyncRoomFinishes(room, link, out int newFloors, out int newWalls);
                            floorCount += newFloors;
                            wallCount += newWalls;
                        }
                        else
                        {
                            ProcessRoom(room, link, null, null, out int newFloors, out int newWalls);
                            floorCount += newFloors;
                            wallCount += newWalls;
                        }
                    }

                    t.Commit();
                    TaskDialog.Show("Success", $"Operation completed successfully!\n\nCreated {floorCount} Floor Finishes\nCreated {wallCount} Wall Finishes");
                }
                catch (Exception ex)
                {
                    t.RollBack();
                    TaskDialog.Show("Error", $"An error occurred:\n{ex.Message}\n{ex.StackTrace}");
                }
            }
        }

        public string GetName() => "Room Finish Generator";

        private List<(Room Room, RevitLinkInstance Link)> GetRooms()
        {
            var results = new List<(Room, RevitLinkInstance)>();
            UIDocument uidoc = _app.ActiveUIDocument;

            if (Scope == RoomScope.Selection)
            {
                var selectedIds = uidoc.Selection.GetElementIds();
                foreach (var id in selectedIds)
                {
                    if (_doc.GetElement(id) is Room r && r.Area > 0)
                        results.Add((r, null));
                }
            }
            else if (Scope == RoomScope.Level)
            {
                var activeView = uidoc.ActiveView;
                var level = activeView.GenLevel;
                if (level != null)
                {
                    var rooms = new FilteredElementCollector(_doc)
                        .OfCategory(BuiltInCategory.OST_Rooms)
                        .WhereElementIsNotElementType()
                        .Cast<Room>()
                        .Where(r => r.LevelId == level.Id && r.Area > 0);
                    foreach (var r in rooms) results.Add((r, null));
                }
            }
            else if (Scope == RoomScope.Host)
            {
                var rooms = new FilteredElementCollector(_doc)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .WhereElementIsNotElementType()
                    .Cast<Room>()
                    .Where(r => r.Area > 0);
                foreach (var r in rooms) results.Add((r, null));
            }
            else if (Scope == RoomScope.Linked)
            {
                var links = new FilteredElementCollector(_doc)
                    .OfClass(typeof(RevitLinkInstance))
                    .Cast<RevitLinkInstance>();

                foreach (var link in links)
                {
                    var linkDoc = link.GetLinkDocument();
                    if (linkDoc == null) continue;

                    var rooms = new FilteredElementCollector(linkDoc)
                        .OfCategory(BuiltInCategory.OST_Rooms)
                        .WhereElementIsNotElementType()
                        .Cast<Room>()
                        .Where(r => r.Area > 0);
                    
                    foreach (var r in rooms) results.Add((r, link));
                }
                
                // Also get host rooms
                var hostRooms = new FilteredElementCollector(_doc)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .WhereElementIsNotElementType()
                    .Cast<Room>()
                    .Where(r => r.Area > 0);
                foreach (var r in hostRooms) results.Add((r, null));
            }

            if (SelectedRoomId != ElementId.InvalidElementId)
            {
                results = results.Where(x => x.Item1.Id == SelectedRoomId).ToList();
            }

            return results;
        }

        private void SyncRoomFinishes(Room room, RevitLinkInstance link, out int floorsCreated, out int wallsCreated)
        {
            floorsCreated = 0;
            wallsCreated = 0;
            string roomNumber = room.Number;

            // 1. Find existing finishes for this room
            var existingFloors = new FilteredElementCollector(_doc)
                .OfClass(typeof(Floor))
                .Where(e => GetComments(e).Contains($"{FinishMarker}_Room:{roomNumber}"))
                .ToList();

            var existingWalls = new FilteredElementCollector(_doc)
                .OfClass(typeof(Wall))
                .Where(e => GetComments(e).Contains($"{FinishMarker}_Room:{roomNumber}"))
                .ToList();

            // We need to store their parameters before deleting
            var floorParamsList = new List<Dictionary<string, string>>();
            foreach (var f in existingFloors) floorParamsList.Add(ExtractParameters(f));

            var wallParamsList = new List<Dictionary<string, string>>();
            foreach (var w in existingWalls) wallParamsList.Add(ExtractParameters(w));

            // Delete old finishes
            foreach (var f in existingFloors) _doc.Delete(f.Id);
            foreach (var w in existingWalls) _doc.Delete(w.Id);

            // Re-generate finishes
            ProcessRoom(room, link, floorParamsList, wallParamsList, out floorsCreated, out wallsCreated);
        }

        private void ProcessRoom(Room room, RevitLinkInstance link, List<Dictionary<string, string>> oldFloorParams, List<Dictionary<string, string>> oldWallParams, out int floorsCreated, out int wallsCreated)
        {
            floorsCreated = 0;
            wallsCreated = 0;

            var options = new SpatialElementBoundaryOptions
            {
                SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Finish
            };

            var boundaries = room.GetBoundarySegments(options);
            if (boundaries == null || boundaries.Count == 0) return;

            Level level = _doc.GetElement(room.LevelId) as Level;
            if (link != null)
            {
                // In linked models, we need a corresponding level in host
                level = GetClosestLevel(_doc, room.Level.Elevation);
            }
            if (level == null) return;

            Transform t = link?.GetTotalTransform() ?? Transform.Identity;

            if (CreateFloorFinish && FloorTypeId != ElementId.InvalidElementId)
            {
                List<CurveLoop> curveLoops = new List<CurveLoop>();
                foreach (var boundaryList in boundaries)
                {
                    CurveLoop loop = new CurveLoop();
                    foreach (var segment in boundaryList)
                    {
                        Curve c = segment.GetCurve();
                        if (link != null) c = c.CreateTransformed(t);
                        loop.Append(c);
                    }
                    curveLoops.Add(loop);
                }

                if (curveLoops.Any())
                {
                    Floor newFloor = Floor.Create(_doc, curveLoops, FloorTypeId, level.Id);
                    
                    // Set marker
                    SetComments(newFloor, $"{FinishMarker}_Room:{room.Number}");

                    // Restore parameters
                    if (oldFloorParams != null && oldFloorParams.Any())
                    {
                        ApplyParameters(newFloor, oldFloorParams.First());
                    }

                    floorsCreated++;
                }
            }

            if (CreateWallFinish && WallTypeId != ElementId.InvalidElementId)
            {
                double height = GetWallHeight(room, link, t);

                // Offset the wall inward slightly to avoid overlapping host wall faces exactly
                double offsetInward = 0.01; // very small offset to ensure it's inside the room

                int wallIndex = 0;
                foreach (var boundaryList in boundaries)
                {
                    foreach (var segment in boundaryList)
                    {
                        ElementId hostId = segment.ElementId;
                        Curve curve = segment.GetCurve();
                        if (link != null) curve = curve.CreateTransformed(t);

                        // Only create finish against actual bounding elements
                        if (hostId == ElementId.InvalidElementId) continue;

                        Element hostElem = null;
                        if (link != null)
                        {
                            var linkDoc = link.GetLinkDocument();
                            if (linkDoc != null) hostElem = linkDoc.GetElement(hostId);
                        }
                        else
                        {
                            hostElem = _doc.GetElement(hostId);
                        }

                        if (hostElem is Wall hostWall)
                        {
                            // Create Wall
                            Wall newWall = Wall.Create(_doc, curve, WallTypeId, level.Id, height, 0, false, false);
                            
                            // Adjust location line
                            Parameter locLine = newWall.get_Parameter(BuiltInParameter.WALL_KEY_REF_PARAM);
                            if (locLine != null && !locLine.IsReadOnly)
                            {
                                locLine.Set((int)WallLocationLine.FinishFaceExterior); // Face toward host
                            }

                            SetComments(newWall, $"{FinishMarker}_Room:{room.Number}_HostWall:{hostWall.Id}");

                            // Restore parameters
                            if (oldWallParams != null && wallIndex < oldWallParams.Count)
                            {
                                ApplyParameters(newWall, oldWallParams[wallIndex]);
                            }

                            // Join geometry to cut openings
                            if (link == null) // We can only join elements in the same document
                            {
                                try { JoinGeometryUtils.JoinGeometry(_doc, hostWall, newWall); } catch { }
                            }

                            wallIndex++;
                            wallsCreated++;
                        }
                    }
                }
            }
        }

        private double GetWallHeight(Room room, RevitLinkInstance link, Transform t)
        {
            if (HeightOverrideMm > 0)
                return HeightOverrideMm / 304.8; // Convert mm to decimal feet

            // Default fallback
            double defaultHeight = room.UnboundedHeight;
            if (defaultHeight <= 0) defaultHeight = 10.0; // 10 ft

            // Raycast setup
            XYZ roomCenter = GetRoomCenter(room);
            if (link != null) roomCenter = t.OfPoint(roomCenter);

            // Slightly above floor
            XYZ rayOrigin = new XYZ(roomCenter.X, roomCenter.Y, roomCenter.Z + 1.0); 
            XYZ rayDir = XYZ.BasisZ;

            double minHitDistance = double.MaxValue;

            if (FindCeilingHost)
            {
                ElementFilter filter = new ElementMulticategoryFilter(new List<BuiltInCategory> { BuiltInCategory.OST_Ceilings, BuiltInCategory.OST_Floors, BuiltInCategory.OST_Roofs });
                ReferenceIntersector intersector = new ReferenceIntersector(filter, FindReferenceTarget.Element, (View3D)Get3DView(_doc));
                
                var refWithContext = intersector.FindNearest(rayOrigin, rayDir);
                if (refWithContext != null)
                {
                    minHitDistance = refWithContext.Proximity + 1.0;
                }
            }

            if (FindCeilingLinked)
            {
                var links = new FilteredElementCollector(_doc).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>();
                foreach (var l in links)
                {
                    var lDoc = l.GetLinkDocument();
                    if (lDoc == null) continue;

                    Transform lTransform = l.GetTotalTransform();
                    XYZ localOrigin = lTransform.Inverse.OfPoint(rayOrigin);
                    XYZ localDir = lTransform.Inverse.OfVector(rayDir);

                    ElementFilter filter = new ElementMulticategoryFilter(new List<BuiltInCategory> { BuiltInCategory.OST_Ceilings, BuiltInCategory.OST_Floors, BuiltInCategory.OST_Roofs });
                    
                    var view3d = Get3DView(lDoc);
                    if (view3d != null)
                    {
                        ReferenceIntersector intersector = new ReferenceIntersector(filter, FindReferenceTarget.Element, (View3D)view3d);
                        var refWithContext = intersector.FindNearest(localOrigin, localDir);
                        if (refWithContext != null)
                        {
                            double dist = refWithContext.Proximity + 1.0;
                            if (dist < minHitDistance) minHitDistance = dist;
                        }
                    }
                }
            }

            if (minHitDistance < 50.0) // Reasonable max height
            {
                return minHitDistance;
            }

            return defaultHeight;
        }

        private XYZ GetRoomCenter(Room room)
        {
            BoundingBoxXYZ bbox = room.get_BoundingBox(null);
            if (bbox != null)
            {
                return new XYZ((bbox.Min.X + bbox.Max.X) / 2, (bbox.Min.Y + bbox.Max.Y) / 2, bbox.Min.Z);
            }
            if (room.Location is LocationPoint lp) return lp.Point;
            return XYZ.Zero;
        }

        private View Get3DView(Document d)
        {
            return new FilteredElementCollector(d)
                .OfClass(typeof(View3D))
                .Cast<View3D>()
                .FirstOrDefault(v => !v.IsTemplate);
        }

        private Level GetClosestLevel(Document d, double elevation)
        {
            var levels = new FilteredElementCollector(d)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => Math.Abs(l.Elevation - elevation))
                .ToList();
            return levels.FirstOrDefault();
        }

        private string GetComments(Element e)
        {
            Parameter p = e.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
            if (p != null && p.HasValue) return p.AsString();
            return "";
        }

        private void SetComments(Element e, string val)
        {
            Parameter p = e.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
            if (p != null && !p.IsReadOnly) p.Set(val);
        }

        private Dictionary<string, string> ExtractParameters(Element e)
        {
            var dict = new Dictionary<string, string>();
            foreach (Parameter p in e.Parameters)
            {
                if (!p.IsReadOnly && p.StorageType == StorageType.String)
                {
                    if (p.Definition.Name == "Comments") continue; // skip marker
                    if (p.HasValue) dict[p.Definition.Name] = p.AsString();
                }
                else if (!p.IsReadOnly && p.StorageType == StorageType.Double)
                {
                    if (p.HasValue) dict[p.Definition.Name] = p.AsDouble().ToString();
                }
                else if (!p.IsReadOnly && p.StorageType == StorageType.Integer)
                {
                    if (p.HasValue) dict[p.Definition.Name] = p.AsInteger().ToString();
                }
            }
            return dict;
        }

        private void ApplyParameters(Element e, Dictionary<string, string> dict)
        {
            foreach (var kvp in dict)
            {
                Parameter p = e.LookupParameter(kvp.Key);
                if (p != null && !p.IsReadOnly)
                {
                    if (p.StorageType == StorageType.String) p.Set(kvp.Value);
                    else if (p.StorageType == StorageType.Double && double.TryParse(kvp.Value, out double dVal)) p.Set(dVal);
                    else if (p.StorageType == StorageType.Integer && int.TryParse(kvp.Value, out int iVal)) p.Set(iVal);
                }
            }
        }
    }
}
