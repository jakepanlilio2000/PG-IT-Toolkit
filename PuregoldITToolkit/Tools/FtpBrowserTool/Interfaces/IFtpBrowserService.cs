using PuregoldITToolkit.Tools.FtpBrowserTool.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PuregoldITToolkit.Tools.FtpBrowserTool.Interfaces
{
    public interface IFtpBrowserService
    {
        Task<List<FtpStoreModel>> GetStoresAsync();
        Task<List<FtpFileModel>> GetFilesAsync(string path);
        Task<bool> DownloadFileAsync(string remotePath, string localPath);
    }
}