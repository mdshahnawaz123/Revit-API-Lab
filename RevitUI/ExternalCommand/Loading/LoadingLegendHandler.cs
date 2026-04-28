using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RevitUI.ExternalCommand.Loading
{
    public class LoadingLegendHandler : IExternalEventHandler
    {
        public void Execute(UIApplication app)
        {
            var doc = app.ActiveUIDocument.Document;
            var uidoc = app.ActiveUIDocument;
            var view = doc.ActiveView;

            try
            {
                using (Transaction tx = new Transaction(doc, "Create Loading Legend"))
                {
                    tx.Start();

                    // 1. Collect Used Types/Styles
                    var roomFillTypes = new FilteredElementCollector(doc)
                        .OfClass(typeof(FilledRegionType))
                        .Cast<FilledRegionType>()
                        .Where(rt => rt.Name.StartsWith("RoomFill_"))
                        .OrderBy(rt => rt.Name)
                        .ToList();

                    Category linesCat = doc.Settings.Categories.get_Item(BuiltInCategory.OST_Lines);
                    var wallLineStyles = linesCat.SubCategories
                        .Cast<Category>()
                        .Where(c => c.Name.StartsWith("WallStyle_"))
                        .OrderBy(c => c.Name)
                        .ToList();

                    if (!roomFillTypes.Any() && !wallLineStyles.Any())
                    {
                        TaskDialog.Show("Legend", "No Loading styles found to create a legend.");
                        tx.RollBack();
                        return;
                    }

                    // 2. Define Insertion Point (use selection or default)
                    XYZ startPoint = XYZ.Zero;
                    try { startPoint = uidoc.Selection.PickPoint("Pick a point for the legend"); }
                    catch { startPoint = view.Origin; }

                    double yOffset = 0;
                    double rowSpacing = 2.0; // feet
                    double columnSpacing = 5.0; // feet

                    // 3. Create Room Legend
                    if (roomFillTypes.Any())
                    {
                        CreateText(doc, view, startPoint + new XYZ(0, 0.5, 0), "ROOM LOADING LEGEND", true);
                        foreach (var rt in roomFillTypes)
                        {
                            yOffset -= rowSpacing;
                            XYZ point = startPoint + new XYZ(0, yOffset, 0);
                            
                            // Create a small square filled region
                            double size = 0.8;
                            List<Curve> curves = new List<Curve> {
                                Line.CreateBound(point, point + new XYZ(size, 0, 0)),
                                Line.CreateBound(point + new XYZ(size, 0, 0), point + new XYZ(size, size, 0)),
                                Line.CreateBound(point + new XYZ(size, size, 0), point + new XYZ(0, size, 0)),
                                Line.CreateBound(point + new XYZ(0, size, 0), point)
                            };
                            CurveLoop loop = CurveLoop.Create(curves);
                            FilledRegion.Create(doc, rt.Id, view.Id, new List<CurveLoop> { loop });

                            // Create text label
                            CreateText(doc, view, point + new XYZ(size + 0.5, size/2.0, 0), rt.Name.Replace("RoomFill_", ""));
                        }
                        yOffset -= rowSpacing * 2;
                    }

                    // 4. Create Wall Legend
                    if (wallLineStyles.Any())
                    {
                        CreateText(doc, view, startPoint + new XYZ(0, yOffset + 0.5, 0), "WALL LOADING LEGEND", true);
                        foreach (var subCat in wallLineStyles)
                        {
                            yOffset -= rowSpacing;
                            XYZ point = startPoint + new XYZ(0, yOffset, 0);

                            // Create a detail line
                            double length = 1.5;
                            Line line = Line.CreateBound(point + new XYZ(0, 0.4, 0), point + new XYZ(length, 0.4, 0));
                            DetailCurve dc = doc.Create.NewDetailCurve(view, line);
                            dc.LineStyle = subCat.GetGraphicsStyle(GraphicsStyleType.Projection);

                            // Create text label
                            CreateText(doc, view, point + new XYZ(length + 0.5, 0.4, 0), subCat.Name.Replace("WallStyle_", ""));
                        }
                    }

                    tx.Commit();
                }
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("User canceled")) return;
                TaskDialog.Show("Error", ex.Message);
            }
        }

        private void CreateText(Document doc, View view, XYZ point, string text, bool bold = false)
        {
            ElementId typeId = doc.GetDefaultElementTypeId(ElementTypeGroup.TextNoteType);
            TextNote.Create(doc, view.Id, point, text, typeId);
        }

        public string GetName()
        {
            return "LoadingLegendHandler";
        }
    }
}
