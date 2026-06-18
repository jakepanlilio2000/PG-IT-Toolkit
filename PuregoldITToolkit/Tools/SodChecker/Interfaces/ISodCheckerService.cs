using PuregoldITToolkit.Tools.SodChecker.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace PuregoldITToolkit.Tools.SodChecker.Interfaces
{
    public interface ISodCheckerService
    {
        Task<List<StoreConfig>> GetStoreListAsync();
        Task SaveStoresAsync(IEnumerable<StoreConfig> stores);
        Task ScanRowsAsync(IEnumerable<SodStoreResult> rows, string targetColumn, IProgress<string> progress, IProgress<int> percentage);
        Task LoadCachedDataAsync(IEnumerable<SodStoreResult> rows);
    }
}