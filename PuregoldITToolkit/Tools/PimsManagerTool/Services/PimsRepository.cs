using PuregoldITToolkit.Tools.PimsManagerTool.Interfaces;
using PuregoldITToolkit.Tools.PimsManagerTool.Models;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace PuregoldITToolkit.Tools.PimsManagerTool.Services
{
    public class PimsRepository : IPimsRepository
    {
        private string _connectionString;

        public void SetCredentials(string server, string database, string username, string password)
        {
            _connectionString = $"Server={server};Database={database};User Id={username};Password={password};Connection Timeout=5;";
        }

        public async Task<(IEnumerable<VendorModel> Vendors, int TotalCount, string ErrorMessage)> GetVendorsPagedAsync(int pageNumber, int pageSize, string searchQuery = "")
        {
            var vendors = new List<VendorModel>();
            int totalCount = 0;

            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    string whereClause = string.IsNullOrWhiteSpace(searchQuery) ? "" : "WHERE [VENDORCD] LIKE @Search OR [VENDOR] LIKE @Search OR [SORT] LIKE @Search";

                    string countQuery = $"SELECT COUNT(*) FROM [PGBIS].[dbo].[DIM_VENDORS] {whereClause}";
                    using (SqlCommand countCmd = new SqlCommand(countQuery, conn))
                    {
                        if (!string.IsNullOrWhiteSpace(searchQuery))
                            countCmd.Parameters.AddWithValue("@Search", $"%{searchQuery}%");

                        totalCount = (int)await countCmd.ExecuteScalarAsync();
                    }

                    string pagedQuery = $@"
                        WITH PagedVendors AS (
                            SELECT 
                                [VENDORCD], 
                                [VENDOR], 
                                [SORT],
                                ROW_NUMBER() OVER (ORDER BY [VENDORCD]) AS RowNum
                            FROM [PGBIS].[dbo].[DIM_VENDORS]
                            {whereClause}
                        )
                        SELECT [VENDORCD], [VENDOR], [SORT]
                        FROM PagedVendors
                        WHERE RowNum > @Offset AND RowNum <= (@Offset + @PageSize)";

                    using (SqlCommand cmd = new SqlCommand(pagedQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@Offset", (pageNumber - 1) * pageSize);
                        cmd.Parameters.AddWithValue("@PageSize", pageSize);

                        if (!string.IsNullOrWhiteSpace(searchQuery))
                            cmd.Parameters.AddWithValue("@Search", $"%{searchQuery}%");

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                vendors.Add(new VendorModel
                                {
                                    VendorCd = reader["VENDORCD"]?.ToString(),
                                    Vendor = reader["VENDOR"]?.ToString(),
                                    Sort = reader["SORT"]?.ToString()
                                });
                            }
                        }
                    }
                }
                return (vendors, totalCount, string.Empty);
            }
            catch (SqlException ex) { return (vendors, 0, GetFriendlySqlErrorMessage(ex)); }
            catch (Exception ex) { return (vendors, 0, $"Unexpected Error: {ex.Message}"); }
        }

        public async Task<(bool Success, string ErrorMessage)> InsertVendorAsync(VendorModel vendor)
        {
            string query = "INSERT INTO [PGBIS].[dbo].[DIM_VENDORS] ([VENDORCD], [VENDOR], [SORT]) VALUES (@VendorCd, @Vendor, @Sort)";
            return await ExecuteQueryAsync(query, vendor);
        }

        public async Task<(bool Success, string ErrorMessage)> UpdateVendorAsync(VendorModel vendor)
        {
            string query = "UPDATE [PGBIS].[dbo].[DIM_VENDORS] SET [VENDOR] = @Vendor, [SORT] = @Sort WHERE [VENDORCD] = @VendorCd";
            return await ExecuteQueryAsync(query, vendor);
        }

        public async Task<(bool Success, string ErrorMessage)> DeleteVendorAsync(string vendorCd)
        {
            string query = "DELETE FROM [PGBIS].[dbo].[DIM_VENDORS] WHERE [VENDORCD] = @VendorCd";
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@VendorCd", vendorCd);
                    await conn.OpenAsync();
                    int rowsAffected = await cmd.ExecuteNonQueryAsync();

                    if (rowsAffected == 0) return (false, "Vendor Code not found in database.");
                    return (true, string.Empty);
                }
            }
            catch (SqlException ex) { return (false, GetFriendlySqlErrorMessage(ex)); }
            catch (Exception ex) { return (false, $"Unexpected Error: {ex.Message}"); }
        }
        public async Task<(bool Success, string ErrorMessage)> ResetEmployeeSalePromoAsync()
        {
            string query = @"
                USE [FreeItemsDB];
                DELETE FROM [dbo].[TBLPROMOMASTER] WHERE [CPROMOID] = '8888888888';
                INSERT INTO [dbo].[TBLPROMOMASTER] 
                    ([CPROMOID], [CPROMODESCRIPTION], [CSUPPLIERID], [DDATEFROM], [DDATETO], [IALLOCQTY], [CSTSNO])
                VALUES 
                    ('8888888888', 'SALE TO EMPLOYEE', 101, '1/1/1900', '12/31/2078', 99999999, 0);";

            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    await conn.OpenAsync();
                    await cmd.ExecuteNonQueryAsync();
                    return (true, string.Empty);
                }
            }
            catch (SqlException ex) { return (false, GetFriendlySqlErrorMessage(ex)); }
            catch (Exception ex) { return (false, $"Unexpected Error: {ex.Message}"); }
        }

        private async Task<(bool Success, string ErrorMessage)> ExecuteQueryAsync(string query, VendorModel vendor)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@VendorCd", vendor.VendorCd);
                    cmd.Parameters.AddWithValue("@Vendor", vendor.Vendor);
                    cmd.Parameters.AddWithValue("@Sort", vendor.Sort);

                    await conn.OpenAsync();
                    await cmd.ExecuteNonQueryAsync();
                    return (true, string.Empty);
                }
            }
            catch (SqlException ex) { return (false, GetFriendlySqlErrorMessage(ex)); }
            catch (Exception ex) { return (false, $"Unexpected Error: {ex.Message}"); }
        }

        private string GetFriendlySqlErrorMessage(SqlException ex)
        {
            switch (ex.Number)
            {
                case 2627:
                case 2601: return "Database Error: This Vendor Code already exists.";
                case 18456: return "Connection Error: Invalid SQL Username or Password.";
                case 53:
                case 2:
                case -2: return "Connection Error: Cannot reach the SQL Server. Check IP Address or Network connection.";
                case 208: return "Database Error: A required table was not found in the target database.";
                default: return $"SQL Error ({ex.Number}): {ex.Message}";
            }
        }
    }
}