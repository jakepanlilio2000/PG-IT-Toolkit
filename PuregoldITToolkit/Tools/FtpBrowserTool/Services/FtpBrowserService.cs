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
        private static readonly SemaphoreSlim _ftpSemaphore = new SemaphoreSlim(1, 1);

        private Session ConnectToFtp(string host = null, Action<Session> configureSession = null)
        {
            var settings = SettingsViewModel.GetCurrentSettings();
            string primaryFtp = string.IsNullOrWhiteSpace(settings.DefaultFtpServer) ? "192.168.200.177" : settings.DefaultFtpServer;
            string actualHost = host ?? primaryFtp;
            string pass = string.IsNullOrWhiteSpace(settings.FtpPassword) ? "pw@1234" : settings.FtpPassword;

            Session session = null;
            for (int i = 1; i <= 12; i++)
            {
                try
                {
                    if (session != null)
                    {
                        if (session.Opened) session.Close();
                        session.Dispose();
                    }

                    session = new Session();

                    configureSession?.Invoke(session);

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
                    if (session != null)
                    {
                        if (session.Opened) session.Close();
                        session.Dispose();
                    }
                    session = new Session();

                    configureSession?.Invoke(session);

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

            if (session != null)
            {
                if (session.Opened) session.Close();
                session.Dispose();
            }

            throw new InvalidOperationException($"Failed to connect to {actualHost}. All authentication attempts failed.");
        }

        public async Task<List<FtpStoreModel>> GetStoresAsync()
        {
            await _ftpSemaphore.WaitAsync();
            try
            {
                return await Task.Run(() => {
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
            finally { _ftpSemaphore.Release(); }
        }

        public async Task<List<FtpFileModel>> GetFilesAsync(string host, string path)
        {
            await _ftpSemaphore.WaitAsync();
            try
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
                                    LastModified = f.LastWriteTime,
                                    FilePermissions = f.FilePermissions?.Text ?? "",
                                    Owner = f.Owner ?? "",
                                    Group = f.Group ?? ""
                                });
                            }
                        }
                        catch (Exception ex) when (!(ex is DirectoryNotFoundException))
                        { throw new Exception($"Failed to list files: {ex.Message}", ex); }
                    }
                    return list.OrderByDescending(x => x.IsDirectory).ThenBy(x => x.FileName).ToList();
                });
            }
            finally { _ftpSemaphore.Release(); }
        }

        public async Task<List<FtpFileModel>> DeepSearchAsync(string host, string basePath, string searchPattern, IProgress<DeepSearchProgress> progress, CancellationToken cancellationToken)
        {
            await _ftpSemaphore.WaitAsync(cancellationToken);
            try
            {
                return await Task.Run(() => {
                    var list = new List<FtpFileModel>();
                    using (var session = ConnectToFtp(host))
                    {
                        try
                        {
                            if (!session.FileExists(basePath)) throw new DirectoryNotFoundException($"Base path '{basePath}' not found.");
                            var files = session.EnumerateRemoteFiles(basePath, $"*{searchPattern}*", EnumerationOptions.AllDirectories);
                            int count = 0; var startTime = DateTime.UtcNow;
                            foreach (var f in files)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                list.Add(new FtpFileModel { FileName = f.FullName, FullPath = f.FullName, IsDirectory = f.IsDirectory, Size = f.Length, LastModified = f.LastWriteTime });
                                count++;
                                var elapsed = DateTime.UtcNow - startTime;
                                double speed = elapsed.TotalSeconds > 0 ? count / elapsed.TotalSeconds : 0;
                                progress?.Report(new DeepSearchProgress { FilesFound = count, ElapsedTime = elapsed, FilesPerSecond = speed });
                            }
                        }
                        catch (OperationCanceledException) { throw; }
                        catch (Exception ex) { throw new Exception($"Deep search failed: {ex.Message}", ex); }
                    }
                    return list.OrderByDescending(x => x.IsDirectory).ThenBy(x => x.FileName).ToList();
                }, cancellationToken);
            }
            finally { _ftpSemaphore.Release(); }
        }

        public async Task<bool> DownloadFileAsync(string host, string remotePath, string localPath, IProgress<TransferProgress> progress = null)
        {
            await _ftpSemaphore.WaitAsync();
            try
            {
                return await Task.Run(() =>
                {
                    using (var session = ConnectToFtp(host, s =>
                    {
                        if (progress != null)
                        {
                            s.FileTransferProgress += (sender, e) =>
                            {
                                progress.Report(new TransferProgress
                                {
                                    Percentage = e.OverallProgress * 100,
                                    CurrentFile = Path.GetFileName(e.FileName),
                                    TotalFiles = 1,
                                    FilesProcessed = e.OverallProgress >= 1.0 ? 1 : 0
                                });
                            };
                        }
                    }))
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
            finally { _ftpSemaphore.Release(); }
        }

        public async Task UploadFilesAsync(string host, string remotePath, string[] localPaths, IProgress<TransferProgress> progress, CancellationToken cancellationToken)
        {
            await _ftpSemaphore.WaitAsync(cancellationToken);
            try
            {
                await Task.Run(() =>
                {
                    int currentFileIndex = 0;
                    int total = localPaths.Length;

                    using (var session = ConnectToFtp(host, s =>
                    {
                      
                        s.FileTransferProgress += (sender, e) =>
                        {
                            double baseProgress = (double)currentFileIndex / total;
                            double fileWeight = 1.0 / total;
                            double currentOverall = baseProgress + (e.FileProgress * fileWeight);

                            progress?.Report(new TransferProgress
                            {
                                Percentage = currentOverall * 100,
                                CurrentFile = Path.GetFileName(localPaths[currentFileIndex]),
                                TotalFiles = total,
                                FilesProcessed = currentFileIndex
                            });
                        };
                    }))
                    {
                        try
                        {
                            if (!remotePath.EndsWith("/")) remotePath += "/";
                            for (int i = 0; i < total; i++)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                currentFileIndex = i; 
                                var localPath = localPaths[i];
                                var remoteFilePath = RemotePath.Combine(remotePath, Path.GetFileName(localPath));

                                session.PutFiles(localPath, remoteFilePath).Check();
                            }
                        }
                        catch (OperationCanceledException) { throw; }
                        catch (Exception ex) { throw new Exception($"Upload failed: {ex.Message}", ex); }
                    }
                });
            }
            finally { _ftpSemaphore.Release(); }
        }

        public async Task DownloadFilesAsync(string host, string[] remotePaths, string localFolderPath, IProgress<TransferProgress> progress, CancellationToken cancellationToken)
        {
            await _ftpSemaphore.WaitAsync(cancellationToken);
            try
            {
                await Task.Run(() =>
                {
                    int currentFileIndex = 0;
                    int total = remotePaths.Length;

                    using (var session = ConnectToFtp(host, s =>
                    {
                        s.FileTransferProgress += (sender, e) =>
                        {
                            double baseProgress = (double)currentFileIndex / total;
                            double fileWeight = 1.0 / total;
                            double currentOverall = baseProgress + (e.FileProgress * fileWeight);

                            progress?.Report(new TransferProgress
                            {
                                Percentage = currentOverall * 100,
                                CurrentFile = Path.GetFileName(remotePaths[currentFileIndex]),
                                TotalFiles = total,
                                FilesProcessed = currentFileIndex
                            });
                        };
                    }))
                    {
                        try
                        {
                            for (int i = 0; i < total; i++)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                currentFileIndex = i; 
                                var remotePath = remotePaths[i];
                                var localFilePath = Path.Combine(localFolderPath, Path.GetFileName(remotePath));

                                session.GetFiles(remotePath, localFilePath).Check();
                            }
                        }
                        catch (OperationCanceledException) { throw; }
                        catch (Exception ex) { throw new Exception($"Download failed: {ex.Message}", ex); }
                    }
                });
            }
            finally { _ftpSemaphore.Release(); }
        }

        public async Task DownloadFolderAsync(string host, string remoteFolderPath, string localFolderPath, IProgress<TransferProgress> progress, CancellationToken cancellationToken)
        {
            await _ftpSemaphore.WaitAsync(cancellationToken);
            try
            {
                await Task.Run(() =>
                {
                    int currentFileIndex = 0;
                    int total = 0;
                    string remotePathNormalized = remoteFolderPath.Replace("\\", "/");
                    if (!remotePathNormalized.EndsWith("/")) remotePathNormalized += "/";

                    string[] pathParts = remotePathNormalized.TrimEnd('/').Split('/');
                    string remoteFolderName = pathParts.Length > 0 ? pathParts.Last() : "";
                    string targetLocalFolder = string.IsNullOrEmpty(remoteFolderName) ? localFolderPath : Path.Combine(localFolderPath, remoteFolderName);

                    
                    Directory.CreateDirectory(targetLocalFolder);
                    

                    using (var session = ConnectToFtp(host, s =>
                    {
                        s.FileTransferProgress += (sender, e) =>
                        {
                            if (total > 0)
                            {
                                double baseProgress = (double)currentFileIndex / total;
                                double fileWeight = 1.0 / total;
                                double currentOverall = baseProgress + (e.FileProgress * fileWeight);
                                progress?.Report(new TransferProgress
                                {
                                    Percentage = currentOverall * 100,
                                    CurrentFile = Path.GetFileName(e.FileName),
                                    TotalFiles = total,
                                    FilesProcessed = currentFileIndex
                                });
                            }
                        };
                    }))
                    {
                        try
                        {
                            var files = session.EnumerateRemoteFiles(remotePathNormalized, "*", EnumerationOptions.AllDirectories)
                                               .Where(f => !f.IsDirectory).ToList();
                            total = files.Count;
                            for (int i = 0; i < total; i++)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                currentFileIndex = i;
                                var f = files[i];
                                string relativePath = f.FullName;
                                if (relativePath.StartsWith(remotePathNormalized, StringComparison.OrdinalIgnoreCase))
                                {
                                    relativePath = relativePath.Substring(remotePathNormalized.Length);
                                }
                                relativePath = relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                                var localFilePath = Path.Combine(targetLocalFolder, relativePath);
                                Directory.CreateDirectory(Path.GetDirectoryName(localFilePath));
                                session.GetFiles(f.FullName, localFilePath).Check();
                            }
                        }
                        catch (OperationCanceledException) { throw; }
                        catch (Exception ex) { throw new Exception($"Folder download failed: {ex.Message}", ex); }
                    }
                });
            }
            finally { _ftpSemaphore.Release(); }
        }

        public async Task RenameFileAsync(string host, string oldPath, string newPath)
        {
            await _ftpSemaphore.WaitAsync();
            try
            {
                await Task.Run(() =>
                {
                    using (var session = ConnectToFtp(host))
                    {
                        try { session.MoveFile(oldPath, newPath); }
                        catch (Exception ex) { throw new Exception($"Rename failed: {ex.Message}", ex); }
                    }
                });
            }
            finally { _ftpSemaphore.Release(); }
        }

        public async Task DeleteFilesAsync(string host, string[] paths)
        {
            await _ftpSemaphore.WaitAsync();
            try
            {
                await Task.Run(() =>
                {
                    using (var session = ConnectToFtp(host))
                    {
                        try
                        {
                            foreach (var path in paths) session.RemoveFiles(path);
                        }
                        catch (Exception ex) { throw new Exception($"Delete failed: {ex.Message}", ex); }
                    }
                });
            }
            finally { _ftpSemaphore.Release(); }
        }

        public async Task<string> ReadRemoteTextFileAsync(string host, string remotePath)
        {
            await _ftpSemaphore.WaitAsync();
            try
            {
                return await Task.Run(() =>
                {
                    using (var session = ConnectToFtp(host))
                    {
                        try
                        {
                            var tempFile = Path.GetTempFileName();
                            session.GetFiles(remotePath, tempFile).Check();
                            var content = File.ReadAllText(tempFile);
                            File.Delete(tempFile);
                            return content;
                        }
                        catch (Exception ex) { throw new Exception($"Read file failed: {ex.Message}", ex); }
                    }
                });
            }
            finally { _ftpSemaphore.Release(); }
        }

        public async Task<FilePropertiesModel> GetFilePropertiesAsync(string host, string remotePath)
        {
            await _ftpSemaphore.WaitAsync();
            try
            {
                return await Task.Run(() =>
                {
                    using (var session = ConnectToFtp(host))
                    {
                        try
                        {
                            var info = session.GetFileInfo(remotePath);
                            var props = new FilePropertiesModel
                            {
                                FileName = info.Name,
                                FullPath = info.FullName,
                                IsDirectory = info.IsDirectory,
                                Size = info.Length,
                                LastModified = info.LastWriteTime,
                                Permissions = info.FilePermissions?.Text ?? "N/A",
                                Owner = info.Owner ?? "N/A",
                                Group = info.Group ?? "N/A"
                            };

                            if (info.IsDirectory)
                            {
                                try
                                {
                                    var allFiles = session.EnumerateRemoteFiles(remotePath, "*", EnumerationOptions.AllDirectories)
                                                          .Where(f => !f.IsDirectory).ToList();
                                    props.Size = allFiles.Sum(f => f.Length);
                                    props.FileCount = allFiles.Count;
                                }
                                catch { /* Ignore if permission denied */ }
                            }

                            props.DisplaySize = new FtpFileModel { Size = props.Size, IsDirectory = false }.DisplaySize;
                            return props;
                        }
                        catch (Exception ex) { throw new Exception($"Get properties failed: {ex.Message}", ex); }
                    }
                });
            }
            finally { _ftpSemaphore.Release(); }
        }
    }
}