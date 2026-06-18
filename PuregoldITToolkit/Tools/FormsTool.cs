using PuregoldITToolkit.Core.Base;
using PuregoldITToolkit.Core.Interfaces;
using PuregoldITToolkit.Tools.FormsTool.ViewModels;

namespace PuregoldITToolkit.Tools.FormsTool
{
    public class FormsTool : ITool
    {
        public string ToolName => "Puregold Forms";
        public string Description => "Generate standard INF, OB, SSRF, and TSRF forms.";
        public string IconResourceKey => "FormsIcon";

        public ViewModelBase ToolViewModel { get; }

        public FormsTool(FormsMainViewModel viewModel)
        {
            ToolViewModel = viewModel;
        }
    }
}