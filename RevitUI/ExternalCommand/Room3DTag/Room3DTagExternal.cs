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

        /// <summary>ElementId of the Phase selected in PhaseComboBox.</summary>
        public ElementId SelectedPhaseId { get; set; }

        /// <summary>Workset ID selected in WorksetComboBox (-1 = not set).</summary>
        public int SelectedWorksetId { get; set; } = -1;

        /// <summary>
        /// When true, update existing 3D tag instances of the selected type
        /// (Name/Number) while preserving their manual positions.
        /// When false, skip existing tags and only create missing ones.
        /// </summary>
        public bool UpdateExisting { get; set; } = true;

        /// <summary>
        /// The FamilySymbol (type) of the "3D Room Tag (BDD)" family to place.
        /// </summary>
        public ElementId TagSymbolId { get; set; }

        /// <summary>Include rooms from the host model.</summary>
        public bool IncludeHostModel { get; set; } = true;

        /// <summary>Include rooms from linked models.</summary>
        public bool IncludeLinkedModel { get; set; } = false;

        // ─────────────────────────────────────────────────────────────────────

        public void Execute(UIApplication app)
        {
            var uidoc = app.ActiveUIDocument;
            var doc = uidoc.Document;

            try
            {
                if (!IncludeHostModel && !IncludeLinkedModel)
                {
                    TaskDialog.Show("Info",
                        "Please select at least one source:\n• Host Model\n• Linked Model");
                    return;
                }

                // ── Resolve the FamilySymbol ─────────────────────────────────
                FamilySymbol tagSymbol = null;

                if (TagSymbolId != null && TagSymbolId != ElementId.InvalidElementId)
                {
                    var inst = doc.GetElement(TagSymbolId) as FamilyInstance;
                    tagSymbol = inst?.Symbol;
                }

                // Fallback: find any symbol from the tag family
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
                    TaskDialog.Show("Error",
                        $"Family \"{TagFamilyName}\" not found in the project.\n" +
                        "Please load it first.");
                    return;
                }

                // ── Resolve Phase ────────────────────────────────────────────
                Phase selectedPhase = null;
                if (SelectedPhaseId != null && SelectedPhaseId != ElementId.InvalidElementId)
                    selectedPhase = doc.GetElement(SelectedPhaseId) as Phase;

                // ── Collect rooms from selected sources ──────────────────────
                // Each entry: (Room, point in host coordinates, unique key)
                var roomEntries = new List<(Room room, XYZ point, string roomKey)>();

                if (IncludeHostModel)
                {
                    var hostRooms = new FilteredElementCollector(doc)
                        .OfCategory(BuiltInCategory.OST_Rooms)
                        .WhereElementIsNotElementType()
                        .Cast<SpatialElement>()
                        .OfType<Room>()
                        .Where(r => r.Area > 0 && r.Location != null)
                        .ToList();

                    // Filter by phase
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
                        // Key = "HOST_<elementId>" to ensure uniqueness
                        roomEntries.Add((room, loc.Point, $"HOST_{room.Id.Value}"));
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
                            .Where(r => r.Area > 0 && r.Location != null)
                            .ToList();

                        foreach (var room in linkedRooms)
                        {
                            var loc = room.Location as LocationPoint;
                            if (loc == null) continue;
                            // Transform linked point to host coordinates
                            XYZ hostPoint = transform.OfPoint(loc.Point);
                            // Key = "LINK_<linkInstanceId>_<elementId>" to ensure uniqueness
                            roomEntries.Add((room, hostPoint, $"LINK_{link.Id.Value}_{room.Id.Value}"));
                        }
                    }
                }

                if (!roomEntries.Any())
                {
                    TaskDialog.Show("Info", "No placed rooms found in the selected source(s).");
                    return;
                }

                // ── Collect existing tags and build RoomId lookup ─────────────
                var existingTags = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilyInstance))
                    .OfCategory(BuiltInCategory.OST_GenericModel)
                    .Cast<FamilyInstance>()
                    .Where(fi => fi.Symbol?.Family?.Name == TagFamilyName)
                    .ToList();

                // Build a set of RoomId values that already have a tag placed
                var taggedRoomIds = new HashSet<string>();
                // Also build a lookup for update-existing logic
                var tagByRoomId = new Dictionary<string, FamilyInstance>();

                foreach (var tag in existingTags)
                {
                    var roomIdParam = tag.LookupParameter("RoomId");
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
                            // If it's a numeric-only key, we assume it's a host room ID
                            // (since links require prefixed keys which numeric params can't store)
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

                doc.DoAction(() =>
                {
                    if (!tagSymbol.IsActive)
                        tagSymbol.Activate();

                    foreach (var (room, point, roomKey) in roomEntries)
                    {
                        string roomName = room.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString() ?? "";
                        string roomNumber = room.get_Parameter(BuiltInParameter.ROOM_NUMBER)?.AsString() ?? "";

                        // ── Check if a tag already exists for this RoomId ────
                        if (taggedRoomIds.Contains(roomKey))
                        {
                            if (UpdateExisting && tagByRoomId.TryGetValue(roomKey, out var existingTag))
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
                                sb.AppendLine($"  ⏭ Skipped (exists): {roomNumber} | {roomName}");
                            }
                            continue;
                        }

                        // ── Place a new tag instance ─────────────────────────
                        var newTag = doc.Create.NewFamilyInstance(
                            point,
                            tagSymbol,
                            Autodesk.Revit.DB.Structure.StructuralType.NonStructural);

                        if (newTag == null) { skipped++; continue; }

                        // Store the room key so we can track this tag
                        var roomIdParam = newTag.LookupParameter("RoomId");
                        if (roomIdParam != null && !roomIdParam.IsReadOnly)
                        {
                            if (roomIdParam.StorageType == StorageType.String)
                                roomIdParam.Set(roomKey);
                            else if (roomIdParam.StorageType == StorageType.Integer)
                                roomIdParam.Set(room.Id.Value);
                            else if (roomIdParam.StorageType == StorageType.Double)
                                roomIdParam.Set((double)room.Id.Value);
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
                }, "Create / Update 3D Room Tags");

                // ── Summary ──────────────────────────────────────────────────
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
                TaskDialog.Show("Error", $"{ex.Message}\n\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Sets the "Name" and "Number" instance parameters on a 3D Room Tag family instance.
        /// </summary>
        private static void SetTagParameters(FamilyInstance tag, string name, string number)
        {
            var nameParam = tag.LookupParameter("Name");
            if (nameParam != null && !nameParam.IsReadOnly)
                nameParam.Set(name);

            var numberParam = tag.LookupParameter("Number");
            if (numberParam != null && !numberParam.IsReadOnly)
                numberParam.Set(number);
        }

        public string GetName() => "Create / Update 3D Room Tags";
    }
}
