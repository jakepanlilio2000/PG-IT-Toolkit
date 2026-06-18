using PuregoldITToolkit.Tools.EJConsolidator.Models;
using System;
using System.Threading.Tasks;

namespace PuregoldITToolkit.Tools.EJConsolidator.Interfaces
{
    public interface IEJConsolidatorService
    {
        Task<int> ProcessConsolidationAsync(EJFilterOptions options, IProgress<string> textProgress, IProgress<int> pctProgress);
    }
}