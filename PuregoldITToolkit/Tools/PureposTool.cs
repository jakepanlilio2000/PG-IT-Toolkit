using PuregoldITToolkit.Core.Base;
using PuregoldITToolkit.Core.Interfaces;
using PuregoldITToolkit.Tools.PureposTool.ViewModels;

namespace PuregoldITToolkit.Tools.PureposTools
{
    public class PureposTool : ITool
    {
        public string ToolName => "Purepos Toolkits";
        public string Description => "Automate POS Permissions and EJ Saves.";
        public string IconResourceKey => "TerminalIcon"; 

        public ViewModelBase ToolViewModel { get; }

        public PureposTool(PureposViewModel viewModel)
        {
            ToolViewModel = viewModel;
        }
    }
}