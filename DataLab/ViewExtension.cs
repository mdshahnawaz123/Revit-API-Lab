using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace DataLab
{
    public static class ViewExtension
    {
        public static Transform GetViewTransform(this View view)
        {
            var transform = Transform.Identity;
            transform.BasisX = view.RightDirection;
            transform.BasisY = view.UpDirection;
            transform.BasisZ = view.ViewDirection;
            return transform;
        }

        public static void Visualize(this Transform transform, Document doc, int scale)
        {
            var origin = transform.Origin;

            var Xline = Line.CreateBound(origin, origin + transform.BasisX * scale);
            var Yline = Line.CreateBound(origin, origin + transform.BasisY * scale);
            var Zline = Line.CreateBound(origin, origin + transform.BasisZ * scale);

            var view = doc.ActiveView;
            // X axis (Red)
            var xShape = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
            xShape.SetShape(new List<GeometryObject>() { Xline});

            var xOverride = new OverrideGraphicSettings();
            xOverride.SetProjectionLineColor(new Color(255, 0, 0));
            view.SetElementOverrides(xShape.Id, xOverride);

            // Y axis (Green)
            var yShape = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
            yShape.SetShape(new List<GeometryObject>() { Yline });

            var yOverride = new OverrideGraphicSettings();
            yOverride.SetProjectionLineColor(new Color(0, 255, 0));
            view.SetElementOverrides(yShape.Id, yOverride);

            // Z axis (Blue)
            var zShape = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
            zShape.SetShape(new List<GeometryObject>() { Zline });

            var zOverride = new OverrideGraphicSettings();
            zOverride.SetProjectionLineColor(new Color(0, 0, 255));
            view.SetElementOverrides(zShape.Id, zOverride);
        }

    
    }
}
