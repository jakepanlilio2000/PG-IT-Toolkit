using PuregoldITToolkit.Tools.SettingsTool.Models;
using System.Threading.Tasks;

namespace PuregoldITToolkit.Tools.SettingsTool.Interfaces
{
    public interface ISettingsService
    {
        Task<SettingsModel> LoadSettingsAsync();
        Task<bool> SaveSettingsAsync(SettingsModel settings);
        Task<bool> SaveSignatureAsync(string signatureHtml);
        Task<bool> ClearSignatureAsync();
    }
}