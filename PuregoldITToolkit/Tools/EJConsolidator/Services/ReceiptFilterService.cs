using PuregoldITToolkit.Tools.EJConsolidator.Interfaces;
using PuregoldITToolkit.Tools.EJConsolidator.Models;
using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace PuregoldITToolkit.Tools.EJConsolidator.Services
{
    public class ReceiptFilterService : IReceiptFilterService
    {
        public bool IsMatch(string block, EJFilterOptions filters, string sourceLane = null) => GetMatchDetails(block, filters, sourceLane).IsMatch;

        public FilterMatchResult GetMatchDetails(string block, EJFilterOptions filters, string sourceLane = null)
        {
            if (filters == null) throw new ArgumentNullException(nameof(filters));
            var result = new FilterMatchResult { IsMatch = true };
            void Check(string name, bool matched)
            {
                result.ConditionResults[name] = matched;
                if (!matched) { result.IsMatch = false; result.FailedReasons.Add(name); }
            }
            bool Has(string value) => block.IndexOf(value.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
            bool Matches(string pattern) => Regex.IsMatch(block, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
            Check("ReceiptContent", !string.IsNullOrWhiteSpace(block));
            if (!result.IsMatch) return result;

            if (filters.IsModeTrxFinder && !string.IsNullOrWhiteSpace(filters.TargetTrxNumber))
            {
                var ids = Regex.Matches(block, @"(?:\bTrans(?:action)?\s*(?:#|No\.?|Number)?|\bINVOICE\s*#?|\bSales\s+Invoice|\bTr|\bReceipt\s*(?:No\.?|Number|#)?|\b(?:SI|OR)\s*(?:No\.?|#))\s*[:#]?\s*(\d[\d-]*)", RegexOptions.IgnoreCase);
                string wanted = filters.TargetTrxNumber.Trim();
                bool found = ids.Cast<Match>().Any(m => IdMatches(m.Groups[1].Value, wanted));
                Check("TargetTrxNumber", found);
                return result;
            }
            if (!string.IsNullOrWhiteSpace(filters.SearchText)) Check("SearchText", Has(filters.SearchText));
            if (!string.IsNullOrWhiteSpace(filters.ExcludeText)) Check("ExcludeText", !Has(filters.ExcludeText));
            if (filters.SecondReceiptOnly) Check("SecondReceiptOnly", Matches(@"\bSECOND\s+RECEIPT\b"));
            if (filters.GcashBarcodeOnly) Check("GcashBarcodeOnly", Matches(@"\bGCASH\s+BARCODE\b"));
            if (filters.HacsOnlineOnly) Check("HacsOnlineOnly", Matches(@"\bHACS\s+ONLINE\b"));
            if (filters.XReadZReadOnly) Check("XReadZReadOnly", Matches(@"\b[ZX][-\s]?(?:READ|REPORT)\b"));

            bool senior = Matches(@"\b(?:OSCA\s*(?:ID\s*)?No|SENIOR\s*(?:CITIZEN\s*)?(?:ID|DISCOUNT)|SC\s*(?:ID|DISCOUNT|Items\s+Amount))\b");
            bool pwd = Matches(@"\bPWD\s*(?:(?:ID\s*)?No|ID|DISCOUNT)\b");
            if (filters.SeniorCitizenOnly || filters.PwdOnly)
                Check("SC/PWD category", (filters.SeniorCitizenOnly && senior) || (filters.PwdOnly && pwd));

            void Field(string name, string label, string value)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    Check(name, Matches(@"\b(?:" + label + @")\s*[:#=]\s*[^\r\n]*" + Regex.Escape(value.Trim())));
            }
            Field("SpecificCashier", @"Cashier(?:\s*Name)?|Ch\.", filters.SpecificCashier);
            Field("SpecificBagger", "Bagger", filters.SpecificBagger);
            Field("FilterMemberName", @"Member\s+Name|Customer\s+Name|Name\s*of\s+Customer|Name", filters.FilterMemberName);
            Field("FilterScPwdId", @"OSCA\s*(?:ID\s*)?No|SENIOR\s*(?:CITIZEN\s*)?ID|PWD\s*(?:ID\s*)?No|SC\s*ID|PWD\s*ID", filters.FilterScPwdId);
            if (!string.IsNullOrWhiteSpace(filters.FilterCardLast4))
            {
                var cards = Regex.Matches(block, @"(?:Member\s+Card\s+Number|Card\s*(?:No\.?|Number)|Loyalty\s*Card|PC\s*Card)\s*[:#]?\s*([\dxX*• -]+)", RegexOptions.IgnoreCase);
                Check("FilterCardLast4", cards.Cast<Match>().Any(m => Regex.Replace(m.Groups[1].Value, @"\D", "").EndsWith(filters.FilterCardLast4.Trim(), StringComparison.Ordinal)));
            }
            if (!string.IsNullOrWhiteSpace(filters.FilterProductOrSku)) Check("FilterProductOrSku", Has(filters.FilterProductOrSku));
            if (!string.IsNullOrWhiteSpace(filters.PaymentMethod) && filters.PaymentMethod != "Any")
            {
                string payment = filters.PaymentMethod == "Card" ? @"(?:(?:CREDIT|DEBIT)\s+)?CARD|VISA|MASTERCARD" : Regex.Escape(filters.PaymentMethod);
                Check("PaymentMethod", Matches(@"^\s*(?:(?:Payment|Tender)(?:\s+(?:Type|Method))?\s*[:=]?\s*)?" + "(?:" + payment + @")\b"));
            }
            if (!string.IsNullOrWhiteSpace(filters.ReceiptType) && filters.ReceiptType != "Any")
            {
                bool isVoid = Matches(@"^\s*(?:(?:TRANSACTION|SALE)\s+)?VOID(?:ED)?\b|\bVOID\s+(?:TRANSACTION|SALE)\b");
                bool isReturn = Matches(@"^\s*(?:RETURN|REFUND|SALES\s+RETURN)\b");
                Check("ReceiptType", filters.ReceiptType == "Void" ? isVoid : filters.ReceiptType == "Return" ? isReturn : !isVoid && !isReturn && Matches(@"\b(?:INVOICE|Trans(?:action)?\s*#|Tr:)"));
            }

            if (!string.IsNullOrWhiteSpace(filters.FilterExactAmount) || filters.MinimumAmount.HasValue || filters.MaximumAmount.HasValue)
            {
                var amounts = Regex.Matches(block, @"^\s*(?:Grand\s+Total|Total(?:\s+(?:Amount|Amt)(?:\s+Due)?)?|Amount\s+Due|Net\s+(?:Total|Amount))\s*[:=]?\s*(?:PHP|₱|P)?\s*([+-]?[\d,]+(?:\.\d{1,2})?)\s*(?:PHP)?\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)
                    .Cast<Match>().Select(m => ParseAmount(m.Groups[1].Value)).Where(a => a.HasValue).Select(a => a.Value).ToList();
                decimal? exact = ParseAmount(filters.FilterExactAmount);
                Check("TotalAmount", amounts.Any(a => (string.IsNullOrWhiteSpace(filters.FilterExactAmount) || exact == a) &&
                    (!filters.MinimumAmount.HasValue || a >= filters.MinimumAmount.Value) && (!filters.MaximumAmount.HasValue || a <= filters.MaximumAmount.Value)));
            }
            if (filters.TimeFrom.HasValue || filters.TimeTo.HasValue)
            {
                var times = Regex.Matches(block, @"\b(?:[01]?\d|2[0-3]):[0-5]\d(?::[0-5]\d)?\s*(?:AM|PM)?\b", RegexOptions.IgnoreCase);
                Check("TimeRange", times.Cast<Match>().Any(m => DateTime.TryParse(m.Value, CultureInfo.InvariantCulture, DateTimeStyles.NoCurrentDateDefault, out var t) &&
                    (!filters.TimeFrom.HasValue || t.TimeOfDay >= filters.TimeFrom) && (!filters.TimeTo.HasValue || t.TimeOfDay <= filters.TimeTo)));
            }
            if (filters.PosLanes != null && filters.PosLanes.Count > 0)
            {
                var lane = Regex.Match(block, @"(?:INVOICE\s*#?|Sales\s+Invoice)\s*:?\s*\d+-(\d{1,3})-\d+", RegexOptions.IgnoreCase);
                if (!lane.Success) lane = Regex.Match(block, @"(?:TERMINAL|POS|Lane|Rg|Reg)\s*[:#]?\s*(\d{1,3})\b", RegexOptions.IgnoreCase);
                if (!lane.Success) lane = Regex.Match(block, @"Trans(?:action)?\s*#\s*:?\s*\d{3}(\d{3})\d+", RegexOptions.IgnoreCase);
                string actual = lane.Success ? lane.Groups[1].Value : sourceLane;
                Check("PosLanes", int.TryParse(actual, out int n) && filters.PosLanes.Any(v => int.TryParse(v, out int wanted) && wanted == n));
            }
            return result;
        }
        private static bool IdMatches(string actual, string wanted)
        {
            if (string.Equals(actual, wanted, StringComparison.OrdinalIgnoreCase)) return true;
            if (wanted.Contains("-")) return false;
            string sequence = actual.Contains("-") ? actual.Split('-').Last()
                : actual.Length > 6 ? actual.Substring(6) : actual;
            return sequence.TrimStart('0') == wanted.TrimStart('0');
        }
        public static decimal? ParseAmount(string value) => decimal.TryParse(value?.Replace(",", ""),
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal amount) ? (decimal?)amount : null;
    }
}
