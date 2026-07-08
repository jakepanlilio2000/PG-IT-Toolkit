using CommunityToolkit.Mvvm.Input;
using PuregoldITToolkit.Core.Base;
using PuregoldITToolkit.Tools.FtpBrowserTool.Interfaces;
using PuregoldITToolkit.Tools.FtpBrowserTool.Models;
using PuregoldITToolkit.Tools.FtpBrowserTool.Views; // Added for CustomDialog
using PuregoldITToolkit.Tools.SettingsTool.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace PuregoldITToolkit.Tools.FtpBrowserTool.ViewModels
{
    public class FtpBrowserViewModel : ViewModelBase
    {
        private readonly IFtpBrowserService _service;
        private readonly string _cacheFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FtpStoresCache.json");
        private CancellationTokenSource _deepSearchCts;

        public ObservableCollection<FtpStoreModel> StoresList { get; } = new ObservableCollection<FtpStoreModel>();
        public ObservableCollection<FtpFileModel> FilesList { get; } = new ObservableCollection<FtpFileModel>();
        public ObservableCollection<FtpFileModel> FilteredFilesList { get; } = new ObservableCollection<FtpFileModel>();

        private FtpStoreModel _selectedStore;
        public FtpStoreModel SelectedStore { get => _selectedStore; set { if (SetProperty(ref _selectedStore, value)) _ = UpdatePathAndLoadAsync(); } }

        private bool _isTabOthers = true;
        public bool IsTabOthers { get => _isTabOthers; set { if (SetProperty(ref _isTabOthers, value) && value) { IsTabFromHo = IsTabInstallers = IsTabRoot = false; _ = UpdatePathAndLoadAsync(); } } }
        private bool _isTabFromHo;
        public bool IsTabFromHo { get => _isTabFromHo; set { if (SetProperty(ref _isTabFromHo, value) && value) { IsTabOthers = IsTabInstallers = IsTabRoot = false; _ = UpdatePathAndLoadAsync(); } } }
        private bool _isTabInstallers;
        public bool IsTabInstallers { get => _isTabInstallers; set { if (SetProperty(ref _isTabInstallers, value) && value) { IsTabOthers = IsTabFromHo = IsTabRoot = false; _ = UpdatePathAndLoadAsync(); } } }
        private bool _isTabRoot;
        public bool IsTabRoot { get => _isTabRoot; set { if (SetProperty(ref _isTabRoot, value) && value) { IsTabOthers = IsTabFromHo = IsTabInstallers = false; _ = UpdatePathAndLoadAsync(); } } }

        private string _currentHost = "192.168.200.177";
        public string CurrentHost { get => _currentHost; set => SetProperty(ref _currentHost, value); }
        private string _currentPath = "Select a store or tab...";
        public string CurrentPath { get => _currentPath; set => SetProperty(ref _currentPath, value); }
        private string _searchText;
        public string SearchText { get => _searchText; set { if (SetProperty(ref _searchText, value)) ApplySearchFilter(); } }
        private string _statusMessage = "Ready.";
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
        private bool _isBusy;
        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }
        private bool _isDeepSearching;
        public bool IsDeepSearching { get => _isDeepSearching; set => SetProperty(ref _isDeepSearching, value); }
        private string _deepSearchStatus = "";
        public string DeepSearchStatus { get => _deepSearchStatus; set => SetProperty(ref _deepSearchStatus, value); }
        private bool _canCancelSearch;
        public bool CanCancelSearch { get => _canCancelSearch; set => SetProperty(ref _canCancelSearch, value); }
        private bool _isTransferring;
        public bool IsTransferring { get => _isTransferring; set => SetProperty(ref _isTransferring, value); }
        private double _transferPercentage;
        public double TransferPercentage { get => _transferPercentage; set => SetProperty(ref _transferPercentage, value); }
        private string _transferStatus = "";
        public string TransferStatus { get => _transferStatus; set => SetProperty(ref _transferStatus, value); }
        private List<FtpFileModel> _selectedFiles = new List<FtpFileModel>();
        public List<FtpFileModel> SelectedFiles { get => _selectedFiles; set => SetProperty(ref _selectedFiles, value); }
        public object DragDataObject { get; set; }

        public ICommand LoadStoresCommand { get; }
        public ICommand RefreshCacheCommand { get; }
        public ICommand DownloadCommand { get; }
        public ICommand DownloadMultipleCommand { get; }
        public ICommand UploadCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand OpenDirectoryCommand { get; }
        public ICommand GoUpCommand { get; }
        public ICommand DeepSearchCommand { get; }
        public ICommand CancelDeepSearchCommand { get; }
        public ICommand QuickViewCommand { get; }
        public ICommand OpenInExplorerCommand { get; }
        public ICommand CopyPathCommand { get; }
        public ICommand RenameCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand ViewPropertiesCommand { get; }

        public FtpBrowserViewModel(IFtpBrowserService service)
        {
            _service = service;
            LoadStoresCommand = new AsyncRelayCommand(() => LoadStoresAsync(false));
            RefreshCacheCommand = new AsyncRelayCommand(() => LoadStoresAsync(true));
            DownloadCommand = new AsyncRelayCommand(DownloadAsync);
            DownloadMultipleCommand = new AsyncRelayCommand(DownloadMultipleAsync);
            UploadCommand = new AsyncRelayCommand(UploadAsync);
            RefreshCommand = new AsyncRelayCommand(async () => await LoadPathAsync(CurrentPath));
            OpenDirectoryCommand = new AsyncRelayCommand<FtpFileModel>(OpenDirectoryAsync);
            GoUpCommand = new AsyncRelayCommand(GoUpAsync);
            DeepSearchCommand = new AsyncRelayCommand(ExecuteDeepSearchAsync);
            CancelDeepSearchCommand = new RelayCommand(CancelDeepSearch);
            QuickViewCommand = new AsyncRelayCommand(QuickViewAsync);
            OpenInExplorerCommand = new RelayCommand(OpenInExplorer);
            CopyPathCommand = new RelayCommand(CopyPath);
            RenameCommand = new AsyncRelayCommand(RenameAsync);
            DeleteCommand = new AsyncRelayCommand(DeleteAsync);
            ViewPropertiesCommand = new AsyncRelayCommand(ViewPropertiesAsync);
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
                catch { }
            }
            StatusMessage = "Fetching store list from FTP Network...";
            try
            {
                var stores = await _service.GetStoresAsync();
                StoresList.Clear();
                foreach (var store in stores) StoresList.Add(store);
                try { File.WriteAllText(_cacheFile, JsonSerializer.Serialize(stores)); } catch { }
                StatusMessage = $"Loaded {StoresList.Count} stores from network.";
            }
            catch (Exception ex) { CustomDialog.ShowAlert("FTP Error", ex.Message); }
            finally { IsBusy = false; }
        }

        private async Task UpdatePathAndLoadAsync()
        {
            var settings = SettingsViewModel.GetCurrentSettings();
            _currentHost = string.IsNullOrWhiteSpace(settings.DefaultFtpServer) ? "192.168.200.177" : settings.DefaultFtpServer;
            string newPath = "";
            if (IsTabRoot) newPath = "/";
            else if (IsTabInstallers) { _currentHost = "192.168.200.132"; newPath = "/installers/"; }
            else if (IsTabFromHo) newPath = SelectedStore != null ? $"/fromho/{SelectedStore.OriginalFolderName}/" : "/fromho/";
            else if (IsTabOthers) newPath = SelectedStore != null ? $"/toho/{SelectedStore.OriginalFolderName}/Others/" : "/toho/";
            else { FilesList.Clear(); FilteredFilesList.Clear(); CurrentPath = "Please select a tab."; return; }
            await LoadPathAsync(newPath);
        }

        private async Task LoadPathAsync(string targetPath)
        {
            if (string.IsNullOrWhiteSpace(targetPath)) return;
            if (!targetPath.EndsWith("/")) targetPath += "/";
            CurrentPath = targetPath;
            IsBusy = true;
            StatusMessage = $"Loading {CurrentPath} from {_currentHost}...";
            try
            {
                var files = await _service.GetFilesAsync(_currentHost, CurrentPath);
                FilesList.Clear();
                foreach (var file in files) FilesList.Add(file);
                ApplySearchFilter();
                StatusMessage = $"Ready. {FilesList.Count} items found.";
            }
            catch (Exception ex) { CustomDialog.ShowAlert("Directory Error", ex.Message); }
            finally { IsBusy = false; }
        }

        private async Task ExecuteDeepSearchAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchText)) { CustomDialog.ShowAlert("Validation", "Please enter a keyword."); return; }
            if (string.IsNullOrWhiteSpace(CurrentPath) || CurrentPath == "Select a store or tab...") { CustomDialog.ShowAlert("Validation", "Navigate to a directory first."); return; }
            IsBusy = true; IsDeepSearching = true; CanCancelSearch = true;
            DeepSearchStatus = "Starting search...";
            _deepSearchCts = new CancellationTokenSource();
            var progress = new Progress<DeepSearchProgress>(p => {
                DeepSearchStatus = $"Elapsed: {p.ElapsedTime:mm\\:ss} | Found: {p.FilesFound} files | Speed: {p.FilesPerSecond:F1} files/sec";
            });
            try
            {
                var files = await _service.DeepSearchAsync(_currentHost, CurrentPath, SearchText, progress, _deepSearchCts.Token);
                FilesList.Clear();
                foreach (var file in files) FilesList.Add(file);
                ApplySearchFilter();
                StatusMessage = $"Deep Search Complete. Found {FilesList.Count} matching files.";
            }
            catch (OperationCanceledException) { StatusMessage = "Deep Search cancelled."; }
            catch (Exception ex) { CustomDialog.ShowAlert("Search Error", ex.Message); }
            finally { IsBusy = false; IsDeepSearching = false; CanCancelSearch = false; DeepSearchStatus = ""; _deepSearchCts?.Dispose(); }
        }

        private void CancelDeepSearch() { _deepSearchCts?.Cancel(); StatusMessage = "Cancelling..."; }
        private void ApplySearchFilter()
        {
            FilteredFilesList.Clear();
            if (string.IsNullOrWhiteSpace(SearchText)) { foreach (var f in FilesList) FilteredFilesList.Add(f); }
            else { string lowerSearch = SearchText.ToLower(); foreach (var f in FilesList) { if (f.FileName.ToLower().Contains(lowerSearch)) FilteredFilesList.Add(f); } }
        }

        private async Task OpenDirectoryAsync(FtpFileModel file)
        {
            if (file != null && file.IsDirectory) await LoadPathAsync(file.FullPath);
            else if (file != null && !file.IsDirectory) await DownloadAsync();
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

        // --- UPDATED: SMART DOWNLOAD (Handles Files AND Folders) ---
        private async Task DownloadAsync()
        {
            var file = SelectedFiles?.FirstOrDefault();
            if (file == null) { CustomDialog.ShowAlert("Validation", "Please select a file or folder."); return; }

            if (file.IsDirectory)
            {
                // Use OpenFileDialog hack to get a folder path cleanly in WPF
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Select Destination Folder for " + file.FileName,
                    ValidateNames = false,
                    CheckFileExists = false,
                    CheckPathExists = true,
                    FileName = "Select Folder"
                };
                if (dlg.ShowDialog() == true)
                {
                    string folder = Path.GetDirectoryName(dlg.FileName) ?? dlg.FileName;
                    IsBusy = true; IsTransferring = true; TransferPercentage = 0; TransferStatus = "Downloading folder...";
                    var progress = new Progress<TransferProgress>(p => { TransferPercentage = p.Percentage; TransferStatus = $"Downloading {p.CurrentFile} ({p.Percentage:F0}%)"; });
                    try
                    {
                        await _service.DownloadFolderAsync(_currentHost, file.FullPath, folder, progress, CancellationToken.None);
                        StatusMessage = "Folder download complete.";
                        try { Process.Start("explorer.exe", folder); } catch { }
                    }
                    catch (Exception ex) { CustomDialog.ShowAlert("Error", ex.Message); }
                    finally { IsBusy = false; IsTransferring = false; }
                }
            }
            else
            {
                var dlg = new Microsoft.Win32.SaveFileDialog { FileName = file.FileName, Title = "Save FTP File" };
                if (dlg.ShowDialog() == true)
                {
                    IsBusy = true; IsTransferring = true; TransferPercentage = 0; TransferStatus = "Downloading...";
                    var progress = new Progress<TransferProgress>(p => { TransferPercentage = p.Percentage; TransferStatus = $"Downloading {p.CurrentFile} ({p.Percentage:F0}%)"; });
                    try
                    {
                        await _service.DownloadFileAsync(_currentHost, file.FullPath, dlg.FileName, progress);
                        StatusMessage = "Download complete.";
                        try { Process.Start("explorer.exe", $"/select,\"{dlg.FileName}\""); } catch { }
                    }
                    catch (Exception ex) { CustomDialog.ShowAlert("Error", ex.Message); }
                    finally { IsBusy = false; IsTransferring = false; }
                }
            }
        }

        private async Task DownloadMultipleAsync()
        {
            var selected = SelectedFiles?.ToList();
            if (selected == null || !selected.Any()) { CustomDialog.ShowAlert("Validation", "Please select files or folders to download."); return; }

            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Destination Folder",
                ValidateNames = false,
                CheckFileExists = false,
                CheckPathExists = true,
                FileName = "Select Folder"
            };
            if (dlg.ShowDialog() == true)
            {
                string baseFolder = Path.GetDirectoryName(dlg.FileName) ?? dlg.FileName;
                IsBusy = true; IsTransferring = true; TransferPercentage = 0; TransferStatus = "Downloading...";
                var progress = new Progress<TransferProgress>(p => { TransferPercentage = p.Percentage; TransferStatus = $"Downloading {p.CurrentFile} ({p.Percentage:F0}%)"; });

                try
                {
                    var files = selected.Where(f => !f.IsDirectory).ToList();
                    var folders = selected.Where(f => f.IsDirectory).ToList();

                    if (files.Any())
                    {
                        var paths = files.Select(f => f.FullPath).ToArray();
                        await _service.DownloadFilesAsync(_currentHost, paths, baseFolder, progress, CancellationToken.None);
                    }

                    foreach (var folder in folders)
                    {
                        await _service.DownloadFolderAsync(_currentHost, folder.FullPath, baseFolder, progress, CancellationToken.None);
                    }

                    StatusMessage = "Download complete.";
                    try { Process.Start("explorer.exe", baseFolder); } catch { }
                }
                catch (Exception ex) { CustomDialog.ShowAlert("Error", ex.Message); }
                finally { IsBusy = false; IsTransferring = false; }
            }
        }

        private async Task UploadAsync()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Title = "Select Files to Upload", Multiselect = true };
            if (dlg.ShowDialog() == true) await ExecuteUploadFromDrop(dlg.FileNames);
        }

        public async Task ExecuteUploadFromDrop(string[] files)
        {
            if (files == null || !files.Any()) return;
            IsBusy = true; IsTransferring = true; TransferPercentage = 0; TransferStatus = "Uploading...";
            var progress = new Progress<TransferProgress>(p => { TransferPercentage = p.Percentage; TransferStatus = $"Uploading {p.CurrentFile} ({p.Percentage:F0}%)"; });
            try
            {
                await _service.UploadFilesAsync(_currentHost, CurrentPath, files, progress, CancellationToken.None);
                StatusMessage = "Upload complete.";
                await LoadPathAsync(CurrentPath);
            }
            catch (Exception ex) { CustomDialog.ShowAlert("Error", ex.Message); }
            finally { IsBusy = false; IsTransferring = false; }
        }

        public async Task PrepareDragAsync()
        {
            if (SelectedFiles == null || !SelectedFiles.Any()) return;
            string tempDir = Path.Combine(Path.GetTempPath(), "FtpDrag_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(tempDir);
            IsBusy = true; StatusMessage = "Preparing drag operation...";
            try
            {
                var filesToDownload = SelectedFiles.Where(f => !f.IsDirectory).Select(f => f.FullPath).ToArray();
                if (filesToDownload.Any())
                {
                    var progress = new Progress<TransferProgress>();
                    await _service.DownloadFilesAsync(_currentHost, filesToDownload, tempDir, progress, CancellationToken.None);
                    var downloadedFiles = Directory.GetFiles(tempDir);
                    if (downloadedFiles.Any())
                    {
                        var data = new DataObject();
                        var list = new System.Collections.Specialized.StringCollection();
                        list.AddRange(downloadedFiles);
                        data.SetFileDropList(list);
                        DragDataObject = data;
                    }
                }
            }
            catch { DragDataObject = null; }
            finally { IsBusy = false; StatusMessage = "Ready."; }
        }

        // --- CONTEXT MENU OPERATIONS ---
        private void OpenInExplorer()
        {
            var file = SelectedFiles?.FirstOrDefault();
            if (file != null)
            {
                string ftpUrl = $"ftp://{_currentHost}{file.FullPath}";
                try { Process.Start(ftpUrl); }
                catch (Exception ex) { CustomDialog.ShowAlert("Error", $"Failed to open Explorer: {ex.Message}"); }
            }
        }

        private async Task QuickViewAsync()
        {
            var file = SelectedFiles?.FirstOrDefault();
            if (file == null || file.IsDirectory) return;
            IsBusy = true;
            try
            {
                string content = await _service.ReadRemoteTextFileAsync(_currentHost, file.FullPath);
                var viewer = new Window { Title = $"Quick View: {file.FileName}", Width = 800, Height = 600, WindowStartupLocation = WindowStartupLocation.CenterScreen };
                var textBox = new System.Windows.Controls.TextBox { Text = content, IsReadOnly = true, AcceptsReturn = true, VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto, FontFamily = new System.Windows.Media.FontFamily("Consolas") };
                viewer.Content = textBox;
                viewer.ShowDialog();
            }
            catch (Exception ex) { CustomDialog.ShowAlert("Error", ex.Message); }
            finally { IsBusy = false; }
        }

        private void CopyPath()
        {
            var file = SelectedFiles?.FirstOrDefault();
            if (file != null) { Clipboard.SetText($"ftp://{_currentHost}{file.FullPath}"); StatusMessage = "FTP Path copied!"; }
        }

        private async Task RenameAsync()
        {
            var file = SelectedFiles?.FirstOrDefault();
            if (file == null) return;

            // Replaced ShowInputBox with CustomDialog
            string newName = CustomDialog.ShowInput("Rename", "Enter new name:", file.FileName);
            if (!string.IsNullOrWhiteSpace(newName) && newName != file.FileName)
            {
                IsBusy = true;
                try
                {
                    string dir = Path.GetDirectoryName(file.FullPath)?.Replace("\\", "/") ?? "/";
                    await _service.RenameFileAsync(_currentHost, file.FullPath, $"{dir}/{newName}");
                    await LoadPathAsync(CurrentPath);
                }
                catch (Exception ex) { CustomDialog.ShowAlert("Error", ex.Message); }
                finally { IsBusy = false; }
            }
        }

        private async Task DeleteAsync()
        {
            if (SelectedFiles == null || !SelectedFiles.Any()) return;

            // Replaced MessageBox with CustomDialog
            if (CustomDialog.ShowConfirm("Confirm Delete", $"Are you sure you want to delete {SelectedFiles.Count} items?"))
            {
                IsBusy = true;
                try
                {
                    var paths = SelectedFiles.Select(f => f.FullPath).ToArray();
                    await _service.DeleteFilesAsync(_currentHost, paths);
                    await LoadPathAsync(CurrentPath);
                }
                catch (Exception ex) { CustomDialog.ShowAlert("Error", ex.Message); }
                finally { IsBusy = false; }
            }
        }

        private async Task ViewPropertiesAsync()
        {
            var file = SelectedFiles?.FirstOrDefault();
            if (file == null) return;
            IsBusy = true;
            try
            {
                var props = await _service.GetFilePropertiesAsync(_currentHost, file.FullPath);
                string msg = $"Name: {props.FileName}\nPath: {props.FullPath}\nSize: {props.DisplaySize}\nModified: {props.LastModified}\nPermissions: {props.Permissions}\nOwner: {props.Owner}";
                CustomDialog.ShowAlert("File Properties", msg);
            }
            catch (Exception ex) { CustomDialog.ShowAlert("Error", ex.Message); }
            finally { IsBusy = false; }
        }
    }
}