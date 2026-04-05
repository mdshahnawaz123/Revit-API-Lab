using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataLab
{
    public static class Visualizer
    {
        public static void PointVisualise(this XYZ point,Document doc)
        {
            doc.CreateDirectShape(Point.Create(point),new ElementId(BuiltInCategory.OST_GenericModel));
        }

        public static void LineVisualise(this Line line,Document doc)
        {
            doc.CreateDirectShape(line, new ElementId(BuiltInCategory.OST_GenericModel));
        }
    }
}
