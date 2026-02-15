using NLog;
using SoapySA.Model;
using SoapySA.View.measurements;
using SoapySA.View.tabs;
using SoapyVNACommon.Fonts;

namespace SoapySA.View;

public partial class MainWindowView
{
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();


    public List<TabViewModel> tabsService;
    private TabViewModel? _ActiveTab;
}