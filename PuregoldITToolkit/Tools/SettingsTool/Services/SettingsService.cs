using PuregoldITToolkit.Services;
using PuregoldITToolkit.Tools.SettingsTool.Interfaces;
using PuregoldITToolkit.Tools.SettingsTool.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PuregoldITToolkit.Tools.SettingsTool.Services
{
    public class SettingsService : ISettingsService
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("PuregoldITToolkit_DPAPI_Entropy_v48");

        private readonly string _signatureFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EmailSignature.txt");
        public static readonly string ConfigFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
        public static readonly string LegacyConfigFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GlobalConfig.json");

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
                catch (Exception ex)
                {
                    LoggingService.Instance.LogError("Failed to save email signature", ex);
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
                catch (Exception ex)
                {
                    LoggingService.Instance.LogError("Failed to clear email signature", ex);
                    return false;
                }
            });
        }

        public async Task<SettingsModel> LoadSettingsAsync()
        {
            return await Task.Run(() => LoadSettingsInternal());
        }

        public async Task<bool> SaveSettingsAsync(SettingsModel settings)
        {
            return await Task.Run(() => SaveSettingsInternal(settings));
        }

        public static string CreateDefaultConfiguration() => JsonSerializer.Serialize(new SettingsModel(), new JsonSerializerOptions { WriteIndented = true });

        public static SettingsModel LoadSettingsInternal()
        {
            string targetPath = File.Exists(ConfigFilePath) ? ConfigFilePath : (File.Exists(LegacyConfigFilePath) ? LegacyConfigFilePath : null);

            if (targetPath != null)
            {
                try
                {
                    string json = File.ReadAllText(targetPath);
                    var model = JsonSerializer.Deserialize<SettingsModel>(json);
                    if (model != null)
                    {
                        DecryptCredentials(model);
                        return model;
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.LogError($"Failed to load configuration from {targetPath}", ex);
                }
            }

            return new SettingsModel();
        }

        public static bool SaveSettingsInternal(SettingsModel settings)
        {
            if (settings == null) return false;

            try
            {
                // Clone model for serialization so in-memory values remain decrypted
                var clonedModel = CloneSettings(settings);
                EncryptCredentials(clonedModel);

                string json = JsonSerializer.Serialize(clonedModel, new JsonSerializerOptions { WriteIndented = true });

                // Save to settings.json
                File.WriteAllText(ConfigFilePath, json);

                // Also maintain legacy GlobalConfig.json for complete backward compatibility
                try
                {
                    File.WriteAllText(LegacyConfigFilePath, json);
                }
                catch { }

                // Apply log retention policy
                LoggingService.Instance.ApplyRetentionPolicy(settings.LogRetentionDays);

                LoggingService.Instance.LogSuccess("Universal settings persisted to disk successfully.");
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("Failed to save configuration", ex);
                return false;
            }
        }

        // ==========================================
        // Diagnostic Tools
        // ==========================================
        public async Task<(bool Success, string Message)> TestFtpConnectionAsync(string host, int port, string username, string password, int timeoutSeconds, bool passive)
        {
            return await Task.Run(() =>
            {
                if (string.IsNullOrWhiteSpace(host))
                    return (false, "FTP Host cannot be blank.");

                if (port <= 0 || port > 65535) port = 21;
                int timeoutMs = Math.Max(3000, timeoutSeconds * 1000);

                try
                {
                    using (var session = PuregoldITToolkit.Services.AutomaticFtpLogin.Open(host, port, timeoutSeconds, passive))
                        return (true, "Connected to FTP. Login was selected automatically.");
                }
                catch (Exception) { return (false, "Could not connect. Check the FTP address and network connection."); }

            });
        }

        public async Task<(bool AllValid, List<string> Details)> ValidateDirectoriesAsync(SettingsModel settings)
        {
            return await Task.Run(() =>
            {
                var details = new List<string>();
                bool allValid = true;

                void CheckDir(string path, string label)
                {
                    if (string.IsNullOrWhiteSpace(path))
                    {
                        details.Add($"❌ {label}: Path is empty.");
                        allValid = false;
                        return;
                    }

                    try
                    {
                        if (!Directory.Exists(path))
                        {
                            Directory.CreateDirectory(path);
                            details.Add($"⚠️ {label}: Directory did not exist, but was successfully created ({path}).");
                        }
                        else
                        {
                            details.Add($"✅ {label}: Directory exists ({path}).");
                        }

                        // Test write permission
                        string testFile = Path.Combine(path, $".test_write_{Guid.NewGuid():N}.tmp");
                        File.WriteAllText(testFile, "Puregold IT Toolkit Permission Check");
                        File.Delete(testFile);
                        details.Add($"   └ Write/Read access verified.");
                    }
                    catch (Exception ex)
                    {
                        details.Add($"❌ {label}: Access test failed for '{path}' - {ex.Message}");
                        allValid = false;
                    }
                }

                CheckDir(settings.BackOfficeRootDir, "Back-Office Root Directory");
                CheckDir(settings.EjInputDirectory, "Default EJ Input Directory");
                CheckDir(settings.EjOutputDirectory, "Processed Output Directory");

                return (allValid, details);
            });
        }

        // ==========================================
        // DPAPI Credential Encryption Helpers
        // ==========================================
        private static void EncryptCredentials(SettingsModel model)
        {
            if (model == null) return;
            model.FtpPassword = EncryptString(model.FtpPassword);
            model.ConsoPassword = EncryptString(model.ConsoPassword);
            model.PosPassword = EncryptString(model.PosPassword);
            model.SmtpPass = EncryptString(model.SmtpPass);
            model.OutboundFtpPassword = EncryptString(model.OutboundFtpPassword);
            model.PimsSqlPassword = EncryptString(model.PimsSqlPassword);
        }

        private static void DecryptCredentials(SettingsModel model)
        {
            if (model == null) return;
            model.FtpPassword = DecryptString(model.FtpPassword);
            model.ConsoPassword = DecryptString(model.ConsoPassword);
            model.PosPassword = DecryptString(model.PosPassword);
            model.SmtpPass = DecryptString(model.SmtpPass);
            model.OutboundFtpPassword = DecryptString(model.OutboundFtpPassword);
            model.PimsSqlPassword = DecryptString(model.PimsSqlPassword);
        }

        private static string EncryptString(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return string.Empty;
            if (plainText.StartsWith("enc:")) return plainText;

            try
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                byte[] encryptedBytes = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.CurrentUser);
                return "enc:" + Convert.ToBase64String(encryptedBytes);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DPAPI Encryption failed, falling back to plaintext: {ex.Message}");
                return plainText;
            }
        }

        private static string DecryptString(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return string.Empty;
            if (!cipherText.StartsWith("enc:")) return cipherText;

            try
            {
                string base64 = cipherText.Substring(4);
                byte[] cipherBytes = Convert.FromBase64String(base64);
                byte[] plainBytes = ProtectedData.Unprotect(cipherBytes, Entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DPAPI Decryption failed: {ex.Message}");
                return cipherText;
            }
        }

        private static SettingsModel CloneSettings(SettingsModel source)
        {
            string json = JsonSerializer.Serialize(source);
            return JsonSerializer.Deserialize<SettingsModel>(json);
        }
    }
}
