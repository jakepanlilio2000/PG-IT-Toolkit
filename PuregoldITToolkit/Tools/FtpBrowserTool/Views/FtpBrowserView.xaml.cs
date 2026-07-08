using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PuregoldITToolkit.Tools.FtpBrowserTool.Models;
using PuregoldITToolkit.Tools.FtpBrowserTool.ViewModels;

namespace PuregoldITToolkit.Tools.FtpBrowserTool.Views
{
    public partial class FtpBrowserView : UserControl
    {
        public FtpBrowserView()
        {
            InitializeComponent();
        }

        private void FilesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is FtpBrowserViewModel vm)
            {
                vm.SelectedFiles = ((DataGrid)sender).SelectedItems.Cast<FtpFileModel>().ToList();
            }
        }

        private async void FilesGrid_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && DataContext is FtpBrowserViewModel vm && vm.SelectedFiles.Any())
            {
                await vm.PrepareDragAsync();
                if (vm.DragDataObject != null)
                {
                    DragDrop.DoDragDrop((DependencyObject)sender, vm.DragDataObject, DragDropEffects.Copy);
                    vm.DragDataObject = null;
                }
            }
        }

        private void FilesGrid_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (DataContext is FtpBrowserViewModel vm)
                {
                    _ = vm.ExecuteUploadFromDrop(files);
                }
            }
        }
    }
}