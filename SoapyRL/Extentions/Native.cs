using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using static SoapyVNACommon.Extentions.Imports;

namespace SoapyRL.Extentions;

public class Imports
{

    public static double Scale(double value, double oldMin, double oldMax, double newMin, double newMax)
    {
        return newMin + (value - oldMin) * (newMax - newMin) / (oldMax - oldMin);
    }
}