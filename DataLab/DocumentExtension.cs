using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace DataLab
{
    public static class DocumentExtension
    {
        public static TElement GetElementByName<TElement>(this Document doc, string name)
            where TElement : Element
        {
            var ele = new FilteredElementCollector(doc)
                .OfClass(typeof(TElement))
                .FirstOrDefault(x => x.Name == name);

            if (ele == null)
            {
                throw new ArgumentNullException(nameof(name), $"Element with name '{name}' not found.");
            }
            return (TElement)ele;
        }

        public static DirectShape CreateDirectShape(this Document doc, List<GeometryObject> geometries, ElementId categoryId)
        {
            var ds = DirectShape.CreateElement(doc, categoryId);
            ds.SetShape(geometries);

            return ds;
        }

        public static DirectShape CreateDirectShape(this Document doc, GeometryObject geometryObject, ElementId categoryId)
        {
            var ds = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
            ds.SetShape(new List<GeometryObject>() { geometryObject });

            return ds;
        }

        //public static void DoAction(this Document doc, Action action, string name)
        //{
        //    using (var tx = new Transaction(doc, name))
        //    {
        //        tx.Start();
        //        action.Invoke();
        //        tx.Commit();
        //    }
        //}

        public static IList<Level> GetLevel(this Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .ToList();
        }

        public static IList<Category> GetCategory(this Document doc)
        {
            var cat = doc.Settings.Categories
                .Cast<Category>()
                .Where(x => x.CategoryType == CategoryType.Model && !x.IsTagCategory)
                .OrderBy(x => x.Name)
                .ToList();

            return cat;
        }

        public static IList<Element> GetElementByCategory(this Document doc, Category category)
        {
            return new FilteredElementCollector(doc)
                .OfCategoryId(category.Id)
                .WhereElementIsNotElementType()
                .ToElements();
        }

        public static IList<Element> GetElementTypeByCategory(this Document doc, Category category)
        {
            return new FilteredElementCollector(doc)
                .OfCategoryId(category.Id)
                .Cast<Element>()
                .ToList();

        }

        public static IList<FamilyInstance> GetFamilyInstances(this Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(FamilyInstance))
                .WhereElementIsNotElementType()
                .Cast<FamilyInstance>()
                .ToList();
        }

        public static IList<Wall> GetWalls(this Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(Wall))
                .WhereElementIsNotElementType()
                .Cast<Wall>()
                .ToList();
        }

        public static IList<FamilyInstance> GetFamilyByCategory(this Document doc, BuiltInCategory bic)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(FamilyInstance))
                .WhereElementIsNotElementType()
                .Cast<FamilyInstance>()
                .Where(x => x.Category != null && x.Category.Id.Value == (int)bic)
                .ToList();
        }

        public static IList<(FamilyInstance Instance, Transform Transform)>
            GetLinkedFamilyWithTransform(this Document doc, BuiltInCategory bic)
        {
            var result = new List<(FamilyInstance, Transform)>();

            var links = new FilteredElementCollector(doc)
                .OfClass(typeof(RevitLinkInstance))
                .WhereElementIsNotElementType()
                .Cast<RevitLinkInstance>()
                .ToList();

            TaskDialog.Show("Debug", $"Found {links.Count} links in host doc.");

            foreach (var link in links)
            {
                var linkDoc = link.GetLinkDocument();
                if (linkDoc == null) continue;

                var transform = link.GetTransform();

                var instances = new FilteredElementCollector(linkDoc)
                    .OfClass(typeof(FamilyInstance))
                    .OfCategory(bic)
                    .WhereElementIsNotElementType()
                    .Cast<FamilyInstance>();

                foreach (var inst in instances)
                {
                    result.Add((inst, transform));
                }
            }

            return result;
        }

        public static IList<SpatialElement> GetRooms(this Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(SpatialElement))
                .WhereElementIsNotElementType()
                .Cast<SpatialElement>()
                .Where(x => x.Category != null && x.Category.Id.Value == (int)BuiltInCategory.OST_Rooms)
                .ToList();
        }

        public static IList<Element> GetPhasing(this Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(Phase))
                .WhereElementIsNotElementType()
                .ToList();
        }
    }
        
}
