using System.Threading.Tasks;

namespace PuregoldITToolkit.Tools.SettingsTool.Interfaces
{
    public interface ISettingsService
    {
        Task<string> LoadSignatureAsync();
        Task<bool> SaveSignatureAsync(string signatureHtml);
        Task<bool> ClearSignatureAsync();
    }
}