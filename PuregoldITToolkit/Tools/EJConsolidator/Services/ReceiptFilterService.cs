using PuregoldITToolkit.Tools.EJConsolidator.Interfaces;
using PuregoldITToolkit.Tools.EJConsolidator.Models;
using System;
using System.Text.RegularExpressions;

namespace PuregoldITToolkit.Tools.EJConsolidator.Services
{
    public class ReceiptFilterService : IReceiptFilterService
    {
        public bool IsMatch(string block, EJFilterOptions filters)
        {
            // Transaction Finder Mode Override
            if (filters.IsModeTrxFinder && !string.IsNullOrWhiteSpace(filters.TargetTrxNumber))
            {
                return block.IndexOf(filters.TargetTrxNumber.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
            }

            // Standard Consolidator Filters
            if (filters.SecondReceiptOnly && block.IndexOf("SECOND RECEIPT", StringComparison.OrdinalIgnoreCase) < 0)
                return false;

            if (filters.GcashBarcodeOnly && block.IndexOf("GCASH BARCODE", StringComparison.OrdinalIgnoreCase) < 0)
                return false;

            if (filters.HacsOnlineOnly && block.IndexOf("HACS ONLINE", StringComparison.OrdinalIgnoreCase) < 0)
                return false;

            if (filters.XReadZReadOnly && !Regex.IsMatch(block, @"(?i)(X|Z)\s*[-]?\s*READ|(?i)(X|Z)\s*[-]?\s*REPORT"))
                return false;

            if (!string.IsNullOrWhiteSpace(filters.SpecificCashier))
            {
                string pattern = $@"(?i)(Cashier|Ch)[^:]*:\s*{Regex.Escape(filters.SpecificCashier.Trim())}";
                if (!Regex.IsMatch(block, pattern)) return false;
            }

            if (!string.IsNullOrWhiteSpace(filters.SpecificBagger))
            {
                string pattern = $@"(?i)Bagger[^:]*:\s*{Regex.Escape(filters.SpecificBagger.Trim())}";
                if (!Regex.IsMatch(block, pattern)) return false;
            }

            if (!string.IsNullOrWhiteSpace(filters.FilterCardLast4))
            {
                string pattern = $@"(?i)Member Card Number:\s*.*?{Regex.Escape(filters.FilterCardLast4.Trim())}\b";
                if (!Regex.IsMatch(block, pattern)) return false;
            }

            if (!string.IsNullOrWhiteSpace(filters.FilterMemberName))
            {
                string pattern = $@"(?i)Member Name:\s*.*?{Regex.Escape(filters.FilterMemberName.Trim())}";
                if (!Regex.IsMatch(block, pattern)) return false;
            }

            if (!string.IsNullOrWhiteSpace(filters.FilterExactAmount))
            {
                string amountEscaped = Regex.Escape(filters.FilterExactAmount.Trim());
                string pattern = $@"(?i)Total(?: Amt Due)?\s*(?:Php)?\s*{amountEscaped}(?:\s|$)";
                if (!Regex.IsMatch(block, pattern)) return false;
            }

            return true;
        }
    }
}