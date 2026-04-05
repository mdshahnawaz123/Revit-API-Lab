using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataLab
{
    public static class Structure
    {
        public static IList<RebarBarType> GetBarType(Document doc)
        {
            return new FilteredElementCollector(doc)
                       .OfClass(typeof(RebarBarType))
                       .Cast<RebarBarType>()
                       .OrderBy(t => t.Name)
                       .ToList();
        }              
    }
}
