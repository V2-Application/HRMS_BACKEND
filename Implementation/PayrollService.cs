using ClosedXML.Excel;
using HRMSAPI.Data;
using HRMSAPI.DTO;
using HRMSAPI.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Roomsy.DTOS.GenericsResponses;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace HRMSAPI.Implementation
{
    public class PayrollRepository : IPayrollService
    {
        private readonly HRMSContext _context;

        public PayrollRepository(HRMSContext context)
        {
            _context = context;
        }

        public async Task<(List<EmployeePayrollDTO> Records, int TotalRecords)> GetPayrollRecordsAsync(
      string? searchTerm = null,
      string? ecode = null,
      long? employeeId = null,
      string? location = null,
      int page = 1,
      int pageSize = 10,
      bool fetchAll = false)
        {
            try
            {
                // Ensure page and pageSize are valid when not fetching all
                if (!fetchAll)
                {
                    page = Math.Max(1, page);
                    pageSize = Math.Max(1, pageSize);
                }

                // Build query with left join to handle cases where Location may not match
                var query = from ep in _context.EmployeePayrolls.AsNoTracking()
                            join loc in _context.tblLocations.AsNoTracking()
                                on ep.Location equals loc.LocationId.ToString() into locations
                            from loc in locations.DefaultIfEmpty()
                            select new EmployeePayrollDTO
                            {
                                EmployeePayRollId = ep.EmployeePayRollId,
                                Location = loc != null ? loc.STCode : null,
                                Ecode = ep.Ecode,
                                BGT_Salary = ep.BGT_Salary ?? 0m,
                                Payable_Days = ep.Payable_Days ?? 0m,
                                Gross_Salary = ep.Gross_Salary ?? 0m,
                                Total_Deduction = ep.Total_Deduction ?? 0m,
                                Payable_Salary = ep.Payable_Salary ?? 0m,
                                PF = ep.PF ?? 0m,
                                ESI = ep.ESI ?? 0m,
                                TDS = ep.TDS ?? 0m,
                                P_TAX = ep.P_TAX ?? 0m,
                                CASH_SHORT = ep.CASH_SHORT ?? 0m,
                                DIESEL = ep.DIESEL ?? 0m,
                                PENALTY = ep.PENALTY ?? 0m,
                                LOAN = ep.LOAN ?? 0m,
                                OT_AMT = ep.OT_AMT ?? 0m,
                                INCENTIVE_AMT = ep.INCENTIVE_AMT ?? 0m,
                                FOODING_ALL = ep.FOODING_ALL ?? 0m,
                                ARRERS = ep.ARRERS ?? 0m,
                                EXTRA_DAYS_ALLOWANCE = ep.EXTRA_DAYS_ALLOWANCE ?? 0m,
                                CretedOn = ep.CretedOn ?? DateTime.MinValue,
                                CreatedBy = ep.CreatedBy,
                                LastUpdatedBy = ep.LastUpdatedBy,
                                LastUpdatedOn = ep.LastUpdatedOn,
                                EmployeeId = ep.EmployeeId,
                                FullName = ep.Employee != null ? ep.Employee.FULL_NAME : null,
                                EmailAddress = ep.Employee != null ? ep.Employee.EMAIL_ADDRESS : null,
                                MonthYear = ep.MonthYear
                            };

                // Apply search across all columns
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    searchTerm = searchTerm.Trim().ToLower();
                    query = query.Where(ep =>
                        (ep.Ecode != null && ep.Ecode.ToLower().Contains(searchTerm)) ||
                        (ep.Location != null && ep.Location.ToLower().Contains(searchTerm)) ||
                        (ep.FullName != null && ep.FullName.ToLower().Contains(searchTerm)) ||
                        (ep.EmailAddress != null && ep.EmailAddress.ToLower().Contains(searchTerm)) ||
                        ep.BGT_Salary.ToString().Contains(searchTerm) ||
                        ep.Payable_Days.ToString().Contains(searchTerm) ||
                        ep.Gross_Salary.ToString().Contains(searchTerm) ||
                        ep.Total_Deduction.ToString().Contains(searchTerm) ||
                        ep.Payable_Salary.ToString().Contains(searchTerm) ||
                        ep.PF.ToString().Contains(searchTerm) ||
                        ep.ESI.ToString().Contains(searchTerm) ||
                        ep.TDS.ToString().Contains(searchTerm) ||
                        ep.P_TAX.ToString().Contains(searchTerm) ||
                        ep.CASH_SHORT.ToString().Contains(searchTerm) ||
                        ep.DIESEL.ToString().Contains(searchTerm) ||
                        ep.PENALTY.ToString().Contains(searchTerm) ||
                        ep.LOAN.ToString().Contains(searchTerm) ||
                        ep.OT_AMT.ToString().Contains(searchTerm) ||
                        ep.INCENTIVE_AMT.ToString().Contains(searchTerm) ||
                        ep.FOODING_ALL.ToString().Contains(searchTerm) ||
                        ep.ARRERS.ToString().Contains(searchTerm) ||
                        ep.EXTRA_DAYS_ALLOWANCE.ToString().Contains(searchTerm) ||
                        ep.CretedOn.ToString().Contains(searchTerm) ||
                        (ep.CreatedBy != null && ep.CreatedBy.ToLower().Contains(searchTerm)) ||
                        (ep.LastUpdatedBy != null && ep.LastUpdatedBy.ToLower().Contains(searchTerm)) ||
                        (ep.LastUpdatedOn != null && ep.LastUpdatedOn.ToString().Contains(searchTerm)));
                }

                // Apply specific filters
                if (!string.IsNullOrWhiteSpace(ecode))
                {
                    query = query.Where(ep => ep.Ecode != null && ep.Ecode.Contains(ecode, StringComparison.OrdinalIgnoreCase));
                }
                if (employeeId.HasValue)
                {
                    query = query.Where(ep => ep.EmployeeId == employeeId.Value);
                }
                if (!string.IsNullOrWhiteSpace(location))
                {
                    query = query.Where(ep => ep.Location != null && ep.Location.Contains(location, StringComparison.OrdinalIgnoreCase));
                }

                // Get total records
                var totalRecords = await query.CountAsync();

                // Apply pagination only if fetchAll is false
                if (!fetchAll)
                {
                    query = query
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize);
                }

                // Fetch records
                var records = await query
                    .OrderByDescending(ep => ep.CretedOn)
                    .ToListAsync();

                return (records, totalRecords);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving payroll records: {ex.Message}");
                return (new List<EmployeePayrollDTO>(), 0);
            }
        }
        public async Task<(bool Success, string Message)> UploadPayrollDataAsync(IFormFile file, string createdBy)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return (false, "No file uploaded");

                using (var stream = file.OpenReadStream())
                {
                    using (var workbook = new XLWorkbook(stream))
                    {
                        var worksheet = workbook.Worksheet(1); // Get first worksheet
                        var rows = worksheet.RowsUsed().Skip(1); // Skip header row

                        foreach (var row in rows)
                        {
                            // Handle string columns with "NA" default
                            var locationStoreName = row.Cell(1).GetValue<string>()?.Trim() ?? "NA";
                            var ecode = row.Cell(2).GetValue<string>()?.Trim() ?? "NA";
                            var monthYearStr = row.Cell(3).GetValue<string>()?.Trim() ?? "NA";

                            // Handle decimal columns with 0.0 default
                            var bgtSalary = row.Cell(4).IsEmpty() ? 0.0m : row.Cell(4).GetValue<decimal>();
                            var payableDays = row.Cell(5).IsEmpty() ? 0.0m : row.Cell(5).GetValue<decimal>();
                            var otAmt = row.Cell(6).IsEmpty() ? 0.0m : row.Cell(6).GetValue<decimal>();
                            var incentiveAmt = row.Cell(7).IsEmpty() ? 0.0m : row.Cell(7).GetValue<decimal>();
                            var foodingAll = row.Cell(8).IsEmpty() ? 0.0m : row.Cell(8).GetValue<decimal>();
                            var arrers = row.Cell(9).IsEmpty() ? 0.0m : row.Cell(9).GetValue<decimal>();
                            var extraDaysAllowance = row.Cell(10).IsEmpty() ? 0.0m : row.Cell(10).GetValue<decimal>();
                            var grossSalary = row.Cell(11).IsEmpty() ? 0.0m : row.Cell(11).GetValue<decimal>();
                            var pf = row.Cell(12).IsEmpty() ? 0.0m : row.Cell(12).GetValue<decimal>();
                            var esi = row.Cell(13).IsEmpty() ? 0.0m : row.Cell(13).GetValue<decimal>();
                            var tds = row.Cell(14).IsEmpty() ? 0.0m : row.Cell(14).GetValue<decimal>();
                            var pTax = row.Cell(15).IsEmpty() ? 0.0m : row.Cell(15).GetValue<decimal>();
                            var cashShort = row.Cell(16).IsEmpty() ? 0.0m : row.Cell(16).GetValue<decimal>();
                            var diesel = row.Cell(17).IsEmpty() ? 0.0m : row.Cell(17).GetValue<decimal>();
                            var penalty = row.Cell(18).IsEmpty() ? 0.0m : row.Cell(18).GetValue<decimal>();
                            var loan = row.Cell(19).IsEmpty() ? 0.0m : row.Cell(19).GetValue<decimal>();
                            var totalDeduction = row.Cell(20).IsEmpty() ? 0.0m : row.Cell(20).GetValue<decimal>();
                            var payableSalary = row.Cell(21).IsEmpty() ? 0.0m : row.Cell(21).GetValue<decimal>();

                            // Parse MonthYear to DateTime? (nullable)
                            DateTime? monthYear = null;
                            if (!string.IsNullOrWhiteSpace(monthYearStr) && monthYearStr != "NA" && DateTime.TryParse(monthYearStr, out var parsedMonthYear))
                            {
                                monthYear = parsedMonthYear;
                            }

                            // Get LocationId from LocationStoreName
                            var location = await _context.tblLocations.AsNoTracking().AsQueryable()
                                .FirstOrDefaultAsync(l => l.STCode == locationStoreName);

                            if (location == null)
                            {
                                return (false, $"Location not found: {locationStoreName}");
                            }

                            // Check if record exists for the Ecode
                            var existingRecord = await _context.EmployeePayrolls
                                .FirstOrDefaultAsync(p => p.Ecode == ecode);

                            if (existingRecord != null)
                            {
                                // Update existing record
                                existingRecord.Location = location.LocationId.ToString();
                                existingRecord.MonthYear = monthYear;
                                existingRecord.BGT_Salary = bgtSalary;
                                existingRecord.Payable_Days = payableDays;
                                existingRecord.OT_AMT = otAmt;
                                existingRecord.INCENTIVE_AMT = incentiveAmt;
                                existingRecord.FOODING_ALL = foodingAll;
                                existingRecord.ARRERS = arrers;
                                existingRecord.EXTRA_DAYS_ALLOWANCE = extraDaysAllowance;
                                existingRecord.Gross_Salary = grossSalary;
                                existingRecord.PF = pf;
                                existingRecord.ESI = esi;
                                existingRecord.TDS = tds;
                                existingRecord.P_TAX = pTax;
                                existingRecord.CASH_SHORT = cashShort;
                                existingRecord.DIESEL = diesel;
                                existingRecord.PENALTY = penalty;
                                existingRecord.LOAN = loan;
                                existingRecord.Total_Deduction = totalDeduction;
                                existingRecord.Payable_Salary = payableSalary;
                                existingRecord.CretedOn = DateTime.UtcNow;
                                existingRecord.CreatedBy = createdBy;
                            }
                            else
                            {
                                // Create new payroll record
                                var payrollRecord = new EmployeePayroll
                                {
                                    Location = location.LocationId.ToString(),
                                    Ecode = ecode,
                                    MonthYear = monthYear,
                                    BGT_Salary = bgtSalary,
                                    Payable_Days = payableDays,
                                    OT_AMT = otAmt,
                                    INCENTIVE_AMT = incentiveAmt,
                                    FOODING_ALL = foodingAll,
                                    ARRERS = arrers,
                                    EXTRA_DAYS_ALLOWANCE = extraDaysAllowance,
                                    Gross_Salary = grossSalary,
                                    PF = pf,
                                    ESI = esi,
                                    TDS = tds,
                                    P_TAX = pTax,
                                    CASH_SHORT = cashShort,
                                    DIESEL = diesel,
                                    PENALTY = penalty,
                                    LOAN = loan,
                                    Total_Deduction = totalDeduction,
                                    Payable_Salary = payableSalary,
                                    CretedOn = DateTime.UtcNow,
                                    CreatedBy = createdBy
                                };
                                _context.EmployeePayrolls.Add(payrollRecord);
                            }
                        }

                        await _context.SaveChangesAsync();
                        return (true, "Payroll data uploaded successfully");
                    }
                }
            }
            catch (Exception ex)
            {
                return (false, $"Error uploading payroll data: {ex.Message}");
            }
        }

        public async Task<PayrollSummaryResponseDto> GetPayrollSummaryAsync(DateTime startDate, DateTime endDate, int pageNumber, int pageSize)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100; // Prevent excessive data retrieval

            var payrollRecords = new List<PayRollSummaryDto>();
            var totals = new TotalSummary();

            try
            {
                using var connection = _context.Database.GetDbConnection();
                using var command = connection.CreateCommand();
                command.CommandText = "sp_GetPayrollSummary";
                command.CommandType = CommandType.StoredProcedure;

                command.Parameters.Add(new SqlParameter("@StartDate", SqlDbType.Date) { Value = startDate });
                command.Parameters.Add(new SqlParameter("@EndDate", SqlDbType.Date) { Value = endDate });
                command.Parameters.Add(new SqlParameter("@PageNumber", SqlDbType.Int) { Value = pageNumber });
                command.Parameters.Add(new SqlParameter("@PageSize", SqlDbType.Int) { Value = pageSize });

                await connection.OpenAsync();
                using var reader = await command.ExecuteReaderAsync();

                // Read payroll records
                while (await reader.ReadAsync())
                {
                    payrollRecords.Add(new PayRollSummaryDto
                    {
                        LocationName = reader["LocationName"] as string,
                        STCode = reader["STCode"] as string,
                        Ecode = reader["Ecode"] as string,
                        MonthYear = Convert.ToDateTime(reader["MonthYear"]).ToString("yyyy-MM"),
                        PayableSalary = reader["Payable_Salary"] == DBNull.Value ? null : Convert.ToDecimal(reader["Payable_Salary"]),
                        GiventoBank = reader["GiventoBank"] == DBNull.Value ? null : Convert.ToDecimal(reader["GiventoBank"]),
                        PaidByBank = reader["PaidByBank"] == DBNull.Value ? null : Convert.ToDecimal(reader["PaidByBank"]),
                        ReturnByBank = reader["ReturnByBank"] == DBNull.Value ? null : Convert.ToDecimal(reader["ReturnByBank"]),
                        DifferencePayableMinusGiven = reader["DifferencePayableMinusGiven"] == DBNull.Value ? null : Convert.ToDecimal(reader["DifferencePayableMinusGiven"]),
                        DifferencePayableMinusPaid = reader["DifferencePayableMinusPaid"] == DBNull.Value ? null : Convert.ToDecimal(reader["DifferencePayableMinusPaid"]),
                        DifferencePayableMinusReturned = reader["DifferencePayableMinusReturned"] == DBNull.Value ? null : Convert.ToDecimal(reader["DifferencePayableMinusReturned"]),
                        DifferenceGivenMinusPaid = reader["DifferenceGivenMinusPaid"] == DBNull.Value ? null : Convert.ToDecimal(reader["DifferenceGivenMinusPaid"]),
                        DifferenceGivenMinusReturned = reader["DifferenceGivenMinusReturned"] == DBNull.Value ? null : Convert.ToDecimal(reader["DifferenceGivenMinusReturned"])
                    });
                }

                // Read totals
                if (await reader.NextResultAsync() && await reader.ReadAsync())
                {
                    totals = new TotalSummary
                    {
                        TotalPayableSalary = reader["TotalPayableSalary"] == DBNull.Value ? null : Convert.ToDecimal(reader["TotalPayableSalary"]),
                        TotalGivenToBank = reader["TotalGivenToBank"] == DBNull.Value ? null : Convert.ToDecimal(reader["TotalGivenToBank"]),
                        TotalPaidByBank = reader["TotalPaidByBank"] == DBNull.Value ? null : Convert.ToDecimal(reader["TotalPaidByBank"]),
                        TotalReturnByBank = reader["TotalReturnByBank"] == DBNull.Value ? null : Convert.ToDecimal(reader["TotalReturnByBank"]),
                        TotalDifferencePayableMinusGiven = reader["TotalDifferencePayableMinusGiven"] == DBNull.Value ? null : Convert.ToDecimal(reader["TotalDifferencePayableMinusGiven"]),
                        TotalDifferencePayableMinusPaid = reader["TotalDifferencePayableMinusPaid"] == DBNull.Value ? null : Convert.ToDecimal(reader["TotalDifferencePayableMinusPaid"]),
                        TotalDifferencePayableMinusReturned = reader["TotalDifferencePayableMinusReturned"] == DBNull.Value ? null : Convert.ToDecimal(reader["TotalDifferencePayableMinusReturned"]),
                        TotalDifferenceGivenMinusPaid = reader["TotalDifferenceGivenMinusPaid"] == DBNull.Value ? null : Convert.ToDecimal(reader["TotalDifferenceGivenMinusPaid"]),
                        TotalDifferenceGivenMinusReturned = reader["TotalDifferenceGivenMinusReturned"] == DBNull.Value ? null : Convert.ToDecimal(reader["TotalDifferenceGivenMinusReturned"])
                    };
                }

                return new PayrollSummaryResponseDto
                {
                    PayrollRecords = payrollRecords,
                    Totals = totals
                };
            }
            catch (Exception ex)
            {

                throw;
            }
        }
        public async Task<ExecuteAndReponse> UpsertPFApprovalAsync(tblPF_Approval dto, CancellationToken ct = default)
        {
            if (dto == null)
                return Fail(HttpStatusCode.BadRequest, "Request body is required.");

            if (string.IsNullOrWhiteSpace(dto.E_Code))
                return Fail(HttpStatusCode.BadRequest, "E_Code is required.");

            if (string.IsNullOrWhiteSpace(dto._Month))
                return Fail(HttpStatusCode.BadRequest, "_Month is required in format 'MMM-yy' (e.g., 'Dec-25').");

            if (string.IsNullOrWhiteSpace(dto.Challan_No))
                return Fail(HttpStatusCode.BadRequest, "Challan_No is required.");

            if (string.IsNullOrWhiteSpace(dto.Attachment))
                return Fail(HttpStatusCode.BadRequest, "Attachment path is required.");

            // Normalize and validate month format
            if (!DateTime.TryParseExact(dto._Month.Trim(), "MMM-yy", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var parsedMonth))
                return Fail(HttpStatusCode.BadRequest, $"Month '{dto._Month}' is not in correct format 'MMM-yy'.");

            dto._Month = parsedMonth.ToString("MMM-yy", CultureInfo.InvariantCulture);

            // Employee exists + active
            var employeeExists = await _context.tblEmployees.AsNoTracking()
                .AnyAsync(e => e.Ecode == dto.E_Code && e.IsActive == true && e.IsDeleted != true, ct);

            if (!employeeExists)
                return Fail(HttpStatusCode.BadRequest, $"Employee with ECode '{dto.E_Code}' does not exist or is inactive.");

            try
            {
                var existing = await _context.tblPF_Approvals
                    .FirstOrDefaultAsync(p => p.E_Code == dto.E_Code && p._Month == dto._Month, ct);

                if (existing != null)
                {
                    existing.Challan_No = dto.Challan_No;
                    existing.Attachment = dto.Attachment;
                    existing.updatedBy = dto.updatedBy;
                    existing.updatedOn = DateTime.Now;

                    // no need to call Update; tracked entity will update
                }
                else
                {
                    dto.createdOn = DateTime.Now;
                    await _context.tblPF_Approvals.AddAsync(dto, ct);
                }

                await _context.SaveChangesAsync(ct);

                return new ExecuteAndReponse
                {
                    Status = true,
                    Message = "PF approval upserted successfully.",
                    Code = HttpStatusCode.OK
                };
            }
            catch (DbUpdateException dbEx)
            {
                // If unique constraint triggers etc.
                return Fail(HttpStatusCode.Conflict, $"Database update failed: {dbEx.InnerException?.Message ?? dbEx.Message}");
            }
            catch (Exception ex)
            {
                return Fail(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        public async Task<(List<SalaryProcessDTO> Records, int TotalRecords)> GetSalaryProcessListAsync(string? searchTerm, int pageNumber, int pageSize)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            var records = new List<SalaryProcessDTO>();
            int totalRecords = 0;

            try
            {
                using var connection = _context.Database.GetDbConnection();
                using var command = connection.CreateCommand();
                command.CommandText = "sp_ProcessSalary_List";
                command.CommandType = CommandType.StoredProcedure;

                command.Parameters.Add(new SqlParameter("@SearchTerm", SqlDbType.NVarChar) { Value = (object?)searchTerm ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@PageNumber", SqlDbType.Int) { Value = pageNumber });
                command.Parameters.Add(new SqlParameter("@PageSize", SqlDbType.Int) { Value = pageSize });

                await connection.OpenAsync();
                using var reader = await command.ExecuteReaderAsync();

                // 1. Read Total Count (First Result Set)
                if (await reader.ReadAsync())
                {
                    totalRecords = reader["TotalCount"] != DBNull.Value ? Convert.ToInt32(reader["TotalCount"]) : 0;
                }

                // 2. Read Data (Next Result Set)
                if (await reader.NextResultAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        records.Add(MapSalaryProcessDTO(reader));
                    }
                }
            }
            catch (Exception ex)
            {
                // In production, log error
                Console.WriteLine($"Error getting salary process list: {ex.Message}");
            }

            return (records, totalRecords);
        }

        public async Task<List<SalaryProcessDTO>> GetSalaryProcessExportDataAsync(string? searchTerm)
        {
            var records = new List<SalaryProcessDTO>();
            try
            {
                using var connection = _context.Database.GetDbConnection();
                using var command = connection.CreateCommand();
                command.CommandText = "sp_ProcessSalary_Export";
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.Add(new SqlParameter("@SearchTerm", SqlDbType.NVarChar) { Value = (object?)searchTerm ?? DBNull.Value });

                await connection.OpenAsync();
                using var reader = await command.ExecuteReaderAsync();
                
                while (await reader.ReadAsync())
                {
                    records.Add(MapSalaryProcessDTO(reader));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting salary process export data: {ex.Message}");
            }
            return records;
        }

        // Helper method to map DTO from reader (reused)
        private SalaryProcessDTO MapSalaryProcessDTO(DbDataReader reader)
        {
             var dto = new SalaryProcessDTO
                        {
                            SalaryProcessId = Convert.ToInt32(reader["SalaryProcessId"]),
                            Ecode = reader["Ecode"] as string,
                            Location_Code = reader["Location_Code"] as string,
                            LocationName = reader["LocationName"] as string,
                            EmployeeName = reader["EmployeeName"] as string,
                            Designation = reader["Designation"] as string,
                            Department = reader["Department"] as string,
                            MonthYear = reader["MonthYear"] as string,
                            Status = reader["Status"] as string,
                            MONTH = reader["MONTH"] as string,
                            SalaryStatus = reader["SalaryStatus"] as string,
                            CreatedBy = reader["CreatedBy"] as string,
                            CreatedOn = reader["CreatedOn"] != DBNull.Value ? Convert.ToDateTime(reader["CreatedOn"]) : null,
                            RunAt = reader["RunAt"] != DBNull.Value ? Convert.ToDateTime(reader["RunAt"]) : null,
                            BatchNo = reader["BatchNo"] != DBNull.Value ? (int?)Convert.ToInt32(reader["BatchNo"]) : null,
                            ID = reader["ID"] != DBNull.Value ? (int?)Convert.ToInt32(reader["ID"]) : null
                        };

                        // Helper to safely get decimal
                        decimal? GetDecimal(string col) => reader[col] != DBNull.Value ? Convert.ToDecimal(reader[col]) : null;

                        dto.TtlBgtDays = GetDecimal("TtlBgtDays");
                        dto.ActualTtlDays = GetDecimal("ActualTtlDays");
                        dto.GF = GetDecimal("GF");
                        dto.Machine = GetDecimal("Machine");
                        dto.MachineWP = GetDecimal("MachineWP");
                        dto.MANUAL = GetDecimal("MANUAL");
                        dto.ActualWeekly = GetDecimal("ActualWeekly");
                        dto.PresentWeeklyOff = GetDecimal("PresentWeeklyOff");
                        dto.HolidayOff = reader["HolidayOff"] != DBNull.Value ? Convert.ToInt32(reader["HolidayOff"]) : null;
                        dto.PaybleDays = GetDecimal("PaybleDays");
                        dto.ExtraDays = GetDecimal("ExtraDays");
                        dto.Absent = GetDecimal("Absent");
                        dto.LWP = GetDecimal("LWP");
                        dto.AdjustedDays = GetDecimal("AdjustedDays");
                        dto.BasicSalaryBud = GetDecimal("BasicSalaryBud");
                        dto.HRABud = GetDecimal("HRABud");
                        dto.CCABud = GetDecimal("CCABud");
                        dto.SpecialAllowanceBud = GetDecimal("SpecialAllowanceBud");
                        dto.DABud = GetDecimal("DABud");
                        dto.ReimbersmentBud = GetDecimal("ReimbersmentBud");
                        dto.FuelAndMaintenanceBud = GetDecimal("FuelAndMaintenanceBud");
                        dto.BooksAndPeriodicalsBud = GetDecimal("BooksAndPeriodicalsBud");
                        dto.ProfessionalAttireBud = GetDecimal("ProfessionalAttireBud");
                        dto.DriverWagesBud = GetDecimal("DriverWagesBud");
                        dto.MobileBillBud = GetDecimal("MobileBillBud");
                        dto.MealVoucherBud = GetDecimal("MealVoucherBud");
                        dto.MonthlyGrossCTCBud = GetDecimal("MonthlyGrossCTCBud");
                        dto.BasicSalaryActual = GetDecimal("BasicSalaryActual");
                        dto.HRAActual = GetDecimal("HRAActual");
                        dto.CCAActual = GetDecimal("CCAActual");
                        dto.SpecialAllowanceActual = GetDecimal("SpecialAllowanceActual");
                        dto.DAActual = GetDecimal("DAActual");
                        dto.ExtraDayAllowance = GetDecimal("ExtraDayAllowance");
                        dto.ReimbersmentActual = GetDecimal("ReimbersmentActual");
                        dto.FuelAndMaintenanceActual = GetDecimal("FuelAndMaintenanceActual");
                        dto.BooksAndPeriodicalsActual = GetDecimal("BooksAndPeriodicalsActual");
                        dto.ProfessionalAttireActual = GetDecimal("ProfessionalAttireActual");
                        dto.DriverWagesActual = GetDecimal("DriverWagesActual");
                        dto.MobileBillActual = GetDecimal("MobileBillActual");
                        dto.MealVoucherActual = GetDecimal("MealVoucherActual");
                        dto.PFEmployee = GetDecimal("PFEmployee");
                        dto.PFEmployer = GetDecimal("PFEmployer");
                        dto.PFTotal = GetDecimal("PFTotal");
                        dto.ESICEmployee = GetDecimal("ESICEmployee");
                        dto.ESICEmployer = GetDecimal("ESICEmployer");
                        dto.ESICTotal = GetDecimal("ESICTotal");
                        dto.TDS = GetDecimal("TDS");
                        dto.PTax = GetDecimal("PTax");
                        dto.Loan = GetDecimal("Loan");
                        dto.CashShort = GetDecimal("CashShort");
                        dto.DieselDeduction = GetDecimal("DieselDeduction");
                        dto.Penality = GetDecimal("Penality");
                        dto.Lwf = GetDecimal("Lwf");
                        dto.TotalDeductions = GetDecimal("TotalDeductions");
                        dto.Incentive = GetDecimal("Incentive");
                        dto.ARREAR = GetDecimal("ARREAR");
                        dto.Overtime = GetDecimal("Overtime");
                        dto.FoodingAllowance = GetDecimal("FoodingAllowance");
                        dto.MobileBill = GetDecimal("MobileBill");
                        dto.MonthlyGrossCTCActual = GetDecimal("MonthlyGrossCTCActual");
                        dto.MonthlyGrossCTCActualAfterDeductionAndAddOns = GetDecimal("MonthlyGrossCTCActualAfterDeductionAndAddOns");
                        dto.Payble_Days2 = GetDecimal("Payble_Days2");
                        dto.LeaveUsed = GetDecimal("LeaveUsed");
                        dto.OpeningEL = GetDecimal("OpeningEL");
                        dto.EarnedLeaveAcquired = GetDecimal("EarnedLeaveAcquired");
                        dto.EarnedLeaveUsed = GetDecimal("EarnedLeaveUsed");
                        dto.EarnedLeaveBalance = GetDecimal("EarnedLeaveBalance");
                        dto.OpeningCL = GetDecimal("OpeningCL");
                        dto.CasualLeaveAcquired = GetDecimal("CasualLeaveAcquired");
                        dto.CasualLeaveUsed = GetDecimal("CasualLeaveUsed");
                        dto.CasualLeaveBalance = GetDecimal("CasualLeaveBalance");
                        dto.OpeningCompoOff = GetDecimal("OpeningCompoOff");
                        dto.CompoOffAcquired = GetDecimal("CompoOffAcquired");
                        dto.CompoOffUsed = GetDecimal("CompoOffUsed");
                        dto.CompoOffBalance = GetDecimal("CompoOffBalance");
            return dto;
        }

        public async Task<(bool Success, string Message)> UploadSalaryProcessAsync(IFormFile file, string createdBy)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return (false, "No file uploaded");

                var ecode = await _context.tblEmployees.AsNoTracking()
                    .Where(e => e.IsActive == true && e.IsDeleted != true)
                    .Select(e => e.Ecode)
                    .FirstOrDefaultAsync();

                if (ecode == null)
                    return (false, "No active employees found in the system to validate against.");

                using (var stream = file.OpenReadStream())
                using (var workbook = new XLWorkbook(stream))
                {
                    var worksheet = workbook.Worksheet(1);
                    var rows = worksheet.RowsUsed().Skip(1); // Skip header

                    var dt = new DataTable();
                    // Create structure matching DB 
                    // Note: We'll construct the SQL INSERT statement dynamically or use BulkCopy if performance needed. 
                    // For simplicity and direct control given constraints, we will iterate and Insert.
                    // But to be efficient, let's use a single parameterized insert in a transaction or StringBuilder.
                    
                    // Given the number of columns, let's use a StringBuilder to construct values for batch insert 
                    // OR use SqlBulkCopy if possible. SqlBulkCopy is best for "Uploader" tasks.
                    
                    // Let's create a DataTable that matches tblSalaryProcess structure for BulkCopy
                    var bulkDt = new DataTable();
                    bulkDt.Columns.Add("Ecode", typeof(string));
                    bulkDt.Columns.Add("Location_Code", typeof(string));
                    bulkDt.Columns.Add("LocationName", typeof(string));
                    bulkDt.Columns.Add("EmployeeName", typeof(string));
                    bulkDt.Columns.Add("Designation", typeof(string));
                    bulkDt.Columns.Add("Department", typeof(string));
                    bulkDt.Columns.Add("MonthYear", typeof(string));
                    bulkDt.Columns.Add("TtlBgtDays", typeof(decimal));
                    bulkDt.Columns.Add("ActualTtlDays", typeof(decimal));
                    bulkDt.Columns.Add("GF", typeof(decimal));
                    bulkDt.Columns.Add("Machine", typeof(decimal));
                    bulkDt.Columns.Add("MachineWP", typeof(decimal));
                    bulkDt.Columns.Add("MANUAL", typeof(decimal));
                    bulkDt.Columns.Add("ActualWeekly", typeof(decimal));
                    bulkDt.Columns.Add("PresentWeeklyOff", typeof(decimal));
                    bulkDt.Columns.Add("HolidayOff", typeof(int));
                    bulkDt.Columns.Add("PaybleDays", typeof(decimal));
                    bulkDt.Columns.Add("ExtraDays", typeof(decimal));
                    bulkDt.Columns.Add("Absent", typeof(decimal));
                    bulkDt.Columns.Add("LWP", typeof(decimal));
                    bulkDt.Columns.Add("AdjustedDays", typeof(decimal));
                    bulkDt.Columns.Add("Status", typeof(string));
                    bulkDt.Columns.Add("BasicSalaryBud", typeof(decimal));
                    bulkDt.Columns.Add("HRABud", typeof(decimal));
                    bulkDt.Columns.Add("CCABud", typeof(decimal));
                    bulkDt.Columns.Add("SpecialAllowanceBud", typeof(decimal));
                    bulkDt.Columns.Add("DABud", typeof(decimal));
                    bulkDt.Columns.Add("ReimbersmentBud", typeof(decimal));
                    bulkDt.Columns.Add("FuelAndMaintenanceBud", typeof(decimal));
                    bulkDt.Columns.Add("BooksAndPeriodicalsBud", typeof(decimal));
                    bulkDt.Columns.Add("ProfessionalAttireBud", typeof(decimal));
                    bulkDt.Columns.Add("DriverWagesBud", typeof(decimal));
                    bulkDt.Columns.Add("MobileBillBud", typeof(decimal));
                    bulkDt.Columns.Add("MealVoucherBud", typeof(decimal));
                    bulkDt.Columns.Add("MonthlyGrossCTCBud", typeof(decimal));
                    bulkDt.Columns.Add("BasicSalaryActual", typeof(decimal));
                    bulkDt.Columns.Add("HRAActual", typeof(decimal));
                    bulkDt.Columns.Add("CCAActual", typeof(decimal));
                    bulkDt.Columns.Add("SpecialAllowanceActual", typeof(decimal));
                    bulkDt.Columns.Add("DAActual", typeof(decimal));
                    bulkDt.Columns.Add("ExtraDayAllowance", typeof(decimal));
                    bulkDt.Columns.Add("ReimbersmentActual", typeof(decimal));
                    bulkDt.Columns.Add("FuelAndMaintenanceActual", typeof(decimal));
                    bulkDt.Columns.Add("BooksAndPeriodicalsActual", typeof(decimal));
                    bulkDt.Columns.Add("ProfessionalAttireActual", typeof(decimal));
                    bulkDt.Columns.Add("DriverWagesActual", typeof(decimal));
                    bulkDt.Columns.Add("MobileBillActual", typeof(decimal));
                    bulkDt.Columns.Add("MealVoucherActual", typeof(decimal));
                    bulkDt.Columns.Add("PFEmployee", typeof(decimal));
                    bulkDt.Columns.Add("PFEmployer", typeof(decimal));
                    bulkDt.Columns.Add("PFTotal", typeof(decimal));
                    bulkDt.Columns.Add("ESICEmployee", typeof(decimal));
                    bulkDt.Columns.Add("ESICEmployer", typeof(decimal));
                    bulkDt.Columns.Add("ESICTotal", typeof(decimal));
                    bulkDt.Columns.Add("TDS", typeof(decimal));
                    bulkDt.Columns.Add("PTax", typeof(decimal));
                    bulkDt.Columns.Add("Loan", typeof(decimal));
                    bulkDt.Columns.Add("CashShort", typeof(decimal));
                    bulkDt.Columns.Add("DieselDeduction", typeof(decimal));
                    bulkDt.Columns.Add("Penality", typeof(decimal));
                    bulkDt.Columns.Add("Lwf", typeof(decimal));
                    bulkDt.Columns.Add("TotalDeductions", typeof(decimal));
                    bulkDt.Columns.Add("Incentive", typeof(decimal));
                    bulkDt.Columns.Add("ARREAR", typeof(decimal));
                    bulkDt.Columns.Add("Overtime", typeof(decimal));
                    bulkDt.Columns.Add("FoodingAllowance", typeof(decimal));
                    bulkDt.Columns.Add("MobileBill", typeof(decimal));
                    bulkDt.Columns.Add("MonthlyGrossCTCActual", typeof(decimal));
                    bulkDt.Columns.Add("MonthlyGrossCTCActualAfterDeductionAndAddOns", typeof(decimal));
                    bulkDt.Columns.Add("Payble_Days2", typeof(decimal));
                    bulkDt.Columns.Add("LeaveUsed", typeof(decimal));
                    bulkDt.Columns.Add("OpeningEL", typeof(decimal));
                    bulkDt.Columns.Add("EarnedLeaveAcquired", typeof(decimal));
                    bulkDt.Columns.Add("EarnedLeaveUsed", typeof(decimal));
                    bulkDt.Columns.Add("EarnedLeaveBalance", typeof(decimal));
                    bulkDt.Columns.Add("OpeningCL", typeof(decimal));
                    bulkDt.Columns.Add("CasualLeaveAcquired", typeof(decimal));
                    bulkDt.Columns.Add("CasualLeaveUsed", typeof(decimal));
                    bulkDt.Columns.Add("CasualLeaveBalance", typeof(decimal));
                    bulkDt.Columns.Add("OpeningCompoOff", typeof(decimal));
                    bulkDt.Columns.Add("CompoOffAcquired", typeof(decimal));
                    bulkDt.Columns.Add("CompoOffUsed", typeof(decimal));
                    bulkDt.Columns.Add("CompoOffBalance", typeof(decimal));
                    bulkDt.Columns.Add("MONTH", typeof(string));
                    bulkDt.Columns.Add("BatchNo", typeof(int));
                    bulkDt.Columns.Add("RunAt", typeof(DateTime));
                    bulkDt.Columns.Add("SalaryStatus", typeof(string));
                    bulkDt.Columns.Add("ID", typeof(int));
                    bulkDt.Columns.Add("CreatedOn", typeof(DateTime));
                    bulkDt.Columns.Add("CreatedBy", typeof(string));

                    foreach (var row in rows)
                    {
                        var dr = bulkDt.NewRow();
                        
                        // Map by index assuming the Excel follows the exact order as sample or common sense.
                        // Assuming header order matches the order in table definition for simplicity, 
                        // or we map by header name. Let's map by cell index assuming consistent format.
                        int c = 1;
                        dr["Ecode"] = row.Cell(c++).GetValue<string>();
                        dr["Location_Code"] = row.Cell(c++).GetValue<string>(); // Location_Code
                        dr["LocationName"] = row.Cell(c++).GetValue<string>();
                        dr["EmployeeName"] = row.Cell(c++).GetValue<string>();
                        dr["Designation"] = row.Cell(c++).GetValue<string>(); // designation (lowercase in sample head, map to proper)
                        dr["Department"] = row.Cell(c++).GetValue<string>();
                        var monthCell = row.Cell(c++);
                        if (monthCell.DataType == XLDataType.DateTime)
                        {
                            dr["MonthYear"] = monthCell.GetDateTime().ToString("MMM-yy");
                        }
                        else
                        {
                            dr["MonthYear"] = monthCell.GetValue<string>();
                        }
                        //dr["MonthYear"] = row.Cell(c++).GetValue<string>();
                        
                        decimal GetDec(int idx) => row.Cell(idx).IsEmpty() ? 0m : row.Cell(idx).GetValue<decimal>();
                        int GetInt(int idx) => row.Cell(idx).IsEmpty() ? 0 : row.Cell(idx).GetValue<int>();
                        string GetStr(int idx) => row.Cell(idx).GetValue<string>();
                        DateTime? GetDate(int idx) => row.Cell(idx).IsEmpty() ? null : (DateTime?)row.Cell(idx).GetDateTime();

                        dr["TtlBgtDays"] = GetDec(c++);
                        dr["ActualTtlDays"] = GetDec(c++);
                        dr["GF"] = GetDec(c++);
                        dr["Machine"] = GetDec(c++);
                        dr["MachineWP"] = GetDec(c++);
                        dr["MANUAL"] = GetDec(c++);
                        dr["ActualWeekly"] = GetDec(c++);
                        dr["PresentWeeklyOff"] = GetDec(c++);
                        dr["HolidayOff"] = GetInt(c++);
                        dr["PaybleDays"] = GetDec(c++);
                        dr["ExtraDays"] = GetDec(c++);
                        dr["Absent"] = GetDec(c++);
                        dr["LWP"] = GetDec(c++);
                        dr["AdjustedDays"] = GetDec(c++);
                        dr["Status"] = row.Cell(c++).GetValue<string>();
                        dr["BasicSalaryBud"] = GetDec(c++);
                        dr["HRABud"] = GetDec(c++);
                        dr["CCABud"] = GetDec(c++);
                        dr["SpecialAllowanceBud"] = GetDec(c++);
                        dr["DABud"] = GetDec(c++);
                        dr["ReimbersmentBud"] = GetDec(c++);
                        dr["FuelAndMaintenanceBud"] = GetDec(c++);
                        dr["BooksAndPeriodicalsBud"] = GetDec(c++);
                        dr["ProfessionalAttireBud"] = GetDec(c++);
                        dr["DriverWagesBud"] = GetDec(c++);
                        dr["MobileBillBud"] = GetDec(c++);
                        dr["MealVoucherBud"] = GetDec(c++);
                        dr["MonthlyGrossCTCBud"] = GetDec(c++);
                        dr["BasicSalaryActual"] = GetDec(c++);
                        dr["HRAActual"] = GetDec(c++);
                        dr["CCAActual"] = GetDec(c++);
                        dr["SpecialAllowanceActual"] = GetDec(c++);
                        dr["DAActual"] = GetDec(c++);
                        dr["ExtraDayAllowance"] = GetDec(c++);
                        dr["ReimbersmentActual"] = GetDec(c++);
                        dr["FuelAndMaintenanceActual"] = GetDec(c++);
                        dr["BooksAndPeriodicalsActual"] = GetDec(c++);
                        dr["ProfessionalAttireActual"] = GetDec(c++);
                        dr["DriverWagesActual"] = GetDec(c++);
                        dr["MobileBillActual"] = GetDec(c++);
                        dr["MealVoucherActual"] = GetDec(c++);
                        dr["PFEmployee"] = GetDec(c++);
                        dr["PFEmployer"] = GetDec(c++);
                        dr["PFTotal"] = GetDec(c++);
                        dr["ESICEmployee"] = GetDec(c++);
                        dr["ESICEmployer"] = GetDec(c++);
                        dr["ESICTotal"] = GetDec(c++);
                        dr["TDS"] = GetDec(c++);
                        dr["PTax"] = GetDec(c++);
                        dr["Loan"] = GetDec(c++);
                        dr["CashShort"] = GetDec(c++);
                        dr["DieselDeduction"] = GetDec(c++);
                        dr["Penality"] = GetDec(c++);
                        dr["Lwf"] = GetDec(c++);
                        dr["TotalDeductions"] = GetDec(c++);
                        dr["Incentive"] = GetDec(c++);
                        dr["ARREAR"] = GetDec(c++);
                        dr["Overtime"] = GetDec(c++);
                        dr["FoodingAllowance"] = GetDec(c++);
                        dr["MobileBill"] = GetDec(c++);
                        dr["MonthlyGrossCTCActual"] = GetDec(c++);
                        dr["MonthlyGrossCTCActualAfterDeductionAndAddOns"] = GetDec(c++);
                        dr["Payble_Days2"] = GetDec(c++);
                        dr["LeaveUsed"] = GetDec(c++);
                        dr["OpeningEL"] = GetDec(c++);
                        dr["EarnedLeaveAcquired"] = GetDec(c++);
                        dr["EarnedLeaveUsed"] = GetDec(c++);
                        dr["EarnedLeaveBalance"] = GetDec(c++);
                        dr["OpeningCL"] = GetDec(c++);
                        dr["CasualLeaveAcquired"] = GetDec(c++);
                        dr["CasualLeaveUsed"] = GetDec(c++);
                        dr["CasualLeaveBalance"] = GetDec(c++);
                        dr["OpeningCompoOff"] = GetDec(c++);
                        dr["CompoOffAcquired"] = GetDec(c++);
                        dr["CompoOffUsed"] = GetDec(c++);
                        dr["CompoOffBalance"] = GetDec(c++);
                        dr["MONTH"] = row.Cell(c++).GetValue<string>();
                        dr["BatchNo"] = GetInt(c++);

                        //var runAtStr = row.Cell(c++).GetValue<string>();
                        //if (DateTime.TryParse(runAtStr, out var dRun)) dr["RunAt"] = dRun;
                        //else dr["RunAt"] = DBNull.Value;

                        var cell = row.Cell(c++);
                        DateTime runAt;
                        if (cell.TryGetValue<DateTime>(out var excelDateTime))
                        {
                            // Excel already gave proper DateTime
                            runAt = excelDateTime;
                            dr["RunAt"] = runAt;
                        }
                        //else if (cell.TryGetValue<TimeSpan>(out var excelTime))
                        //{
                        //    // Combine with known date
                        //    var excelDate = new DateTime(2026, 2, 5); // from another column
                        //    runAt = excelDate.Date + excelTime;
                        //}
                        else
                        {
                            dr["RunAt"] = DBNull.Value;
                            
                        }

                        dr["SalaryStatus"] = row.Cell(c++).GetValue<string>();
                        dr["ID"] = GetInt(c++);

                        dr["CreatedOn"] = DateTime.Now;
                        if(createdBy == "System")
                        {
                            dr["CreatedBy"] = createdBy;
                        }
                        else
                        {
                            dr["CreatedBy"] = ecode;
                        }

                        bulkDt.Rows.Add(dr);
                    }

                    using var connection = _context.Database.GetDbConnection();
                    if (connection.State != ConnectionState.Open) await connection.OpenAsync();

                    using var bulkCopy = new SqlBulkCopy((SqlConnection)connection);
                    bulkCopy.DestinationTableName = "tblSalaryProcess";
                    
                    // Map columns explicitly if names don't match 1:1, but here we made them match
                    foreach (DataColumn col in bulkDt.Columns)
                    {
                        if (col.ColumnName == "Payble_Days2") // Map table column name difference
                            bulkCopy.ColumnMappings.Add("Payble_Days2", "Payble_Days2"); // Actually DB has Payble_Days2
                        else
                            bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);
                    }

                    await bulkCopy.WriteToServerAsync(bulkDt);
                }

                return (true, "Salary Process data uploaded successfully");
            }
            catch (Exception ex)
            {
                return (false, $"Error uploading data: {ex.Message}");
            }
        }

        private static ExecuteAndReponse Fail(HttpStatusCode code, string message) =>
            new ExecuteAndReponse { Status = false, Message = message, Code = code };
    }
}