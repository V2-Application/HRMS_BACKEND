using ClosedXML.Excel;
using HRMSAPI.Data;
using HRMSAPI.DTO;
using HRMSAPI.Interfaces;
using HRMSAPI.Models.Auth;
using HRMSAPI.Utility;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Roomsy.DTOS.GenericsResponses;
using System.Data;
using System.Globalization;
using System.Text.Json;

namespace HRMSAPI.Services
{
    public sealed class FnfService : IFnfService
    {
        private readonly HRMSContext _db;
        private readonly string _connectionString;
        private readonly ISalaryRecalculate _salaryRecalculateService;
        private readonly IEmpAttendanceViewSnapshotService _empAttendanceSnapshotService;

        public FnfService(HRMSContext db, ISalaryRecalculate recalculateService, IEmpAttendanceViewSnapshotService empattendanceService)
        {
            _db = db;
            _connectionString = _db.Database.GetConnectionString();
            _salaryRecalculateService = recalculateService;
            _empAttendanceSnapshotService = empattendanceService;
        }


        public async Task<PaginatedResponse<FnfEmployeeDropdownDto>> FetchEmployeesForFNF(string? ecode, string? globalSearch, DateTime? fromDate, DateTime? toDate, int page = 1, int pageSize = 20)
        {
            var result = new PaginatedResponse<FnfEmployeeDropdownDto>();

            // Ensure Data list is initialized
            if (result.Data == null)
                result.Data = new List<FnfEmployeeDropdownDto>();

            var conn = _db.Database.GetDbConnection();

            if (conn == null)
            {
                result.TotalRecords = 0;
                result.PageNumber = page;
                result.PageSize = pageSize;
                return result;
            }

            await conn.OpenAsync();
            try
            {
                using (var cmd = conn.CreateCommand())
                {
                    if (cmd == null)
                    {
                        result.TotalRecords = 0;
                        result.PageNumber = page;
                        result.PageSize = pageSize;
                        return result;
                    }

                    // DEBUG: Log the parameters
                    Console.WriteLine($"DEBUG API: ecode='{ecode}', globalSearch='{globalSearch}', page={page}, pageSize={pageSize}");

                    cmd.CommandText = "dbo.sp_FNF_GetEmployeesByCode";
                    cmd.CommandType = CommandType.StoredProcedure;

                    // Use existing stored procedure parameters for now
                    var p1 = cmd.CreateParameter();
                    if (p1 != null)
                    {
                        p1.ParameterName = "@SearchEcode";
                        p1.Value = (object?)ecode ?? DBNull.Value;
                        cmd.Parameters.Add(p1);
                        Console.WriteLine($"DEBUG API: Added @SearchEcode = '{ecode}'");
                    }

                    var p2 = cmd.CreateParameter();
                    if (p2 != null)
                    {
                        p2.ParameterName = "@TopRows";
                        p2.Value = DBNull.Value; // Remove the 50000 limit
                        cmd.Parameters.Add(p2);
                        Console.WriteLine($"DEBUG API: Added @TopRows = NULL");
                    }

                    // Add global search parameter if provided
                    if (!string.IsNullOrEmpty(globalSearch))
                    {
                        try
                        {
                            var pGlobalSearch = cmd.CreateParameter();
                            if (pGlobalSearch != null)
                            {
                                pGlobalSearch.ParameterName = "@GlobalSearch";
                                pGlobalSearch.Value = (object?)globalSearch ?? DBNull.Value;
                                cmd.Parameters.Add(pGlobalSearch);
                                Console.WriteLine($"DEBUG API: Added @GlobalSearch = '{globalSearch}'");
                            }
                        }
                        catch
                        {
                            // If parameter not supported, continue without it
                        }
                    }

                    // Add date range parameters if provided
                    if (fromDate.HasValue || toDate.HasValue)
                    {
                        try
                        {
                            var pFromDate = cmd.CreateParameter();
                            if (pFromDate != null)
                            {
                                pFromDate.ParameterName = "@FromDate";
                                pFromDate.Value = (object?)fromDate ?? DBNull.Value;
                                cmd.Parameters.Add(pFromDate);
                                Console.WriteLine($"DEBUG API: Added @FromDate = {fromDate}");
                            }

                            var pToDate = cmd.CreateParameter();
                            if (pToDate != null)
                            {
                                pToDate.ParameterName = "@ToDate";
                                pToDate.Value = (object?)toDate ?? DBNull.Value;
                                cmd.Parameters.Add(pToDate);
                                Console.WriteLine($"DEBUG API: Added @ToDate = {toDate}");
                            }
                        }
                        catch
                        {
                            // If parameters not supported, continue without them
                        }
                    }

                    // Try new parameters if database supports them
                    try
                    {
                        var p3 = cmd.CreateParameter();
                        if (p3 != null)
                        {
                            p3.ParameterName = "@Page";
                            p3.Value = page <= 0 ? 1 : page;
                            cmd.Parameters.Add(p3);
                            Console.WriteLine($"DEBUG API: Added @Page = {page}");
                        }

                        var p4 = cmd.CreateParameter();
                        if (p4 != null)
                        {
                            p4.ParameterName = "@PageSize";
                            p4.Value = pageSize <= 0 ? 20 : pageSize;
                            cmd.Parameters.Add(p4);
                            Console.WriteLine($"DEBUG API: Added @PageSize = {pageSize}");
                        }
                    }
                    catch
                    {
                        // If new parameters not supported, continue without them
                    }

                    using var rdr = await cmd.ExecuteReaderAsync();

                    if (rdr != null)
                    {
                        // Process all result sets
                        do
                        {
                            if (rdr.FieldCount > 0)
                            {
                                // Check if this is the total count result (new version)
                                try
                                {
                                    if (rdr.GetName(0) == "TotalCount")
                                    {
                                        if (await rdr.ReadAsync() && !rdr.IsDBNull(0))
                                        {
                                            result.TotalRecords = rdr.GetInt32(0);
                                            Console.WriteLine($"DEBUG API: TotalCount = {result.TotalRecords}");
                                        }
                                    }
                                    else
                                    {
                                        // Process employee data
                                        var recordCount = 0;
                                        while (await rdr.ReadAsync())
                                        {
                                            try
                                            {
                                                var dto = CreateEmployeeDto(rdr);
                                                if (dto != null && result.Data != null)
                                                    result.Data.Add(dto);
                                                recordCount++;
                                            }
                                            catch
                                            {
                                                // Skip problematic records
                                                continue;
                                            }
                                        }
                                        Console.WriteLine($"DEBUG API: Records returned = {recordCount}");
                                    }
                                }
                                catch
                                {
                                    // If we can't get field name, treat as employee data
                                    try
                                    {
                                        while (await rdr.ReadAsync())
                                        {
                                            try
                                            {
                                                var dto = CreateEmployeeDto(rdr);
                                                if (dto != null && result.Data != null)
                                                    result.Data.Add(dto);
                                            }
                                            catch
                                            {
                                                // Skip problematic records
                                                continue;
                                            }
                                        }
                                    }
                                    catch
                                    {
                                        // Skip this result set entirely
                                        continue;
                                    }
                                }
                            }
                        } while (await rdr.NextResultAsync());
                    }

                    // If total count wasn't set (old version), use data count
                    if (result.TotalRecords == 0 && result.Data != null)
                        result.TotalRecords = result.Data.Count;
                }
            }
            catch (Exception ex)
            {
                // Log error if needed, but don't throw
                Console.WriteLine($"Error in FetchEmployeesForFNF: {ex.Message}");

                // Ensure we have a valid result even on error
                if (result.Data == null)
                    result.Data = new List<FnfEmployeeDropdownDto>();
                result.TotalRecords = 0;
            }
            finally
            {
                if (conn != null)
                    await conn.CloseAsync();
            }

            // Final safety checks
            if (result.Data == null)
                result.Data = new List<FnfEmployeeDropdownDto>();

            // Set pagination metadata
            result.PageNumber = page <= 0 ? 1 : page;
            result.PageSize = pageSize <= 0 ? 20 : pageSize;

            return result;
        }

        private FnfEmployeeDropdownDto CreateEmployeeDto(System.Data.Common.DbDataReader rdr)
        {
            var dto = new FnfEmployeeDropdownDto();

            // Safe field access with null checks and exception handling
            if (rdr?.FieldCount > 0)
            {
                try
                {
                    // EmployeeId - check if field exists and not null
                    var employeeIdOrdinal = rdr.GetOrdinal("EmployeeId");
                    if (employeeIdOrdinal >= 0 && employeeIdOrdinal < rdr.FieldCount && !rdr.IsDBNull(employeeIdOrdinal))
                        dto.EmployeeId = rdr.GetInt64(employeeIdOrdinal);
                }
                catch { /* Skip if field doesn't exist or is invalid */ }

                try
                {
                    // EmployeeCode
                    var employeeCodeOrdinal = rdr.GetOrdinal("EmployeeCode");
                    if (employeeCodeOrdinal >= 0 && employeeCodeOrdinal < rdr.FieldCount)
                        dto.EmployeeCode = rdr.IsDBNull(employeeCodeOrdinal) ? "" : (rdr[employeeCodeOrdinal]?.ToString() ?? "");
                }
                catch { dto.EmployeeCode = ""; }

                try
                {
                    // Name
                    var nameOrdinal = rdr.GetOrdinal("Name");
                    if (nameOrdinal >= 0 && nameOrdinal < rdr.FieldCount)
                        dto.Name = rdr.IsDBNull(nameOrdinal) ? "" : (rdr[nameOrdinal]?.ToString() ?? "");
                }
                catch { dto.Name = ""; }

                try
                {
                    // ResignationType
                    var resignationTypeOrdinal = rdr.GetOrdinal("ResignationType");
                    if (resignationTypeOrdinal >= 0 && resignationTypeOrdinal < rdr.FieldCount)
                        dto.ResignationType = rdr.IsDBNull(resignationTypeOrdinal) ? "" : (rdr[resignationTypeOrdinal]?.ToString() ?? "");
                }
                catch { dto.ResignationType = ""; }

                try
                {
                    // Department
                    var departmentOrdinal = rdr.GetOrdinal("Department");
                    if (departmentOrdinal >= 0 && departmentOrdinal < rdr.FieldCount)
                        dto.Department = rdr.IsDBNull(departmentOrdinal) ? "" : (rdr[departmentOrdinal]?.ToString() ?? "");
                }
                catch { dto.Department = ""; }

                try
                {
                    // Designation
                    var designationOrdinal = rdr.GetOrdinal("Designation");
                    if (designationOrdinal >= 0 && designationOrdinal < rdr.FieldCount)
                        dto.Designation = rdr.IsDBNull(designationOrdinal) ? "" : (rdr[designationOrdinal]?.ToString() ?? "");
                }
                catch { dto.Designation = ""; }

                try
                {
                    // DateOfLeaving
                    var dateOfLeavingOrdinal = rdr.GetOrdinal("DateOfLeaving");
                    if (dateOfLeavingOrdinal >= 0 && dateOfLeavingOrdinal < rdr.FieldCount && !rdr.IsDBNull(dateOfLeavingOrdinal))
                        dto.DateOfLeaving = rdr.GetDateTime(dateOfLeavingOrdinal);
                }
                catch { /* Keep null */ }

                try
                {
                    // DateOfJoining - map from DOJ field from stored procedure
                    var dateOfJoiningOrdinal = rdr.GetOrdinal("DOJ");
                    if (dateOfJoiningOrdinal >= 0 && dateOfJoiningOrdinal < rdr.FieldCount && !rdr.IsDBNull(dateOfJoiningOrdinal))
                        dto.DateOfJoining = rdr.GetDateTime(dateOfJoiningOrdinal);
                }
                catch { /* Keep null */ }

                try
                {
                    // IsFNFCompleted
                    var isFNFCompletedOrdinal = rdr.GetOrdinal("IsFNFCompleted");
                    if (isFNFCompletedOrdinal >= 0 && isFNFCompletedOrdinal < rdr.FieldCount && !rdr.IsDBNull(isFNFCompletedOrdinal))
                        dto.IsFNFCompleted = rdr.GetBoolean(isFNFCompletedOrdinal);
                }
                catch { /* Keep null */ }

                try
                {
                    // UnpaidSalaryAmount
                    var unpaidSalaryAmountOrdinal = rdr.GetOrdinal("UnpaidSalaryAmount");
                    if (unpaidSalaryAmountOrdinal >= 0 && unpaidSalaryAmountOrdinal < rdr.FieldCount && !rdr.IsDBNull(unpaidSalaryAmountOrdinal))
                        dto.UnpaidSalaryAmount = rdr.GetDecimal(unpaidSalaryAmountOrdinal);
                }
                catch { /* Keep null */ }

                try
                {
                    // UnpaidSalaryDays
                    var unpaidSalaryDaysOrdinal = rdr.GetOrdinal("UnpaidSalaryDays");
                    if (unpaidSalaryDaysOrdinal >= 0 && unpaidSalaryDaysOrdinal < rdr.FieldCount && !rdr.IsDBNull(unpaidSalaryDaysOrdinal))
                        dto.UnpaidSalaryDays = rdr.GetInt32(unpaidSalaryDaysOrdinal);
                }
                catch { /* Keep null */ }

                try
                {
                    // UnpaidSalaryMonth
                    var unpaidSalaryMonthOrdinal = rdr.GetOrdinal("UnpaidSalaryMonth");
                    if (unpaidSalaryMonthOrdinal >= 0 && unpaidSalaryMonthOrdinal < rdr.FieldCount)
                        dto.UnpaidSalaryMonth = rdr.IsDBNull(unpaidSalaryMonthOrdinal) ? null : rdr[unpaidSalaryMonthOrdinal]?.ToString();
                }
                catch { dto.UnpaidSalaryMonth = null; }

                try
                {
                    // ResignationAttachment
                    var resignationAttachmentOrdinal = rdr.GetOrdinal("ResignationAttachment");
                    if (resignationAttachmentOrdinal >= 0 && resignationAttachmentOrdinal < rdr.FieldCount)
                        dto.ResignationAttachment = rdr.IsDBNull(resignationAttachmentOrdinal) ? null : rdr[resignationAttachmentOrdinal]?.ToString();
                }
                catch { dto.ResignationAttachment = null; }
            }

            return dto;
        }

