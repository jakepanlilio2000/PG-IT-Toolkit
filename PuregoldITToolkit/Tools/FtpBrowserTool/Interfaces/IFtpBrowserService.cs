using PuregoldITToolkit.Tools.FtpBrowserTool.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PuregoldITToolkit.Tools.FtpBrowserTool.Interfaces
{

    public interface IFtpBrowserService
    {
        Task<List<FtpStoreModel>> GetStoresAsync();
        Task<List<FtpFileModel>> GetFilesAsync(string host, string path);
        Task<List<FtpFileModel>> DeepSearchAsync(string host, string basePath, string searchPattern, IProgress<DeepSearchProgress> progress, CancellationToken cancellationToken);

        Task<bool> DownloadFileAsync(string host, string remotePath, string localPath, IProgress<TransferProgress> progress = null);
        Task DownloadFilesAsync(string host, string[] remotePaths, string localFolderPath, IProgress<TransferProgress> progress, CancellationToken cancellationToken);
        Task DownloadFolderAsync(string host, string remoteFolderPath, string localFolderPath, IProgress<TransferProgress> progress, CancellationToken cancellationToken);

        Task UploadFilesAsync(string host, string remotePath, string[] localPaths, IProgress<TransferProgress> progress, CancellationToken cancellationToken);
        Task RenameFileAsync(string host, string oldPath, string newPath);
        Task DeleteFilesAsync(string host, string[] paths);
        Task<string> ReadRemoteTextFileAsync(string host, string remotePath);
        Task<FilePropertiesModel> GetFilePropertiesAsync(string host, string remotePath);
    }
}