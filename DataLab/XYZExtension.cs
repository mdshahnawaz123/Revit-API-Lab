using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataLab
{
    public static class XYZExtension
    {
        public static double DegreeToRadian(this double Degree)
        {
            return Degree * (Math.PI / 180);
        }

        public static double RadianToDegree(this double Radian)
        {
            return Radian * (180 / Math.PI);
        }
    }
}
