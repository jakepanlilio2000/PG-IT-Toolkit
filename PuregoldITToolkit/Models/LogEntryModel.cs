using System;

namespace PuregoldITToolkit.Models
{
    public class LogEntryModel
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string Level { get; set; } = "INFO";
        public string Message { get; set; } = string.Empty;
        public string DisplayColor { get; set; } = "#2C3E50";

        public string FormattedTimestamp => Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
        public string FormattedLine => $"[{FormattedTimestamp}] [{Level,-7}] {Message}";
    }
}

