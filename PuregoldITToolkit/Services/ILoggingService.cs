using PuregoldITToolkit.Models;
using System;
using System.Collections.ObjectModel;

namespace PuregoldITToolkit.Services
{
    public interface ILoggingService
    {
        ObservableCollection<LogEntryModel> Logs { get; }
        string LatestMessage { get; }
        string LatestTimestamp { get; }
        string LatestColor { get; }

        event Action<LogEntryModel> LogReceived;

        void LogInfo(string message);
        void LogWarning(string message);
        void LogError(string message, Exception ex = null);
        void LogSuccess(string message);

        void Clear();
        void OpenLogFolder();
        void ApplyRetentionPolicy(int retentionDays);
    }
}

