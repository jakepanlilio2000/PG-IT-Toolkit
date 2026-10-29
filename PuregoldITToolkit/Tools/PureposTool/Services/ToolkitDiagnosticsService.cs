using PuregoldITToolkit.Tools.PureposTool.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.ServiceProcess;
using System.Threading.Tasks;

namespace PuregoldITToolkit.Tools.PureposTool.Services
{
    public class ToolkitDiagnosticsService
    {
        public async Task<PingTargetResult> PingHostAsync(string targetName, string ipAddress, int timeoutMs = 1200)
        {
            var result = new PingTargetResult
            {
                TargetName = targetName,
                IpAddress = ipAddress,
                Status = "Testing...",
                StatusColor = "#F59E0B"
            };

            if (string.IsNullOrWhiteSpace(ipAddress))
            {
                result.Status = "Invalid IP";
                result.StatusColor = "#EF4444";
                return result;
            }

            try
            {
                using (var ping = new Ping())
                {
                    var reply = await ping.SendPingAsync(ipAddress.Trim(), timeoutMs);
                    if (reply.Status == IPStatus.Success)
                    {
                        result.Status = "Online";
                        result.LatencyMs = reply.RoundtripTime;
                        result.StatusColor = "#10B981";
                    }
                    else
                    {
                        result.Status = reply.Status.ToString();
                        result.StatusColor = "#EF4444";
                    }
                }
            }
            catch (Exception ex)
            {
                result.Status = $"Error: {ex.Message}";
                result.StatusColor = "#EF4444";
            }

            return result;
        }

        public async Task<PortCheckResult> CheckPortAsync(string host, int port, string serviceName, int timeoutMs = 1500)
        {
            var result = new PortCheckResult
            {
                Port = port,
                ServiceName = serviceName,
                Status = "Testing...",
                StatusColor = "#F59E0B"
            };

            if (string.IsNullOrWhiteSpace(host))
            {
                result.Status = "Invalid Host";
                result.StatusColor = "#EF4444";
                return result;
            }

            var stopwatch = Stopwatch.StartNew();
            try
            {
                using (var client = new TcpClient())
                {
                    var connectTask = client.ConnectAsync(host.Trim(), port);
                    var timeoutTask = Task.Delay(timeoutMs);

                    var completedTask = await Task.WhenAny(connectTask, timeoutTask);
                    stopwatch.Stop();

                    if (completedTask == connectTask) await connectTask;
                    else _ = connectTask.ContinueWith(t => { var ignored = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);

                    if (completedTask == connectTask && client.Connected)
                    {
                        result.Status = "Open";
                        result.LatencyMs = stopwatch.ElapsedMilliseconds;
                        result.StatusColor = "#10B981";
                    }
                    else
                    {
                        result.Status = "Closed / Filtered";
                        result.LatencyMs = stopwatch.ElapsedMilliseconds;
                        result.StatusColor = "#EF4444";
                    }
                }
            }
            catch (SocketException sex)
            {
                stopwatch.Stop();
                result.Status = sex.SocketErrorCode == SocketError.ConnectionRefused ? "Closed (Refused)" : "Filtered";
                result.LatencyMs = stopwatch.ElapsedMilliseconds;
                result.StatusColor = "#EF4444";
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                result.Status = $"Error: {ex.Message}";
                result.StatusColor = "#EF4444";
            }

            return result;
        }

        public List<NetworkAdapterDetail> GetNetworkAdapters()
        {
            var list = new List<NetworkAdapterDetail>();

            try
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces();
                foreach (var ni in interfaces)
                {
                    // Focus on Ethernet and Wireless interfaces
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                        ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                        continue;

                    var ipProps = ni.GetIPProperties();
                    var ipv4 = ipProps.UnicastAddresses
                        .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork);

                    var gateway = ipProps.GatewayAddresses
                        .FirstOrDefault(g => g.Address.AddressFamily == AddressFamily.InterNetwork);

                    var dnsList = ipProps.DnsAddresses
                        .Where(d => d.AddressFamily == AddressFamily.InterNetwork)
                        .Select(d => d.ToString());

                    string physicalAddress = string.Join(":", ni.GetPhysicalAddress().GetAddressBytes().Select(b => b.ToString("X2")));

                    var item = new NetworkAdapterDetail
                    {
                        Name = ni.Name,
                        Description = ni.Description,
                        IpAddress = ipv4 != null ? ipv4.Address.ToString() : "N/A",
                        SubnetMask = ipv4?.IPv4Mask != null ? ipv4.IPv4Mask.ToString() : "N/A",
                        Gateway = gateway != null ? gateway.Address.ToString() : "N/A",
                        DnsServers = dnsList.Any() ? string.Join(", ", dnsList) : "N/A",
                        MacAddress = string.IsNullOrEmpty(physicalAddress) ? "N/A" : physicalAddress,
                        Speed = $"{(ni.Speed / 1_000_000):N0} Mbps",
                        OperationalStatus = ni.OperationalStatus.ToString()
                    };

                    list.Add(item);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to retrieve network adapters: {ex.Message}");
            }

            return list;
        }

