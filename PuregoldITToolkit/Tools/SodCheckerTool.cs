using PuregoldITToolkit.Core.Base;
using PuregoldITToolkit.Core.Interfaces;
using PuregoldITToolkit.Tools.SodChecker.ViewModels;

namespace PuregoldITToolkit.Tools.SodChecker
{
    public class SodCheckerTool : ITool
    {
        public string ToolName => "SOD/EOD Checker";
        public string Description => "Monitor FTP server file status for CRM, BIR, EJ, and logs.";
        public string IconResourceKey => "MonitorIcon";

        public ViewModelBase ToolViewModel { get; }

        public SodCheckerTool(SodCheckerViewModel viewModel)
        {
            ToolViewModel = viewModel;
        }
    }
}