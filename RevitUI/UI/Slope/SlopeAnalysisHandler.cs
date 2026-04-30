using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RevitUI.UI.Slope
{
    public class SlopeAnalysisHandler : IExternalEventHandler
    {
        public bool AnalyzeFloors { get; set; }
        public bool AnalyzeRoofs { get; set; }
        public bool AnalyzeTopo { get; set; }
        public bool ApplyColor { get; set; }

        public void Execute(UIApplication app)
        {
            Document doc = app.ActiveUIDocument.Document;
            
            using (Transaction trans = new Transaction(doc, "Slope Analysis"))
            {
                trans.Start();
                try
                {
                    var elements = GetTargetElements(doc);
                    int count = 0;

                    foreach (var elem in elements)
                    {
                        double maxSlope = GetMaxSlope(elem);
                        
                        if (ApplyColor)
                        {
                            ApplySlopeColor(doc, elem, maxSlope);
                        }
                        count++;
                    }

                    TaskDialog.Show("B-Lab Analysis", $"Processed {count} elements.\nCheck properties for slope data.");
                    trans.Commit();
                }
                catch (Exception ex)
                {
                    trans.RollBack();
                    TaskDialog.Show("Analysis Error", ex.Message);
                }
            }
        }

        private List<Element> GetTargetElements(Document doc)
        {
            var collector = new FilteredElementCollector(doc, doc.ActiveView.Id);
            var categories = new List<BuiltInCategory>();
            
            if (AnalyzeFloors) categories.Add(BuiltInCategory.OST_Floors);
            if (AnalyzeRoofs) categories.Add(BuiltInCategory.OST_Roofs);
            if (AnalyzeTopo) categories.Add(BuiltInCategory.OST_Topography);

            if (categories.Count == 0) return new List<Element>();

            ElementMulticategoryFilter filter = new ElementMulticategoryFilter(categories);
            return collector.WherePasses(filter).WhereElementIsNotElementType().ToList();
        }

        private double GetMaxSlope(Element elem)
        {
            Options opt = new Options { DetailLevel = ViewDetailLevel.Fine };
            GeometryElement geo = elem.get_Geometry(opt);
            double maxSlope = 0;

            foreach (var obj in geo)
            {
                if (obj is Solid solid)
                {
                    foreach (Face face in solid.Faces)
                    {
                        XYZ normal = face.ComputeNormal(new UV(0.5, 0.5));
                        double angle = Math.Acos(Math.Abs(normal.Z)); // Angle in Radians
                        double slopeDegrees = angle * (180 / Math.PI);
                        if (slopeDegrees > maxSlope) maxSlope = slopeDegrees;
                    }
                }
            }
            return maxSlope;
        }

        private void ApplySlopeColor(Document doc, Element elem, double slope)
        {
            // Simple logic: If slope > 10 degrees, make it red/orange
            Color color;
            if (slope > 30) color = new Color(255, 0, 0); // Very Steep
            else if (slope > 10) color = new Color(255, 165, 0); // Warning
            else color = new Color(144, 238, 144); // Flat/Safe

            OverrideGraphicSettings ogs = new OverrideGraphicSettings();
            ogs.SetSurfaceForegroundPatternColor(color);
            
            // Find a solid fill pattern
            var fillPattern = new FilteredElementCollector(doc)
                .OfClass(typeof(FillPatternElement))
                .Cast<FillPatternElement>()
                .FirstOrDefault(fp => fp.GetFillPattern().IsSolidFill);

            if (fillPattern != null)
            {
                ogs.SetSurfaceForegroundPatternId(fillPattern.Id);
            }

            doc.ActiveView.SetElementOverrides(elem.Id, ogs);
        }

        public string GetName() => "Slope Analysis Handler";
    }
}
