using PuregoldITToolkit.Core.Base;
using PuregoldITToolkit.Core.Interfaces;
using PuregoldITToolkit.Tools.ScPwdReportTool.ViewModels;

namespace PuregoldITToolkit.Tools.ScPwdReportTool
{
    public class ScPwdReportTool : ITool
    {
        public string ToolName => "SC/PWD Reports";
        public string Description => "SC and PWD Report Generator";

        public string IconResourceKey => "ReportIcon";
        public ViewModelBase ToolViewModel { get; }

        public ScPwdReportTool(ScPwdReportViewModel viewModel)
        {
            ToolViewModel = viewModel;
        }
    }
}