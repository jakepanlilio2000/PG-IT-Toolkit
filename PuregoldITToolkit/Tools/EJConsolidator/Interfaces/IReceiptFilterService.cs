using PuregoldITToolkit.Tools.EJConsolidator.Models;

namespace PuregoldITToolkit.Tools.EJConsolidator.Interfaces
{
    public interface IReceiptFilterService
    {
        bool IsMatch(string block, EJFilterOptions filters);
    }
}