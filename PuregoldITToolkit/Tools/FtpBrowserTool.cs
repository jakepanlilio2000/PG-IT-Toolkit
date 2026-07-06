using PuregoldITToolkit.Core.Base;
using PuregoldITToolkit.Core.Interfaces;
using PuregoldITToolkit.Tools.FtpBrowserTool.ViewModels;

namespace PuregoldITToolkit.Tools.FtpBrowserTool
{
    public class FtpBrowserTool : ITool
    {
        public string ToolName => "FTP Browser";
        public string Description => "Browse and download files directly from the Primary FTP servers.";

        public string IconResourceKey => "FolderIcon"; 
        public ViewModelBase ToolViewModel { get; }

        public FtpBrowserTool(FtpBrowserViewModel viewModel)
        {
            ToolViewModel = viewModel;
        }
    }
}