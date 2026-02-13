using SoapySA.Model;
using SoapySA.View;
using SoapySA.View.tabs;
using SoapyVNACommon.Extentions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SoapySA.Extentions
{
    public static class FeaturesServiceFactory
    {
        public static List<TabViewModel> createMainFeatures(MainWindowView initiator,SdrDeviceCom device)
        {
            List<TabViewModel> features = new(){
            new DeviceView(initiator,device),
            new AmplitudeView(initiator),
            new VideoView(initiator),
            new FrequencyView(initiator),
            new MarkerView(initiator),
            new TraceView(initiator),
            new MeasurementsView(initiator),
            new CalibrationView(initiator),
            };

            return features;
        }
    }
}
