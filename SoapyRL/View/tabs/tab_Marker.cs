using ImGuiNET;
using SoapyRL.Extentions;
using System.Diagnostics;

namespace SoapyRL.UI
{
    public static class tab_Marker
    {
        private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();
        public static marker s_Marker;

        public struct marker
        {
            public marker()
            {
            }

            public int id, reference = 1;
            public string txtStatus;
            public bool isActive;
            public double position, value;
            public double valueRef;
        }
    }
}