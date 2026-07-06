using CommunityToolkit.Mvvm.Input;
using PuregoldITToolkit.Core.Base;
using PuregoldITToolkit.Tools.FtpBrowserTool.Interfaces;
using PuregoldITToolkit.Tools.FtpBrowserTool.Models;
using PuregoldITToolkit.Tools.SettingsTool.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;

namespace PuregoldITToolkit.Tools.FtpBrowserTool.ViewModels
{
    public class FtpBrowserViewModel : ViewModelBase
    {
        private readonly IFtpBrowserService _service;
        private readonly string _cacheFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FtpStoresCache.json");

        public ObservableCollection<FtpStoreModel> StoresList { get; } = new ObservableCollection<FtpStoreModel>();
        public ObservableCollection<FtpFileModel> FilesList { get; } = new ObservableCollection<FtpFileModel>();
        public ObservableCollection<FtpFileModel> FilteredFilesList { get; } = new ObservableCollection<FtpFileModel>(); // For Search

        private FtpStoreModel _selectedStore;
        public FtpStoreModel SelectedStore
        {
            get => _selectedStore;
            set { if (SetProperty(ref _selectedStore, value)) _ = UpdatePathAndLoadAsync(); }
        }

        // Toggles
        private bool _isTabOthers = true;
        public bool IsTabOthers { get => _isTabOthers; set { if (SetProperty(ref _isTabOthers, value) && value) _ = UpdatePathAndLoadAsync(); } }

        private bool _isTabFromHo;
        public bool IsTabFromHo { get => _isTabFromHo; set { if (SetProperty(ref _isTabFromHo, value) && value) _ = UpdatePathAndLoadAsync(); } }

        private bool _isTabInstallers;
        public bool IsTabInstallers { get => _isTabInstallers; set { if (SetProperty(ref _isTabInstallers, value) && value) _ = UpdatePathAndLoadAsync(); } }

        private bool _isTabRoot;
        public bool IsTabRoot { get => _isTabRoot; set { if (SetProperty(ref _isTabRoot, value) && value) _ = UpdatePathAndLoadAsync(); } }

        // Core Tracking
        private string _currentHost = "192.168.200.177";
        private string _currentPath = "Select a store or tab...";
        public string CurrentPath { get => _currentPath; set => SetProperty(ref _currentPath, value); }

        private FtpFileModel _selectedFile;
        public FtpFileModel SelectedFile { get => _selectedFile; set => SetProperty(ref _selectedFile, value); }

        // Search Engine
        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set { if (SetProperty(ref _searchText, value)) ApplySearchFilter(); }
        }

        private string _statusMessage = "Ready.";
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

        // Commands
        public ICommand LoadStoresCommand { get; }
        public ICommand DownloadCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand OpenDirectoryCommand { get; }
        public ICommand GoUpCommand { get; }
        public ICommand RefreshCacheCommand { get; }

        public FtpBrowserViewModel(IFtpBrowserService service)
        {
            _service = service;
            LoadStoresCommand = new AsyncRelayCommand(() => LoadStoresAsync(false));
            RefreshCacheCommand = new AsyncRelayCommand(() => LoadStoresAsync(true)); // Forces a network fetch
            DownloadCommand = new AsyncRelayCommand(DownloadFileAsync);
            RefreshCommand = new AsyncRelayCommand(async () => await LoadPathAsync(CurrentPath));
            OpenDirectoryCommand = new AsyncRelayCommand<FtpFileModel>(OpenDirectoryAsync);
            GoUpCommand = new AsyncRelayCommand(GoUpAsync);

            _ = LoadStoresAsync(false);
        }

