using PuregoldITToolkit.Tools.EJConsolidator.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PuregoldITToolkit.Tools.EJConsolidator.Interfaces
{
    public interface IEJConsolidatorService
    {
        Task<System.Collections.Generic.List<EJStoreInfo>> GetStoresAsync(EJFilterOptions options, CancellationToken cancellationToken);
        Task<int> ProcessConsolidationAsync(EJFilterOptions options, IProgress<string> textProgress, IProgress<int> pctProgress);
        Task<int> ProcessConsolidationAsync(EJFilterOptions options, IProgress<EJProgressReport> progress, CancellationToken cancellationToken);
    }
}
