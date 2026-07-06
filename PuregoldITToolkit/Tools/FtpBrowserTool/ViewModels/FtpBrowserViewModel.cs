using CommunityToolkit.Mvvm.Input;
using PuregoldITToolkit.Core.Base;
using PuregoldITToolkit.Tools.FtpBrowserTool.Interfaces;
using PuregoldITToolkit.Tools.FtpBrowserTool.Models;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;

namespace PuregoldITToolkit.Tools.FtpBrowserTool.ViewModels
{
    public class FtpBrowserViewModel : ViewModelBase
    {
        private readonly IFtpBrowserService _service;

        public ObservableCollection<FtpStoreModel> StoresList { get; } = new ObservableCollection<FtpStoreModel>();
        public ObservableCollection<FtpFileModel> FilesList { get; } = new ObservableCollection<FtpFileModel>();

        private FtpStoreModel _selectedStore;
        public FtpStoreModel SelectedStore
        {
            get => _selectedStore;
            set
            {
                if (SetProperty(ref _selectedStore, value))
                {
                    _ = UpdatePathAndLoadAsync();
                }
            }
        }

        private bool _isTabOthers = true;
        public bool IsTabOthers { get => _isTabOthers; set { if (SetProperty(ref _isTabOthers, value) && value) _ = UpdatePathAndLoadAsync(); } }

        private bool _isTabFromHo;
        public bool IsTabFromHo { get => _isTabFromHo; set { if (SetProperty(ref _isTabFromHo, value) && value) _ = UpdatePathAndLoadAsync(); } }

        private bool _isTabRoot;
        public bool IsTabRoot { get => _isTabRoot; set { if (SetProperty(ref _isTabRoot, value) && value) _ = UpdatePathAndLoadAsync(); } }

        private string _currentPath = "Select a store or tab...";
        public string CurrentPath { get => _currentPath; set => SetProperty(ref _currentPath, value); }

        private FtpFileModel _selectedFile;
        public FtpFileModel SelectedFile { get => _selectedFile; set => SetProperty(ref _selectedFile, value); }

        private string _statusMessage = "Ready.";
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

        public ICommand LoadStoresCommand { get; }
        public ICommand DownloadCommand { get; }
        public ICommand RefreshCommand { get; }

        public FtpBrowserViewModel(IFtpBrowserService service)
        {
            _service = service;
            LoadStoresCommand = new AsyncRelayCommand(LoadStoresAsync);
            DownloadCommand = new AsyncRelayCommand(DownloadFileAsync);
            RefreshCommand = new AsyncRelayCommand(UpdatePathAndLoadAsync);

            _ = LoadStoresAsync();
        }

        private async Task LoadStoresAsync()
        {
            IsBusy = true;
            StatusMessage = "Fetching store list from FTP...";

            var stores = await _service.GetStoresAsync();
            StoresList.Clear();
            foreach (var store in stores) StoresList.Add(store);

            StatusMessage = $"Loaded {StoresList.Count} stores.";
            IsBusy = false;
        }

        private async Task UpdatePathAndLoadAsync()
        {
            if (IsTabRoot)
            {
                CurrentPath = "/";
            }
            else if (SelectedStore != null)
            {
                if (IsTabOthers)
                    CurrentPath = $"/toho/{SelectedStore.OriginalFolderName}/Others/";
                else if (IsTabFromHo)
                    CurrentPath = $"/fromho/{SelectedStore.OriginalFolderName}/";
            }
            else
            {
                FilesList.Clear();
                CurrentPath = "Please select a store.";
                return;
            }

            IsBusy = true;
            StatusMessage = $"Loading {CurrentPath}...";

            var files = await _service.GetFilesAsync(CurrentPath);
            FilesList.Clear();
            foreach (var file in files) FilesList.Add(file);

            StatusMessage = $"Ready. {FilesList.Count} items found.";
            IsBusy = false;
        }

        private async Task DownloadFileAsync()
        {
            if (SelectedFile == null || SelectedFile.IsDirectory)
            {
                StatusMessage = "Please select a valid file to download.";
                return;
            }

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                FileName = SelectedFile.FileName,
                Title = "Save FTP File"
            };

            if (dlg.ShowDialog() == true)
            {
                IsBusy = true;
                StatusMessage = $"Downloading {SelectedFile.FileName}...";

                bool success = await _service.DownloadFileAsync(SelectedFile.FullPath, dlg.FileName);

                if (success)
                {
                    StatusMessage = $"Successfully downloaded {SelectedFile.FileName}!";
                    try { Process.Start("explorer.exe", $"/select,\"{dlg.FileName}\""); } catch { }
                }
                else
                {
                    StatusMessage = $"Failed to download {SelectedFile.FileName}.";
                }

                IsBusy = false;
            }
        }
    }
}