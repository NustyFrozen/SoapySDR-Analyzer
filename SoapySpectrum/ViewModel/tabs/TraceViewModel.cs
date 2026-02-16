using NLog;
using SoapySA.Model;

namespace SoapySA.View.tabs;

public partial class TraceView 
{
    public override string tabName => "\uf3c5 Trace";
    public static string[] SComboTraces = new[] { "Trace 1", "Trace 2", "Trace 3", "Trace 4", "Trace 5", "Trace 6" };
    
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();
}