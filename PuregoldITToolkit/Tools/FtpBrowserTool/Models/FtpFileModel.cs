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
        public string FilePermissions { get; set; }
        public string Owner { get; set; }
        public string Group { get; set; }

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

    public class TransferProgress
    {
        public double Percentage { get; set; }
        public string CurrentFile { get; set; }
        public int TotalFiles { get; set; }
        public int FilesProcessed { get; set; }
    }

    public class DeepSearchProgress
    {
        public int FilesFound { get; set; }
        public TimeSpan ElapsedTime { get; set; }
        public double FilesPerSecond { get; set; }
    }

    public class FilePropertiesModel
    {
        public string FileName { get; set; }
        public string FullPath { get; set; }
        public bool IsDirectory { get; set; }
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
        public string Permissions { get; set; }
        public string Owner { get; set; }
        public string Group { get; set; }
        public int FileCount { get; set; }
        public string DisplaySize { get; set; }
    }
}