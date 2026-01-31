using NLog;
using SoapyVNACommon;
using SoapyVNACommon.Extentions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SoapySA.View.measurements
{
    internal class SourceView(MainWindowView initiator, SdrDeviceCom com)
    {
        private readonly Logger _logger = LogManager.GetCurrentClassLogger();
        static readonly string[] availableSourceModes = new string[] { "Disabled","CW","Tracking"};
        int selectedSourceMode = 0;
        public void RenderSourceViewSettings()
        {
            if(com.TxAntenna is null)
            {
                Theme.Text("This Widget was defined with no Tx Source!");
                return;
            }


            Theme.Text("Tx Source");
            Theme.Text("Source Mode");
            if (Theme.GlowingCombo("TxSourceSelected",
               ref selectedSourceMode, availableSourceModes,
               Theme.InputTheme))
            {
            }
        }
    }
}
