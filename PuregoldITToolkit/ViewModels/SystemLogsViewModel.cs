using CommunityToolkit.Mvvm.Input;
using PuregoldITToolkit.Core.Base;
using PuregoldITToolkit.Models;
using PuregoldITToolkit.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Input;

namespace PuregoldITToolkit.ViewModels
{
    public class SystemLogsViewModel : ViewModelBase
    {
        public ObservableCollection<LogEntryModel> Logs => LoggingService.Instance.Logs;
        public ICollectionView FilteredLogs { get; }

        private string _filterText = string.Empty;
        public string FilterText
        {
            get => _filterText;
            set
            {
                if (SetProperty(ref _filterText, value))
                {
                    FilteredLogs.Refresh();
                }
            }
        }

        private string _selectedLevel = "ALL";
        public string SelectedLevel
        {
            get => _selectedLevel;
            set
            {
                if (SetProperty(ref _selectedLevel, value))
                {
                    FilteredLogs.Refresh();
                }
            }
        }

        public ObservableCollection<string> AvailableLevels { get; } = new ObservableCollection<string>
        {
            "ALL", "INFO", "WARN", "ERROR", "SUCCESS"
        };

        public ICommand ClearLogsCommand { get; }
        public ICommand OpenLogFolderCommand { get; }
        public ICommand ExportLogsCommand { get; }

        public SystemLogsViewModel()
        {
            FilteredLogs = CollectionViewSource.GetDefaultView(Logs);
            FilteredLogs.Filter = FilterLogEntry;

            ClearLogsCommand = new RelayCommand(() => LoggingService.Instance.Clear());
            OpenLogFolderCommand = new RelayCommand(() => LoggingService.Instance.OpenLogFolder());
            ExportLogsCommand = new RelayCommand(ExportLogs);
        }

        private bool FilterLogEntry(object obj)
        {
            if (!(obj is LogEntryModel entry)) return false;

            // Level filter
            if (!string.IsNullOrEmpty(SelectedLevel) && SelectedLevel != "ALL")
            {
                if (!entry.Level.Equals(SelectedLevel, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            // Text search
            if (!string.IsNullOrWhiteSpace(FilterText))
            {
                string search = FilterText.Trim();
                if (entry.Message.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0 &&
                    entry.Level.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0 &&
                    entry.FormattedTimestamp.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return false;
                }
            }

            return true;
        }

        private void ExportLogs()
        {
            using (var dlg = new SaveFileDialog())
            {
                dlg.Title = "Export System Logs";
                dlg.Filter = "Text Files (*.txt)|*.txt|Log Files (*.log)|*.log";
                dlg.FileName = $"Toolkit_Exported_Logs_{DateTime.Now:yyyyMMdd_HHmmss}.txt";

                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        var sb = new StringBuilder();
                        sb.AppendLine($"Puregold IT Toolkit - Log Export generated on {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                        sb.AppendLine(new string('=', 80));
                        foreach (var log in Logs)
                        {
                            sb.AppendLine(log.FormattedLine);
                        }

                        File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                        LoggingService.Instance.LogSuccess($"Exported {Logs.Count} log lines to {Path.GetFileName(dlg.FileName)}");
                    }
                    catch (Exception ex)
                    {
                        LoggingService.Instance.LogError("Failed to export logs", ex);
                    }
                }
            }
        }
    }
}