        private async Task LoadStoresAsync(bool forceRefresh)
        {
            IsBusy = true;

            if (!forceRefresh && File.Exists(_cacheFile))
            {
                try
                {
                    var json = File.ReadAllText(_cacheFile);
                    var cachedStores = JsonSerializer.Deserialize<List<FtpStoreModel>>(json);
                    if (cachedStores != null && cachedStores.Count > 0)
                    {
                        StoresList.Clear();
                        foreach (var s in cachedStores) StoresList.Add(s);
                        StatusMessage = $"Loaded {StoresList.Count} stores from Cache.";
                        IsBusy = false;
                        return;
                    }
                }
                catch { } // Fallback to network if cache is corrupted
            }

            StatusMessage = "Fetching store list from FTP Network...";
            var stores = await _service.GetStoresAsync();

            StoresList.Clear();
            foreach (var store in stores) StoresList.Add(store);

            try { File.WriteAllText(_cacheFile, JsonSerializer.Serialize(stores)); } catch { }

            StatusMessage = $"Loaded {StoresList.Count} stores from network.";
            IsBusy = false;
        }

        private async Task UpdatePathAndLoadAsync()
        {
            var settings = SettingsViewModel.GetCurrentSettings();
            _currentHost = string.IsNullOrWhiteSpace(settings.DefaultFtpServer) ? "192.168.200.177" : settings.DefaultFtpServer;
            string newPath = "";

            if (IsTabRoot)
            {
                newPath = "/";
            }
            else if (IsTabInstallers)
            {
                _currentHost = "192.168.200.132";
                newPath = "/installers/";
            }
            else if (SelectedStore != null)
            {
                if (IsTabOthers)
                    newPath = $"/toho/{SelectedStore.OriginalFolderName}/Others/";
                else if (IsTabFromHo)
                    newPath = $"/FromHO/{SelectedStore.OriginalFolderName}/"; // FIX: Capitalized properly for Linux FTP
            }
            else
            {
                FilesList.Clear();
                FilteredFilesList.Clear();
                CurrentPath = "Please select a store.";
                return;
            }

            await LoadPathAsync(newPath);
        }

        private async Task LoadPathAsync(string targetPath)
        {
            if (string.IsNullOrWhiteSpace(targetPath)) return;
            if (!targetPath.EndsWith("/")) targetPath += "/";

            CurrentPath = targetPath;
            IsBusy = true;
            StatusMessage = $"Loading {CurrentPath} from {_currentHost}...";

            var files = await _service.GetFilesAsync(_currentHost, CurrentPath);

            FilesList.Clear();
            foreach (var file in files) FilesList.Add(file);

            ApplySearchFilter();

            StatusMessage = $"Ready. {FilesList.Count} items found.";
            IsBusy = false;
        }

        private void ApplySearchFilter()
        {
            FilteredFilesList.Clear();
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                foreach (var f in FilesList) FilteredFilesList.Add(f);
            }
            else
            {
                string lowerSearch = SearchText.ToLower();
                foreach (var f in FilesList)
                {
                    if (f.FileName.ToLower().Contains(lowerSearch))
                        FilteredFilesList.Add(f);
                }
            }
        }

        private async Task OpenDirectoryAsync(FtpFileModel file)
        {
            if (file != null && file.IsDirectory)
                await LoadPathAsync(file.FullPath);
            else if (file != null && !file.IsDirectory)
                await DownloadFileAsync();
        }

        private async Task GoUpAsync()
        {
            if (string.IsNullOrWhiteSpace(CurrentPath) || CurrentPath == "/") return;

            string path = CurrentPath;
            if (path.EndsWith("/") && path.Length > 1) path = path.TrimEnd('/');

            int lastSlashIndex = path.LastIndexOf('/');
            if (lastSlashIndex >= 0)
            {
                string parentPath = path.Substring(0, lastSlashIndex + 1);
                if (string.IsNullOrWhiteSpace(parentPath)) parentPath = "/";
                await LoadPathAsync(parentPath);
            }
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
                StatusMessage = $"Downloading {SelectedFile.FileName} from {_currentHost}...";

                bool success = await _service.DownloadFileAsync(_currentHost, SelectedFile.FullPath, dlg.FileName);

                if (success)
                {
                    StatusMessage = $"Successfully downloaded {SelectedFile.FileName}!";
                    try { Process.Start("explorer.exe", $"/select,\"{dlg.FileName}\""); } catch { }
                }
                else
                {
                    StatusMessage = $"Failed to download {SelectedFile.FileName}. Check connection.";
                }

                IsBusy = false;
            }
        }
    }
}