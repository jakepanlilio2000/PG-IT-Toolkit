using PuregoldITToolkit.Tools.PureposTool.Models;
using System;
using System.Threading.Tasks;

namespace PuregoldITToolkit.Tools.PureposTool.Interfaces
{
    public interface IPureposService
    {
        Task RunAutoPermissionAsync(AutoPermissionModel config, Action<string> logCallback);
        Task RunManualEjSaveAsync(EjSaveModel config, Action<string> logCallback);
        Task<string> RunSshCommandAsync(string host, string username, string password, string command);
        Task RunEodGeneratorAsync(EodModel config, Action<string> logCallback);
    }
}