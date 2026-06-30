using System;
using System.IO;
using System.Reflection;
using System.Drawing;

namespace PuregoldITToolkit.Core.Helpers
{
    public static class ResourceHelper
    {
        public static Image LoadEmbeddedImage(string fileName)
        {
            string resourceName = $"PuregoldITToolkit.Assets.{fileName}";
            var assembly = Assembly.GetExecutingAssembly();

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    throw new Exception($"Embedded Resource '{resourceName}' not found.");

                return Image.FromStream(stream);
            }
        }
    }
}