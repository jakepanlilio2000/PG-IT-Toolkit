using PuregoldITToolkit.Core.Base;
using PuregoldITToolkit.Core.Interfaces;
using PuregoldITToolkit.Tools.SettingsTool.ViewModels;

namespace PuregoldITToolkit.Tools.SettingsTool
{
    public class SettingsTool : ITool
    {
        public string ToolName => "Global Settings";
        public string Description => "Configure global email signatures and application preferences";
        public string IconResourceKey => "GearIcon";

        public ViewModelBase ToolViewModel { get; }

        public SettingsTool(SettingsViewModel viewModel)
        {
            ToolViewModel = viewModel;
        }
    }
}