using PuregoldITToolkit.Tools.FtpBrowserTool.Interfaces;
using PuregoldITToolkit.Tools.FtpBrowserTool.Models;
using PuregoldITToolkit.Tools.SettingsTool.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WinSCP;

namespace PuregoldITToolkit.Tools.FtpBrowserTool.Services
{
    public class FtpBrowserService : IFtpBrowserService
    {
        private Session ConnectToFtp(string host = null)
        {
            var session = new Session();
            var settings = SettingsViewModel.GetCurrentSettings();
            string primaryFtp = string.IsNullOrWhiteSpace(settings.DefaultFtpServer) ? "192.168.200.177" : settings.DefaultFtpServer;
            string actualHost = host ?? primaryFtp;
            string pass = string.IsNullOrWhiteSpace(settings.FtpPassword) ? "pw@1234" : settings.FtpPassword;

            // Attempt Puregold credentials first
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

            // Fallback for .132 installers if standard credentials fail
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
            return null;
        }

        public async Task<List<FtpStoreModel>> GetStoresAsync()
        {
            return await Task.Run(() =>
            {
                var list = new List<FtpStoreModel>();
                using (var session = ConnectToFtp())
                {
                    if (session == null) return list;

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
                    catch { }
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
                    if (session == null) return list;

                    try
                    {
                        if (!session.FileExists(path)) return list;

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
                    catch { }
                }
                return list.OrderByDescending(x => x.IsDirectory).ThenBy(x => x.FileName).ToList();
            });
        }

        public async Task<bool> DownloadFileAsync(string host, string remotePath, string localPath)
        {
            return await Task.Run(() =>
            {
                using (var session = ConnectToFtp(host))
                {
                    if (session == null) return false;

                    try
                    {
                        session.GetFiles(remotePath, localPath).Check();
                        return true;
                    }
                    catch { return false; }
                }
            });
        }
    }
}