        public async Task<FnfIdResponse> SaveAdditionsAsync(FnfAdditionsDto dto)
        {
            var conn = _db.Database.GetDbConnection();
            await conn.OpenAsync();
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "dbo.sp_FNF_SaveAdditions";
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.Add(new SqlParameter("@EmployeeId", dto.EmployeeId));
                cmd.Parameters.Add(new SqlParameter("@FNFDate", (object?)dto.FNFDate ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@DateOfLeaving", (object?)dto.DateOfLeaving ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@UnpaidSalaryAmount", (object?)dto.UnpaidSalaryAmount ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@Rate", (object?)dto.Rate ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@Days", (object?)dto.Days ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@SalaryMonth", (object?)dto.SalaryMonth ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@Bonus", (object?)dto.Bonus ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@BonusPeriodFrom", (object?)dto.BonusPeriodFrom ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@BonusPeriodTill", (object?)dto.BonusPeriodTill ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@Gratuity", (object?)dto.Gratuity ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@CalculatedAs", (object?)dto.CalculatedAs ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@E_LeaveAmount", (object?)dto.E_LeaveAmount ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@ELDays", (object?)dto.ELDays ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@NoticeSalary", (object?)dto.NoticeSalary ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@OtherAddition1", (object?)dto.OtherAddition1 ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@OtherAddition2", (object?)dto.OtherAddition2 ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@OtherAddition3", (object?)dto.OtherAddition3 ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@OtherAddition4", (object?)dto.OtherAddition4 ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@User", (object?)dto.User ?? DBNull.Value));

                using var rdr = await cmd.ExecuteReaderAsync();
                long fnfId = 0;
                if (await rdr.ReadAsync()) fnfId = rdr.GetInt64(rdr.GetOrdinal("FNFId"));
                return new FnfIdResponse { FNFId = fnfId };
            }
            finally { await conn.CloseAsync(); }
        }

