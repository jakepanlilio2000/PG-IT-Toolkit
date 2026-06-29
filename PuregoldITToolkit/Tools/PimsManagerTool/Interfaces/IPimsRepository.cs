using PuregoldITToolkit.Tools.PimsManagerTool.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PuregoldITToolkit.Tools.PimsManagerTool.Interfaces
{
    public interface IPimsRepository
    {
        void SetCredentials(string server, string database, string username, string password);

        Task<(IEnumerable<VendorModel> Vendors, int TotalCount, string ErrorMessage)> GetVendorsPagedAsync(int pageNumber, int pageSize, string searchQuery = "");

        Task<(bool Success, string ErrorMessage)> InsertVendorAsync(VendorModel vendor);
        Task<(bool Success, string ErrorMessage)> UpdateVendorAsync(VendorModel vendor);
        Task<(bool Success, string ErrorMessage)> DeleteVendorAsync(string vendorCd);

        Task<(bool Success, string ErrorMessage)> ResetEmployeeSalePromoAsync();
    }
}