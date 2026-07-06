using PuregoldITToolkit.Tools.FtpBrowserTool.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PuregoldITToolkit.Tools.FtpBrowserTool.Interfaces
{
    public interface IFtpBrowserService
    {
        Task<List<FtpStoreModel>> GetStoresAsync();
        Task<List<FtpFileModel>> GetFilesAsync(string host, string path);
        Task<bool> DownloadFileAsync(string host, string remotePath, string localPath);
    }
}