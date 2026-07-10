using PuregoldITToolkit.Tools.EJConsolidator.Models;
using System.Collections.Generic;

namespace PuregoldITToolkit.Tools.EJConsolidator.Interfaces
{
    public interface IReceiptFilterService
    {
        bool IsMatch(string block, EJFilterOptions filters);

        FilterMatchResult GetMatchDetails(string block, EJFilterOptions filters);
    }

    public class FilterMatchResult
    {
        public bool IsMatch { get; set; }
        public Dictionary<string, bool> ConditionResults { get; set; } = new Dictionary<string, bool>();
        public List<string> FailedReasons { get; set; } = new List<string>();
    }
}