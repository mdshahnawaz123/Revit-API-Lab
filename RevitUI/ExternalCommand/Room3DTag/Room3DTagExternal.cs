using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using DataLab.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RevitUI.ExternalCommand.Room3DTag
{
    public class Room3DTagExternal : IExternalEventHandler
    {
        private const string TagFamilyName = "3D Room Tag (BDD)";

        // ── Properties set by the UI before Raise() ──────────────────────────

        public ElementId SelectedPhaseId { get; set; }
        public int SelectedWorksetId { get; set; } = -1;
        public bool UpdateExisting { get; set; } = true;
        public ElementId TagSymbolId { get; set; }
        public bool IncludeHostModel { get; set; } = true;
        public bool IncludeLinkedModel { get; set; } = false;

        // ─────────────────────────────────────────────────────────────────────

        public void Execute(UIApplication app)
        {
            var uidoc = app.ActiveUIDocument;
            var doc = uidoc?.Document;

            try
            {
                if (!IncludeHostModel && !IncludeLinkedModel)
                {
                    TaskDialog.Show("Info", "Please select at least one source:\n• Host Model\n• Linked Model");
                    return;
                }

                // ── Resolve the FamilySymbol ─────────────────────────────────
                FamilySymbol tagSymbol = null;

                if (TagSymbolId != null && TagSymbolId != ElementId.InvalidElementId)
                {
                    tagSymbol = doc.GetElement(TagSymbolId) as FamilySymbol;
                }

                if (tagSymbol == null)
                {
                    tagSymbol = new FilteredElementCollector(doc)
                        .OfClass(typeof(FamilySymbol))
                        .OfCategory(BuiltInCategory.OST_GenericModel)
                        .Cast<FamilySymbol>()
                        .FirstOrDefault(fs => fs.Family.Name == TagFamilyName);
                }

                if (tagSymbol == null)
                {
                    TaskDialog.Show("Error", $"Family \"{TagFamilyName}\" not found in the project.\n" + "Please load it first.");
                    return;
                }

                Phase selectedPhase = null;
                if (SelectedPhaseId != null && SelectedPhaseId != ElementId.InvalidElementId)
                    selectedPhase = doc.GetElement(SelectedPhaseId) as Phase;

                var roomEntries = new List<(Room room, XYZ point, string roomKey, double elevation)>();

                if (IncludeHostModel)
                {
                    var hostRooms = new FilteredElementCollector(doc)
                        .OfCategory(BuiltInCategory.OST_Rooms)
                        .WhereElementIsNotElementType()
                        .Cast<SpatialElement>()
                        .OfType<Room>()
                        .Where(r => r.Location != null)
                        .ToList();

                    if (selectedPhase != null)
                    {
                        hostRooms = hostRooms.Where(r =>
                        {
                            var phParam = r.get_Parameter(BuiltInParameter.ROOM_PHASE);
                            return phParam != null && phParam.AsElementId() == selectedPhase.Id;
                        }).ToList();
                    }

                    foreach (var room in hostRooms)
                    {
                        var loc = room.Location as LocationPoint;
                        if (loc == null) continue;
                        double elev = room.Level?.Elevation ?? loc.Point.Z;
                        roomEntries.Add((room, loc.Point, $"HOST_{room.Id.Value}", elev));
                    }
                }

                if (IncludeLinkedModel)
                {
                    var links = new FilteredElementCollector(doc)
                        .OfClass(typeof(RevitLinkInstance))
                        .WhereElementIsNotElementType()
                        .Cast<RevitLinkInstance>()
                        .ToList();

                    foreach (var link in links)
                    {
                        var linkDoc = link.GetLinkDocument();
                        if (linkDoc == null) continue;

                        var transform = link.GetTransform();

                        var linkedRooms = new FilteredElementCollector(linkDoc)
                            .OfCategory(BuiltInCategory.OST_Rooms)
                            .WhereElementIsNotElementType()
                            .Cast<SpatialElement>()
                            .OfType<Room>()
                            .Where(r => r.Location != null)
                            .ToList();

                        if (selectedPhase != null)
                        {
                            linkedRooms = linkedRooms.Where(r =>
                            {
                                var phParam = r.get_Parameter(BuiltInParameter.ROOM_PHASE);
                                if (phParam == null) return false;
                                var ph = linkDoc.GetElement(phParam.AsElementId());
                                return ph != null && ph.Name == selectedPhase.Name;
                            }).ToList();
                        }

                        foreach (var room in linkedRooms)
                        {
                            var loc = room.Location as LocationPoint;
                            if (loc == null) continue;
                            XYZ hostPoint = transform.OfPoint(loc.Point);
                            double elev = room.Level != null ? transform.OfPoint(new XYZ(0, 0, room.Level.Elevation)).Z : hostPoint.Z;
                            roomEntries.Add((room, hostPoint, $"LINK_{link.Id.Value}_{room.Id.Value}", elev));
                        }
                    }
                }

                if (!roomEntries.Any())
                {
                    TaskDialog.Show("Info", "No placed rooms found in the selected source(s).");
                    return;
                }

                var existingTags = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilyInstance))
                    .OfCategory(BuiltInCategory.OST_GenericModel)
                    .Cast<FamilyInstance>()
                    .Where(fi => fi.Symbol?.Family?.Name == TagFamilyName)
                    .ToList();

                var taggedRoomIds = new HashSet<string>();
                var tagByRoomId = new Dictionary<string, FamilyInstance>();

                foreach (var tag in existingTags)
                {
                    var roomIdParam = tag.GetParameters("RoomId").FirstOrDefault(p => !p.IsReadOnly) ?? tag.GetParameters("RoomId").FirstOrDefault();
                    if (roomIdParam != null)
                    {
                        string rid = "";
                        if (roomIdParam.StorageType == StorageType.String)
                            rid = roomIdParam.AsString() ?? "";
                        else if (roomIdParam.StorageType == StorageType.Integer)
                            rid = roomIdParam.AsInteger().ToString();
                        else if (roomIdParam.StorageType == StorageType.Double)
                            rid = ((long)roomIdParam.AsDouble()).ToString();

                        if (!string.IsNullOrEmpty(rid))
                        {
                            string normalizedRid = rid;
                            if (int.TryParse(rid, out _) && !rid.StartsWith("HOST_") && !rid.StartsWith("LINK_"))
                                normalizedRid = "HOST_" + rid;

                            taggedRoomIds.Add(normalizedRid);
                            if (!tagByRoomId.ContainsKey(normalizedRid))
                                tagByRoomId[normalizedRid] = tag;
                        }
                    }
                }

                int created = 0;
                int updated = 0;
                int skipped = 0;
                var sb = new StringBuilder();

                using (Transaction tx = new Transaction(doc, "Create / Update 3D Room Tags"))
                {
                    tx.Start();

                    if (!tagSymbol.IsActive) tagSymbol.Activate();

                    foreach (var (room, point, roomKey, elev) in roomEntries)
                    {
                        string roomName = room.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString() ?? "";
                        string roomNumber = room.get_Parameter(BuiltInParameter.ROOM_NUMBER)?.AsString() ?? "";

                        // ── Check if a tag already exists for this RoomId ────
                        if (taggedRoomIds.Contains(roomKey))
                        {
                            if (UpdateExisting && tagByRoomId.TryGetValue(roomKey, out var existingTag))
                            {
                                var existingNameParam = existingTag.GetParameters("Name").FirstOrDefault(p => !p.IsReadOnly);
                                var existingNumberParam = existingTag.GetParameters("Number").FirstOrDefault(p => !p.IsReadOnly);

                                string existingName = existingNameParam?.AsString() ?? "";
                                string existingNumber = existingNumberParam?.AsString() ?? "";
                                
                                bool nameChanged = existingName != roomName;
                                bool numberChanged = existingNumber != roomNumber;

                                if (nameChanged || numberChanged)
                                {
                                    SetTagParameters(existingTag, roomName, roomNumber);

                                    if (SelectedWorksetId >= 0)
                                    {
                                        var wsParam = existingTag.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);
                                        if (wsParam != null && !wsParam.IsReadOnly)
                                            wsParam.Set(SelectedWorksetId);
                                    }

                                    updated++;
                                    sb.AppendLine($"  ✔ Updated: {roomNumber} | {roomName}");
                                }
                                else
                                {
                                    skipped++;
                                }
                            }
                            else
                            {
                                skipped++;
                            }
                            continue;
                        }

                        // ── Place a new tag instance ─────────────────────────
                        Level hostLevel = GetClosestLevel(doc, elev);
                        FamilyInstance newTag = null;

                        if (hostLevel != null)
                        {
                            newTag = doc.Create.NewFamilyInstance(
                                point,
                                tagSymbol,
                                hostLevel,
                                Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                        }
                        else
                        {
                            newTag = doc.Create.NewFamilyInstance(
                                point,
                                tagSymbol,
                                Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                        }

                        if (newTag == null) { skipped++; continue; }

                        // Store the room key so we can track this tag
                        var roomIdParam = newTag.GetParameters("RoomId").FirstOrDefault(p => !p.IsReadOnly);
                        if (roomIdParam != null)
                        {
                            if (roomIdParam.StorageType == StorageType.String)
                            {
                                roomIdParam.Set(roomKey);
                            }
                            else if (roomIdParam.StorageType == StorageType.Integer)
                            {
                                // Handle Revit 2024+ ElementId (long) cast to int for Parameter.Set
                                roomIdParam.Set((int)room.Id.Value);
                            }
                            else if (roomIdParam.StorageType == StorageType.Double)
                            {
                                roomIdParam.Set((double)room.Id.Value);
                            }
                        }

                        SetTagParameters(newTag, roomName, roomNumber);

                        // Set phase
                        if (selectedPhase != null)
                        {
                            var phaseParam = newTag.get_Parameter(BuiltInParameter.PHASE_CREATED);
                            if (phaseParam != null && !phaseParam.IsReadOnly)
                                phaseParam.Set(selectedPhase.Id);
                        }

                        // Set workset
                        if (SelectedWorksetId >= 0)
                        {
                            var wsParam = newTag.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);
                            if (wsParam != null && !wsParam.IsReadOnly)
                                wsParam.Set(SelectedWorksetId);
                        }

                        created++;
                        sb.AppendLine($"  + Created: {roomNumber} | {roomName}");
                    }

                    tx.Commit();
                }

                var summary = new StringBuilder();
                summary.AppendLine($"Created:  {created}");
                summary.AppendLine($"Updated:  {updated}");
                if (skipped > 0)
                    summary.AppendLine($"Skipped:  {skipped}");
                summary.AppendLine();
                summary.Append(sb);

                TaskDialog.Show("3D Room Tags — Complete", summary.ToString());
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", $"Exception in Room3DTagExternal.Execute:\n\n{ex.Message}\n\n{ex.StackTrace}");
            }
        }

        private static void SetTagParameters(FamilyInstance tag, string name, string number)
        {
            var nameParam = tag.GetParameters("Name").FirstOrDefault(p => !p.IsReadOnly);
            if (nameParam != null)
                nameParam.Set(name);

            var numberParam = tag.GetParameters("Number").FirstOrDefault(p => !p.IsReadOnly);
            if (numberParam != null)
                numberParam.Set(number);
        }

        private static Level GetClosestLevel(Document doc, double elevation)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => Math.Abs(l.Elevation - elevation))
                .FirstOrDefault();
        }

        public string GetName() => "Create / Update 3D Room Tags";
    }
}
