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
        public SlopeUnit Unit { get; set; } = SlopeUnit.Degrees;
        public List<SlopeRange> Ranges { get; set; } = new List<SlopeRange>();

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
                        
                        double val;
                        if (Unit == SlopeUnit.Percentage)
                            val = Math.Tan(angle) * 100;
                        else
                            val = angle * (180 / Math.PI);

                        if (val > maxSlope) maxSlope = val;
                    }
                }
            }
            return maxSlope;
        }

        private void ApplySlopeColor(Document doc, Element elem, double slope)
        {
            if (Ranges == null || Ranges.Count == 0) return;

            // Find the highest range that is less than or equal to the current slope
            var matchedRange = Ranges.OrderByDescending(r => r.Threshold)
                                     .FirstOrDefault(r => slope >= r.Threshold);

            if (matchedRange == null) return;

            Color color = matchedRange.Color;

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

    public class SlopeRange
    {
        public double Threshold { get; set; }
        public Color Color { get; set; }

        public SlopeRange(double threshold, byte r, byte g, byte b)
        {
            Threshold = threshold;
            Color = new Color(r, g, b);
        }
    }

    public enum SlopeUnit { Degrees, Percentage }
}
