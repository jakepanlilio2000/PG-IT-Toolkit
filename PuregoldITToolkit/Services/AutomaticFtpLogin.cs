using PuregoldITToolkit.Tools.SettingsTool.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using WinSCP;

namespace PuregoldITToolkit.Services
{
    public static class AutomaticFtpLogin
    {
        public static IReadOnlyList<string> Usernames { get; } = Array.AsReadOnly(
            Enumerable.Range(1, 12).Select(i => $"ftp{i}@puregold")
                .Concat(new[] { "puregold/ftp1", "puregold/ftp1a", "puregold/ftp1b", "puregold/ftp1c" })
                .Concat(Enumerable.Range(2, 10).Select(i => $"puregold/ftp{i}")).ToArray());

        public static Session Open(string host, int port = 21, int timeoutSeconds = 15, bool passive = true,
            CancellationToken cancellationToken = default(CancellationToken), Action<Session> configure = null,
            Func<Session, bool> accepts = null)
        {
            if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("Enter the FTP address.");
            if (port < 1 || port > 65535) throw new ArgumentException("Check the FTP connection settings.");
            cancellationToken.ThrowIfCancellationRequested();
            // Stop immediately when the server cannot be reached, rather than retrying every account.
            using (var client = new TcpClient())
            {
                var connection = client.ConnectAsync(host, port);
                if (!connection.Wait(Math.Max(1, timeoutSeconds) * 1000, cancellationToken))
                {
                    _ = connection.ContinueWith(t => { var ignored = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
                    throw new IOException("The FTP server is not responding.");
                }
                connection.GetAwaiter().GetResult();
            }
            var settings = SettingsViewModel.GetCurrentSettings();
            string password = settings.FtpPassword;
            IEnumerable<string> candidates = Usernames;
            if (host.Split('.').LastOrDefault() == "132") candidates = candidates.Concat(new[] { "anonymous" });
            foreach (string username in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var session = new Session();
                try
                {
                    configure?.Invoke(session);
                    session.FileTransferProgress += (sender, e) => e.Cancel = cancellationToken.IsCancellationRequested;
                    session.Open(new SessionOptions { Protocol = Protocol.Ftp, HostName = host, PortNumber = port,
                        UserName = username, Password = username == "anonymous" ? "anonymous@" : password,
                        Timeout = TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds)), FtpMode = passive ? FtpMode.Passive : FtpMode.Active });
                    if (accepts == null || accepts(session)) return session;
                }
                catch (OperationCanceledException) { session.Dispose(); throw; }
                catch (SessionException) { }
                catch { session.Dispose(); throw; }
                session.Dispose();
            }
            throw new IOException("Could not open the requested FTP folder with the saved accounts. Check the server address and store code.");
        }
    }
}
