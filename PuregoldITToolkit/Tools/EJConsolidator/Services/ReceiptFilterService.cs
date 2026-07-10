using PuregoldITToolkit.Tools.EJConsolidator.Interfaces;
using PuregoldITToolkit.Tools.EJConsolidator.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace PuregoldITToolkit.Tools.EJConsolidator.Services
{
    public class ReceiptFilterService : IReceiptFilterService
    {
        public bool IsMatch(string block, EJFilterOptions filters)
        {
            return GetMatchDetails(block, filters).IsMatch;
        }

        public FilterMatchResult GetMatchDetails(string block, EJFilterOptions filters)
        {
            var result = new FilterMatchResult { IsMatch = true };

            void EvaluateCondition(string conditionName, bool conditionMet)
            {
                result.ConditionResults[conditionName] = conditionMet;
                if (!conditionMet)
                {
                    result.IsMatch = false;
                    result.FailedReasons.Add(conditionName);
                }
            }

            // 1. TrxFinder Mode (Fast fail)
            if (filters.IsModeTrxFinder && !string.IsNullOrWhiteSpace(filters.TargetTrxNumber))
            {
                // Try multiple sources for Transaction Number
                string trxPattern = $@"(?i)(?:Trans(?:action)?\s*#|INVOICE#|Sales\s+Invoice|Tr:|Reg\s+\d+\s+Trans)\s*:?\s*{Regex.Escape(filters.TargetTrxNumber.Trim())}";
                bool found = Regex.IsMatch(block, trxPattern) ||
                             block.IndexOf(filters.TargetTrxNumber.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
                EvaluateCondition("TargetTrxNumber", found);
                if (!found) return result;
            }

            // 2. Simple String Contains Filters
            EvaluateCondition("SecondReceiptOnly", !filters.SecondReceiptOnly ||
                Regex.IsMatch(block, @"(?i)SECOND\s+RECEIPT"));

            EvaluateCondition("GcashBarcodeOnly", !filters.GcashBarcodeOnly ||
                Regex.IsMatch(block, @"(?i)GCASH\s+BARCODE"));

            EvaluateCondition("HacsOnlineOnly", !filters.HacsOnlineOnly ||
                Regex.IsMatch(block, @"(?i)HACS\s+ONLINE"));

            // X-Read / Z-Read (Multiple possible formats)
            EvaluateCondition("XReadZReadOnly", !filters.XReadZReadOnly ||
                Regex.IsMatch(block, @"(?i)(?:X|Z)[-\s]?(?:READ|REPORT)|CASHIER\s+X-REPORT|TERMINAL\s+Z-REPORT"));

            // 3. Senior / PWD (Multiple possible formats)
            EvaluateCondition("SeniorCitizenOnly", !filters.SeniorCitizenOnly ||
                Regex.IsMatch(block, @"(?i)OSCA\s*(?:ID\s*)?No|SENIOR\s*(?:CITIZEN\s*)?ID|SC\s*ID|Senior\s+Discount|SC\s+Discount|SC\s+Items\s+Amount"));

            EvaluateCondition("PwdOnly", !filters.PwdOnly ||
                Regex.IsMatch(block, @"(?i)PWD\s*(?:ID\s*)?No|PWD\s*No|PWD\s+Discount"));

            // 4. Specific Cashier / Bagger (Multiple possible formats)
            if (!string.IsNullOrWhiteSpace(filters.SpecificCashier))
            {
                // Matches "Cashier: NAME", "Cashier : 298 / NAME", "Ch: NAME"
                string pattern = $@"(?i)(?:Cashier|Ch|Cashier\s*Name)[^:]*:\s*(?:\d+\s*/\s*)?{Regex.Escape(filters.SpecificCashier.Trim())}";
                EvaluateCondition("SpecificCashier", Regex.IsMatch(block, pattern));
            }

            if (!string.IsNullOrWhiteSpace(filters.SpecificBagger))
            {
                string pattern = $@"(?i)Bagger[^:]*:\s*{Regex.Escape(filters.SpecificBagger.Trim())}";
                EvaluateCondition("SpecificBagger", Regex.IsMatch(block, pattern));
            }

            // 5. Advanced Filters
            if (!string.IsNullOrWhiteSpace(filters.FilterCardLast4))
            {
                // Tries: Member Card Number, Card No, Card Number, Loyalty Card
                string pattern = $@"(?i)(?:Member\s+Card\s+Number|Card\s*No|Card\s+Number|Loyalty\s*Card|PC\s*Card)[^:]*:\s*.*?{Regex.Escape(filters.FilterCardLast4.Trim())}\b";
                EvaluateCondition("FilterCardLast4", Regex.IsMatch(block, pattern));
            }

            if (!string.IsNullOrWhiteSpace(filters.FilterMemberName))
            {
                // Tries: Member Name, Customer Name, Name of Customer, Name
                string pattern = $@"(?i)(?:Member\s+Name|Customer\s+Name|Name\s*of\s+Customer|Name)\s*:\s*.*?{Regex.Escape(filters.FilterMemberName.Trim())}";
                EvaluateCondition("FilterMemberName", Regex.IsMatch(block, pattern));
            }

            if (!string.IsNullOrWhiteSpace(filters.FilterScPwdId))
            {
                // Tries: OSCA ID No, OSCA No, PWD ID No, PWD No
                string pattern = $@"(?i)(?:OSCA\s*(?:ID\s*)?No|PWD\s*(?:ID\s*)?No|SC\s*ID|PWD\s*No)\s*:\s*.*?{Regex.Escape(filters.FilterScPwdId.Trim())}";
                EvaluateCondition("FilterScPwdId", Regex.IsMatch(block, pattern));
            }

            if (!string.IsNullOrWhiteSpace(filters.FilterExactAmount))
            {
                bool amountFound = false;
                // Parse target amount to decimal to ignore comma formatting differences (e.g. 1,721.80 vs 1721.80)
                if (decimal.TryParse(filters.FilterExactAmount.Trim().Replace(",", ""), out decimal targetAmount))
                {
                    // Extract all potential total amounts from the block
                    var amountMatches = Regex.Matches(block, @"(?i)(?:Total(?:\s*Amt\s*Due)?|Amount\s*Due|Total|Cash|Amount)\s*(?:Php)?\s*([\d,]+\.?\d*)");
                    foreach (Match m in amountMatches)
                    {
                        if (decimal.TryParse(m.Groups[1].Value.Replace(",", ""), out decimal blockAmount))
                        {
                            if (blockAmount == targetAmount)
                            {
                                amountFound = true;
                                break; // Found a match, stop searching
                            }
                        }
                    }
                }
                else
                {
                    // Fallback to string match if parsing fails
                    string amountEscaped = Regex.Escape(filters.FilterExactAmount.Trim());
                    amountFound = Regex.IsMatch(block, $@"(?i)(?:Total(?:\s*Amt\s*Due)?|Amount\s*Due|Total|Cash|Amount)\s*(?:Php)?\s*{amountEscaped}(?:\s|$)");
                }
                EvaluateCondition("FilterExactAmount", amountFound);
            }

            if (!string.IsNullOrWhiteSpace(filters.FilterProductOrSku))
            {
                EvaluateCondition("FilterProductOrSku", block.IndexOf(filters.FilterProductOrSku.Trim(), StringComparison.OrdinalIgnoreCase) >= 0);
            }

            // 6. POS Lane Filtering (Checks multiple receipt sources for Lane # sequentially)
            if (filters.PosLanes.Count > 0)
            {
                string laneFromReceipt = null;

                // Source 1: INVOICE# 722-002-000000029964 OR Sales Invoice: 722-002-...
                var invoiceMatch = Regex.Match(block, @"(?i)(?:INVOICE#|Sales\s+Invoice:)\s*\d{3}-(\d{3})-\d+");
                if (invoiceMatch.Success) laneFromReceipt = invoiceMatch.Groups[1].Value;

                // Source 2: Trans # 722002000000031227 (Lane is usually the 4th to 6th digit)
                if (laneFromReceipt == null)
                {
                    var transMatch = Regex.Match(block, @"(?i)Trans(?:action)?\s*#[:\s]*\d{3}(\d{3})\d+");
                    if (transMatch.Success) laneFromReceipt = transMatch.Groups[1].Value;
                }

                // Source 3: TERMINAL #: 002
                if (laneFromReceipt == null)
                {
                    var terminalMatch = Regex.Match(block, @"(?i)TERMINAL\s*#[:\s]*(\d{3})");
                    if (terminalMatch.Success) laneFromReceipt = terminalMatch.Groups[1].Value;
                }

                // Source 4: POS # / Lane #
                if (laneFromReceipt == null)
                {
                    var posMatch = Regex.Match(block, @"(?i)(?:POS|Lane)\s*#[:\s]*(\d{3})");
                    if (posMatch.Success) laneFromReceipt = posMatch.Groups[1].Value;
                }

                // Source 5: ST: 722 | Rg: 002 | Ch: ROSABEL
                if (laneFromReceipt == null)
                {
                    var rgMatch = Regex.Match(block, @"(?i)\|\s*Rg:\s*(\d{3})\s*\|");
                    if (rgMatch.Success) laneFromReceipt = rgMatch.Groups[1].Value;
                }

                // Source 6: Reg 002 Trans ...
                if (laneFromReceipt == null)
                {
                    var regMatch = Regex.Match(block, @"(?i)Reg\s+(\d{3})\s+Trans");
                    if (regMatch.Success) laneFromReceipt = regMatch.Groups[1].Value;
                }

                bool laneAllowed = false;
                if (!string.IsNullOrEmpty(laneFromReceipt))
                {
                    foreach (string allowedLane in filters.PosLanes)
                    {
                        if (int.TryParse(allowedLane, out int allowedNum) &&
                            int.TryParse(laneFromReceipt, out int receiptNum) &&
                            allowedNum == receiptNum)
                        {
                            laneAllowed = true; break;
                        }
                        else if (laneFromReceipt == allowedLane.PadLeft(3, '0'))
                        {
                            laneAllowed = true; break;
                        }
                    }
                }

                
                EvaluateCondition("PosLanes", laneAllowed);
            }

            return result;
        }
    }
}