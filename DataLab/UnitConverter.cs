using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataLab
{
    public static class UnitConverter
    {
        public static double MM(this double interunit)
        {
            return UnitUtils.ConvertFromInternalUnits(interunit, UnitTypeId.Millimeters);
        }

        public static double SqMeters(this double interunit)
        {
            return UnitUtils.ConvertFromInternalUnits(interunit, UnitTypeId.SquareMeters);
        }
    }
}
