using System;

namespace PuregoldITToolkit.Tools.FtpBrowserTool.Models
{
    public class FtpFileModel
    {
        public string FileName { get; set; }
        public string FullPath { get; set; }
        public bool IsDirectory { get; set; }
        public long Size { get; set; }
        public DateTime LastModified { get; set; }

        public string DisplaySize => IsDirectory ? "" : FormatSize(Size);
        public string TypeIcon => IsDirectory ? "📁" : "📄";

        private string FormatSize(long bytes)
        {
            string[] suf = { "B", "KB", "MB", "GB", "TB" };
            if (bytes == 0) return "0 B";
            long bytesCopy = Math.Abs(bytes);
            int place = Convert.ToInt32(Math.Floor(Math.Log(bytesCopy, 1024)));
            double num = Math.Round(bytesCopy / Math.Pow(1024, place), 1);
            return $"{Math.Sign(bytes) * num} {suf[place]}";
        }
    }
}