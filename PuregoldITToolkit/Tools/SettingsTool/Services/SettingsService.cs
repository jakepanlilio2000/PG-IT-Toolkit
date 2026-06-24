using PuregoldITToolkit.Tools.SettingsTool.Interfaces;
using PuregoldITToolkit.Tools.SettingsTool.Models;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace PuregoldITToolkit.Tools.SettingsTool.Services
{
    public class SettingsService : ISettingsService
    {
        private readonly string _signatureFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EmailSignature.txt");
        public static readonly string ConfigFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GlobalConfig.json");
        public async Task<string> LoadSignatureAsync()
        {
            return await Task.Run(() =>
            {
                if (File.Exists(_signatureFilePath))
                    return File.ReadAllText(_signatureFilePath);

                return string.Empty;
            });
        }

        public async Task<bool> SaveSignatureAsync(string signatureHtml)
        {
            return await Task.Run(() =>
            {
                try
                {
                    File.WriteAllText(_signatureFilePath, signatureHtml ?? string.Empty);
                    return true;
                }
                catch
                {
                    return false;
                }
            });
        }

        public async Task<bool> ClearSignatureAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (File.Exists(_signatureFilePath))
                        File.Delete(_signatureFilePath);

                    return true;
                }
                catch
                {
                    return false;
                }
            });
        }
        public async Task<SettingsModel> LoadSettingsAsync()
        {
            return await Task.Run(() =>
            {
                if (File.Exists(ConfigFilePath))
                {
                    try
                    {
                        return JsonSerializer.Deserialize<SettingsModel>(File.ReadAllText(ConfigFilePath)) ?? new SettingsModel();
                    }
                    catch { }
                }
                return new SettingsModel();
            });
        }

        public async Task<bool> SaveSettingsAsync(SettingsModel settings)
        {
            return await Task.Run(() =>
            {
                try
                {
                    File.WriteAllText(ConfigFilePath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
                    return true;
                }
                catch
                {
                    return false;
                }
            });
        }
    }
}