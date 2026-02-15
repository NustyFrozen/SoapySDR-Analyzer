using SoapySA.Model;
using SoapySA.View;
using SoapySA.View.measurements;
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

        /// <summary>
        /// Initializes Depdendency Injection for all services and Elements
        /// </summary>
        /// <param name="WidgetName"></param>
        /// <param name="initiator"></param>
        /// <param name="device"></param>
        /// <returns></returns>
        public static List<TabViewModel> createMainFeatures(string WidgetName,MainWindowView initiator,SdrDeviceCom device)
        {
            //Main Services
            Configuration config = new Configuration(WidgetName, initiator);
            GraphPlotManager graphData = new GraphPlotManager(config);
            config.InitConfiguration();
            PerformFft fftManager = new PerformFft(initiator,config,device, graphData);

            //Special measurement Modes
            List<MeasurementFeature> measurementFeatures = new() {
             new NormalMeasurementView(config,graphData),
            new ChannelPowerView(config,graphData),
             new FilterBandwithView(config,graphData),
            new NoiseFigureMeasurementView(config,graphData),
            new SourceView(config,device)
                };

            //Other Features
            List<TabViewModel> features = new(){
            new DeviceView(device,config,fftManager),
            new AmplitudeView(fftManager,config),
            new VideoView(fftManager,config),
            new FrequencyView(device,config,fftManager),
            new MarkerView(config,graphData),
            new TraceView(graphData),
            new MeasurementsView(fftManager,graphData,config,device,measurementFeatures), //adding all the required Measurement Modes
            new CalibrationView(config,fftManager),
            };

            return features;
        }
    }
}
