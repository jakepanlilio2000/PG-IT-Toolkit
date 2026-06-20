using PuregoldITToolkit.Core.Base;
using PuregoldITToolkit.Core.Interfaces;
using PuregoldITToolkit.Tools.ServiceDeskTool.ViewModels;

namespace PuregoldITToolkit.Tools.ServiceDeskTool
{
    public class ServiceDeskTool : ITool
    {
        public string ToolName => "Service Desk Utilities";
        public string Description => "OT Monitoring & Broadband Outage Drafter";
        public string IconResourceKey => "FormsIcon"; 

        public ViewModelBase ToolViewModel { get; }

        public ServiceDeskTool(ServiceDeskViewModel viewModel)
        {
            ToolViewModel = viewModel;
        }
    }
}