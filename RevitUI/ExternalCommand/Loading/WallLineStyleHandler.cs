using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RevitUI.ExternalCommand.Loading
{
    public class WallLineStyleHandler : IExternalEventHandler
    {
        public bool IsHostModelOption { get; set; } = true;
        public List<Element> SelectedWalls { get; set; } = new List<Element>();

        public void Execute(UIApplication app)
        {
            var doc = app.ActiveUIDocument.Document;
            var sb = new StringBuilder();
            int createdCount = 0;
            int updatedCount = 0;
            int skippedCount = 0;

            try
            {
                var uniqueWallTypes = SelectedWalls.OfType<Wall>().Select(w => w.WallType.Name).Distinct().ToList();
                var selectedWallTypeStyles = uniqueWallTypes.Select(wt => "WallStyle_" + wt).ToHashSet();
                View activeView = doc.ActiveView;

                // Pre-collect existing lines to check for cleanup
                var existingCurvesInView = new FilteredElementCollector(doc, activeView.Id)
                    .OfClass(typeof(CurveElement))
                    .Where(x => x is DetailCurve)
                    .Cast<DetailCurve>()
                    .ToList();

                var linesToCleanup = existingCurvesInView.Where(dc => 
                {
                    if (!dc.IsValidObject) return false;
                    var p = dc.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS) ?? dc.LookupParameter("Comments");
                    if (p == null || !p.AsString().StartsWith("WallLoading:")) return false;
                    
                    // Check if style name matches any selected wall type style
                    return selectedWallTypeStyles.Contains(dc.LineStyle.Name);
                }).ToList();

                bool shouldDelete = false;
                if (linesToCleanup.Any())
                {
                    TaskDialog td = new TaskDialog("Existing Wall Lines Found");
                    td.MainInstruction = $"Found {linesToCleanup.Count} existing lines matching the selected wall types.";
                    td.MainContent = "Would you like to delete these existing lines and create new ones? \n\n" +
                                     "Yes: Delete all existing lines for these types and create fresh ones.\n" +
                                     "No: Keep existing lines and only update them (standard behavior).\n" +
                                     "Cancel: Abort the operation.";
                    td.CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No | TaskDialogCommonButtons.Cancel;
                    td.DefaultButton = TaskDialogResult.Yes;

                    var result = td.Show();
                    if (result == TaskDialogResult.Cancel) return;
                    shouldDelete = (result == TaskDialogResult.Yes);
                }

                using (Transaction tx = new Transaction(doc, "Create/Update Detail Lines for Walls"))
                {
                    tx.Start();

                    if (shouldDelete)
                    {
                        foreach (var line in linesToCleanup)
                        {
                            if (line.IsValidObject) doc.Delete(line.Id);
                        }
                    }

                    Category linesCat = doc.Settings.Categories.get_Item(BuiltInCategory.OST_Lines);
                    Dictionary<string, GraphicsStyle> lineStyles = new Dictionary<string, GraphicsStyle>();

                    int colorIndex = 0;
                    List<Color> colors = new List<Color> {
                        new Color(255, 0, 0), new Color(0, 255, 0), new Color(0, 0, 255),
                        new Color(255, 128, 0), new Color(128, 0, 255), new Color(0, 128, 255)
                    };

                    foreach (var wallType in uniqueWallTypes)
                    {
                        string styleName = "WallStyle_" + wallType;
                        Category subCat = linesCat.SubCategories.Contains(styleName) 
                            ? linesCat.SubCategories.get_Item(styleName) 
                            : doc.Settings.Categories.NewSubcategory(linesCat, styleName);

                        subCat.LineColor = colors[colorIndex % colors.Count];
                        subCat.SetLineWeight(5, GraphicsStyleType.Projection);
                        lineStyles[wallType] = subCat.GetGraphicsStyle(GraphicsStyleType.Projection);
                        colorIndex++;
                    }

                    foreach (var wall in SelectedWalls.OfType<Wall>())
                    {
                        string wallIdentifier = "WallLoading:" + wall.UniqueId;
                        
                        // Robust lookup for existing curve
                        var existingCurve = existingCurvesInView
                            .FirstOrDefault(dc => 
                            {
                                if (!dc.IsValidObject) return false;
                                var p = dc.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS) ?? dc.LookupParameter("Comments");
                                return p != null && p.AsString() == wallIdentifier;
                            });

                        LocationCurve locCurve = wall.Location as LocationCurve;
                        if (locCurve == null || locCurve.Curve == null) continue;

                        // Apply transform if wall is from a link
                        Transform transform = Transform.Identity;
                        if (!wall.Document.Equals(doc))
                        {
                            var link = new FilteredElementCollector(doc)
                                .OfClass(typeof(RevitLinkInstance))
                                .Cast<RevitLinkInstance>()
                                .FirstOrDefault(l => l.GetLinkDocument() != null && l.GetLinkDocument().Equals(wall.Document));
                            if (link != null) transform = link.GetTransform();
                        }

                        Curve currentCurve = locCurve.Curve.CreateTransformed(transform);
                        
                        // Get expected style for this wall type
                        GraphicsStyle expectedStyle = null;
                        lineStyles.TryGetValue(wall.WallType.Name, out expectedStyle);

                        if (existingCurve != null)
                        {
                            bool geomChanged = !AreCurvesEqual(currentCurve, existingCurve.GeometryCurve);
                            bool styleChanged = expectedStyle != null && existingCurve.LineStyle.Id != expectedStyle.Id;

                            if (!geomChanged && !styleChanged)
                            {
                                skippedCount++;
                                sb.AppendLine($"Skipped: Wall '{wall.Name}' (No change)");
                                continue;
                            }
                            else
                            {
                                try 
                                { 
                                    if (geomChanged) existingCurve.GeometryCurve = currentCurve;
                                    if (styleChanged) existingCurve.LineStyle = expectedStyle;
                                    
                                    updatedCount++;
                                    string msg = geomChanged && styleChanged ? "Geometry & Style updated" : (geomChanged ? "Geometry updated" : "Style updated");
                                    sb.AppendLine($"Updated: Wall '{wall.Name}' ({msg})");
                                    continue; 
                                }
                                catch (Exception ex)
                                { 
                                    sb.AppendLine($"Error updating wall '{wall.Name}': {ex.Message}");
                                    continue;
                                }
                            }
                        }
                        else
                        {
                            createdCount++;
                            sb.AppendLine($"Created: Wall '{wall.Name}'");
                        }

                        try
                        {
                            DetailCurve detailCurve = doc.Create.NewDetailCurve(activeView, currentCurve);
                            var p = detailCurve.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS) ?? detailCurve.LookupParameter("Comments");
                            p?.Set(wallIdentifier);

                            if (expectedStyle != null)
                            {
                                detailCurve.LineStyle = expectedStyle;
                            }
                        }
                        catch { }
                    }

                    tx.Commit();
                }

                if (sb.Length > 0)
                {
                    TaskDialog.Show("Wall Loading Summary", 
                        $"Results:\nCreated: {createdCount}\nUpdated: {updatedCount}\nSkipped: {skippedCount}\n\nDetails:\n" + sb.ToString());
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", ex.Message);
            }     
        }

        private bool AreCurvesEqual(Curve c1, Curve c2)
        {
            if (Math.Abs(c1.Length - c2.Length) > 0.001) return false;
            return c1.GetEndPoint(0).DistanceTo(c2.GetEndPoint(0)) < 0.001 && 
                   c1.GetEndPoint(1).DistanceTo(c2.GetEndPoint(1)) < 0.001;
        }

        public string GetName()
        {
            return "WallLineStyleHandler";
        }
    }
}
