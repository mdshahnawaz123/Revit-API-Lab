using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.DB.Architecture;

namespace RevitUI.UI.RoomSheet
{
    public class RoomSheetHandler : IExternalEventHandler
    {
        public List<RoomSheetDashboard.RoomItem> SelectedRooms { get; set; } = new List<RoomSheetDashboard.RoomItem>();
        public int ScaleValue { get; set; } = 50;
        public Action<string> OnSuccess { get; set; }

        public ElementId TitleBlockId { get; set; }
        public bool CreateAllElevations { get; set; } = true;
        public bool Create3DView { get; set; } = true;

        public void Execute(UIApplication app)
        {
            Document hostDoc = app.ActiveUIDocument.Document;
            if (SelectedRooms == null || !SelectedRooms.Any()) return;

            foreach (var item in SelectedRooms)
            {
                Document sourceDoc = item.SourceDoc;
                Room room = sourceDoc.GetElement(item.Id) as Room;
                if (room == null) continue;

                using (Transaction trans = new Transaction(hostDoc, "Create Room Data Sheet - " + room.Number))
                {
                    trans.Start();
                    try
                    {
                        // 1. Find Level in Host (Matching Name)
                        Level hostLevel = new FilteredElementCollector(hostDoc)
                            .OfClass(typeof(Level))
                            .Cast<Level>()
                            .FirstOrDefault(l => l.Name.Equals(room.Level.Name, StringComparison.InvariantCultureIgnoreCase));

                        if (hostLevel == null) hostLevel = new FilteredElementCollector(hostDoc).OfClass(typeof(Level)).FirstElement() as Level;

                        // 2. Plan View (Created in Host)
                        ViewFamilyType planType = new FilteredElementCollector(hostDoc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>().FirstOrDefault(x => x.ViewFamily == ViewFamily.FloorPlan);
                        ViewPlan roomPlan = ViewPlan.Create(hostDoc, planType.Id, hostLevel.Id);
                        roomPlan.Name = GetUniqueViewName(hostDoc, "RDS_Plan_" + room.Number);
                        roomPlan.Scale = ScaleValue;

                        // Crop Plan (Match room bounds)
                        BoundingBoxXYZ bbox = room.get_BoundingBox(null);
                        roomPlan.get_Parameter(BuiltInParameter.VIEWER_CROP_REGION).Set(1);
                        
                        // 3. Sheet
                        if (TitleBlockId == null) TitleBlockId = new FilteredElementCollector(hostDoc).OfCategory(BuiltInCategory.OST_TitleBlocks).WhereElementIsElementType().FirstElementId();
                        ViewSheet sheet = ViewSheet.Create(hostDoc, TitleBlockId);
                        sheet.SheetNumber = GetUniqueSheetNumber(hostDoc, "RDS-" + room.Number);
                        sheet.Name = "RDS - " + room.Name.ToUpper();

                        // 4. Elevations
                        List<ViewSection> elevations = new List<ViewSection>();
                        if (CreateAllElevations)
                        {
                            ViewFamilyType elevType = new FilteredElementCollector(hostDoc)
                                .OfClass(typeof(ViewFamilyType))
                                .Cast<ViewFamilyType>()
                                .FirstOrDefault(x => x.ViewFamily == ViewFamily.Elevation && x.Name.Contains("Interior"))
                                ?? new FilteredElementCollector(hostDoc)
                                    .OfClass(typeof(ViewFamilyType))
                                    .Cast<ViewFamilyType>()
                                    .FirstOrDefault(x => x.ViewFamily == ViewFamily.Elevation);

                            XYZ roomCenter = (bbox.Max + bbox.Min) / 2;
                            ElevationMarker marker = ElevationMarker.CreateElevationMarker(hostDoc, elevType.Id, roomCenter, ScaleValue);
                            
                            for (int i = 0; i < 4; i++)
                            {
                                try {
                                    if (marker.IsAvailableIndex(i))
                                    {
                                        var v = marker.CreateElevation(hostDoc, roomPlan.Id, i);
                                        v.Name = GetUniqueViewName(hostDoc, $"RDS_Elev_{room.Number}_{i}");
                                        elevations.Add(v);
                                    }
                                } catch { }
                            }
                        }

                        // 5. 3D View
                        View3D view3d = null;
                        if (Create3DView)
                        {
                            ViewFamilyType v3dType = new FilteredElementCollector(hostDoc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>().FirstOrDefault(x => x.ViewFamily == ViewFamily.ThreeDimensional);
                            view3d = View3D.CreateIsometric(hostDoc, v3dType.Id);
                            view3d.Name = GetUniqueViewName(hostDoc, "RDS_3D_" + room.Number);
                            
                            BoundingBoxXYZ sectionBox = new BoundingBoxXYZ { Min = bbox.Min, Max = bbox.Max };
                            view3d.SetSectionBox(sectionBox);
                            view3d.IsSectionBoxActive = true;
                        }

                        // 6. Placement
                        Viewport.Create(hostDoc, sheet.Id, roomPlan.Id, new XYZ(0.8, 1.2, 0));
                        double yStep = 0.5;
                        for (int i = 0; i < elevations.Count; i++)
                        {
                            Viewport.Create(hostDoc, sheet.Id, elevations[i].Id, new XYZ(2.0, 0.4 + (i * yStep), 0));
                        }
                        if (view3d != null) Viewport.Create(hostDoc, sheet.Id, view3d.Id, new XYZ(2.0, 2.5, 0));

                        trans.Commit();
                        OnSuccess?.Invoke(sheet.SheetNumber);
                    }
                    catch (Exception ex)
                    {
                        trans.RollBack();
                    }
                }
            }
            TaskDialog.Show("B-Lab", "Batch creation complete!");
        }

        private string GetUniqueViewName(Document doc, string baseName)
        {
            string name = baseName;
            int i = 1;
            while (new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>().Any(v => v.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase)))
            {
                name = baseName + " (" + i++ + ")";
            }
            return name;
        }

        private string GetUniqueSheetNumber(Document doc, string baseNumber)
        {
            string num = baseNumber;
            int i = 1;
            while (new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>().Any(s => s.SheetNumber.Equals(num, StringComparison.InvariantCultureIgnoreCase)))
            {
                num = baseNumber + "-" + i++;
            }
            return num;
        }

        public string GetName() => "RoomSheetHandler";
    }
}
