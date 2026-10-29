using PuregoldITToolkit.Tools.SettingsTool.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PuregoldITToolkit.Tools.SettingsTool.Interfaces
{
    public interface ISettingsService
    {
        Task<string> LoadSignatureAsync();
        Task<bool> SaveSignatureAsync(string signatureHtml);
        Task<bool> ClearSignatureAsync();

        Task<SettingsModel> LoadSettingsAsync();
        Task<bool> SaveSettingsAsync(SettingsModel settings);

        // Diagnostic tools
        Task<(bool Success, string Message)> TestFtpConnectionAsync(string host, int port, string username, string password, int timeoutSeconds, bool passive);
        Task<(bool AllValid, List<string> Details)> ValidateDirectoriesAsync(SettingsModel settings);
    }
}
