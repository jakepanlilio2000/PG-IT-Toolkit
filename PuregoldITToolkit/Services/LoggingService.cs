using PuregoldITToolkit.Core.Base;
using PuregoldITToolkit.Models;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;

namespace PuregoldITToolkit.Services
{
    public class LoggingService : ViewModelBase, ILoggingService
    {
        private static readonly Lazy<LoggingService> _lazyInstance = new Lazy<LoggingService>(() => new LoggingService());
        public static LoggingService Instance => _lazyInstance.Value;

        private readonly string _logsDirectory;
        private readonly object _fileLock = new object();
        private const int MaxInMemoryLogs = 1000;

        public ObservableCollection<LogEntryModel> Logs { get; } = new ObservableCollection<LogEntryModel>();

        private string _latestMessage = "System ready.";
        public string LatestMessage
        {
            get => _latestMessage;
            private set => SetProperty(ref _latestMessage, value);
        }

        private string _latestTimestamp = DateTime.Now.ToString("HH:mm:ss");
        public string LatestTimestamp
        {
            get => _latestTimestamp;
            private set => SetProperty(ref _latestTimestamp, value);
        }

        private string _latestColor = "#2C3E50";
        public string LatestColor
        {
            get => _latestColor;
            private set => SetProperty(ref _latestColor, value);
        }

        public event Action<LogEntryModel> LogReceived;

        public LoggingService()
        {
            _logsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            try
            {
                if (!Directory.Exists(_logsDirectory))
                {
                    Directory.CreateDirectory(_logsDirectory);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to create logs directory: {ex.Message}");
            }
        }

        public void LogInfo(string message)
        {
            WriteEntry(new LogEntryModel
            {
                Level = "INFO",
                Message = message,
                DisplayColor = "#2C3E50"
            });
        }

        public void LogWarning(string message)
        {
            WriteEntry(new LogEntryModel
            {
                Level = "WARN",
                Message = message,
                DisplayColor = "#E67E22"
            });
        }

        public void LogError(string message, Exception ex = null)
        {
            string fullMsg = ex == null ? message : $"{message} | Exception: {ex.Message}";
            WriteEntry(new LogEntryModel
            {
                Level = "ERROR",
                Message = fullMsg,
                DisplayColor = "#C41E3A"
            });
        }

        public void LogSuccess(string message)
        {
            WriteEntry(new LogEntryModel
            {
                Level = "SUCCESS",
                Message = message,
                DisplayColor = "#27AE60"
            });
        }

        private void WriteEntry(LogEntryModel entry)
        {
            // 1. Dispatch to UI collection and update latest status
            Application.Current?.Dispatcher?.InvokeAsync(() =>
            {
                try
                {
                    if (Logs.Count >= MaxInMemoryLogs)
                    {
                        Logs.RemoveAt(0);
                    }
                    Logs.Add(entry);

                    LatestMessage = entry.Message;
                    LatestTimestamp = entry.Timestamp.ToString("HH:mm:ss");
                    LatestColor = entry.DisplayColor;

                    LogReceived?.Invoke(entry);
                }
                catch { }
            });

            // 2. Persist to disk asynchronously/safely
            try
            {
                string logFile = Path.Combine(_logsDirectory, $"toolkit_{DateTime.Now:yyyyMMdd}.log");
                lock (_fileLock)
                {
                    using (var stream = new FileStream(logFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                    using (var writer = new StreamWriter(stream, Encoding.UTF8))
                    {
                        writer.WriteLine(entry.FormattedLine);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"File logging failed: {ex.Message}");
            }
        }

        public void Clear()
        {
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                Logs.Clear();
                LatestMessage = "Logs cleared.";
                LatestTimestamp = DateTime.Now.ToString("HH:mm:ss");
                LatestColor = "#7F8FA6";
            });
        }

        public void OpenLogFolder()
        {
            try
            {
                if (!Directory.Exists(_logsDirectory))
                {
                    Directory.CreateDirectory(_logsDirectory);
                }
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{_logsDirectory}\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                LogError($"Failed to open log folder: {ex.Message}");
            }
        }

        public void ApplyRetentionPolicy(int retentionDays)
        {
            if (retentionDays <= 0) return;

            try
            {
                if (!Directory.Exists(_logsDirectory)) return;

                var files = Directory.GetFiles(_logsDirectory, "toolkit_*.log");
                DateTime cutoff = DateTime.Now.Date.AddDays(-retentionDays);

                int deleted = 0;
                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.LastWriteTime < cutoff)
                    {
                        try
                        {
                            fileInfo.Delete();
                            deleted++;
                        }
                        catch { }
                    }
                }

                if (deleted > 0)
                {
                    LogInfo($"Cleaned up {deleted} archived log file(s) older than {retentionDays} days.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Log retention cleanup failed: {ex.Message}");
            }
        }
    }
}