        public List<CacheDirectoryInfo> ScanCacheDirectories()
        {
            var results = new List<CacheDirectoryInfo>();
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            var targets = new (string Name, string Path)[]
            {
                ("EJ Cache", Path.Combine(baseDir, "EJ_Cache")),
                ("App Logs", Path.Combine(baseDir, "Logs")),
                ("Saved store data", Path.Combine(baseDir, "data")),
                ("Windows Temp (Puregold)", Path.Combine(Path.GetTempPath(), "Puregold_Temp"))
            };

            foreach (var target in targets)
            {
                var info = new CacheDirectoryInfo
                {
                    DirectoryName = target.Name,
                    Path = target.Path
                };

                if (Directory.Exists(target.Path))
                {
                    try
                    {
                        var files = Directory.GetFiles(target.Path, "*.*", SearchOption.AllDirectories);
                        info.FileCount = files.Length;
                        long bytes = files.Sum(f =>
                        {
                            try { return new FileInfo(f).Length; } catch { return 0; }
                        });
                        info.TotalBytes = bytes;
                        info.FormattedSize = FormatBytes(bytes);
                    }
                    catch
                    {
                        info.FormattedSize = "Access Denied";
                    }
                }
                else
                {
                    info.FormattedSize = "0 B (Not Created)";
                }

                results.Add(info);
            }

            return results;
        }

        public async Task<int> CleanCacheDirectoriesAsync(Action<string> log)
        {
            return await Task.Run(() =>
            {
                int deletedCount = 0;
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;

                var pathsToClean = new[]
                {
                    Path.Combine(baseDir, "EJ_Cache"),
                    Path.Combine(Path.GetTempPath(), "Puregold_Temp")
                };

                foreach (var dir in pathsToClean)
                {
                    string actual = Path.GetFullPath(dir);
                    if (!Directory.Exists(actual)) continue;
                    // Do not follow directory links out of the cache root.
                    if ((File.GetAttributes(actual) & FileAttributes.ReparsePoint) != 0) continue;

                    bool isEjCache = string.Equals(actual, Path.GetFullPath(Path.Combine(baseDir, "EJ_Cache")), StringComparison.OrdinalIgnoreCase);
                    if (isEjCache && !PuregoldITToolkit.Tools.EJConsolidator.Services.EJConsolidatorService.CacheAccess.Wait(0))
                    { log?.Invoke("EJ consolidation is running; its cache was skipped."); continue; }
                    try
                    {
                        var files = EnumerateCacheFiles(actual).ToArray();
                        foreach (var f in files)
                        {
                            try
                            {
                                File.Delete(f);
                                deletedCount++;
                            }
                            catch (Exception ex)
                            {
                                log?.Invoke($"Could not delete {Path.GetFileName(f)}: {ex.Message}");
                            }
                        }
                        log?.Invoke($"Cleaned {dir} ({files.Length} files examined)");
                    }
                    catch (Exception ex)
                    {
                        log?.Invoke($"Directory error cleaning {dir}: {ex.Message}");
                    }
                    finally { if (isEjCache) PuregoldITToolkit.Tools.EJConsolidator.Services.EJConsolidatorService.CacheAccess.Release(); }
                }

                return deletedCount;
            });
        }

        public async Task<string> ArchiveLogsAsync(Action<string> log)
        {
            return await Task.Run(() =>
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string logsDir = Path.Combine(baseDir, "Logs");

                if (!Directory.Exists(logsDir) || !Directory.GetFiles(logsDir).Any())
                {
                    log?.Invoke("No log files found in directory to archive.");
                    return null;
                }

                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string archiveDir = Path.Combine(desktop, "Puregold_Log_Archives");
                if (!Directory.Exists(archiveDir)) Directory.CreateDirectory(archiveDir);

                string zipName = $"Logs_Archive_{DateTime.Now:yyyyMMdd_HHmmss}.zip";
                string zipPath = Path.Combine(archiveDir, zipName);

                try
                {
                    ZipFile.CreateFromDirectory(logsDir, zipPath, CompressionLevel.Optimal, false);
                    log?.Invoke($"Archived logs successfully to: {zipPath}");
                    return zipPath;
                }
                catch (Exception ex)
                {
                    log?.Invoke($"Failed to create log archive: {ex.Message}");
                    return null;
                }
            });
        }

        public async Task<bool> RestartWindowsServiceAsync(string serviceName, Action<string> log)
        {
            return await Task.Run(() =>
            {
                try
                {
                    log?.Invoke($"Locating service '{serviceName}'...");
                    using (ServiceController sc = new ServiceController(serviceName))
                    {
                        if (sc.Status == ServiceControllerStatus.Running || sc.Status == ServiceControllerStatus.Paused)
                        {
                            log?.Invoke($"Stopping service '{serviceName}'...");
                            sc.Stop();
                            sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(15));
                            log?.Invoke($"Service '{serviceName}' stopped.");
                        }

                        log?.Invoke($"Starting service '{serviceName}'...");
                        sc.Start();
                        sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(15));
                        log?.Invoke($"Service '{serviceName}' restarted successfully!");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    log?.Invoke($"Service restart error ({serviceName}): {ex.Message}");
                    return false;
                }
            });
        }

        private static IEnumerable<string> EnumerateCacheFiles(string root)
        {
            foreach (var file in Directory.EnumerateFiles(root))
                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) == 0) yield return file;
            foreach (var directory in Directory.EnumerateDirectories(root))
                if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) == 0)
                    foreach (var file in EnumerateCacheFiles(directory)) yield return file;
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{(bytes / 1024.0):F1} KB";
            return $"{(bytes / (1024.0 * 1024.0)):F2} MB";
        }
    }
}

