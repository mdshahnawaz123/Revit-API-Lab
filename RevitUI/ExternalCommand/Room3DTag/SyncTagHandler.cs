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
    /// <summary>
    /// Syncs existing 3D Room Tag instances with current room data.
    /// Updates Name and Number parameters when the source room has changed,
    /// but does NOT move the tag — its manual position is always preserved.
    /// Matching is done via the "RoomId" parameter stored on each tag.
    /// Supports both host model (HOST_xxx) and linked model (LINK_xxx_yyy) keys.
    /// </summary>
    public class SyncTagHandler : IExternalEventHandler
    {
        private const string TagFamilyName = "3D Room Tag (BDD)";

        public void Execute(UIApplication app)
        {
            var uidoc = app.ActiveUIDocument;
            var doc = uidoc.Document;

            try
            {
                // ── Collect all existing 3D Room Tag instances ────────────────
                var existingTags = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilyInstance))
                    .OfCategory(BuiltInCategory.OST_GenericModel)
                    .Cast<FamilyInstance>()
                    .Where(fi => fi.Symbol?.Family?.Name == TagFamilyName)
                    .ToList();

                if (!existingTags.Any())
                {
                    TaskDialog.Show("Sync", "No 3D Room Tags found to sync.");
                    return;
                }

                // ── Build room lookups ───────────────────────────────────────

                // Host rooms: key = "HOST_<elementId>"
                var hostRooms = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .WhereElementIsNotElementType()
                    .Cast<SpatialElement>()
                    .OfType<Room>()
                    .ToDictionary(r => $"HOST_{r.Id.Value}", r => r);

                // Linked rooms: key = "LINK_<linkInstanceId>_<elementId>"
                var linkedRooms = new Dictionary<string, Room>();
                var links = new FilteredElementCollector(doc)
                    .OfClass(typeof(RevitLinkInstance))
                    .WhereElementIsNotElementType()
                    .Cast<RevitLinkInstance>()
                    .ToList();

                foreach (var link in links)
                {
                    var linkDoc = link.GetLinkDocument();
                    if (linkDoc == null) continue;

                    var rooms = new FilteredElementCollector(linkDoc)
                        .OfCategory(BuiltInCategory.OST_Rooms)
                        .WhereElementIsNotElementType()
                        .Cast<SpatialElement>()
                        .OfType<Room>()
                        .ToList();

                    foreach (var room in rooms)
                    {
                        var key = $"LINK_{link.Id.Value}_{room.Id.Value}";
                        if (!linkedRooms.ContainsKey(key))
                            linkedRooms[key] = room;
                    }
                }

                // Merge both lookups
                var allRooms = new Dictionary<string, Room>(hostRooms);
                foreach (var kvp in linkedRooms)
                {
                    if (!allRooms.ContainsKey(kvp.Key))
                        allRooms[kvp.Key] = kvp.Value;
                }

                int updated = 0;
                int unchanged = 0;
                int orphaned = 0;
                var sb = new StringBuilder();

                doc.DoAction(() =>
                {
                    foreach (var tag in existingTags)
                    {
                        // ── Read the RoomId stored on the tag ────────────────
                        var roomIdParam = tag.LookupParameter("RoomId");
                        if (roomIdParam == null)
                        {
                            orphaned++;
                            sb.AppendLine($"  ⚠ No RoomId parameter: Tag #{tag.Id.Value}");
                            continue;
                        }

                        string roomKey = "";
                        if (roomIdParam.StorageType == StorageType.String)
                            roomKey = roomIdParam.AsString() ?? "";
                        else if (roomIdParam.StorageType == StorageType.Integer)
                            roomKey = roomIdParam.AsInteger().ToString();
                        else if (roomIdParam.StorageType == StorageType.Double)
                            roomKey = ((long)roomIdParam.AsDouble()).ToString();

                        if (string.IsNullOrWhiteSpace(roomKey))
                        {
                            orphaned++;
                            sb.AppendLine($"  ⚠ Empty RoomId: Tag #{tag.Id.Value}");
                            continue;
                        }

                        // If it's a numeric-only key, we assume it's a host room ID
                        // (since links require prefixed keys which numeric params can't store)
                        if (int.TryParse(roomKey, out _) && !roomKey.StartsWith("HOST_") && !roomKey.StartsWith("LINK_"))
                            roomKey = "HOST_" + roomKey;

                        // ── Look up the source room ──────────────────────────
                        if (!allRooms.TryGetValue(roomKey, out var room))
                        {
                            orphaned++;
                            sb.AppendLine($"  ⚠ Room not found for key \"{roomKey}\" — Tag #{tag.Id.Value}");
                            continue;
                        }

                        // ── Read current room data ───────────────────────────
                        string currentName = room.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString() ?? "";
                        string currentNumber = room.get_Parameter(BuiltInParameter.ROOM_NUMBER)?.AsString() ?? "";

                        // ── Read existing tag data ───────────────────────────
                        string tagName = tag.LookupParameter("Name")?.AsString() ?? "";
                        string tagNumber = tag.LookupParameter("Number")?.AsString() ?? "";

                        // ── Compare and update if changed ────────────────────
                        bool nameChanged = tagName != currentName;
                        bool numberChanged = tagNumber != currentNumber;

                        if (nameChanged || numberChanged)
                        {
                            if (nameChanged)
                            {
                                var np = tag.LookupParameter("Name");
                                if (np != null && !np.IsReadOnly) np.Set(currentName);
                            }

                            if (numberChanged)
                            {
                                var np = tag.LookupParameter("Number");
                                if (np != null && !np.IsReadOnly) np.Set(currentNumber);
                            }

                            updated++;
                            sb.AppendLine($"  ✔ Synced: {currentNumber} | {currentName}");
                            if (nameChanged)
                                sb.AppendLine($"      Name: \"{tagName}\" → \"{currentName}\"");
                            if (numberChanged)
                                sb.AppendLine($"      Number: \"{tagNumber}\" → \"{currentNumber}\"");
                        }
                        else
                        {
                            unchanged++;
                        }
                    }
                }, "Sync 3D Room Tags");

                // ── Summary ──────────────────────────────────────────────────
                var summary = new StringBuilder();
                summary.AppendLine($"Total tags scanned:  {existingTags.Count}");
                summary.AppendLine($"Updated:             {updated}");
                summary.AppendLine($"Already up-to-date:  {unchanged}");
                if (orphaned > 0)
                    summary.AppendLine($"Orphaned/Invalid:    {orphaned}");
                summary.AppendLine();
                summary.Append(sb);

                TaskDialog.Show("Sync 3D Room Tags — Complete", summary.ToString());
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", $"{ex.Message}\n\n{ex.StackTrace}");
            }
        }

        public string GetName() => "Sync 3D Room Tags";
    }
}
