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
        public ElementId SelectedRoomId { get; set; }
        public string SheetNumber { get; set; }
        public string SheetName { get; set; }
        public int ScaleValue { get; set; } = 50;
        public Action<string> OnSuccess { get; set; }

        public ElementId TitleBlockId { get; set; }
        public bool CreateAllElevations { get; set; } = true;
        public bool Create3DView { get; set; } = true;

        public void Execute(UIApplication app)
        {
            Document doc = app.ActiveUIDocument.Document;
            if (SelectedRoomId == null) return;

            Room room = doc.GetElement(SelectedRoomId) as Room;
            if (room == null) return;

            using (Transaction trans = new Transaction(doc, "Create Room Data Sheet"))
            {
                trans.Start();
                try
                {
                    // 1. Plan View
                    ViewFamilyType planType = new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>().FirstOrDefault(x => x.ViewFamily == ViewFamily.FloorPlan);
                    ViewPlan roomPlan = ViewPlan.Create(doc, planType.Id, room.Level.Id);
                    roomPlan.Name = GetUniqueViewName(doc, "RDS_Plan_" + room.Number);
                    roomPlan.Scale = ScaleValue;

                    // Crop Plan
                    BoundingBoxXYZ bbox = room.get_BoundingBox(null);
                    double offset = 1.0;
                    roomPlan.get_Parameter(BuiltInParameter.VIEWER_CROP_REGION).Set(1);
                    
                    // 2. Sheet
                    if (TitleBlockId == null) TitleBlockId = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_TitleBlocks).WhereElementIsElementType().FirstElementId();
                    ViewSheet sheet = ViewSheet.Create(doc, TitleBlockId);
                    sheet.SheetNumber = GetUniqueSheetNumber(doc, SheetNumber);
                    sheet.Name = SheetName;

                    // 3. Elevations
                    List<ViewSection> elevations = new List<ViewSection>();
                    if (CreateAllElevations)
                    {
                        // Find an Interior Elevation type if possible, otherwise use any elevation type
                        ViewFamilyType elevType = new FilteredElementCollector(doc)
                            .OfClass(typeof(ViewFamilyType))
                            .Cast<ViewFamilyType>()
                            .FirstOrDefault(x => x.ViewFamily == ViewFamily.Elevation && x.Name.Contains("Interior"))
                            ?? new FilteredElementCollector(doc)
                                .OfClass(typeof(ViewFamilyType))
                                .Cast<ViewFamilyType>()
                                .FirstOrDefault(x => x.ViewFamily == ViewFamily.Elevation);

                        XYZ roomCenter = (bbox.Max + bbox.Min) / 2;
                        ElevationMarker marker = ElevationMarker.CreateElevationMarker(doc, elevType.Id, roomCenter, ScaleValue);
                        
                        for (int i = 0; i < 4; i++)
                        {
                            try 
                            {
                                if (marker.IsAvailableIndex(i))
                                {
                                    var v = marker.CreateElevation(doc, roomPlan.Id, i);
                                    v.Name = GetUniqueViewName(doc, $"RDS_Elev_{room.Number}_{i}");
                                    elevations.Add(v);
                                }
                            }
                            catch { /* Skip occupied or invalid indices */ }
                        }
                    }

                    // 4. 3D View
                    View3D view3d = null;
                    if (Create3DView)
                    {
                        ViewFamilyType v3dType = new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>().FirstOrDefault(x => x.ViewFamily == ViewFamily.ThreeDimensional);
                        view3d = View3D.CreateIsometric(doc, v3dType.Id);
                        view3d.Name = GetUniqueViewName(doc, "RDS_3D_" + room.Number);
                        
                        BoundingBoxXYZ sectionBox = new BoundingBoxXYZ();
                        sectionBox.Min = bbox.Min;
                        sectionBox.Max = bbox.Max;
                        view3d.SetSectionBox(sectionBox);
                        view3d.IsSectionBoxActive = true;
                    }

                    // 5. Placement (Grid Layout)
                    // Plan at center-left
                    Viewport.Create(doc, sheet.Id, roomPlan.Id, new XYZ(0.8, 1.2, 0));

                    // Elevations in a row or column
                    double yStep = 0.5;
                    for (int i = 0; i < elevations.Count; i++)
                    {
                        Viewport.Create(doc, sheet.Id, elevations[i].Id, new XYZ(2.0, 0.4 + (i * yStep), 0));
                    }

                    // 3D View at top-right
                    if (view3d != null)
                    {
                        Viewport.Create(doc, sheet.Id, view3d.Id, new XYZ(2.0, 2.5, 0));
                    }

                    trans.Commit();
                    OnSuccess?.Invoke(sheet.SheetNumber);
                }
                catch (Exception ex)
                {
                    trans.RollBack();
                    TaskDialog.Show("Error", ex.Message);
                }
            }
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
