using PuregoldITToolkit.Tools.FtpBrowserTool.Interfaces;
using PuregoldITToolkit.Tools.FtpBrowserTool.Models;
using PuregoldITToolkit.Tools.SettingsTool.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using WinSCP;

namespace PuregoldITToolkit.Tools.FtpBrowserTool.Services
{
    public class FtpBrowserService : IFtpBrowserService
    {
        private Session ConnectToFtp(string host = null)
        {
            var session = new Session();
            try
            {
                var settings = SettingsViewModel.GetCurrentSettings();
                string primaryFtp = string.IsNullOrWhiteSpace(settings.DefaultFtpServer) ? "192.168.200.177" : settings.DefaultFtpServer;
                string actualHost = host ?? primaryFtp;
                string pass = string.IsNullOrWhiteSpace(settings.FtpPassword) ? "pw@1234" : settings.FtpPassword;

                for (int i = 1; i <= 12; i++)
                {
                    try
                    {
                        session.Open(new SessionOptions
                        {
                            Protocol = Protocol.Ftp,
                            HostName = actualHost,
                            UserName = $"ftp{i}@puregold",
                            Password = pass
                        });
                        return session;
                    }
                    catch { }
                }

                if (actualHost.Contains("132"))
                {
                    try
                    {
                        session.Open(new SessionOptions
                        {
                            Protocol = Protocol.Ftp,
                            HostName = actualHost,
                            UserName = "anonymous",
                            Password = "anonymous@"
                        });
                        return session;
                    }
                    catch { }
                }

                throw new InvalidOperationException($"Failed to connect to {actualHost}. All authentication attempts failed.");
            }
            catch
            {
                session.Dispose();
                throw;
            }
        }

        public async Task<List<FtpStoreModel>> GetStoresAsync()
        {
            return await Task.Run(() =>
            {
                var list = new List<FtpStoreModel>();
                using (var session = ConnectToFtp())
                {
                    try
                    {
                        var files = session.ListDirectory("/toho/").Files;
                        foreach (var f in files)
                        {
                            if (f.Name == "." || f.Name == "..") continue;
                            var match = Regex.Match(f.Name, @"^\((\d+)\)(.*)");
                            if (match.Success)
                            {
                                string code = match.Groups[1].Value;
                                string name = match.Groups[2].Value.Replace("_", " ").ToUpper().Trim();
                                list.Add(new FtpStoreModel { StoreCode = code, StoreName = name, OriginalFolderName = f.Name });
                            }
                        }
                    }
                    catch (Exception ex) { throw new Exception($"Failed to list stores: {ex.Message}", ex); }
                }
                return list.OrderBy(x => x.StoreCode).ToList();
            });
        }

        public async Task<List<FtpFileModel>> GetFilesAsync(string host, string path)
        {
            return await Task.Run(() =>
            {
                var list = new List<FtpFileModel>();
                using (var session = ConnectToFtp(host))
                {
                    try
                    {
                        if (!session.FileExists(path)) throw new DirectoryNotFoundException($"Path '{path}' not found.");
                        var files = session.ListDirectory(path).Files;
                        foreach (var f in files)
                        {
                            if (f.Name == "." || f.Name == "..") continue;
                            list.Add(new FtpFileModel
                            {
                                FileName = f.Name,
                                FullPath = f.FullName,
                                IsDirectory = f.IsDirectory,
                                Size = f.Length,
                                LastModified = f.LastWriteTime
                            });
                        }
                    }
                    catch (Exception ex) when (!(ex is DirectoryNotFoundException))
                    { throw new Exception($"Failed to list files: {ex.Message}", ex); }
                }
                return list.OrderByDescending(x => x.IsDirectory).ThenBy(x => x.FileName).ToList();
            });
        }

        public async Task<List<FtpFileModel>> DeepSearchAsync(string host, string basePath, string searchPattern, IProgress<DeepSearchProgress> progress, CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var list = new List<FtpFileModel>();
                using (var session = ConnectToFtp(host))
                {
                    try
                    {
                        if (!session.FileExists(basePath)) throw new DirectoryNotFoundException($"Base path '{basePath}' not found.");

                        var files = session.EnumerateRemoteFiles(basePath, $"*{searchPattern}*", EnumerationOptions.AllDirectories);
                        int count = 0;
                        var startTime = DateTime.UtcNow;

                        foreach (var f in files)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            list.Add(new FtpFileModel
                            {
                                FileName = f.FullName,
                                FullPath = f.FullName,
                                IsDirectory = f.IsDirectory,
                                Size = f.Length,
                                LastModified = f.LastWriteTime
                            });
                            count++;

                            var elapsed = DateTime.UtcNow - startTime;
                            double speed = elapsed.TotalSeconds > 0 ? count / elapsed.TotalSeconds : 0;

                            progress?.Report(new DeepSearchProgress
                            {
                                FilesFound = count,
                                ElapsedTime = elapsed,
                                FilesPerSecond = speed
                            });
                        }
                    }
                    catch (OperationCanceledException) { throw; } 
                    catch (Exception ex)
                    { throw new Exception($"Deep search failed: {ex.Message}", ex); }
                }
                return list.OrderByDescending(x => x.IsDirectory).ThenBy(x => x.FileName).ToList();
            }, cancellationToken);
        }

        public async Task<bool> DownloadFileAsync(string host, string remotePath, string localPath)
        {
            return await Task.Run(() =>
            {
                using (var session = ConnectToFtp(host))
                {
                    try
                    {
                        session.GetFiles(remotePath, localPath).Check();
                        return true;
                    }
                    catch (Exception ex) { throw new Exception($"Download failed: {ex.Message}", ex); }
                }
            });
        }

    }
}