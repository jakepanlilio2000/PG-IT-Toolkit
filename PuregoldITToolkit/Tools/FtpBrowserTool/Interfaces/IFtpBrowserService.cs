using PuregoldITToolkit.Tools.FtpBrowserTool.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PuregoldITToolkit.Tools.FtpBrowserTool.Interfaces
{
    public class DeepSearchProgress
    {
        public int FilesFound { get; set; }
        public TimeSpan ElapsedTime { get; set; }
        public double FilesPerSecond { get; set; }
    }

    public interface IFtpBrowserService
    {
        Task<List<FtpStoreModel>> GetStoresAsync();
        Task<List<FtpFileModel>> GetFilesAsync(string host, string path);
        Task<bool> DownloadFileAsync(string host, string remotePath, string localPath);
        Task<List<FtpFileModel>> DeepSearchAsync(string host, string basePath, string searchPattern, IProgress<DeepSearchProgress> progress, CancellationToken cancellationToken);
    }
}