using PuregoldITToolkit.Core.Base;
using PuregoldITToolkit.Core.Interfaces;
using PuregoldITToolkit.Tools.PureposTool.ViewModels;

namespace PuregoldITToolkit.Tools.PureposTools
{
    public class PureposTool : ITool
    {
        public string ToolName => "Puregold Toolkits";
        public string Description => "Comprehensive POS Diagnostics, Network Utilities, System Maintenance & Automation.";
        public string IconResourceKey => "TerminalIcon";

        public ViewModelBase ToolViewModel { get; }

        public PureposTool(PureposViewModel viewModel)
        {
            ToolViewModel = viewModel;
        }
    }
}
