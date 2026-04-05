using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataLab
{
    public static class Mechanical
    {
        public static IList<Pipe> GetPipes(this Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(Pipe))
                .WhereElementIsNotElementType()
                .Cast<Pipe>()
                .ToList();
        }

        public static IList<Duct> GetDucts(this Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(Duct))
                .WhereElementIsNotElementType()
                .Cast<Duct>()
                .ToList();
        }

        public static IList<CableTray> GetCableTrays(this Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(CableTray))
                .WhereElementIsNotElementType()
                .Cast<CableTray>()
                .ToList();
        }

        public static IList<Pipe> GetLinkedPipes(this Document doc)
        {
            List<Pipe> pipes = new List<Pipe>();

            var linkInstances = new FilteredElementCollector(doc)
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>();

            foreach (var link in linkInstances)
            {
                Document linkDoc = link.GetLinkDocument();

                if (linkDoc == null) continue;

                var linkPipes = new FilteredElementCollector(linkDoc)
                    .OfClass(typeof(Pipe))
                    .WhereElementIsNotElementType()
                    .Cast<Pipe>();

                pipes.AddRange(linkPipes);
            }

            return pipes.Distinct().ToList();
        }

    }
}
