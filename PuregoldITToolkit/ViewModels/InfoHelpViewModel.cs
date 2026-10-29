using CommunityToolkit.Mvvm.Input;
using PuregoldITToolkit.Core.Base;
using PuregoldITToolkit.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace PuregoldITToolkit.ViewModels
{
    public class InfoHelpViewModel : ViewModelBase
    {
        public string AppName => "Puregold IT Toolkit";
        public string Version => "v" + typeof(App).Assembly.GetName().Version.ToString(3);
        public string BuildDate => "October 2026";
        public string Architecture => "x86 (32-bit) / .NET Framework 4.8";
        public string TargetCompatibility => "Windows 7 SP1 (32-bit & 64-bit) up to Windows 11";

        public string OsVersion { get; }
        public string ClrVersion { get; }
        public string MachineName { get; }
        public string CurrentUser { get; }
        public string LocalIp { get; }
        public string ProcessBitness => Environment.Is64BitProcess ? "64-bit" : "32-bit (x86 Native)";

        private string _activeHelpSection = "EJ";
        public string ActiveHelpSection
        {
            get => _activeHelpSection;
            set => SetProperty(ref _activeHelpSection, value);
        }

        private string _statusMessage = "Ready.";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public ICommand SelectHelpSectionCommand { get; }
        public ICommand CopySystemInfoCommand { get; }
        public ICommand OpenLogDirectoryCommand { get; }
        public ICommand ExportDiagnosticReportCommand { get; }

        public InfoHelpViewModel()
        {
            OsVersion = $"{Environment.OSVersion.VersionString} ({(Environment.Is64BitOperatingSystem ? "64-bit OS" : "32-bit OS")})";
            ClrVersion = $".NET CLR {Environment.Version}";
            MachineName = Environment.MachineName;
            CurrentUser = Environment.UserName;
            LocalIp = ResolveLocalIpAddress();

            SelectHelpSectionCommand = new RelayCommand<string>(s => ActiveHelpSection = s);
            CopySystemInfoCommand = new RelayCommand(CopySystemInfo);
            OpenLogDirectoryCommand = new RelayCommand(() => LoggingService.Instance.OpenLogFolder());
            ExportDiagnosticReportCommand = new RelayCommand(ExportDiagnosticReport);
        }

        private string ResolveLocalIpAddress()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                var ip = host.AddressList.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a));
                return ip != null ? ip.ToString() : "127.0.0.1";
            }
            catch
            {
                return "127.0.0.1";
            }
        }

        private void CopySystemInfo()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== PUREGOLD IT TOOLKIT SYSTEM DIAGNOSTICS ===");
            sb.AppendLine($"Application:       {AppName} {Version}");
            sb.AppendLine($"Architecture:      {Architecture} ({ProcessBitness})");
            sb.AppendLine($"Target OS:         {TargetCompatibility}");
            sb.AppendLine($"Current OS:        {OsVersion}");
            sb.AppendLine($"CLR Version:       {ClrVersion}");
            sb.AppendLine($"Machine Name:      {MachineName}");
            sb.AppendLine($"User Name:         {CurrentUser}");
            sb.AppendLine($"Local IP Address:  {LocalIp}");
            sb.AppendLine($"Timestamp:         {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            try
            {
                Clipboard.SetText(sb.ToString());
                StatusMessage = "System diagnostics copied to clipboard!";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Could not copy to clipboard: {ex.Message}";
            }
        }

        private void ExportDiagnosticReport()
        {
            try
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string reportPath = Path.Combine(desktop, $"Puregold_Diagnostic_Report_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

                var sb = new StringBuilder();
                sb.AppendLine("==================================================================");
                sb.AppendLine("PUREGOLD IT TOOLKIT - COMPREHENSIVE SYSTEM DIAGNOSTIC REPORT");
                sb.AppendLine($"Generated at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine("==================================================================");
                sb.AppendLine();

                sb.AppendLine("[Application Information]");
                sb.AppendLine($"App Name:          {AppName}");
                sb.AppendLine($"Version:           {Version}");
                sb.AppendLine($"Build Date:        {BuildDate}");
                sb.AppendLine($"Target Runtime:    {Architecture}");
                sb.AppendLine($"Process Bitness:   {ProcessBitness}");
                sb.AppendLine();

                sb.AppendLine("[Host Environment]");
                sb.AppendLine($"Operating System:  {OsVersion}");
                sb.AppendLine($"CLR Runtime:       {ClrVersion}");
                sb.AppendLine($"Machine Name:      {MachineName}");
                sb.AppendLine($"Current User:      {CurrentUser}");
                sb.AppendLine($"Primary Local IP:  {LocalIp}");
                sb.AppendLine($"System Directory:  {Environment.SystemDirectory}");
                sb.AppendLine($"Working Directory: {AppDomain.CurrentDomain.BaseDirectory}");
                sb.AppendLine();

                sb.AppendLine("[Disk Drives Status]");
                foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
                {
                    sb.AppendLine($"Drive {drive.Name} ({drive.DriveFormat}) - Free: {drive.TotalFreeSpace / (1024 * 1024 * 1024.0):F2} GB / Total: {drive.TotalSize / (1024 * 1024 * 1024.0):F2} GB");
                }
                sb.AppendLine();

                sb.AppendLine("[Recent Application Log Entries]");
                var logs = LoggingService.Instance.Logs.Take(25);
                foreach (var log in logs)
                {
                    sb.AppendLine($"[{log.FormattedTimestamp}] [{log.Level}] {log.Message}");
                }
                sb.AppendLine();
                sb.AppendLine("==================================================================");
                sb.AppendLine("END OF DIAGNOSTIC REPORT");

                File.WriteAllText(reportPath, sb.ToString(), Encoding.UTF8);
                Process.Start(new ProcessStartInfo { FileName = reportPath, UseShellExecute = true });
                StatusMessage = $"Diagnostic report exported to: {Path.GetFileName(reportPath)}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Report export error: {ex.Message}";
            }
        }
    }
}

