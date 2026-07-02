using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace PuregoldITToolkit.Services
{
    public static class LicenseManager
    {
        private static Dictionary<string, bool> _features = new Dictionary<string, bool>();
        private const string LicenseFile = "license.dat";

        public static void Initialize()
        {
            string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, LicenseFile);

            if (File.Exists(filePath))
            {
                try
                {
                    string base64Text = File.ReadAllText(filePath);

                    byte[] jsonBytes = Convert.FromBase64String(base64Text);
                    string json = Encoding.UTF8.GetString(jsonBytes);

                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                    var deserializedFeatures = JsonSerializer.Deserialize<Dictionary<string, bool>>(json, options);
                    _features = deserializedFeatures ?? new Dictionary<string, bool>();
                }
                catch
                {
                    _features = new Dictionary<string, bool>();
                }
            }
            else
            {
                _features = new Dictionary<string, bool>();
            }
        }

        public static bool IsEnabled(string moduleName)
        {
            return _features.TryGetValue(moduleName, out bool isEnabled) && isEnabled;
        }
    }
}