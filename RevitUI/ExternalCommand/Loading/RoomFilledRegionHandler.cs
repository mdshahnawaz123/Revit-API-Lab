using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RevitUI.ExternalCommand.Loading
{
    public class RoomFilledRegionHandler : IExternalEventHandler
    {
        public bool IsHostModelOption { get; set; } = true;
        public List<Element> SelectedRooms { get; set; } = new List<Element>();

        public void Execute(UIApplication app)
        {
            var doc = app.ActiveUIDocument.Document;
            var sb = new StringBuilder();
            int createdCount = 0;
            int updatedCount = 0;
            int skippedCount = 0;

            try
            {
                using (Transaction tx = new Transaction(doc, "Create/Update Filled Regions for Rooms"))
                {
                    tx.Start();

                    var patterns = new FilteredElementCollector(doc)
                        .OfClass(typeof(FillPatternElement))
                        .Cast<FillPatternElement>()
                        .ToList();

                    var hatchPatterns = patterns
                        .Where(p => !p.GetFillPattern().IsSolidFill && p.GetFillPattern().Target == FillPatternTarget.Drafting)
                        .ToList();

                    var solidPattern = patterns.FirstOrDefault(p => p.GetFillPattern().IsSolidFill);

                    var uniqueRoomTypes = SelectedRooms.OfType<SpatialElement>().Select(r => r.Name).Distinct().ToList();
                    var existingRegionTypes = new FilteredElementCollector(doc)
                        .OfClass(typeof(FilledRegionType))
                        .Cast<FilledRegionType>()
                        .ToList();

                    var baseRegionType = existingRegionTypes.FirstOrDefault();
                    if (baseRegionType == null)
                    {
                        TaskDialog.Show("Error", "No FilledRegionType found in the document.");
                        tx.RollBack();
                        return;
                    }

                    Dictionary<string, ElementId> regionTypes = new Dictionary<string, ElementId>();
                    int colorIndex = 0;
                    List<Color> colors = new List<Color> {
                        new Color(255, 0, 0), new Color(0, 255, 0), new Color(0, 0, 255),
                        new Color(255, 128, 0), new Color(0, 200, 200), new Color(200, 0, 200)
                    };

                    foreach (var roomType in uniqueRoomTypes)
                    {
                        string typeName = "RoomFill_" + roomType;
                        var existing = existingRegionTypes.FirstOrDefault(rt => rt.Name == typeName);
                        FilledRegionType regionType = existing ?? baseRegionType.Duplicate(typeName) as FilledRegionType;

                        regionType.ForegroundPatternColor = colors[colorIndex % colors.Count];
                        if (hatchPatterns.Count > 0)
                            regionType.ForegroundPatternId = hatchPatterns[colorIndex % hatchPatterns.Count].Id;
                        else if (solidPattern != null)
                            regionType.ForegroundPatternId = solidPattern.Id;
                        
                        regionTypes[roomType] = regionType.Id;
                        colorIndex++;
                    }

                    View activeView = doc.ActiveView;
                    var existingRegionsInView = new FilteredElementCollector(doc, activeView.Id)
                        .OfClass(typeof(FilledRegion))
                        .Cast<FilledRegion>()
                        .ToList();

                    foreach (var room in SelectedRooms.OfType<SpatialElement>())
                    {
                        string roomIdentifier = "RoomLoading:" + room.UniqueId;
                        
                        // Find existing region for this room
                        var existingRegion = existingRegionsInView
                            .FirstOrDefault(fr => fr.IsValidObject && fr.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString() == roomIdentifier);

                        SpatialElementBoundaryOptions options = new SpatialElementBoundaryOptions
                        {
                            SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Finish
                        };

                        var boundaries = room.GetBoundarySegments(options);
                        if (boundaries == null || boundaries.Count == 0) continue;

                        List<CurveLoop> currentLoops = new List<CurveLoop>();
                        foreach (var loop in boundaries)
                        {
                            var curves = loop.Select(seg => seg.GetCurve()).ToList();
                            if (curves.Any())
                            {
                                try { currentLoops.Add(CurveLoop.Create(curves)); } catch { }
                            }
                        }

                        if (!currentLoops.Any()) continue;

                        // Compare boundaries if region already exists
                        if (existingRegion != null)
                        {
                            var existingLoops = existingRegion.GetBoundaries();
                            if (AreBoundariesEqual(currentLoops, existingLoops))
                            {
                                skippedCount++;
                                sb.AppendLine($"Skipped: Room '{room.Name}' (Boundary not changed)");
                                continue;
                            }
                            else
                            {
                                doc.Delete(existingRegion.Id);
                                updatedCount++;
                                sb.AppendLine($"Updated: Room '{room.Name}' (Boundary changed)");
                            }
                        }
                        else
                        {
                            createdCount++;
                            sb.AppendLine($"Created: Room '{room.Name}'");
                        }

                        if (regionTypes.TryGetValue(room.Name, out ElementId typeId))
                        {
                            FilledRegion newRegion = FilledRegion.Create(doc, typeId, activeView.Id, currentLoops);
                            newRegion.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.Set(roomIdentifier);
                        }
                    }

                    tx.Commit();
                }

                if (sb.Length > 0)
                {
                    TaskDialog.Show("Room Loading Summary", 
                        $"Results:\nCreated: {createdCount}\nUpdated: {updatedCount}\nSkipped: {skippedCount}\n\nDetails:\n" + sb.ToString());
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", ex.Message);
            }
        }

        private bool AreBoundariesEqual(IList<CurveLoop> loops1, IList<CurveLoop> loops2)
        {
            if (loops1.Count != loops2.Count) return false;
            
            // Simple check: compare total length and number of segments
            double len1 = loops1.Sum(l => l.GetExactLength());
            double len2 = loops2.Sum(l => l.GetExactLength());
            
            if (Math.Abs(len1 - len2) > 0.001) return false;

            int count1 = loops1.Sum(l => l.Count());
            int count2 = loops2.Sum(l => l.Count());

            return count1 == count2;
        }

        public string GetName()
        {
            return "RoomFilledRegionHandler";
        }
    }
}
