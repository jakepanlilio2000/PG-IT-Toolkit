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
            if (filters.IsModeTrxFinder && !string.IsNullOrWhiteSpace(filters.TargetTrxNumber))
            {
                return block.IndexOf(filters.TargetTrxNumber.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
            }

            if (filters.SecondReceiptOnly && block.IndexOf("SECOND RECEIPT", StringComparison.OrdinalIgnoreCase) < 0)
                return false;

            if (filters.GcashBarcodeOnly && block.IndexOf("GCASH BARCODE", StringComparison.OrdinalIgnoreCase) < 0)
                return false;

            if (filters.HacsOnlineOnly && block.IndexOf("HACS ONLINE", StringComparison.OrdinalIgnoreCase) < 0)
                return false;

            if (filters.XReadZReadOnly && !Regex.IsMatch(block, @"(?i)(X|Z)\s*[-]?\s*READ|(?i)(X|Z)\s*[-]?\s*REPORT"))
                return false;

            if (filters.SeniorCitizenOnly && !Regex.IsMatch(block, @"(?i)OSCA\s*(?:ID\s*)?No:"))
                return false;

            if (filters.PwdOnly && !Regex.IsMatch(block, @"(?i)PWD\s*(?:ID\s*)?No:"))
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
                string pattern = $@"(?i)(?:Member\s+)?Name:\s*.*?{Regex.Escape(filters.FilterMemberName.Trim())}";
                if (!Regex.IsMatch(block, pattern)) return false;
            }

            if (!string.IsNullOrWhiteSpace(filters.FilterScPwdId))
            {
                string pattern = $@"(?i)(?:OSCA|PWD)\s*(?:ID\s*)?No:\s*.*?{Regex.Escape(filters.FilterScPwdId.Trim())}";
                if (!Regex.IsMatch(block, pattern)) return false;
            }

            if (!string.IsNullOrWhiteSpace(filters.FilterExactAmount))
            {
                string amountEscaped = Regex.Escape(filters.FilterExactAmount.Trim());
                string pattern = $@"(?i)Total(?: Amt Due)?\s*(?:Php)?\s*{amountEscaped}(?:\s|$)";
                if (!Regex.IsMatch(block, pattern)) return false;
            }

            if (!string.IsNullOrWhiteSpace(filters.FilterProductOrSku))
            {
                if (block.IndexOf(filters.FilterProductOrSku.Trim(), StringComparison.OrdinalIgnoreCase) < 0)
                    return false;
            }

            // POS Lane Filtering
            if (filters.PosLanes.Count > 0)
            {
                string laneFromReceipt = null;

                var invoiceMatch = Regex.Match(block, @"INVOICE#\s*\d{3}-(\d{3})-\d+");
                if (invoiceMatch.Success)
                {
                    laneFromReceipt = invoiceMatch.Groups[1].Value;
                }
                else
                {
                    var transMatch = Regex.Match(block, @"Trans(?:action)?\s*#[:\s]*\d{3}(\d{3})\d+");
                    if (transMatch.Success)
                    {
                        laneFromReceipt = transMatch.Groups[1].Value;
                    }
                    else
                    {
                        var terminalMatch = Regex.Match(block, @"TERMINAL\s*#[:\s]*(\d{3})");
                        if (terminalMatch.Success)
                        {
                            laneFromReceipt = terminalMatch.Groups[1].Value;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(laneFromReceipt))
                {
                    bool laneAllowed = false;
                    foreach (string allowedLane in filters.PosLanes)
                    {
                        if (int.TryParse(allowedLane, out int allowedNum) &&
                            int.TryParse(laneFromReceipt, out int receiptNum) &&
                            allowedNum == receiptNum)
                        {
                            laneAllowed = true;
                            break;
                        }
                        else if (laneFromReceipt == allowedLane.PadLeft(3, '0'))
                        {
                            laneAllowed = true;
                            break;
                        }
                    }
                    if (!laneAllowed) return false;
                }
            }

            return true;
        }
    }
}