using PuregoldITToolkit.Tools.SettingsTool.Interfaces;
using System;
using System.IO;
using System.Threading.Tasks;

namespace PuregoldITToolkit.Tools.SettingsTool.Services
{
    public class SettingsService : ISettingsService
    {
        // This is where the signature is physically stored
        private readonly string _signatureFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EmailSignature.txt");

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
    }
}