        public async Task<FnfIdResponse> SaveDeductionsAsync(FnfDeductionsDto dto)
        {
            var conn = _db.Database.GetDbConnection();
            await conn.OpenAsync();
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "dbo.sp_FNF_SaveDeductions";
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.Add(new SqlParameter("@EmployeeId", dto.EmployeeId));
                cmd.Parameters.Add(new SqlParameter("@LoanBalance", (object?)dto.LoanBalance ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@AdvanceBalance", (object?)dto.AdvanceBalance ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@OtherDeduction1", (object?)dto.OtherDeduction1 ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@OtherDeduction2", (object?)dto.OtherDeduction2 ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@OtherDeduction3", (object?)dto.OtherDeduction3 ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@OtherDeduction4", (object?)dto.OtherDeduction4 ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@TotalPayable", (object?)dto.TotalPayable ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@TDS", (object?)dto.TDS ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@NetPayable", (object?)dto.NetPayable ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@DepositOn", (object?)dto.DepositOn ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@User", (object?)dto.User ?? DBNull.Value));

                using var rdr = await cmd.ExecuteReaderAsync();
                long fnfId = 0;
                if (await rdr.ReadAsync()) fnfId = rdr.GetInt64(rdr.GetOrdinal("FNFId"));
                return new FnfIdResponse { FNFId = fnfId };
            }
            finally { await conn.CloseAsync(); }
        }

        public async Task<PaymentIdResponse> SavePaymentAsync(FnfPaymentDto dto)
        {
            var conn = _db.Database.GetDbConnection();
            await conn.OpenAsync();
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "dbo.sp_FNF_SavePayment";
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.Add(new SqlParameter("@FNFId", dto.FNFId));
                cmd.Parameters.Add(new SqlParameter("@SendForPaymentAmount", (object?)dto.SendForPaymentAmount ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@Remarks", (object?)dto.Remarks ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@ChequeNo", (object?)dto.ChequeNo ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@ChequeDate", (object?)dto.ChequeDate ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@Status", (object?)dto.Status ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@AmountPaid", (object?)dto.AmountPaid ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@PaymentVoucherNo", (object?)dto.PaymentVoucherNo ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@CreatedBy", (object?)dto.CreatedBy ?? DBNull.Value));

                var scalar = await cmd.ExecuteScalarAsync();
                return new PaymentIdResponse { PaymentId = Convert.ToInt64(scalar) };
            }
            finally { await conn.CloseAsync(); }
        }


        public async Task<FnfSaveAllResponse> SaveAllAsync(FnfSaveAllDto dto)
        {
            var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "dbo.sp_FNF_SaveAll";
                cmd.CommandType = CommandType.StoredProcedure;

                void Add(string name, object? val) =>
                    cmd.Parameters.Add(new SqlParameter(name, val ?? DBNull.Value));

                Add("@EmployeeId", dto.EmployeeId);
                Add("@User", dto.User);

                Add("@FNFDate", dto.FNFDate);
                Add("@DateOfLeaving", dto.DateOfLeaving);
                Add("@UnpaidSalaryAmount", dto.UnpaidSalaryAmount);
                Add("@Rate", dto.Rate);
                Add("@Days", dto.Days);
                Add("@SalaryMonth", dto.SalaryMonth);
                Add("@Bonus", dto.Bonus);
                Add("@BonusPeriodFrom", dto.BonusPeriodFrom);
                Add("@BonusPeriodTill", dto.BonusPeriodTill);
                Add("@Gratuity", dto.Gratuity);
                Add("@CalculatedAs", dto.CalculatedAs);
                Add("@E_LeaveAmount", dto.E_LeaveAmount);
                Add("@ELDays", dto.ELDays);
                Add("@NoticeSalary", dto.NoticeSalary);
                Add("@OtherAddition1", dto.OtherAddition1);
                Add("@OtherAddition2", dto.OtherAddition2);
                Add("@OtherAddition3", dto.OtherAddition3);
                Add("@OtherAddition4", dto.OtherAddition4);

                Add("@LoanBalance", dto.LoanBalance);
                Add("@AdvanceBalance", dto.AdvanceBalance);
                Add("@OtherDeduction1", dto.OtherDeduction1);
                Add("@OtherDeduction2", dto.OtherDeduction2);
                Add("@OtherDeduction3", dto.OtherDeduction3);
                Add("@OtherDeduction4", dto.OtherDeduction4);
                Add("@TotalPayable", dto.TotalPayable);
                Add("@TDS", dto.TDS);
                Add("@NetPayable", dto.NetPayable);
                Add("@DepositOn", dto.DepositOn);

                Add("@SendForPaymentAmount", dto.SendForPaymentAmount);
                Add("@Remarks", dto.Remarks);
                Add("@ChequeNo", dto.ChequeNo);
                Add("@ChequeDate", dto.ChequeDate);
                Add("@Status", dto.Status);
                Add("@AmountPaid", dto.AmountPaid);
                Add("@PaymentVoucherNo", dto.PaymentVoucherNo);

                long fnfId = 0;
                using var rdr = await cmd.ExecuteReaderAsync();
                if (await rdr.ReadAsync())
                    fnfId = rdr.GetInt64(rdr.GetOrdinal("FNFId"));

                return new FnfSaveAllResponse { FNFId = fnfId };
            }
            finally { await conn.CloseAsync(); await conn.DisposeAsync(); }
        }
        public async Task<FnfAccountsListResponseDto> GetAccountsListAsync(string? search, DateTime? from, DateTime? to, string? paymentStatus, int page, int pageSize)
        {
            var conn = _db.Database.GetDbConnection();
            await conn.OpenAsync();
            try
            {
                var result = new FnfAccountsListResponseDto();

                // First result: total count
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "dbo.sp_FNF_GetAccountsList";
                cmd.CommandType = CommandType.StoredProcedure;

                void Add(string n, object? v) => cmd.Parameters.Add(new SqlParameter(n, v ?? DBNull.Value));
                Add("@Search", search);
                Add("@FromDate", from);
                Add("@ToDate", to);
                Add("@PaymentStatus", paymentStatus);
                Add("@Page", page <= 0 ? 1 : page);
                Add("@PageSize", pageSize <= 0 ? 20 : pageSize);

                using var rdr = await cmd.ExecuteReaderAsync();

                if (await rdr.ReadAsync())
                    result.TotalCount = rdr.GetInt32(rdr.GetOrdinal("TotalCount"));

                // Move to second result set (paged rows)
                if (await rdr.NextResultAsync())
                {
                    while (await rdr.ReadAsync())
                    {
                        var item = new FnfAccountsListItemDto
                        {
                            FNFId = rdr.GetInt64(rdr.GetOrdinal("FNFId")),
                            EmployeeId = rdr.GetInt64(rdr.GetOrdinal("EmployeeId")),
                            Ecode = rdr["Ecode"] as string ?? "",
                            EmployeeName = rdr["EmployeeName"] as string ?? "",
                            FNFDate = rdr.IsDBNull(rdr.GetOrdinal("FNFDate")) ? (DateTime?)null : rdr.GetDateTime(rdr.GetOrdinal("FNFDate")),
                            DateOfLeaving = rdr.IsDBNull(rdr.GetOrdinal("DateOfLeaving")) ? (DateTime?)null : rdr.GetDateTime(rdr.GetOrdinal("DateOfLeaving")),
                            TotalAdditions = rdr.GetDecimal(rdr.GetOrdinal("TotalAdditions")),
                            TotalDeductions = rdr.GetDecimal(rdr.GetOrdinal("TotalDeductions")),
                            NetAmount = rdr.GetDecimal(rdr.GetOrdinal("NetAmount")),
                            SendForPaymentAmount = rdr.IsDBNull(rdr.GetOrdinal("SendForPaymentAmount")) ? (decimal?)null : rdr.GetDecimal(rdr.GetOrdinal("SendForPaymentAmount")),
                            AmountPaid = rdr.IsDBNull(rdr.GetOrdinal("AmountPaid")) ? (decimal?)null : rdr.GetDecimal(rdr.GetOrdinal("AmountPaid")),
                            PaymentStatus = rdr["PaymentStatus"] as string,
                            ChequeNo = rdr["ChequeNo"] as string,
                            ChequeDate = rdr.IsDBNull(rdr.GetOrdinal("ChequeDate")) ? (DateTime?)null : rdr.GetDateTime(rdr.GetOrdinal("ChequeDate")),
                            PaymentVoucherNo = rdr["PaymentVoucherNo"] as string,
                            PaymentRemarks = rdr["PaymentRemarks"] as string,
                            UnPaidSalary = rdr.IsDBNull(rdr.GetOrdinal("UnpaidSalaryAmount")) ? (decimal?)null : rdr.GetDecimal(rdr.GetOrdinal("UnpaidSalaryAmount")),
                            LastMonth = rdr.IsDBNull(rdr.GetOrdinal("Month-Year")) ? null : rdr.GetString(rdr.GetOrdinal("Month-Year")),
                            Rate = rdr.IsDBNull(rdr.GetOrdinal("Rate")) ? (decimal?)null : rdr.GetDecimal(rdr.GetOrdinal("Rate")),
                            Bonus = rdr.IsDBNull(rdr.GetOrdinal("Bonus")) ? (decimal?)null : rdr.GetDecimal(rdr.GetOrdinal("Bonus")),
                            Gratuity = rdr.IsDBNull(rdr.GetOrdinal("Gratuity")) ? (decimal?)null : rdr.GetDecimal(rdr.GetOrdinal("Gratuity")),
                            NoticeSalary = rdr.IsDBNull(rdr.GetOrdinal("NoticeSalary")) ? (decimal?)null : rdr.GetDecimal(rdr.GetOrdinal("NoticeSalary")),
                            PayableDays = rdr.IsDBNull(rdr.GetOrdinal("paybledays")) ? (decimal?)null : rdr.GetDecimal(rdr.GetOrdinal("paybledays")),
                            AdvanceBalance = rdr.IsDBNull(rdr.GetOrdinal("AdvanceBalance")) ? (decimal?)null : rdr.GetDecimal(rdr.GetOrdinal("AdvanceBalance")),
                            TDS = rdr.IsDBNull(rdr.GetOrdinal("TDS")) ? (decimal?)null : rdr.GetDecimal(rdr.GetOrdinal("TDS")),
                            PF = rdr.IsDBNull(rdr.GetOrdinal("PF")) ? null : rdr.GetString(rdr.GetOrdinal("PF")),
                            ESIC = rdr.IsDBNull(rdr.GetOrdinal("ESIC")) ? null : rdr.GetString(rdr.GetOrdinal("ESIC")),
                            Designation = rdr.IsDBNull(rdr.GetOrdinal("DesignationName")) ? null : rdr.GetString(rdr.GetOrdinal("DesignationName")),
                            PanNo = rdr.IsDBNull(rdr.GetOrdinal("PanNo")) ? null : rdr.GetString(rdr.GetOrdinal("PanNo")),
                            DateOfJoining = rdr.IsDBNull(rdr.GetOrdinal("JoiningDate")) ? (DateTime?)null : rdr.GetDateTime(rdr.GetOrdinal("JoiningDate")),
                            Department = rdr.IsDBNull(rdr.GetOrdinal("DepartmentName")) ? null : rdr.GetString(rdr.GetOrdinal("DepartmentName")),
                            Location = rdr.IsDBNull(rdr.GetOrdinal("LocationName")) ? null : rdr.GetString(rdr.GetOrdinal("LocationName")),
                            BankName = rdr.IsDBNull(rdr.GetOrdinal("BankName")) ? null : rdr.GetString(rdr.GetOrdinal("BankName")),
                            IFSC = rdr.IsDBNull(rdr.GetOrdinal("IFSC")) ? null : rdr.GetString(rdr.GetOrdinal("IFSC")),
                            AccountNo = rdr.IsDBNull(rdr.GetOrdinal("AccountNo")) ? null : rdr.GetString(rdr.GetOrdinal("AccountNo")),
                            PTax = rdr.IsDBNull(rdr.GetOrdinal("PTax")) ? null : rdr.GetString(rdr.GetOrdinal("PTax")),
                            BonusPeriodFrom = rdr.IsDBNull(rdr.GetOrdinal("BonusPeriodFrom")) ? (DateTime?)null : rdr.GetDateTime(rdr.GetOrdinal("BonusPeriodFrom")),
                            BonusPeriodTill = rdr.IsDBNull(rdr.GetOrdinal("BonusPeriodTill")) ? (DateTime?)null : rdr.GetDateTime(rdr.GetOrdinal("BonusPeriodTill")),
                        };
                        result.Items.Add(item);
                    }
                }

                return result;
            }
            finally { await conn.CloseAsync(); }
        }

        public async Task<FnfAccountsListResponseDto> GetProcessedListAsync(string? search, DateTime? from, DateTime? to, string? paymentStatus, int page, int pageSize)
        {
            var conn = _db.Database.GetDbConnection();
            await conn.OpenAsync();
            try
            {
                var result = new FnfAccountsListResponseDto();

                // First result: total count
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "dbo.sp_FNF_GetAccountsList_Unpaid";
                cmd.CommandType = CommandType.StoredProcedure;

                void Add(string n, object? v) => cmd.Parameters.Add(new SqlParameter(n, v ?? DBNull.Value));
                Add("@Search", search);
                Add("@FromDate", from);
                Add("@ToDate", to);
                Add("@PaymentStatus", paymentStatus);
                Add("@Page", page <= 0 ? 1 : page);
                Add("@PageSize", pageSize <= 0 ? 20 : pageSize);

                using var rdr = await cmd.ExecuteReaderAsync();

                if (await rdr.ReadAsync())
                    result.TotalCount = rdr.GetInt32(rdr.GetOrdinal("TotalCount"));

                // Move to second result set (paged rows)
                if (await rdr.NextResultAsync())
                {
                    while (await rdr.ReadAsync())
                    {
                        var item = new FnfAccountsListItemDto
                        {
                            FNFId = rdr.GetInt64(rdr.GetOrdinal("FNFId")),
                            EmployeeId = rdr.GetInt64(rdr.GetOrdinal("EmployeeId")),
                            Ecode = rdr["Ecode"] as string ?? "",
                            EmployeeName = rdr["EmployeeName"] as string ?? "",
                            FNFDate = rdr.IsDBNull(rdr.GetOrdinal("FNFDate")) ? (DateTime?)null : rdr.GetDateTime(rdr.GetOrdinal("FNFDate")),
                            DateOfLeaving = rdr.IsDBNull(rdr.GetOrdinal("DateOfLeaving")) ? (DateTime?)null : rdr.GetDateTime(rdr.GetOrdinal("DateOfLeaving")),
                            TotalAdditions = rdr.GetDecimal(rdr.GetOrdinal("TotalAdditions")),
                            TotalDeductions = rdr.GetDecimal(rdr.GetOrdinal("TotalDeductions")),
                            NetAmount = rdr.GetDecimal(rdr.GetOrdinal("NetAmount")),
                            SendForPaymentAmount = rdr.IsDBNull(rdr.GetOrdinal("SendForPaymentAmount")) ? (decimal?)null : rdr.GetDecimal(rdr.GetOrdinal("SendForPaymentAmount")),
                            AmountPaid = rdr.IsDBNull(rdr.GetOrdinal("AmountPaid")) ? (decimal?)null : rdr.GetDecimal(rdr.GetOrdinal("AmountPaid")),
                            PaymentStatus = rdr["PaymentStatus"] as string,
                            ChequeNo = rdr["ChequeNo"] as string,
                            ChequeDate = rdr.IsDBNull(rdr.GetOrdinal("ChequeDate")) ? (DateTime?)null : rdr.GetDateTime(rdr.GetOrdinal("ChequeDate")),
                            PaymentVoucherNo = rdr["PaymentVoucherNo"] as string,
                            PaymentRemarks = rdr["PaymentRemarks"] as string,
                            UnPaidSalary = rdr.IsDBNull(rdr.GetOrdinal("UnpaidSalaryAmount")) ? (decimal?)null : rdr.GetDecimal(rdr.GetOrdinal("UnpaidSalaryAmount")),
                            LastMonth = rdr.IsDBNull(rdr.GetOrdinal("Month-Year")) ? null : rdr.GetString(rdr.GetOrdinal("Month-Year")),
                            Rate = rdr.IsDBNull(rdr.GetOrdinal("Rate")) ? (decimal?)null : rdr.GetDecimal(rdr.GetOrdinal("Rate")),
                            Bonus = rdr.IsDBNull(rdr.GetOrdinal("Bonus")) ? (decimal?)null : rdr.GetDecimal(rdr.GetOrdinal("Bonus")),
                            Gratuity = rdr.IsDBNull(rdr.GetOrdinal("Gratuity")) ? (decimal?)null : rdr.GetDecimal(rdr.GetOrdinal("Gratuity")),
                            NoticeSalary = rdr.IsDBNull(rdr.GetOrdinal("NoticeSalary")) ? (decimal?)null : rdr.GetDecimal(rdr.GetOrdinal("NoticeSalary")),
                            PayableDays = rdr.IsDBNull(rdr.GetOrdinal("paybledays")) ? (decimal?)null : rdr.GetDecimal(rdr.GetOrdinal("paybledays")),
                            AdvanceBalance = rdr.IsDBNull(rdr.GetOrdinal("AdvanceBalance")) ? (decimal?)null : rdr.GetDecimal(rdr.GetOrdinal("AdvanceBalance")),
                            TDS = rdr.IsDBNull(rdr.GetOrdinal("TDS")) ? (decimal?)null : rdr.GetDecimal(rdr.GetOrdinal("TDS")),
                            PF = rdr.IsDBNull(rdr.GetOrdinal("PF")) ? null : rdr.GetString(rdr.GetOrdinal("PF")),
                            ESIC = rdr.IsDBNull(rdr.GetOrdinal("ESIC")) ? null : rdr.GetString(rdr.GetOrdinal("ESIC")),
                            Designation = rdr.IsDBNull(rdr.GetOrdinal("DesignationName")) ? null : rdr.GetString(rdr.GetOrdinal("DesignationName")),
                            PanNo = rdr.IsDBNull(rdr.GetOrdinal("PanNo")) ? null : rdr.GetString(rdr.GetOrdinal("PanNo")),
                            DateOfJoining = rdr.IsDBNull(rdr.GetOrdinal("JoiningDate")) ? (DateTime?)null : rdr.GetDateTime(rdr.GetOrdinal("JoiningDate")),
                            Department = rdr.IsDBNull(rdr.GetOrdinal("DepartmentName")) ? null : rdr.GetString(rdr.GetOrdinal("DepartmentName")),
                            Location = rdr.IsDBNull(rdr.GetOrdinal("LocationName")) ? null : rdr.GetString(rdr.GetOrdinal("LocationName")),
                            BankName = rdr.IsDBNull(rdr.GetOrdinal("BankName")) ? null : rdr.GetString(rdr.GetOrdinal("BankName")),
                            IFSC = rdr.IsDBNull(rdr.GetOrdinal("IFSC")) ? null : rdr.GetString(rdr.GetOrdinal("IFSC")),
                            AccountNo = rdr.IsDBNull(rdr.GetOrdinal("AccountNo")) ? null : rdr.GetString(rdr.GetOrdinal("AccountNo")),
                            PTax = rdr.IsDBNull(rdr.GetOrdinal("PTax")) ? null : rdr.GetString(rdr.GetOrdinal("PTax")),
                            BonusPeriodFrom = rdr.IsDBNull(rdr.GetOrdinal("BonusPeriodFrom")) ? (DateTime?)null : rdr.GetDateTime(rdr.GetOrdinal("BonusPeriodFrom")),
                            BonusPeriodTill = rdr.IsDBNull(rdr.GetOrdinal("BonusPeriodTill")) ? (DateTime?)null : rdr.GetDateTime(rdr.GetOrdinal("BonusPeriodTill")),
                        };
                        result.Items.Add(item);
                    }
                }

                return result;
            }
            finally { await conn.CloseAsync(); }
        }

        // Returns which FNF tab an exact ecode belongs to: "pending" | "processed" | "completed" | null (not found).
        // pending  = employee has no FNF_Header yet
        // processed = FNF created but still in the unpaid view
        // completed = FNF present and not in the unpaid view (paid/done)
        public async Task<string?> LocateTabByEcodeAsync(string ecode)
        {
            if (string.IsNullOrWhiteSpace(ecode)) return null;
            var trimmed = ecode.Trim();

            var conn = _db.Database.GetDbConnection();
            await conn.OpenAsync();
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
DECLARE @eid BIGINT = (SELECT TOP 1 EmployeeId FROM dbo.tblEmployee WHERE Ecode = @ecode ORDER BY EmployeeId DESC);
IF @eid IS NULL
    SELECT CAST(NULL AS varchar(20)) AS Tab;
ELSE IF NOT EXISTS (SELECT 1 FROM dbo.FNF_Header WHERE EmployeeId = @eid)
    SELECT 'pending' AS Tab;
ELSE IF EXISTS (SELECT 1 FROM dbo.vw_FNF_AccountsList_Unpaid WHERE Ecode = @ecode)
    SELECT 'processed' AS Tab;
ELSE
    SELECT 'completed' AS Tab;";
                var p = cmd.CreateParameter();
                p.ParameterName = "@ecode";
                p.Value = trimmed;
                cmd.Parameters.Add(p);

                var result = await cmd.ExecuteScalarAsync();
                return result == null || result == DBNull.Value ? null : result.ToString();
            }
            finally { await conn.CloseAsync(); }
        }

        public async Task<(List<Dictionary<string, object>>, Dictionary<string, object>?)> CalculateBonusAsync(BonusCalcRequestDto dto)
        {
            var rows = new List<Dictionary<string, object>>();
            Dictionary<string, object>? totals = null;

            var conn = _db.Database.GetDbConnection();
            await conn.OpenAsync();
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "dbo.sp_FNF_CalculateBonus";
                cmd.CommandType = CommandType.StoredProcedure;

                void Add(string n, object? v) => cmd.Parameters.Add(new SqlParameter(n, v ?? DBNull.Value));
                Add("@EmployeeId", dto.EmployeeId);
                Add("@FromDate", dto.FromDate);
                Add("@ToDate", dto.ToDate);
                Add("@BonusRatePct", dto.BonusRatePct);
                Add("@MinWorkedDays", dto.MinWorkedDays);
                Add("@Basic", dto.Basic);
                Add("@DA", dto.DA);
                Add("@HRA", dto.HRA);
                Add("@Conveyance", dto.Conveyance);
                Add("@CCA", dto.CCA);
                Add("@MedicalAllowance", dto.MedicalAllowance);
                Add("@Incentive", dto.Incentive);
                Add("@FoodingAllowance", dto.FoodingAllowance);
                Add("@SpecialAllowance", dto.SpecialAllowance);
                Add("@ExtraAllowance", dto.ExtraAllowance);
                Add("@LeaveEncashment", dto.LeaveEncashment);
                Add("@MedicalReim", dto.MedicalReim);
                Add("@LTA", dto.LTA);
                Add("@BonusExGratia", dto.BonusExGratia);
                Add("@Arrears", dto.Arrears);

                using var rdr = await cmd.ExecuteReaderAsync();

                // first result set: per-month rows
                while (await rdr.ReadAsync())
                {
                    var dict = Enumerable.Range(0, rdr.FieldCount)
                        .ToDictionary(rdr.GetName, i => rdr.IsDBNull(i) ? null! : rdr.GetValue(i));
                    rows.Add(dict);
                }

                // second result set: totals
                if (await rdr.NextResultAsync() && await rdr.ReadAsync())
                {
                    totals = Enumerable.Range(0, rdr.FieldCount)
                        .ToDictionary(rdr.GetName, i => rdr.IsDBNull(i) ? null! : rdr.GetValue(i));
                }

                return (rows, totals);
            }
            finally { await conn.CloseAsync(); }
        }

        public async Task<Dictionary<string, object>> CalculateLeaveEncashmentAsync(LeaveEncashmentRequestDto dto)
        {
            var conn = _db.Database.GetDbConnection();
            await conn.OpenAsync();
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "dbo.sp_FNF_CalcLeaveEncashment";
                cmd.CommandType = CommandType.StoredProcedure;

                void Add(string n, object? v) => cmd.Parameters.Add(new SqlParameter(n, v ?? DBNull.Value));

                Add("@Ecode", dto.Ecode);
                Add("@FromDate", dto.FromDate);
                Add("@ToDate", dto.ToDate);
                Add("@OneLeaveNumberOfDays", dto.OneLeaveNumberOfDays);
                Add("@DivideByDays", dto.DivideByDays);
                Add("@ELDaysOverride", dto.ELDaysOverride);
                Add("@Basic", dto.Basic);
                Add("@DA", dto.DA);
                Add("@HRA", dto.HRA);
                Add("@Conveyance", dto.Conveyance);
                Add("@CCA", dto.CCA);
                Add("@MedicalAllowance", dto.MedicalAllowance);
                Add("@SpecialAllowance", dto.SpecialAllowance);
                Add("@ExtraAllowance", dto.ExtraAllowance);

                using var rdr = await cmd.ExecuteReaderAsync();

                if (!await rdr.ReadAsync())
                    return new Dictionary<string, object>(); // empty if not found

                var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < rdr.FieldCount; i++)
                    dict[rdr.GetName(i)] = rdr.IsDBNull(i) ? null! : rdr.GetValue(i);

                return dict;
            }
            finally { await conn.CloseAsync(); }
        }

        public async Task<Dictionary<string, object>> CalculateGratuityAsync(GratuityRequestDto dto)
        {
            var conn = _db.Database.GetDbConnection();
            await conn.OpenAsync();
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "dbo.sp_FNF_CalcGratuity";
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add(new SqlParameter("@Ecode", (object?)dto.Ecode ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@EmployeeId", (object?)dto.EmployeeId ?? DBNull.Value));

                using var rdr = await cmd.ExecuteReaderAsync();
                await rdr.ReadAsync();
                return Enumerable.Range(0, rdr.FieldCount)
                    .ToDictionary(rdr.GetName, i => rdr.IsDBNull(i) ? null! : rdr.GetValue(i));
            }
            finally { await conn.CloseAsync(); }
        }

        public async Task<FnfBulkUploadResponseDto> BulkUploadAsync(FnfBulkUploadRequestDto request)
        {
            var response = new FnfBulkUploadResponseDto { Success = true };

            // Validate data before sending to stored procedure
            foreach (var row in request.Rows)
            {
                // Check if any date fields have invalid values
                if (row.FNFDate.HasValue && row.FNFDate.Value == DateTime.MinValue)
                    row.FNFDate = null;
                if (row.DateOfLeaving.HasValue && row.DateOfLeaving.Value == DateTime.MinValue)
                    row.DateOfLeaving = null;
                if (row.BonusPeriodFrom.HasValue && row.BonusPeriodFrom.Value == DateTime.MinValue)
                    row.BonusPeriodFrom = null;
                if (row.BonusPeriodTill.HasValue && row.BonusPeriodTill.Value == DateTime.MinValue)
                    row.BonusPeriodTill = null;
                if (row.ChequeDate.HasValue && row.ChequeDate.Value == DateTime.MinValue)
                    row.ChequeDate = null;
            }

            var json = JsonConvert.SerializeObject(request.Rows, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                DateFormatString = "yyyy-MM-dd"
            });

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("dbo.sp_FNF_BulkUpload", conn);
            cmd.CommandType = CommandType.StoredProcedure;

            // Add input parameters
            cmd.Parameters.AddWithValue("@JsonData", json);
            cmd.Parameters.AddWithValue("@CreatedBy", request.User ?? "System");

            // Add output parameters
            var duplicateEcodesParam = new SqlParameter("@DuplicateEcodes", SqlDbType.NVarChar, -1)
            {
                Direction = ParameterDirection.Output
            };
            var alreadyDoneEcodesParam = new SqlParameter("@AlreadyDoneEcodes", SqlDbType.NVarChar, -1)
            {
                Direction = ParameterDirection.Output
            };
            var processedCountParam = new SqlParameter("@ProcessedCount", SqlDbType.Int)
            {
                Direction = ParameterDirection.Output
            };
            var totalRecordsParam = new SqlParameter("@TotalRecords", SqlDbType.Int)
            {
                Direction = ParameterDirection.Output
            };
            var updatedCountParam = new SqlParameter("@UpdatedCount", SqlDbType.Int)
            {
                Direction = ParameterDirection.Output
            };

            cmd.Parameters.Add(duplicateEcodesParam);
            cmd.Parameters.Add(alreadyDoneEcodesParam);
            cmd.Parameters.Add(processedCountParam);
            cmd.Parameters.Add(totalRecordsParam);
            cmd.Parameters.Add(updatedCountParam);

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();

            // Get output values
            response.ProcessedCount = processedCountParam.Value as int? ?? 0;
            response.UpdatedCount = updatedCountParam.Value as int? ?? 0;
            response.TotalRecords = totalRecordsParam.Value as int? ?? 0;

            // Parse JSON arrays to lists
            var duplicateEcodesJson = duplicateEcodesParam.Value as string;
            if (!string.IsNullOrEmpty(duplicateEcodesJson))
            {
                try
                {
                    var duplicateObjects = JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(duplicateEcodesJson);
                    response.DuplicateEcodes = duplicateObjects?.Select(x => x["Ecode"]).ToList() ?? new List<string>();
                }
                catch
                {
                    // If parsing fails, try simple string parsing
                    response.DuplicateEcodes = new List<string> { duplicateEcodesJson };
                }
            }

            var alreadyDoneEcodesJson = alreadyDoneEcodesParam.Value as string;
            if (!string.IsNullOrEmpty(alreadyDoneEcodesJson))
            {
                try
                {
                    var alreadyDoneObjects = JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(alreadyDoneEcodesJson);
                    response.AlreadyDoneEcodes = alreadyDoneObjects?.Select(x => x["Ecode"]).ToList() ?? new List<string>();
                }
                catch
                {
                    // If parsing fails, try simple string parsing
                    response.AlreadyDoneEcodes = new List<string> { alreadyDoneEcodesJson };
                }
            }

            // ---- Build the skipped/duplicate rows (for the "download duplicates" feature) ----
            // Reasons: "Already completed" (FNF already paid), "Duplicate in file" (ecode repeated
            // in the sheet), "Unknown Ecode" (not found in employee master).
            var dupSet = new HashSet<string>(response.DuplicateEcodes ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            var doneSet = new HashSet<string>(response.AlreadyDoneEcodes ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            var inFileDupes = request.Rows
                .GroupBy(r => (r.Ecode ?? "").Trim(), StringComparer.OrdinalIgnoreCase)
                .Where(g => !string.IsNullOrEmpty(g.Key) && g.Count() > 1)
                .Select(g => g.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var row in request.Rows)
            {
                var ec = (row.Ecode ?? "").Trim();
                if (string.IsNullOrEmpty(ec)) continue;

                string? reason = null;
                if (doneSet.Contains(ec)) reason = "Already completed";
                else if (dupSet.Contains(ec))
                    reason = inFileDupes.Contains(ec) ? "Duplicate in file" : "Unknown Ecode (not in employee master)";

                if (reason == null) continue;

                response.DuplicateRows.Add(new FnfDuplicateRowDto
                {
                    Ecode = ec,
                    Reason = reason,
                    TotalPayable = row.TotalPayable,
                    NetPayable = row.NetPayable,
                    PaymentStatus = row.PaymentStatus,
                    ChequeNo = row.ChequeNo,
                    PaymentVoucherNo = row.PaymentVoucherNo,
                    PaymentRemarks = row.PaymentRemarks
                });
            }

            // ---- Message ----
            var didWork = response.ProcessedCount > 0 || response.UpdatedCount > 0;
            if (didWork)
            {
                response.Success = true;
                response.Message =
                    $"Processed {response.TotalRecords} record(s): {response.ProcessedCount} new, " +
                    $"{response.UpdatedCount} updated to Completed.";

                if (response.AlreadyDoneEcodes.Any())
                    response.Message += $" {response.AlreadyDoneEcodes.Count} already completed (skipped).";
                if (response.DuplicateEcodes.Any())
                    response.Message += $" {response.DuplicateEcodes.Count} duplicate/unknown Ecode(s) skipped.";
            }
            else
            {
                response.Success = false;
                response.Message = "No records were processed.";

                if (response.AlreadyDoneEcodes.Any())
                    response.Message += $" All records already had FNF completed: {string.Join(", ", response.AlreadyDoneEcodes)}";
                else if (response.DuplicateEcodes.Any())
                    response.Message += $" All records were duplicate/unknown Ecodes: {string.Join(", ", response.DuplicateEcodes)}";
            }

            return response;
        }


        //public async Task<bool> BulkUploadFromExcelAsync(IFormFile file, string user)
        //{
        //    if (file == null || file.Length == 0)
        //        throw new ArgumentException("No file uploaded");

        //    using var stream = file.OpenReadStream();
        //    using var workbook = new XLWorkbook(stream);
        //    var worksheet = workbook.Worksheet(1);

        //    var firstRow = true;

        //    // Prepare connection for potentially multiple SP calls
        //    var conn = _db.Database.GetDbConnection();
        //    if (conn.State != ConnectionState.Open)
        //        await conn.OpenAsync();

        //    try 
        //    {
        //        foreach (var row in worksheet.Rows())
        //        {
        //            if (firstRow)
        //            {
        //                firstRow = false;
        //                continue; // Skip header row
        //            }

        //            if (row.Cell(1).IsEmpty()) break; // Stop at empty Ecode

        //            var ecode = row.Cell(1).GetString();
        //            if (string.IsNullOrWhiteSpace(ecode)) continue;

        //            // 1. Get EmployeeId from Ecode
        //            var emp = await _db.tblEmployees
        //                .Where(e => e.Ecode == ecode)
        //                .Select(e => new { e.EmployeeId })
        //                .FirstOrDefaultAsync();

        //            if (emp == null) continue; // Skip if employee not found

        //            // 2. Fetch Calculated Details from SP
        //            sp_FNF_GetFnfDetailsByEcodeResult? calcDetails = null;

        //            try 
        //            {
        //                 var result = await _db.Database
        //                    .SqlQueryRaw<sp_FNF_GetFnfDetailsByEcodeResult>(
        //                        "EXEC [dbo].[sp_FNF_GetFnfDetailsByEcode] @Ecode = {0}",
        //                        ecode)
        //                    .ToListAsync();

        //                calcDetails = result.FirstOrDefault();
        //            }
        //            catch 
        //            {
        //                // process without calculated details or log error? 
        //                // For now we continue, relying on Excel or defaults
        //            }

        //            // 3. Map to SaveAllDto
        //            // Priorities: Excel Input > Calculated Value > Default

        //            var dto = new FnfSaveAllDto
        //            {
        //                EmployeeId = emp.EmployeeId,
        //                User = user,

        //                // Dates
        //                FNFDate = GetDateTimeValue(row.Cell(2)) ?? DateTime.Now,
        //                DateOfLeaving = GetDateTimeValue(row.Cell(3)) ?? calcDetails?.LastDay,

        //                // Earnings (Calculated mostly)
        //                UnpaidSalaryAmount = GetDecimalValue(row.Cell(4)) ?? calcDetails?.UnpaidAmount, // Prefer Excel, then SP
        //                Rate = GetDecimalValue(row.Cell(5)) ?? calcDetails?.Rate,
        //                Days = (int?)GetDecimalValue(row.Cell(6)) ?? (int?)calcDetails?.LastPunchMonthDays, // Cast decimal days to int if needed
        //                SalaryMonth = row.Cell(7).GetString() is string sm && !string.IsNullOrEmpty(sm) ? sm : calcDetails?.LastPunchMonth,

        //                // Bonus
        //                Bonus = GetDecimalValue(row.Cell(8)) ?? calcDetails?.FinalBonus,
        //                // Excel dates for bonus period take precedence. 
        //                // SP returns string "Month-Year", difficult to parse reliably without specific logic, 
        //                // so we rely on Excel or leave null if not in Excel.
        //                BonusPeriodFrom = GetDateTimeValue(row.Cell(9)), 
        //                BonusPeriodTill = GetDateTimeValue(row.Cell(10)),

        //                // Gratuity
        //                Gratuity = GetDecimalValue(row.Cell(11)) ?? calcDetails?.GratuityAmount,
        //                CalculatedAs = row.Cell(12).GetString(), // Often manual text

        //                // Leave Encashment
        //                E_LeaveAmount = GetDecimalValue(row.Cell(13)) ?? calcDetails?.EarnedLeaveAmount,
        //                ELDays = (int?)GetDecimalValue(row.Cell(14)) ?? (int?)calcDetails?.EarnedLeaveDays,

        //                // Other Additions (Excel only usually)
        //                NoticeSalary = GetDecimalValue(row.Cell(15)), 
        //                OtherAddition1 = GetDecimalValue(row.Cell(16)),
        //                OtherAddition2 = GetDecimalValue(row.Cell(17)),
        //                OtherAddition3 = GetDecimalValue(row.Cell(18)),
        //                OtherAddition4 = GetDecimalValue(row.Cell(19)),

        //                // Deductions (Excel mainly as per user request)
        //                LoanBalance = GetDecimalValue(row.Cell(20)),
        //                AdvanceBalance = GetDecimalValue(row.Cell(21)),
        //                OtherDeduction1 = GetDecimalValue(row.Cell(22)),
        //                OtherDeduction2 = GetDecimalValue(row.Cell(23)),
        //                OtherDeduction3 = GetDecimalValue(row.Cell(24)),
        //                OtherDeduction4 = GetDecimalValue(row.Cell(25)),

        //                // Totals (Optional, usually recalculated by SP if sent as null? 
        //                // SaveAll SP updates these. We can send what we have.)
        //                TotalPayable = GetDecimalValue(row.Cell(26)),
        //                TDS = GetDecimalValue(row.Cell(27)),
        //                NetPayable = GetDecimalValue(row.Cell(28)),
        //                DepositOn = GetDecimalValue(row.Cell(29)),

        //                // Payment Details
        //                SendForPaymentAmount = GetDecimalValue(row.Cell(30)),
        //                AmountPaid = GetDecimalValue(row.Cell(31)),
        //                Status = row.Cell(32).GetString() is string s && !string.IsNullOrEmpty(s) ? s : "PENDING",
        //                ChequeNo = row.Cell(33).GetString(),
        //                ChequeDate = GetDateTimeValue(row.Cell(34)),
        //                PaymentVoucherNo = row.Cell(35).GetString(),
        //                Remarks = row.Cell(36).GetString() is string rem && !string.IsNullOrEmpty(rem) ? rem : calcDetails?.Remarks
        //            };

        //            // 4. Save
        //            await SaveAllAsync(dto);
        //        }
        //    }
        //    finally
        //    {
        //        if (conn.State == ConnectionState.Open)
        //            await conn.CloseAsync();
        //    }

        //    return true;
        //}

        //By Gautam
        public async Task<bool> BulkUploadFromExcelAsync(IFormFile file, string user)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("No file uploaded");

            using var stream = file.OpenReadStream();
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheet(1);

            var rows = worksheet.RowsUsed().Skip(1).ToList(); // Skip header once

            if (!rows.Any())
                return true;

            // 🔹 1️⃣ Get all ecodes from excel first
            var ecodes = rows
                .Select(r => r.Cell(1).GetString()?.Trim())
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Distinct()
                .ToList();

            // 🔹 2️⃣ Fetch all employees in one DB call
            var employeeDict = await _db.tblEmployees
                .Where(e => ecodes.Contains(e.Ecode))
                .Select(e => new { e.EmployeeId, e.Ecode })
                .ToDictionaryAsync(e => e.Ecode, e => e.EmployeeId);

            foreach (var row in rows)
            {
                var ecode = row.Cell(1).GetString()?.Trim();
                if (string.IsNullOrWhiteSpace(ecode))
                    continue;

                if (!employeeDict.TryGetValue(ecode, out var employeeId))
                    continue;

                try
                {
                    // 🔹 3️⃣ Call SP (optional: can optimize further if needed)
                    sp_FNF_GetFnfDetailsByEcodeResult? calcDetails = null;
                    var result = await _db.Database.SqlQueryRaw<sp_FNF_GetFnfDetailsByEcodeResult>
                        ("EXEC [dbo].[sp_FNF_GetFnfDetailsByEcode] @Ecode = {0}", ecode).ToListAsync();

                    calcDetails = result.FirstOrDefault();
                    var dto = MapRowToDto(row, employeeId, user, calcDetails);

                    var salaryRecalculateDTO = new SalaryRecalculateDto() { ECodes = ecode, Month = calcDetails?.LastDay?.ToString("MMM-yy") };
                    var recalculateResponse = await SalaryRecalculateNew(salaryRecalculateDTO);

                    var empAttendanceSnapshotResponse = await _empAttendanceSnapshotService.GetEligibleEmployeesFastAsync(ecode, calcDetails?.LastDay?.ToString("MMM-yy"));

                    var _snapshotrow = (empAttendanceSnapshotResponse?.Data as List<Dictionary<string, object>>)?.FirstOrDefault();

                    if (_snapshotrow != null)
                    {
                        dto.UnpaidSalaryAmount = (calcDetails?.UnpaidAmount ?? 0) + (_snapshotrow.TryGetValue("Monthly_Gross_CTC_Actual_After_Deduction_AND_AddONS_", out var netObj) && netObj != null
                                            ? Convert.ToDecimal(netObj)
                                            : 0);
                        dto.ELDays = _snapshotrow.TryGetValue("EarnedLeaveBalance", out var elObj) && elObj != null
                                    ? Convert.ToInt32(elObj)
                                    : 0;

                        dto.Days = _snapshotrow.TryGetValue("payableDays", out var pdObj) && pdObj != null
                            ? Convert.ToInt32(pdObj)
                            : (_snapshotrow.TryGetValue("paybledays", out var pdObj2) && pdObj2 != null ? Convert.ToInt32(pdObj2) : 0);
                    }

                    await SaveAllAsync(dto);
                }
                catch
                {
                    continue;
                }
            }

            return true;
        }

        private async Task<ExecuteAndReponse> SalaryRecalculateNew(SalaryRecalculateDto obj)
        {
            try
            {
                // 1. Validate ECodes
                if (string.IsNullOrWhiteSpace(obj.ECodes))
                {
                    return new ExecuteAndReponse
                    {
                        Status = false,
                        Message = "Ecodes cannot be empty."
                    };
                }

                // 2. Validate Month format (MMM-YY)
                if (!DateTime.TryParseExact(obj.Month, "MMM-yy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedMonth))
                {
                    return new ExecuteAndReponse
                    {
                        Status = false,
                        Message = "Month must be in format MMM-YY (e.g., Jul-25)."
                    };
                }

                // 2.a Disallow future months (including future years)
                var currentMonthStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                var parsedMonthStart = new DateTime(parsedMonth.Year, parsedMonth.Month, 1);
                if (parsedMonthStart > currentMonthStart)
                {
                    return new ExecuteAndReponse
                    {
                        Status = false,
                        Message = "Month cannot be in the future.",
                        Code = System.Net.HttpStatusCode.BadRequest
                    };
                }

                // Execute stored procedure
                var ecodeList = obj.ECodes
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(e => e.Trim())
            .ToList();

                if (ecodeList.Count == 0)
                {
                    return new ExecuteAndReponse
                    {
                        Status = false,
                        Message = "No valid ECodes found.",
                        Code = System.Net.HttpStatusCode.BadRequest
                    };
                }

                // 3. Validate that all ECodes exist in tblEmployees (case-insensitive)
                var existingEcodes = await _db.tblEmployees
                    .AsNoTracking()
                    .Select(e => e.Ecode)
                    .ToListAsync();

                var existingSet = new HashSet<string>(existingEcodes.Where(x => x != null).Select(x => x.Trim()), StringComparer.OrdinalIgnoreCase);
                var missingEcodes = ecodeList.Where(e => !existingSet.Contains(e)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (missingEcodes.Any())
                {
                    return new ExecuteAndReponse
                    {
                        Status = false,
                        Message = $"These ECodes do not exist: {string.Join(", ", missingEcodes)}"
                    };
                }
                var skippedMessage = new OutputParameter<string>();
                var result = await _db.GetProcedures().prc_runecode_iterate_New_DevAsync(obj.Month, obj.ECodes, skippedMessage);

                // Call procedure for each ECode
                //foreach (var ecode in ecodeList)
                //{
                //    await _context.GetProcedures()
                //        .prc_runecode_iterate_wrapper_PT_LWFAsync(ecode, obj.Month);
                //}

                return new ExecuteAndReponse
                {
                    Status = true,
                    Message = $"Executed Successfully. {skippedMessage.Value}"
                };
            }
            catch (Exception ex)
            {
                return new ExecuteAndReponse
                {
                    Status = false,
                    Message = ex.Message,
                };
            }
        }

        public async Task<FnfBulkUploadResponseDto> BulkUploadProcessedFromExcelAsync(IFormFile file, string user)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("No file uploaded");

            var response = new FnfBulkUploadResponseDto { Success = true };

            using var stream = file.OpenReadStream();
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheet(1);

            // Read the header row to map column names to their indices
            var headerRow = worksheet.Row(1);
            var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var cell in headerRow.CellsUsed())
            {
                var headerValue = cell.GetString()?.Trim();
                if (!string.IsNullOrEmpty(headerValue))
                {
                    headerMap[headerValue] = cell.Address.ColumnNumber;
                }
            }

            // Helper functions to get value by header name safely
            string? GetCellValueByHeader(IXLRow row, string headerName)
            {
                if (headerMap.TryGetValue(headerName, out int colIndex))
                {
                    var val = row.Cell(colIndex).GetString()?.Trim();
                    return string.IsNullOrEmpty(val) ? null : val;
                }
                return null;
            }

            decimal? GetDecimalValueByHeader(IXLRow row, string headerName)
            {
                var strValue = GetCellValueByHeader(row, headerName);
                if (decimal.TryParse(strValue, out decimal result))
                {
                    return result;
                }
                return null;
            }

            var rows = worksheet.RowsUsed().Skip(1).ToList(); // Skip header once
            response.TotalRecords = rows.Count;

            if (!rows.Any())
            {
                response.Message = "No records found in Excel";
                return response;
            }

            // 🔹 1️⃣ Get all ecodes from excel first
            var ecodes = rows
                .Select(r => GetCellValueByHeader(r, "Ecode") ?? r.Cell(1).GetString()?.Trim())
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Distinct()
                .ToList();

            // 🔹 2️⃣ Fetch all employees in one DB call
            var employeeDict = await _db.tblEmployees
                .Where(e => ecodes.Contains(e.Ecode))
                .Select(e => new { e.EmployeeId, e.Ecode })
                .ToDictionaryAsync(e => e.Ecode, e => e.EmployeeId);

            foreach (var row in rows)
            {
                var ecode = GetCellValueByHeader(row, "Ecode") ?? row.Cell(1).GetString()?.Trim();
                if (string.IsNullOrWhiteSpace(ecode))
                {
                    response.ErrorMessages.Add($"Row {row.RowNumber()}: Ecode is empty.");
                    continue;
                }

                if (!employeeDict.TryGetValue(ecode, out var employeeId))
                {
                    response.ErrorMessages.Add($"Row {row.RowNumber()}: Employee with Ecode {ecode} not found.");
                    continue;
                }

                try
                {
                    // 🔹 3️⃣ Call SP with manual connection to avoid shared context corruption
                    var calcDetails = await GetFnfDetailsByEcodeManualAsync(ecode);

                    if (calcDetails == null)
                    {
                        response.ErrorMessages.Add($"Row {row.RowNumber()} (Ecode {ecode}): Record not found in FNF details calculation.");
                        continue;
                    }

                    var _rate = calcDetails.Rate;
                    var _bonus = calcDetails.FinalBonus;
                    var _gratuity = calcDetails.GratuityAmount;

                   
                    var salaryRecalculateDTO = new SalaryRecalculateDto() { ECodes = ecode, Month = calcDetails.LastDay?.ToString("MMM-yy") };
                    var recalculateResponse = await SalaryRecalculateNew(salaryRecalculateDTO);

                    var empAttendanceSnapshotResponse = await _empAttendanceSnapshotService.GetEligibleEmployeesFastAsync(ecode, calcDetails.LastDay?.ToString("MMM-yy"));

                    var _snapshotrow = (empAttendanceSnapshotResponse?.Data as List<Dictionary<string, object>>)?.FirstOrDefault();

                    decimal _newUnpaidSalary = 0;
                    decimal elDays = 0;
                    decimal payableDays = 0;
                    decimal basicSalary = 0;

                    if (_snapshotrow != null)
                    {
                        if (_snapshotrow.TryGetValue("Monthly_Gross_CTC_Actual_After_Deduction_AND_AddONS_", out var objNet) && objNet != null)
                            _newUnpaidSalary = Convert.ToDecimal(objNet);

                        if (_snapshotrow.TryGetValue("EarnedLeaveBalance", out var objEl) && objEl != null)
                            elDays = Convert.ToDecimal(objEl);

                        if (_snapshotrow.TryGetValue("payableDays", out var objPd) && objPd != null)
                            payableDays = Convert.ToDecimal(objPd);
                        else if (_snapshotrow.TryGetValue("paybledays", out var objPd2) && objPd2 != null)
                            payableDays = Convert.ToDecimal(objPd2);

                        if (_snapshotrow.TryGetValue("BasicSalary_Bud_", out var objBasic) && objBasic != null)
                            basicSalary = Convert.ToDecimal(objBasic);
                    }

                    decimal originalUnpaidSalary = calcDetails.UnpaidAmount ?? 0;
                    decimal _newTotalSalary = originalUnpaidSalary + _newUnpaidSalary;
                    decimal _newElAmount = Math.Floor((basicSalary / 30m) * elDays);

                    var _otherAddition1 = GetDecimalValueByHeader(row, "OtherAddition1");
                    var _otherAddition2 = GetDecimalValueByHeader(row, "OtherAddition2");
                    var _otherAddition3 = GetDecimalValueByHeader(row, "OtherAddition3");
                    var _otherAddition4 = GetDecimalValueByHeader(row, "OtherAddition4");

                    var _otherDeduction1 = GetDecimalValueByHeader(row, "OtherDeduction1");
                    var _otherDeduction2 = GetDecimalValueByHeader(row, "OtherDeduction2");
                    var _otherDeduction3 = GetDecimalValueByHeader(row, "OtherDeduction3");
                    var _otherDeduction4 = GetDecimalValueByHeader(row, "OtherDeduction4");
                    
                    var _advanceBalance = GetDecimalValueByHeader(row, "AdvanceBalance") ?? GetDecimalValueByHeader(row, "Advance Balance");
                    var _loanBalance = GetDecimalValueByHeader(row, "LoanBalance") ?? GetDecimalValueByHeader(row, "Loan Balance");
                    var _tds = GetDecimalValueByHeader(row, "TDS");

                    var _totalPayable = 
                        ((_bonus ?? 0) + (_gratuity ?? 0) + _newElAmount + _newTotalSalary + (_otherAddition1 ?? 0) + (_otherAddition2 ?? 0) + (_otherAddition3 ?? 0) + (_otherAddition4 ?? 0)) 
                        - ((_otherDeduction1 ?? 0) + (_otherDeduction2 ?? 0) + (_otherDeduction3 ?? 0) + (_otherDeduction4 ?? 0) + (_tds ?? 0) + (_advanceBalance ?? 0) + (_loanBalance ?? 0));
                    var _netPayable = _totalPayable;

                    var dto = new FnfSaveAllDto
                    {
                        EmployeeId = employeeId,
                        User = user,

                        FNFDate = DateTime.Now,
                        DateOfLeaving = calcDetails.LastDay,

                        UnpaidSalaryAmount = _newTotalSalary,
                        Rate = calcDetails.Rate,
                        Days = Convert.ToInt32(payableDays),
                        SalaryMonth = calcDetails.LastPunchMonth,

                        Bonus = calcDetails.FinalBonus,
                        BonusPeriodFrom = null,
                        BonusPeriodTill = null,

                        Gratuity = calcDetails.GratuityAmount,
                        CalculatedAs = null,

                        E_LeaveAmount = _newElAmount,
                        ELDays = Convert.ToInt32(elDays),

                        NoticeSalary = null,
                        OtherAddition1 = _otherAddition1,
                        OtherAddition2 = _otherAddition2,
                        OtherAddition3 = _otherAddition3,
                        OtherAddition4 = _otherAddition4,

                        LoanBalance = _loanBalance,
                        AdvanceBalance = _advanceBalance,
                        OtherDeduction1 = _otherDeduction1,
                        OtherDeduction2 = _otherDeduction2,
                        OtherDeduction3 = _otherDeduction3,
                        OtherDeduction4 = _otherDeduction4,

                        TotalPayable = _totalPayable,
                        TDS = _tds,
                        NetPayable = _netPayable,
                        DepositOn = null,

                        SendForPaymentAmount = null,
                        AmountPaid = null,
                        Status = "PENDING",

                        ChequeNo = null,
                        ChequeDate = null,
                        PaymentVoucherNo = null,
                        Remarks = null
                    };

                    await SaveAllAsync(dto);
                    response.ProcessedCount++;
                }
                catch (Exception ex)
                {
                    response.ErrorMessages.Add($"Row {row.RowNumber()} (Ecode {ecode}): {ex.Message}");
                    continue;
                }
            }

            response.Message = $"Processed {response.ProcessedCount} of {response.TotalRecords} records.";
            if (response.ErrorMessages.Any())
            {
                response.Message += $" {response.ErrorMessages.Count} records failed.";
            }

            return response;
        }

        private async Task<sp_FNF_GetFnfDetailsByEcodeByGautamResult?> GetFnfDetailsByEcodeManualAsync(string ecode)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "dbo.sp_FNF_GetFnfDetailsByEcodeByGautam";
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.Add(new SqlParameter("@Ecode", ecode));

            using var rdr = await cmd.ExecuteReaderAsync();
            if (await rdr.ReadAsync())
            {
                return new sp_FNF_GetFnfDetailsByEcodeByGautamResult
                {
                    Ecode = rdr["Ecode"] as string ?? string.Empty,
                    EmployeeName = rdr["EmployeeName"] as string ?? string.Empty,
                    DOJ = rdr.IsDBNull(rdr.GetOrdinal("DOJ")) ? (DateTime?)null : rdr.GetDateTime(rdr.GetOrdinal("DOJ")),
                    LastDay = rdr.IsDBNull(rdr.GetOrdinal("LastDay")) ? (DateTime?)null : rdr.GetDateTime(rdr.GetOrdinal("LastDay")),
                    NoticePeriod = rdr.IsDBNull(rdr.GetOrdinal("NoticePeriod")) ? (decimal?)null : rdr.GetDecimal(rdr.GetOrdinal("NoticePeriod")),
                    ResignationTypeName = rdr["ResignationTypeName"] as string ?? string.Empty,
                    ResignationDate = rdr.IsDBNull(rdr.GetOrdinal("ResignationDate")) ? (DateTime?)null : rdr.GetDateTime(rdr.GetOrdinal("ResignationDate")),
                    Remarks = rdr["Remarks"] as string ?? string.Empty,
                    LastPunchMonth = rdr["LastPunchMonth"] as string ?? string.Empty,
                    LastPunchMonthDays = rdr.IsDBNull(rdr.GetOrdinal("LastPunchMonthDays")) ? (decimal?)null : rdr.GetDecimal(rdr.GetOrdinal("LastPunchMonthDays")),
                    Rate = rdr.IsDBNull(rdr.GetOrdinal("Rate")) ? (decimal?)null : rdr.GetDecimal(rdr.GetOrdinal("Rate")),
                    EarnedLeaveDays = rdr.IsDBNull(rdr.GetOrdinal("EarnedLeaveDays")) ? (decimal?)null : rdr.GetDecimal(rdr.GetOrdinal("EarnedLeaveDays")),
                    EarnedLeaveAmount = rdr.IsDBNull(rdr.GetOrdinal("EarnedLeaveAmount")) ? (decimal?)null : rdr.GetDecimal(rdr.GetOrdinal("EarnedLeaveAmount")),
                    UnpaidAmount = rdr.IsDBNull(rdr.GetOrdinal("UnpaidAmount")) ? (decimal?)null : rdr.GetDecimal(rdr.GetOrdinal("UnpaidAmount")),
                    FinalBonus = rdr.IsDBNull(rdr.GetOrdinal("FinalBonus")) ? (decimal?)null : rdr.GetDecimal(rdr.GetOrdinal("FinalBonus")),
                    BonusStartMonth = rdr["BonusStartMonth"] as string ?? string.Empty,
                    BonusEndMonth = rdr["BonusEndMonth"] as string ?? string.Empty,
                    BonusRemarks = rdr["BonusRemarks"] as string ?? string.Empty,
                    YearsServed = rdr.IsDBNull(rdr.GetOrdinal("YearsServed")) ? (decimal?)null : rdr.GetDecimal(rdr.GetOrdinal("YearsServed")),
                    GratuityAmount = rdr.IsDBNull(rdr.GetOrdinal("GratuityAmount")) ? (decimal?)null : rdr.GetDecimal(rdr.GetOrdinal("GratuityAmount")),
                    ResignationAttachment = rdr["ResignationAttachment"] as string ?? string.Empty
                };
            }
            return null;
        }

        private FnfSaveAllDto MapRowToDto(IXLRow row, long employeeId, string user, sp_FNF_GetFnfDetailsByEcodeResult? calcDetails)
        {
            return new FnfSaveAllDto
            {
                EmployeeId = employeeId,
                User = user,

                FNFDate = GetDateTimeValue(row.Cell(2)) ?? DateTime.Now,
                DateOfLeaving = GetDateTimeValue(row.Cell(3)) ?? calcDetails?.LastDay,

                UnpaidSalaryAmount = GetDecimalValue(row.Cell(4)) ?? calcDetails?.UnpaidAmount,
                Rate = GetDecimalValue(row.Cell(5)) ?? calcDetails?.Rate,
                Days = (int?)GetDecimalValue(row.Cell(6)) ?? (int?)calcDetails?.LastPunchMonthDays,
                SalaryMonth = !string.IsNullOrWhiteSpace(row.Cell(7).GetString())
                                ? row.Cell(7).GetString()
                                : calcDetails?.LastPunchMonth,

                Bonus = GetDecimalValue(row.Cell(8)) ?? calcDetails?.FinalBonus,
                BonusPeriodFrom = GetDateTimeValue(row.Cell(9)),
                BonusPeriodTill = GetDateTimeValue(row.Cell(10)),

                Gratuity = GetDecimalValue(row.Cell(11)) ?? calcDetails?.GratuityAmount,
                CalculatedAs = row.Cell(12).GetString(),

                E_LeaveAmount = GetDecimalValue(row.Cell(13)) ?? calcDetails?.EarnedLeaveAmount,
                ELDays = (int?)GetDecimalValue(row.Cell(14)) ?? (int?)calcDetails?.EarnedLeaveDays,

                NoticeSalary = GetDecimalValue(row.Cell(15)),
                OtherAddition1 = GetDecimalValue(row.Cell(16)),
                OtherAddition2 = GetDecimalValue(row.Cell(17)),
                OtherAddition3 = GetDecimalValue(row.Cell(18)),
                OtherAddition4 = GetDecimalValue(row.Cell(19)),

                LoanBalance = GetDecimalValue(row.Cell(20)),
                AdvanceBalance = GetDecimalValue(row.Cell(21)),
                OtherDeduction1 = GetDecimalValue(row.Cell(22)),
                OtherDeduction2 = GetDecimalValue(row.Cell(23)),
                OtherDeduction3 = GetDecimalValue(row.Cell(24)),
                OtherDeduction4 = GetDecimalValue(row.Cell(25)),

                TotalPayable = GetDecimalValue(row.Cell(26)),
                TDS = GetDecimalValue(row.Cell(27)),
                NetPayable = GetDecimalValue(row.Cell(28)),
                DepositOn = GetDecimalValue(row.Cell(29)),

                SendForPaymentAmount = GetDecimalValue(row.Cell(30)),
                AmountPaid = GetDecimalValue(row.Cell(31)),
                Status = !string.IsNullOrWhiteSpace(row.Cell(32).GetString())
                            ? row.Cell(32).GetString()
                            : "PENDING",

                ChequeNo = row.Cell(33).GetString(),
                ChequeDate = GetDateTimeValue(row.Cell(34)),
                PaymentVoucherNo = row.Cell(35).GetString(),
                Remarks = !string.IsNullOrWhiteSpace(row.Cell(36).GetString())
                            ? row.Cell(36).GetString()
                            : calcDetails?.Remarks
            };
        }

        public async Task<FnfBulkUploadResponseDto> UploadCompletedFNFExcelAsync(IFormFile file, string user)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("No file uploaded");

            using var stream = file.OpenReadStream();
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheet(1);

            var rows = new List<FnfBulkUploadRowDto>();
            var validationErrors = new List<string>();

            // Only Ecode and Remarks are mandatory; every other field is optional.
            foreach (var row in worksheet.RowsUsed().Skip(1)) // skip header
            {
                var ecode = row.Cell(1).GetString()?.Trim();
                var remarks = row.Cell(36).GetString()?.Trim();

                // Fully blank row -> ignore silently
                if (string.IsNullOrWhiteSpace(ecode) && string.IsNullOrWhiteSpace(remarks) && row.IsEmpty())
                    continue;

                if (string.IsNullOrWhiteSpace(ecode))
                {
                    validationErrors.Add($"Row {row.RowNumber()}: Ecode is required.");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(remarks))
                {
                    validationErrors.Add($"Row {row.RowNumber()} (Ecode {ecode}): Remarks is required.");
                    continue;
                }

                var fnfRow = new FnfBulkUploadRowDto
                {
                    Ecode = ecode,
                    FNFDate = GetDateTimeValue(row.Cell(2)),
                    DateOfLeaving = GetDateTimeValue(row.Cell(3)),
                    UnpaidSalaryAmount = GetDecimalValue(row.Cell(4)),
                    Rate = GetDecimalValue(row.Cell(5)),
                    Days = GetDecimalValue(row.Cell(6)),
                    SalaryMonth = row.Cell(7).GetString(),
                    Bonus = GetDecimalValue(row.Cell(8)),
                    BonusPeriodFrom = GetDateTimeValue(row.Cell(9)),
                    BonusPeriodTill = GetDateTimeValue(row.Cell(10)),
                    Gratuity = GetDecimalValue(row.Cell(11)),
                    CalculatedAs = row.Cell(12).GetString(),
                    E_LeaveAmount = GetDecimalValue(row.Cell(13)),
                    ELDays = GetDecimalValue(row.Cell(14)),
                    NoticeSalary = GetDecimalValue(row.Cell(15)),
                    OtherAddition1 = GetDecimalValue(row.Cell(16)),
                    OtherAddition2 = GetDecimalValue(row.Cell(17)),
                    OtherAddition3 = GetDecimalValue(row.Cell(18)),
                    OtherAddition4 = GetDecimalValue(row.Cell(19)),
                    LoanBalance = GetDecimalValue(row.Cell(20)),
                    AdvanceBalance = GetDecimalValue(row.Cell(21)),
                    OtherDeduction1 = GetDecimalValue(row.Cell(22)),
                    OtherDeduction2 = GetDecimalValue(row.Cell(23)),
                    OtherDeduction3 = GetDecimalValue(row.Cell(24)),
                    OtherDeduction4 = GetDecimalValue(row.Cell(25)),
                    TotalPayable = GetDecimalValue(row.Cell(26)),
                    TDS = GetDecimalValue(row.Cell(27)),
                    NetPayable = GetDecimalValue(row.Cell(28)),
                    DepositOn = GetDecimalValue(row.Cell(29)),
                    SendForPaymentAmount = GetDecimalValue(row.Cell(30)),
                    AmountPaid = GetDecimalValue(row.Cell(31)),
                    // This is the "FNF done" upload — mark as completed/paid so the ecode
                    // shows ONLY in the Completed tab (not Pending/Processed). Sheet value wins if provided.
                    PaymentStatus = string.IsNullOrWhiteSpace(row.Cell(32).GetString())
                                        ? "Transfered"
                                        : row.Cell(32).GetString().Trim(),
                    ChequeNo = row.Cell(33).GetString(),
                    ChequeDate = GetDateTimeValue(row.Cell(34)),
                    PaymentVoucherNo = row.Cell(35).GetString(),
                    PaymentRemarks = remarks
                };

                rows.Add(fnfRow);
            }

            if (rows.Count == 0)
            {
                return new FnfBulkUploadResponseDto
                {
                    Success = false,
                    TotalRecords = 0,
                    ProcessedCount = 0,
                    Message = validationErrors.Count > 0
                        ? "No valid rows. " + string.Join(" ", validationErrors)
                        : "No records found in the sheet.",
                    ErrorMessages = validationErrors
                };
            }

            // Fast, single set-based DB call — stores the sheet values as-is (no per-row recalculation).
            var request = new FnfBulkUploadRequestDto { Rows = rows, User = user };
            var response = await BulkUploadAsync(request);

            // Surface any skipped rows (missing Ecode/Remarks) in the response.
            if (validationErrors.Count > 0)
            {
                response.ErrorMessages ??= new List<string>();
                response.ErrorMessages.AddRange(validationErrors);
                response.Message = (response.Message ?? "").TrimEnd() +
                    $" Skipped {validationErrors.Count} row(s) missing Ecode/Remarks.";
            }

            return response;
        }

        private static DateTime? GetDateTimeValue(IXLCell cell)
        {
            if (cell.IsEmpty()) return null;

            try
            {
                // If it's already a DateTime, return it
                if (cell.DataType == XLDataType.DateTime)
                {
                    return cell.GetDateTime();
                }

                // If it's a number, try to convert to DateTime (Excel dates are stored as numbers)
                if (cell.DataType == XLDataType.Number)
                {
                    var dateValue = cell.GetDateTime();
                    return dateValue;
                }

                // If it's text, try to parse as DateTime
                if (cell.DataType == XLDataType.Text)
                {
                    var textValue = cell.GetString();
                    if (DateTime.TryParse(textValue, out DateTime parsedDate))
                    {
                        return parsedDate;
                    }
                }
            }
            catch
            {
                // If any conversion fails, return null
            }

            return null;
        }

        private static decimal? GetDecimalValue(IXLCell cell)
        {
            if (cell.IsEmpty()) return null;
            return cell.DataType == XLDataType.Number ? Convert.ToDecimal(cell.GetValue<decimal>()) : null;
        }

        public async Task<int> UpdatePaymentStatusAsync(long fnfId, string status, string remarks)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("usp_UpdateFNFPaymentStatus", conn);

            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@FNFId", fnfId);
            cmd.Parameters.AddWithValue("@Status", status);
            cmd.Parameters.AddWithValue("@Remarks", (object)remarks ?? DBNull.Value);

            await conn.OpenAsync();
            return await cmd.ExecuteNonQueryAsync();
        }

        public async Task<byte[]> ExportToExcelAsync(string? search, DateTime? from, DateTime? to, string? paymentStatus)
        {
            var allItems = new List<FnfAccountsListItemDto>();
            var page = 1;
            const int pageSize = 10000;
            bool hasMoreData;

            do
            {
                var result = await GetAccountsListAsync(search, from, to, paymentStatus, page, pageSize);
                allItems.AddRange(result.Items);
                hasMoreData = result.Items.Count == pageSize;
                page++;
            } while (hasMoreData);

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("FNF Accounts List");

            // Headers
            worksheet.Cell(1, 1).Value = "Employee Code";
            worksheet.Cell(1, 2).Value = "Employee Name";
            worksheet.Cell(1, 3).Value = "FNF Date";
            worksheet.Cell(1, 4).Value = "Date of Leaving";
            worksheet.Cell(1, 5).Value = "Total Additions";
            worksheet.Cell(1, 6).Value = "Total Deductions";
            worksheet.Cell(1, 7).Value = "Net Amount";
            worksheet.Cell(1, 8).Value = "Send For Payment Amount";
            worksheet.Cell(1, 9).Value = "Amount Paid";
            worksheet.Cell(1, 10).Value = "Payment Status";
            worksheet.Cell(1, 11).Value = "Cheque No";
            worksheet.Cell(1, 12).Value = "Cheque Date";
            worksheet.Cell(1, 13).Value = "Payment Voucher No";
            worksheet.Cell(1, 14).Value = "Payment Remarks";

            // Data
            for (int i = 0; i < allItems.Count; i++)
            {
                var item = allItems[i];
                var row = i + 2;

                worksheet.Cell(row, 1).Value = item.Ecode;
                worksheet.Cell(row, 2).Value = item.EmployeeName;
                worksheet.Cell(row, 3).Value = item.FNFDate?.ToString("yyyy-MM-dd") ?? "";
                worksheet.Cell(row, 4).Value = item.DateOfLeaving?.ToString("yyyy-MM-dd") ?? "";
                worksheet.Cell(row, 5).Value = item.TotalAdditions;
                worksheet.Cell(row, 6).Value = item.TotalDeductions;
                worksheet.Cell(row, 7).Value = item.NetAmount;
                worksheet.Cell(row, 8).Value = item.SendForPaymentAmount ?? 0;
                worksheet.Cell(row, 9).Value = item.AmountPaid ?? 0;
                worksheet.Cell(row, 10).Value = item.PaymentStatus ?? "";
                worksheet.Cell(row, 11).Value = item.ChequeNo ?? "";
                worksheet.Cell(row, 12).Value = item.ChequeDate?.ToString("yyyy-MM-dd") ?? "";
                worksheet.Cell(row, 13).Value = item.PaymentVoucherNo ?? "";
                worksheet.Cell(row, 14).Value = item.PaymentRemarks ?? "";
            }

            // Auto-fit columns
            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        // Comprehensive FNF export across ALL statuses with an optional status filter.
        // status: null/"All" | "Completed" | "Processed" | "Pending"
        // Includes every column (additions, deductions, payment incl. Cheque No/UTR No & Voucher No).
        public async Task<byte[]> ExportAllFnfAsync(string? search, DateTime? from, DateTime? to, string? status)
        {
            var st = (status ?? "all").Trim().ToLowerInvariant();
            bool wantCompleted = st == "all" || st == "completed" || st == "paid";
            bool wantProcessed = st == "all" || st == "processed" || st == "unpaid";
            bool wantPending   = st == "all" || st == "pending";
            bool wantFnf       = wantCompleted || wantProcessed;

            // Column layout: (Header, source column name, kind s=string d=decimal t=date)
            var cols = new (string Header, string Src, char Kind)[]
            {
                ("Employee Code","Ecode",'s'),
                ("Employee Name","EmployeeName",'s'),
                ("Department","DepartmentName",'s'),
                ("Designation","DesignationName",'s'),
                ("Location","LocationName",'s'),
                ("STCode","STCode",'s'),
                ("Date of Joining","JoiningDate",'t'),
                ("Date of Leaving","DateOfLeaving",'t'),
                ("Last Punch Date","LastPunchDate",'t'),
                ("FNF Date","FNFDate",'t'),
                ("Salary Month","SalaryMonth",'s'),
                ("Payable Days","PayableDays",'d'),
                ("Unpaid Salary Amount","UnpaidSalaryAmount",'d'),
                ("Rate","Rate",'d'),
                ("Days","Days",'d'),
                ("Bonus","Bonus",'d'),
                ("Bonus Period From","BonusPeriodFrom",'t'),
                ("Bonus Period Till","BonusPeriodTill",'t'),
                ("Gratuity","Gratuity",'d'),
                ("Earned Leave Amount","E_LeaveAmount",'d'),
                ("EL Days","ELDays",'d'),
                ("Notice Salary","NoticeSalary",'d'),
                ("Other Addition 1","OtherAddition1",'d'),
                ("Other Addition 2","OtherAddition2",'d'),
                ("Other Addition 3","OtherAddition3",'d'),
                ("Other Addition 4","OtherAddition4",'d'),
                ("Total Additions","TotalAdditions",'d'),
                ("Loan Balance","LoanBalance",'d'),
                ("Advance Balance","AdvanceBalance",'d'),
                ("Other Deduction 1","OtherDeduction1",'d'),
                ("Other Deduction 2","OtherDeduction2",'d'),
                ("Other Deduction 3","OtherDeduction3",'d'),
                ("Other Deduction 4","OtherDeduction4",'d'),
                ("TDS","TDS",'d'),
                ("PF","PF",'s'),
                ("ESIC","ESIC",'s'),
                ("P.Tax","PTax",'s'),
                ("Total Deductions","TotalDeductions",'d'),
                ("Total Payable","TotalPayable",'d'),
                ("Net Amount","NetAmount",'d'),
                ("Deposit On","DepositOn",'d'),
                ("Send For Payment Amount","SendForPaymentAmount",'d'),
                ("Amount Paid","AmountPaid",'d'),
                ("Payment Status","PayStatus",'s'),
                ("Cheque No/UTR No","ChequeNo",'s'),
                ("Cheque Date","ChequeDate",'t'),
                ("Payment Voucher No","PaymentVoucherNo",'s'),
                ("Payment Remarks","PayRemarks",'s'),
                ("PAN No","PanNo",'s'),
                ("Bank Name","BankName",'s'),
                ("Account No","AccountNo",'s'),
                ("IFSC","IFSC",'s'),
                ("Status","StatusBucket",'s'),
            };

            const string doneStatuses = "'Transfered','Transferred','Paid','Completed','Done','FNF DONE'";

            var nameExpr = "ISNULL(e.[FULL NAME], CONCAT(ISNULL(e.FirstName,''),' ',ISNULL(NULLIF(e.MiddleName,''),''),CASE WHEN ISNULL(e.LastName,'')<>'' THEN ' '+e.LastName ELSE '' END))";

            // ---- FNF branch (Completed + Processed) ----
            string fnfSql = $@"
SELECT * FROM (
    SELECT
        CASE WHEN p.ChequeNo IS NOT NULL AND (
                 ISNULL(LTRIM(RTRIM(p.ChequeNo)),'')<>'' OR ISNULL(LTRIM(RTRIM(p.PaymentVoucherNo)),'')<>''
              OR ISNULL(LTRIM(RTRIM(p.PayStatus)),'') IN ({doneStatuses}) )
             THEN 'Completed' ELSE 'Processed' END AS StatusBucket,
        e.Ecode,
        {nameExpr} AS EmployeeName,
        dept.DepartmentName, desg.DesignationName, l.LocationName, l.STCode,
        TRY_CONVERT(date, e.[JOINING DATE]) AS JoiningDate,
        a.DateOfLeaving, pn.LastPunchDate, a.FNFDate, a.SalaryMonth,
        sn.paybledays AS PayableDays,
        a.UnpaidSalaryAmount, a.Rate, a.Days, a.Bonus, a.BonusPeriodFrom, a.BonusPeriodTill,
        a.Gratuity, a.E_LeaveAmount, a.ELDays, a.NoticeSalary,
        a.OtherAddition1, a.OtherAddition2, a.OtherAddition3, a.OtherAddition4,
        CAST(ISNULL(a.UnpaidSalaryAmount,0)+ISNULL(a.Bonus,0)+ISNULL(a.Gratuity,0)+ISNULL(a.E_LeaveAmount,0)+ISNULL(a.NoticeSalary,0)
            +ISNULL(a.OtherAddition1,0)+ISNULL(a.OtherAddition2,0)+ISNULL(a.OtherAddition3,0)+ISNULL(a.OtherAddition4,0) AS decimal(18,2)) AS TotalAdditions,
        d.LoanBalance, d.AdvanceBalance, d.OtherDeduction1, d.OtherDeduction2, d.OtherDeduction3, d.OtherDeduction4,
        d.TDS, sn.[PF(Total)] AS PF, sn.[ESIC(Total)] AS ESIC, sn.PTax,
        CAST(ISNULL(d.LoanBalance,0)+ISNULL(d.AdvanceBalance,0)+ISNULL(d.OtherDeduction1,0)+ISNULL(d.OtherDeduction2,0)
            +ISNULL(d.OtherDeduction3,0)+ISNULL(d.OtherDeduction4,0)+ISNULL(d.TDS,0) AS decimal(18,2)) AS TotalDeductions,
        d.TotalPayable,
        CAST((ISNULL(a.UnpaidSalaryAmount,0)+ISNULL(a.Bonus,0)+ISNULL(a.Gratuity,0)+ISNULL(a.E_LeaveAmount,0)+ISNULL(a.NoticeSalary,0)
             +ISNULL(a.OtherAddition1,0)+ISNULL(a.OtherAddition2,0)+ISNULL(a.OtherAddition3,0)+ISNULL(a.OtherAddition4,0))
            -(ISNULL(d.LoanBalance,0)+ISNULL(d.AdvanceBalance,0)+ISNULL(d.OtherDeduction1,0)+ISNULL(d.OtherDeduction2,0)
             +ISNULL(d.OtherDeduction3,0)+ISNULL(d.OtherDeduction4,0)+ISNULL(d.TDS,0)) AS decimal(18,2)) AS NetAmount,
        d.DepositOn,
        p.SendForPaymentAmount, p.AmountPaid, p.PayStatus, p.ChequeNo, p.ChequeDate, p.PaymentVoucherNo, p.PayRemarks,
        e.[PAN NO] AS PanNo, e.[BANK NAME] AS BankName, e.[A/C NO] AS AccountNo, e.[BANK IFSC CODE] AS IFSC
    FROM dbo.FNF_Header h
    LEFT JOIN dbo.tblEmployee e ON e.EmployeeId = h.EmployeeId
    LEFT JOIN dbo.tblDepartment dept ON dept.DepartmentId = e.DepartmentId
    LEFT JOIN dbo.tblDesignation desg ON desg.DesignationId = e.DesignationId
    LEFT JOIN dbo.tblLocation l ON l.LocationId = e.LocationId
    LEFT JOIN dbo.FNF_Additions a ON a.FNFId = h.FNFId
    LEFT JOIN dbo.FNF_Deductions d ON d.FNFId = h.FNFId
    OUTER APPLY (SELECT TOP 1 p2.SendForPaymentAmount, p2.AmountPaid, p2.[Status] AS PayStatus, p2.ChequeNo, p2.ChequeDate, p2.PaymentVoucherNo, p2.Remarks AS PayRemarks
                 FROM dbo.FNF_Payment p2 WHERE p2.FNFId = h.FNFId ORDER BY p2.PaymentId DESC) p
    OUTER APPLY (SELECT TOP 1 s.paybledays, s.[PF(Total)], s.[ESIC(Total)], s.PTax
                 FROM dbo.EmpAttendanceViewSnapshot s WHERE s.Ecode = e.Ecode AND s.[Month-Year] = a.SalaryMonth) sn
    OUTER APPLY (SELECT MAX(x.AttendanceDate) AS LastPunchDate
                 FROM dbo.tbl_fn_GetMonthlyPunchesRange_productionnewnick_test x
                 WHERE x.ECode = e.Ecode AND TRY_CAST(x.TotalWorkingMinutes AS time) >= '04:30') pn
    WHERE e.Ecode LIKE 'V%'
      AND (@search IS NULL OR @search = '' OR e.Ecode LIKE @search + '%' OR {nameExpr} LIKE '%' + @search + '%')
      AND (@from IS NULL OR a.FNFDate >= @from)
      AND (@to   IS NULL OR a.FNFDate <= @to)
) X
WHERE (@bucket = 'both' OR X.StatusBucket = @bucket)";

            // ---- Pending branch (employees with no FNF yet) ----
            string pendingSql = $@"
SELECT
    'Pending' AS StatusBucket,
    e.Ecode, {nameExpr} AS EmployeeName,
    dept.DepartmentName, desg.DesignationName, l.LocationName, l.STCode,
    TRY_CONVERT(date, e.[JOINING DATE]) AS JoiningDate,
    TRY_CONVERT(date, e.[DateOfLeft]) AS DateOfLeaving,
    pn.LastPunchDate,
    e.[PAN NO] AS PanNo, e.[BANK NAME] AS BankName, e.[A/C NO] AS AccountNo, e.[BANK IFSC CODE] AS IFSC
FROM dbo.tblEmployee e
LEFT JOIN dbo.tblDepartment dept ON dept.DepartmentId = e.DepartmentId
LEFT JOIN dbo.tblDesignation desg ON desg.DesignationId = e.DesignationId
LEFT JOIN dbo.tblLocation l ON l.LocationId = e.LocationId
OUTER APPLY (SELECT MAX(x.AttendanceDate) AS LastPunchDate
             FROM dbo.tbl_fn_GetMonthlyPunchesRange_productionnewnick_test x
             WHERE x.ECode = e.Ecode AND TRY_CAST(x.TotalWorkingMinutes AS time) >= '04:30') pn
WHERE ISNULL(e.IsStore,0)=0 AND ISNULL(e.IsActive,0)=0
  AND e.Ecode LIKE 'V%'
  AND NOT EXISTS (SELECT 1 FROM dbo.FNF_Header fh WHERE fh.EmployeeId = e.EmployeeId)
  AND e.[DateOfLeft] IS NOT NULL
  AND (@search IS NULL OR @search = '' OR e.Ecode LIKE @search + '%' OR {nameExpr} LIKE '%' + @search + '%')
  AND (
        (@from IS NOT NULL AND TRY_CONVERT(date, e.[DateOfLeft]) >= @from)
        OR (@from IS NULL AND TRY_CONVERT(date, e.[DateOfLeft]) >= DATEADD(YEAR,-1,GETDATE()))
      )
  AND (@to IS NULL OR TRY_CONVERT(date, e.[DateOfLeft]) <= @to)";

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("FNF Report");
            for (int i = 0; i < cols.Length; i++) ws.Cell(1, i + 1).Value = cols[i].Header;
            ws.Row(1).Style.Font.Bold = true;
            ws.SheetView.FreezeRows(1);

            int rowIdx = 1;

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            async Task WriteFromQuery(string sql, string bucket)
            {
                using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 300 };
                cmd.Parameters.AddWithValue("@search", (object?)search ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@from", (object?)from ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@to", (object?)to ?? DBNull.Value);
                if (bucket != null) cmd.Parameters.AddWithValue("@bucket", bucket);

                using var rdr = await cmd.ExecuteReaderAsync();
                var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < rdr.FieldCount; i++) present.Add(rdr.GetName(i));

                while (await rdr.ReadAsync())
                {
                    rowIdx++;
                    for (int c = 0; c < cols.Length; c++)
                    {
                        if (!present.Contains(cols[c].Src)) continue;
                        var val = rdr[cols[c].Src];
                        if (val == null || val == DBNull.Value) continue;
                        var cell = ws.Cell(rowIdx, c + 1);
                        switch (cols[c].Kind)
                        {
                            case 'd':
                                cell.Value = Convert.ToDouble(val);
                                break;
                            case 't':
                                if (val is DateTime dtv) cell.Value = dtv.ToString("dd-MMM-yy");
                                else cell.Value = val.ToString();
                                break;
                            default:
                                cell.Value = val.ToString();
                                break;
                        }
                    }
                }
            }

            if (wantFnf)
            {
                var bucket = wantCompleted && wantProcessed ? "both" : (wantCompleted ? "Completed" : "Processed");
                await WriteFromQuery(fnfSql, bucket);
            }
            if (wantPending)
            {
                await WriteFromQuery(pendingSql, null);
            }

            ws.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        public async Task<byte[]> ExportPendingToExcelAsync()
        {
            var allItems = new List<FnfEmployeeDropdownDto>();

            var conn = _db.Database.GetDbConnection();
            await conn.OpenAsync();

            try
            {
                var cmd = conn.CreateCommand();
                cmd.CommandText = "dbo.sp_FNF_GetEmployeesByCodeForExport";
                cmd.CommandType = CommandType.StoredProcedure;
                using var rdr = await cmd.ExecuteReaderAsync();

                if (rdr != null)
                {
                    while (await rdr.ReadAsync())
                    {
                        allItems.Add(new()
                        {
                            EmployeeId = rdr.IsDbNull(rdr.GetOrdinal("EmployeeId")) ? 0 : rdr.GetInt64("EmployeeId"),
                            EmployeeCode = rdr.IsDbNull(rdr.GetOrdinal("EmployeeCode")) ? string.Empty : rdr.GetString("EmployeeCode"),
                            Name = rdr.IsDbNull(rdr.GetOrdinal("Name")) ? string.Empty : rdr.GetString("Name"),
                            Department = rdr.IsDbNull(rdr.GetOrdinal("Department")) ? string.Empty : rdr.GetString("Department"),
                            Designation = rdr.IsDbNull(rdr.GetOrdinal("Designation")) ? string.Empty : rdr.GetString("Designation"),
                            DateOfJoining = rdr.IsDbNull(rdr.GetOrdinal("DateOfJoining")) ? null : rdr.GetDateTime("DateOfJoining"),
                            DateOfLeaving = rdr.IsDbNull(rdr.GetOrdinal("DateOfLeaving")) ? null : rdr.GetDateTime("DateOfLeaving"),
                            IsFNFCompleted = rdr.IsDbNull(rdr.GetOrdinal("IsFNFCompleted")) ? null : rdr.GetBoolean("IsFNFCompleted"),
                            UnpaidSalaryAmount = rdr.IsDbNull(rdr.GetOrdinal("UnpaidSalaryAmount")) ? 0 : rdr.GetDecimal("UnpaidSalaryAmount"),
                            UnpaidSalaryDays = rdr.IsDbNull(rdr.GetOrdinal("UnpaidSalaryDays")) ? 0 : rdr.GetInt32("UnpaidSalaryDays"),
                            UnpaidSalaryMonth = rdr.IsDbNull(rdr.GetOrdinal("UnpaidSalaryMonth")) ? string.Empty : rdr.GetString("UnpaidSalaryMonth"),
                            ResignationType = rdr.IsDbNull(rdr.GetOrdinal("ResignationType")) ? string.Empty : rdr.GetString("ResignationType"),
                            ResignationAttachment = rdr.IsDbNull(rdr.GetOrdinal("ResignationAttachment")) ? string.Empty : rdr.GetString("ResignationAttachment"),
                        });
                    }
                }
            }
            finally
            {
                await conn.CloseAsync();
            }

            //do
            //{
            //    var result = await GetAccountsListAsync(search, from, to, paymentStatus, page, pageSize);
            //    allItems.AddRange(result.Items);
            //    hasMoreData = result.Items.Count == pageSize;
            //    page++;
            //} while (hasMoreData);

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("FNF Accounts List");

            // Headers
            worksheet.Cell(1, 1).Value = "Employee Id";
            worksheet.Cell(1, 2).Value = "Employee Code";
            worksheet.Cell(1, 3).Value = "Employee Name";
            worksheet.Cell(1, 4).Value = "Department";
            worksheet.Cell(1, 5).Value = "Designation";
            worksheet.Cell(1, 6).Value = "Date Of Joining";
            worksheet.Cell(1, 7).Value = "Date Of Leaving";
            worksheet.Cell(1, 8).Value = "Is FNF Completed";
            worksheet.Cell(1, 9).Value = "Unpaid Salary AMount";
            worksheet.Cell(1, 10).Value = "Unpaid Salary Days";
            worksheet.Cell(1, 11).Value = "Unpaid Salary Month";
            worksheet.Cell(1, 12).Value = "Resignation Type";
            worksheet.Cell(1, 13).Value = "Resignation Attachment";

            // Data
            for (int i = 0; i < allItems.Count; i++)
            {
                var item = allItems[i];
                var row = i + 2;

                worksheet.Cell(row, 1).Value = item.EmployeeId;
                worksheet.Cell(row, 2).Value = item.EmployeeCode;
                worksheet.Cell(row, 3).Value = item.Name;
                worksheet.Cell(row, 4).Value = item.Department;
                worksheet.Cell(row, 5).Value = item.Designation;
                worksheet.Cell(row, 6).Value = item.DateOfJoining;
                worksheet.Cell(row, 7).Value = item.DateOfLeaving;
                worksheet.Cell(row, 8).Value = item.IsFNFCompleted.ToString();
                worksheet.Cell(row, 9).Value = item.UnpaidSalaryAmount;
                worksheet.Cell(row, 10).Value = item.UnpaidSalaryDays;
                worksheet.Cell(row, 11).Value = item.UnpaidSalaryMonth;
                worksheet.Cell(row, 12).Value = item.ResignationType;
                worksheet.Cell(row, 13).Value = item.ResignationAttachment;
            }

            // Auto-fit columns
            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();

        }

        public async Task<Response> FnfPendingToProcessing(long employeeid)
        {
            using var conn = _db.Database.GetDbConnection();
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "sp_FnfPendingToProcessing";
            cmd.CommandType = CommandType.StoredProcedure;

            var param = cmd.CreateParameter();
            param.ParameterName = "@EmployeeId";
            param.Value = employeeid;
            param.DbType = DbType.Int64;

            cmd.Parameters.Add(param);

            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                int success = reader.GetInt32(reader.GetOrdinal("Success"));
                string message = reader.GetString(reader.GetOrdinal("Message"));

                if (success == 0)
                    throw new Exception(message);

                return new Response() { Status = true, Message = message };
            }

            return new Response() { Status = false, Message = "Error processing fnf." };
        }
    }
}
