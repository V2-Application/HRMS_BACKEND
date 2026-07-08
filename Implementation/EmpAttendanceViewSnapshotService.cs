using HRMSAPI.Data;
using HRMSAPI.DTO;
using HRMSAPI.Extension;
using HRMSAPI.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Roomsy.DTOS.GenericsResponses;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ClosedXML.Excel;
using static Emgu.CV.Stitching.Stitcher;
using Microsoft.Data.SqlClient;
using System.Data;

namespace HRMSAPI.Implementation
{
    public class EmpAttendanceViewSnapshotService : IEmpAttendanceViewSnapshotService
    {
        private readonly HRMSContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public EmpAttendanceViewSnapshotService(HRMSContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<FetchAndResponse> GetEmpAttendanceViewSnapshotsAsync(string month = null, int? status = null, string ecode = null, string batch = null, int? page = null, int? pageSize = null, string search = null)
        {
            try
            {
                // Set defaults if not provided
                if (string.IsNullOrWhiteSpace(month))
                {
                    month = DateTime.Now.ToString("MMM-yy");
                }

                if (!status.HasValue)
                {
                    status = 1;
                }

                // Validate month format
                if (!DateTime.TryParseExact(month, "MMM-yy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedMonth))
                {
                    return new FetchAndResponse()
                    {
                        Status = false,
                        Message = "Invalid month format. Expected format: MMM-YY (e.g., Jul-25)"
                    };
                }

                // If batch (B_MMM-YY_0001) is provided, filter strictly by parsed ID
                if (!string.IsNullOrWhiteSpace(batch))
                {
                    var parsedId = ParseBatchNoToId(batch);
                    if (!parsedId.HasValue)
                    {
                        return new FetchAndResponse()
                        {
                            Status = false,
                            Message = "Invalid batch format. Expected format: B_MMM-YY_0001",
                        };
                    }

                    var singleRow = await _context.EmpAttendanceViewSnapshots.AsNoTracking()
                        .Where(x => x.BatchNo == parsedId.Value)
                        .OrderBy(x => x.Ecode)
                        .ToListAsync();

                    var singleResults = singleRow.Select((item, index) => new EmpAttendanceViewSnapshotResponseDto
                    {
                        Ecode = item.Ecode,
                        Location_Code = item.Location_Code,
                        Location_Name = item.Location_Name,
                        Employee_Name = item.Employee_Name,
                        designation = item.designation,
                        department = item.department,
                        Month_Year = item.Month_Year,
                        ttl_bgt_days = item.ttl_bgt_days,
                        actualttl_days = item.actualttl_days,
                        Machine = item.Machine,
                        MANUAL = item.MANUAL,
                        actualweekly = item.actualweekly,
                        presentweeklyoff = item.presentweeklyoff,
                        HolidayOff = item.HolidayOff,
                        paybledays = item.paybledays,
                        extradays = item.extradays,
                        Absent = item.Absent,
                        LWP = item.LWP,
                        Status = item.Status,
                        BasicSalary_Bud__ = item.BasicSalary_Bud__,
                        HRA_Bud__ = item.HRA_Bud__,
                        CCA_Bud__ = item.CCA_Bud__,
                        SpecialAllowance_Bud__ = item.SpecialAllowance_Bud__,
                        DA_Bud__ = item.DA_Bud__,
                        Reimbersment_Bud__ = item.Reimbersment_Bud__,
                        Fuel_and_Maintenance_Bud__ = item.Fuel_and_Maintenance_Bud__,
                        Books_and_Periodicals_Bud__ = item.Books_and_Periodicals_Bud__,
                        Professional_Attire_Bud__ = item.Professional_Attire_Bud__,
                        Driver_Wages_Bud__ = item.Driver_Wages_Bud__,
                        Mobile_Bill_Bud__ = item.Mobile_Bill_Bud__,
                        Meal_Voucher_Bud__ = item.Meal_Voucher_Bud__,
                        Monthly_Gross_CTC_Bud__ = item.Monthly_Gross_CTC_Bud__,
                        BasicSalary_Actual_ = item.BasicSalary_Actual_,
                        HRA_Actual_ = item.HRA_Actual_,
                        CCA_Actual_ = item.CCA_Actual_,
                        SpecialAllowance_Actual_ = item.SpecialAllowance_Actual_,
                        DA_Actual_ = item.DA_Actual_,
                        ExtraDayAllowance = item.ExtraDayAllowance,
                        Reimbersment_Actual_ = item.Reimbersment_Actual_,
                        Fuel_and_Maintenance_Actual_ = item.Fuel_and_Maintenance_Actual_,
                        Books_and_Periodicals_Actual_ = item.Books_and_Periodicals_Actual_,
                        Professional_Attire_Actual_ = item.Professional_Attire_Actual_,
                        Driver_Wages_Actual_ = item.Driver_Wages_Actual_,
                        Mobile_Bill_Actual_ = item.Mobile_Bill_Actual_,
                        Meal_Voucher_Actual_ = item.Meal_Voucher_Actual_,
                        PF_Employee_ = item.PF_Employee_,
                        PF_Employeer_ = item.PF_Employeer_,
                        PF_Total_ = item.PF_Total_,
                        ESIC_Employee_ = item.ESIC_Employee_,
                        ESIC_Employeer_ = item.ESIC_Employeer_,
                        ESIC_Total_ = item.ESIC_Total_,
                        TDS = item.TDS,
                        PTax = item.PTax,
                        Loan = item.Loan,
                        CashShort = item.CashShort,
                        DieselDeduction = item.DieselDeduction,
                        Penality = item.Penality,
                        Lwf = item.Lwf,
                        TotalDeductions = item.TotalDeductions,
                        Incentive = item.Incentive,
                        ARREAR = item.ARREAR,
                        Overtime = item.Overtime,
                        Fooding_Allowance = item.Fooding_Allowance,
                        Mobile_Bill = item.Mobile_Bill,
                        Monthly_Gross_CTC_Actual_ = item.Monthly_Gross_CTC_Actual_,
                        Monthly_Gross_CTC_Actual_After_Deduction_AND_AddONS_ = item.Monthly_Gross_CTC_Actual_After_Deduction_AND_AddONS_,
                        Payble_Days = item.Payble_Days,
                        Leave_Used = item.Leave_Used,
                        Opening_EL = item.Opening_EL,
                        EarnedLeaveAcquired = item.EarnedLeaveAcquired,
                        EarnedLeaveUsed = item.EarnedLeaveUsed,
                        EarnedLeaveBalance = item.EarnedLeaveBalance,
                        Opening_CL = item.Opening_CL,
                        CasualLeaveAcquired = item.CasualLeaveAcquired,
                        CasualLeaveUsed = item.CasualLeaveUsed,
                        CasualLeaveBalance = item.CasualLeaveBalance,
                        Opening_CompoOff = item.Opening_CompoOff,
                        CompoOffAcquired = item.CompoOffAcquired,
                        CompoOffUsed = item.CompoOffUsed,
                        CompoOffBalance = item.CompoOffBalance,
                        MONTH = item.MONTH,
                        BatchNo = FormatBatchNo(item.MONTH, (int)item.ID),
                        RunAt = item.RunAt,
                        SalaryStatus = item.SalaryStatus,
                        ID = item.ID
                    }).ToList();

                    return new FetchAndResponse()
                    {
                        Status = true,
                        Message = singleResults.Count == 0 ? "No record found for the specified BatchId." : $"Found {singleResults.Count} record for the BatchId.",
                        Data = singleResults
                    };
                }

                // Query the database (filter on MONTH + SalaryStatus is index-backed: IX_Snapshot_Month_SalaryStatus_Ecode)
                var query = _context.EmpAttendanceViewSnapshots.AsNoTracking().AsQueryable()
                    .Where(x => x.MONTH == month && x.SalaryStatus == status.Value);

                if (!string.IsNullOrWhiteSpace(ecode))
                {
                    query = query.Where(x => x.Ecode == ecode);
                }

                // Server-side search across the main text columns (keeps the grid's search working without
                // shipping the whole month to the client).
                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.Trim();
                    query = query.Where(x =>
                        (x.Ecode != null && x.Ecode.Contains(s)) ||
                        (x.Employee_Name != null && x.Employee_Name.Contains(s)) ||
                        (x.Location_Code != null && x.Location_Code.Contains(s)) ||
                        (x.Location_Name != null && x.Location_Name.Contains(s)) ||
                        (x.department != null && x.department.Contains(s)) ||
                        (x.designation != null && x.designation.Contains(s)) ||
                        (x.Month_Year != null && x.Month_Year.Contains(s)));
                }

                // Total matching rows (before paging) so the grid can render its pager.
                var totalCount = await query.CountAsync();

                query = query.OrderBy(x => x.Ecode);

                // Apply paging only when a positive pageSize is requested; otherwise return everything
                // (used by the Export action, which needs all rows).
                if (pageSize.HasValue && pageSize.Value > 0)
                {
                    var pageNumber = (page.HasValue && page.Value > 0) ? page.Value : 1;
                    query = query.Skip((pageNumber - 1) * pageSize.Value).Take(pageSize.Value);
                }

                var data = await query.ToListAsync();

                // Transform to response DTO with BatchNo formatting
                var results = data.Select((item, index) => new EmpAttendanceViewSnapshotResponseDto
                {
                    Ecode = item.Ecode,
                    Location_Code = item.Location_Code,
                    Location_Name = item.Location_Name,
                    Employee_Name = item.Employee_Name,
                    designation = item.designation,
                    department = item.department,
                    Month_Year = item.Month_Year,
                    ttl_bgt_days = item.ttl_bgt_days,
                    actualttl_days = item.actualttl_days,
                    Machine = item.Machine,
                    MANUAL = item.MANUAL,
                    actualweekly = item.actualweekly,
                    presentweeklyoff = item.presentweeklyoff,
                    HolidayOff = item.HolidayOff,
                    paybledays = item.paybledays,
                    extradays = item.extradays,
                    Absent = item.Absent,
                    LWP = item.LWP,
                    Status = item.Status,
                    BasicSalary_Bud__ = item.BasicSalary_Bud__,
                    HRA_Bud__ = item.HRA_Bud__,
                    CCA_Bud__ = item.CCA_Bud__,
                    SpecialAllowance_Bud__ = item.SpecialAllowance_Bud__,
                    DA_Bud__ = item.DA_Bud__,
                    Reimbersment_Bud__ = item.Reimbersment_Bud__,
                    Fuel_and_Maintenance_Bud__ = item.Fuel_and_Maintenance_Bud__,
                    Books_and_Periodicals_Bud__ = item.Books_and_Periodicals_Bud__,
                    Professional_Attire_Bud__ = item.Professional_Attire_Bud__,
                    Driver_Wages_Bud__ = item.Driver_Wages_Bud__,
                    Mobile_Bill_Bud__ = item.Mobile_Bill_Bud__,
                    Meal_Voucher_Bud__ = item.Meal_Voucher_Bud__,
                    Monthly_Gross_CTC_Bud__ = item.Monthly_Gross_CTC_Bud__,
                    BasicSalary_Actual_ = item.BasicSalary_Actual_,
                    HRA_Actual_ = item.HRA_Actual_,
                    CCA_Actual_ = item.CCA_Actual_,
                    SpecialAllowance_Actual_ = item.SpecialAllowance_Actual_,
                    DA_Actual_ = item.DA_Actual_,
                    ExtraDayAllowance = item.ExtraDayAllowance,
                    Reimbersment_Actual_ = item.Reimbersment_Actual_,
                    Fuel_and_Maintenance_Actual_ = item.Fuel_and_Maintenance_Actual_,
                    Books_and_Periodicals_Actual_ = item.Books_and_Periodicals_Actual_,
                    Professional_Attire_Actual_ = item.Professional_Attire_Actual_,
                    Driver_Wages_Actual_ = item.Driver_Wages_Actual_,
                    Mobile_Bill_Actual_ = item.Mobile_Bill_Actual_,
                    Meal_Voucher_Actual_ = item.Meal_Voucher_Actual_,
                    PF_Employee_ = item.PF_Employee_,
                    PF_Employeer_ = item.PF_Employeer_,
                    PF_Total_ = item.PF_Total_,
                    ESIC_Employee_ = item.ESIC_Employee_,
                    ESIC_Employeer_ = item.ESIC_Employeer_,
                    ESIC_Total_ = item.ESIC_Total_,
                    TDS = item.TDS,
                    PTax = item.PTax,
                    Loan = item.Loan,
                    CashShort = item.CashShort,
                    DieselDeduction = item.DieselDeduction,
                    Penality = item.Penality,
                    Lwf = item.Lwf,
                    TotalDeductions = item.TotalDeductions,
                    Incentive = item.Incentive,
                    ARREAR = item.ARREAR,
                    Overtime = item.Overtime,
                    Fooding_Allowance = item.Fooding_Allowance,
                    Mobile_Bill = item.Mobile_Bill,
                    Monthly_Gross_CTC_Actual_ = item.Monthly_Gross_CTC_Actual_,
                    Monthly_Gross_CTC_Actual_After_Deduction_AND_AddONS_ = item.Monthly_Gross_CTC_Actual_After_Deduction_AND_AddONS_,
                    Payble_Days = item.Payble_Days,
                    Leave_Used = item.Leave_Used,
                    Opening_EL = item.Opening_EL,
                    EarnedLeaveAcquired = item.EarnedLeaveAcquired,
                    EarnedLeaveUsed = item.EarnedLeaveUsed,
                    EarnedLeaveBalance = item.EarnedLeaveBalance,
                    Opening_CL = item.Opening_CL,
                    CasualLeaveAcquired = item.CasualLeaveAcquired,
                    CasualLeaveUsed = item.CasualLeaveUsed,
                    CasualLeaveBalance = item.CasualLeaveBalance,
                    Opening_CompoOff = item.Opening_CompoOff,
                    CompoOffAcquired = item.CompoOffAcquired,
                    CompoOffUsed = item.CompoOffUsed,
                    CompoOffBalance = item.CompoOffBalance,
                    MONTH = item.MONTH,
                    BatchNo = FormatBatchNo(item.MONTH, (int)item.ID), // Format as B_MMM-YY_0001
                    RunAt = item.RunAt,
                    SalaryStatus = item.SalaryStatus,
                    ID = item.ID
                }).ToList();

                return new FetchAndResponse()
                {
                    Status = true,
                    Message = $"Found {totalCount} records for month {month} with status {status}",
                    Data = results,
                    TotalCount = totalCount
                };
            }
            catch (Exception ex)
            {
                return new FetchAndResponse()
                {
                    Status = false,
                    Message = ex.Message,
                    Data = new List<EmpAttendanceViewSnapshotResponseDto>()
                };
            }
        }

        private static string FormatBatchNo(string month, int serialNumber)
        {
            // Format: B_MMM-YY_0001
            return $"B_{month}_{serialNumber.ToString("D4")}";
        }

        private static int? ParseBatchNoToId(string batchNo)
        {
            try
            {
                // Format: B_MMM-YY_0001
                if (string.IsNullOrWhiteSpace(batchNo) || !batchNo.StartsWith("B_"))
                {
                    return null;
                }

                var parts = batchNo.Split('_');
                if (parts.Length != 3)
                {
                    return null;
                }

                // Extract the serial number part (last part)
                var serialNumberPart = parts[2];
                if (int.TryParse(serialNumberPart, out int id))
                {
                    return id;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private static string FormatIdByStatus(long id, int status)
        {
            return status switch
            {
                2 => $"GTB_{id.ToString("D8")}", // GivenToBank - GTB_ID padded with 8 zeros
                3 => $"PIC_{id.ToString("D8")}", // PaidInCash - PIC_ID padded with 8 zeros
                4 => $"PBB_{id.ToString("D8")}", // PaidByBank - PBB_ID padded with 8 zeros
                5 => $"RBB_{id.ToString("D8")}", // ReturnByBank - RBB_ID padded with 8 zeros
                _ => $"UNK_{id.ToString("D8")}"  // Unknown status
            };
        }

        private static long? ParseTransactionIdToId(string transactionId)
        {
            try
            {
                // Format: GTB_00000001, PIC_00000001, PBB_00000001, RBB_00000001
                if (string.IsNullOrWhiteSpace(transactionId))
                {
                    return null;
                }

                var parts = transactionId.Split('_');
                if (parts.Length != 2)
                {
                    return null;
                }

                // Extract the ID part (last part)
                var idPart = parts[1];
                if (long.TryParse(idPart, out long id))
                {
                    return id;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        public async Task<FetchAndResponse> GetSalaryStatusList(int status, string month = null)
        {
            try
            {
                // Set default to current month if not provided
                if (string.IsNullOrWhiteSpace(month))
                {
                    month = DateTime.Now.ToString("MMM-yy");
                }

                // Validate month format
                if (!DateTime.TryParseExact(month, "MMM-yy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedMonth))
                {
                    return new FetchAndResponse()
                    {
                        Status = false,
                        Message = "Invalid month format. Expected format: MMM-YY (e.g., Jul-25)"
                    };
                }

                // Validate status
                if (status < 2 || status > 5)
                {
                    return new FetchAndResponse()
                    {
                        Status = false,
                        Message = "Status must be 2 (GivenToBank), 3 (PaidInCash), 4 (PaidByBank), or 5 (ReturnByBank)."
                    };
                }

                List<BankTransferAndPaidInCashResponseDto> results;

                if (status == 2)
                {
                    // Fetch from GivenToBank table
                    var data = await _context.GivenToBanks.AsNoTracking()
                        .Where(x => x.Month==month && 
                                    x.IsActive == true &&
                                    x.IsDeleted != true)
                        .OrderBy(x => x.Ecode)
                        .ToListAsync();

                    // Resolve employee names and creator info
                    var ecodes = data.Select(d => d.Ecode).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
                    var empByEcode = await _context.tblEmployees.AsNoTracking()
                        .Where(e => ecodes.Contains(e.Ecode))
                        .Select(e => new { e.Ecode, e.FULL_NAME, e.FirstName, e.LastName })
                        .ToListAsync();
                    var empNameByEcode = empByEcode.ToDictionary(
                        k => k.Ecode,
                        v => string.IsNullOrWhiteSpace(v.FULL_NAME) ? ($"{(v.FirstName ?? string.Empty).Trim()} {(v.LastName ?? string.Empty).Trim()}".Trim()) : v.FULL_NAME.Trim(),
                        StringComparer.OrdinalIgnoreCase);

                    var createdByIds = data.Select(d => d.CreatedBy).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
                    var createdByLongIds = createdByIds.Select(s => long.TryParse(s, out var id) ? (long?)id : null).Where(id => id.HasValue).Select(id => id.Value).Distinct().ToList();
                    var creators = await _context.tblEmployees.AsNoTracking()
                        .Where(e => createdByLongIds.Contains(e.EmployeeId))
                        .Select(e => new { e.EmployeeId, e.FULL_NAME, e.FirstName, e.LastName, e.Ecode })
                        .ToListAsync();
                    var creatorById = creators.ToDictionary(
                        k => k.EmployeeId,
                        v => new { Name = string.IsNullOrWhiteSpace(v.FULL_NAME) ? ($"{(v.FirstName ?? string.Empty).Trim()} {(v.LastName ?? string.Empty).Trim()}".Trim()) : v.FULL_NAME.Trim(), v.Ecode });

                    results = data.Select(x => new BankTransferAndPaidInCashResponseDto
                    {
                        Id = x.BankTransferId, // GivenToBank ID
                        Ecode = x.Ecode,
                        EmployeeName = (x.Ecode != null && empNameByEcode.TryGetValue(x.Ecode, out var empName)) ? empName : null,
                        Month = x.Month,
                        A_C = x.A_C,
                        BankTransfer = x.BankTransfer,
                        CreatedBy = x.CreatedBy,
                        CreatedByName = (long.TryParse(x.CreatedBy, out var cbid) && creatorById.TryGetValue(cbid, out var cinfo)) ? cinfo.Name : null,
                        CreatedByEcode = (long.TryParse(x.CreatedBy, out var cbid2) && creatorById.TryGetValue(cbid2, out var cinfo2)) ? cinfo2.Ecode : null,
                        CreatedOn = x.CreatedOn,
                        LastUpdatedBy = x.LastUpdatedBy,
                        LastUpdatedOn = x.LastUpdatedOn,
                        IsActive = x.IsActive,
                        IsDeleted = x.IsDeleted,
                        BatchId = FormatBatchNo(x.Month,(int)x.BatchId),
                        FormattedId = FormatIdByStatus(x.BankTransferId, 2) // GivenToBank
                    }).ToList();
                }
                else if (status == 3)
                {
                    // Fetch from PaidInCash table
                    var data = await _context.PaidInCashes.AsNoTracking()
                        .Where(x => x.Month == month &&
                                    x.IsActive == true &&
                                    x.IsDeleted != true)
                        .OrderBy(x => x.Ecode)
                        .ToListAsync();

                    var ecodes = data.Select(d => d.Ecode).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
                    var empByEcode = await _context.tblEmployees.AsNoTracking()
                        .Where(e => ecodes.Contains(e.Ecode))
                        .Select(e => new { e.Ecode, e.FULL_NAME, e.FirstName, e.LastName })
                        .ToListAsync();
                    var empNameByEcode = empByEcode.ToDictionary(
                        k => k.Ecode,
                        v => string.IsNullOrWhiteSpace(v.FULL_NAME) ? ($"{(v.FirstName ?? string.Empty).Trim()} {(v.LastName ?? string.Empty).Trim()}".Trim()) : v.FULL_NAME.Trim(),
                        StringComparer.OrdinalIgnoreCase);

                    var createdByIds = data.Select(d => d.CreatedBy).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
                    var createdByLongIds = createdByIds.Select(s => long.TryParse(s, out var id) ? (long?)id : null).Where(id => id.HasValue).Select(id => id.Value).Distinct().ToList();
                    var creators = await _context.tblEmployees.AsNoTracking()
                        .Where(e => createdByLongIds.Contains(e.EmployeeId))
                        .Select(e => new { e.EmployeeId, e.FULL_NAME, e.FirstName, e.LastName, e.Ecode })
                        .ToListAsync();
                    var creatorById = creators.ToDictionary(
                        k => k.EmployeeId,
                        v => new { Name = string.IsNullOrWhiteSpace(v.FULL_NAME) ? ($"{(v.FirstName ?? string.Empty).Trim()} {(v.LastName ?? string.Empty).Trim()}".Trim()) : v.FULL_NAME.Trim(), v.Ecode });

                    results = data.Select(x => new BankTransferAndPaidInCashResponseDto
                    {
                        Id = x.PaidInCashId, // PaidInCash ID
                        Ecode = x.Ecode,
                        EmployeeName = (x.Ecode != null && empNameByEcode.TryGetValue(x.Ecode, out var empName)) ? empName : null,
                        Month = x.Month,
                        A_C = x.A_C,
                        BankTransfer = x.BankTransfer,
                        CreatedBy = x.CreatedBy,
                        CreatedByName = (long.TryParse(x.CreatedBy, out var cbid) && creatorById.TryGetValue(cbid, out var cinfo)) ? cinfo.Name : null,
                        CreatedByEcode = (long.TryParse(x.CreatedBy, out var cbid2) && creatorById.TryGetValue(cbid2, out var cinfo2)) ? cinfo2.Ecode : null,
                        CreatedOn = x.CreatedOn,
                        LastUpdatedBy = x.LastUpdatedBy,
                        LastUpdatedOn = x.LastUpdatedOn,
                        IsActive = x.IsActive,
                        IsDeleted = x.IsDeleted,
                        BatchId = FormatBatchNo(x.Month, (int)x.BatchId),
                        FormattedId = FormatIdByStatus(x.PaidInCashId, 3) // PaidInCash
                    }).ToList();
                }
                else if (status == 4)
                {
                    // Fetch from PaidByBank table
                    var data = await _context.PaidByBanks.AsNoTracking()
                        .Where(x => x.Month == month &&
                                    x.IsActive == true &&
                                    x.IsDeleted != true)
                        .OrderBy(x => x.Ecode)
                        .ToListAsync();

                    var ecodes = data.Select(d => d.Ecode).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
                    var empByEcode = await _context.tblEmployees.AsNoTracking()
                        .Where(e => ecodes.Contains(e.Ecode))
                        .Select(e => new { e.Ecode, e.FULL_NAME, e.FirstName, e.LastName })
                        .ToListAsync();
                    var empNameByEcode = empByEcode.ToDictionary(
                        k => k.Ecode,
                        v => string.IsNullOrWhiteSpace(v.FULL_NAME) ? ($"{(v.FirstName ?? string.Empty).Trim()} {(v.LastName ?? string.Empty).Trim()}".Trim()) : v.FULL_NAME.Trim(),
                        StringComparer.OrdinalIgnoreCase);

                    var createdByIds = data.Select(d => d.CreatedBy).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
                    var createdByLongIds = createdByIds.Select(s => long.TryParse(s, out var id) ? (long?)id : null).Where(id => id.HasValue).Select(id => id.Value).Distinct().ToList();
                    var creators = await _context.tblEmployees.AsNoTracking()
                        .Where(e => createdByLongIds.Contains(e.EmployeeId))
                        .Select(e => new { e.EmployeeId, e.FULL_NAME, e.FirstName, e.LastName, e.Ecode })
                        .ToListAsync();
                    var creatorById = creators.ToDictionary(
                        k => k.EmployeeId,
                        v => new { Name = string.IsNullOrWhiteSpace(v.FULL_NAME) ? ($"{(v.FirstName ?? string.Empty).Trim()} {(v.LastName ?? string.Empty).Trim()}".Trim()) : v.FULL_NAME.Trim(), v.Ecode });

                    results = data.Select(x => new BankTransferAndPaidInCashResponseDto
                    {
                        Id = x.BankTransferId, // PaidByBank ID (uses BankTransferId field)
                        Ecode = x.Ecode,
                        EmployeeName = (x.Ecode != null && empNameByEcode.TryGetValue(x.Ecode, out var empName)) ? empName : null,
                        Month = x.Month,
                        A_C = x.A_C,
                        BankTransfer = x.BankTransfer,
                        CreatedBy = x.CreatedBy,
                        CreatedByName = (long.TryParse(x.CreatedBy, out var cbid) && creatorById.TryGetValue(cbid, out var cinfo)) ? cinfo.Name : null,
                        CreatedByEcode = (long.TryParse(x.CreatedBy, out var cbid2) && creatorById.TryGetValue(cbid2, out var cinfo2)) ? cinfo2.Ecode : null,
                        CreatedOn = x.CreatedOn,
                        LastUpdatedBy = x.LastUpdatedBy,
                        LastUpdatedOn = x.LastUpdatedOn,
                        IsActive = x.IsActive,
                        IsDeleted = x.IsDeleted,
                        BatchId = FormatBatchNo(x.Month, (int)x.BatchId),
                        FormattedId = FormatIdByStatus(x.BankTransferId, 4) // PaidByBank
                    }).ToList();
                }
                else // status == 5
                {
                    // Fetch from ReturnByBank table
                    var data = await _context.ReturnByBankNews.AsNoTracking()
                        .Where(x => x.Month == month &&
                                    x.IsActive == true &&
                                    x.IsDeleted != true)
                        .OrderBy(x => x.Ecode)
                        .ToListAsync();

                    var ecodes = data.Select(d => d.Ecode).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
                    var empByEcode = await _context.tblEmployees.AsNoTracking()
                        .Where(e => ecodes.Contains(e.Ecode))
                        .Select(e => new { e.Ecode, e.FULL_NAME, e.FirstName, e.LastName })
                        .ToListAsync();
                    var empNameByEcode = empByEcode.ToDictionary(
                        k => k.Ecode,
                        v => string.IsNullOrWhiteSpace(v.FULL_NAME) ? ($"{(v.FirstName ?? string.Empty).Trim()} {(v.LastName ?? string.Empty).Trim()}".Trim()) : v.FULL_NAME.Trim(),
                        StringComparer.OrdinalIgnoreCase);

                    var createdByIds = data.Select(d => d.CreatedBy).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
                    var createdByLongIds = createdByIds.Select(s => long.TryParse(s, out var id) ? (long?)id : null).Where(id => id.HasValue).Select(id => id.Value).Distinct().ToList();
                    var creators = await _context.tblEmployees.AsNoTracking()
                        .Where(e => createdByLongIds.Contains(e.EmployeeId))
                        .Select(e => new { e.EmployeeId, e.FULL_NAME, e.FirstName, e.LastName, e.Ecode })
                        .ToListAsync();
                    var creatorById = creators.ToDictionary(
                        k => k.EmployeeId,
                        v => new { Name = string.IsNullOrWhiteSpace(v.FULL_NAME) ? ($"{(v.FirstName ?? string.Empty).Trim()} {(v.LastName ?? string.Empty).Trim()}".Trim()) : v.FULL_NAME.Trim(), v.Ecode });

                    results = data.Select(x => new BankTransferAndPaidInCashResponseDto
                    {
                        Id = x.BankTransferId, // ReturnByBank ID
                        Ecode = x.Ecode,
                        EmployeeName = (x.Ecode != null && empNameByEcode.TryGetValue(x.Ecode, out var empName)) ? empName : null,
                        Month = x.Month,
                        A_C = x.A_C,
                        ReturnByBank1 = x.BankTransfer,
                        CreatedBy = x.CreatedBy,
                        CreatedByName = (long.TryParse(x.CreatedBy, out var cbid) && creatorById.TryGetValue(cbid, out var cinfo)) ? cinfo.Name : null,
                        CreatedByEcode = (long.TryParse(x.CreatedBy, out var cbid2) && creatorById.TryGetValue(cbid2, out var cinfo2)) ? cinfo2.Ecode : null,
                        CreatedOn = x.CreatedOn,
                        LastUpdatedBy = x.LastUpdatedBy,
                        LastUpdatedOn = x.LastUpdatedOn,
                        IsActive = x.IsActive,
                        IsDeleted = x.IsDeleted,
                        BatchId = FormatBatchNo(x.Month, (int)x.BatchId),
                        FormattedId = FormatIdByStatus(x.BankTransferId, 5) // ReturnByBank
                    }).ToList();
                }

                // Check if no data found
                if (results.Count == 0)
                {
                    return new FetchAndResponse()
                    {
                        Status = false,
                        Message = "No data found for the specified criteria.",
                        Data = new List<BankTransferAndPaidInCashResponseDto>()
                    };
                }

                return new FetchAndResponse()
                {
                    Status = true,
                    Message = $"Found {results.Count} records for month {month}",
                    Data = results
                };
            }
            catch (Exception ex)
            {
                return new FetchAndResponse()
                {
                    Status = false,
                    Message = ex.Message,
                    Data = new List<BankTransferAndPaidInCashResponseDto>()
                };
            }
        }

        public async Task<ExecuteAndReponse> SalaryProcessToGivenToBankOrPaidByCash(long id, int status)
        {
            try
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                
                // Get the EmpAttendanceViewSnapshot record
                var snapshot = await _context.EmpAttendanceViewSnapshots
                    .FirstOrDefaultAsync(x => x.ID == id);
                    
                if (snapshot == null)
                {
                    return new ExecuteAndReponse
                    {
                        Status = false,
                        Message = "Record not found with the given ID."
                    };
                }
                
                // Update SalaryStatus
                snapshot.SalaryStatus = status;
                _context.EmpAttendanceViewSnapshots.Update(snapshot);
                
                // If status is 2, create GivenToBank record
                if (status == 2)
                {
                    // Get employee details for A_C
                    var employee = await _context.tblEmployees.AsNoTracking().AsQueryable()
                        .FirstOrDefaultAsync(x => x.Ecode == snapshot.Ecode);
                        
                    if (employee == null)
                    {
                        return new ExecuteAndReponse
                        {
                            Status = false,
                            Message = $"Employee not found for Ecode: {snapshot.Ecode}"
                        };
                    }
                    
                    // Get current user from session
                    var identity = _httpContextAccessor.HttpContext?.User?.Identity as ClaimsIdentity;
                    var user = AuthenticUserDetails.GetCurrentUserDetails(identity);
                    var createdBy = user?.EmployeeId ?? "System";
                    
                    // Create GivenToBank record - copy BatchNo to BatchId
                    var givenToBank = new GivenToBank
                    {
                        Ecode = snapshot.Ecode,
                        Month = snapshot.Month_Year,
                        A_C = employee.A_C_NO,
                        BankTransfer = snapshot.Monthly_Gross_CTC_Actual_After_Deduction_AND_AddONS_.ToString(),
                        BatchId = (int)snapshot.ID, // Copy BatchNo from snapshot to BatchId
                        CreatedBy = createdBy,
                        CreatedOn = DateTime.Now,
                        IsActive = true,
                        IsDeleted = false
                    };
                    
                    _context.GivenToBanks.Add(givenToBank);
                }
                // If status is 3, create PaidInCash record
                else if (status == 3)
                {
                    // Get employee details for A_C
                    var employee = await _context.tblEmployees.AsNoTracking().AsQueryable()
                        .FirstOrDefaultAsync(x => x.Ecode == snapshot.Ecode);
                        
                    if (employee == null)
                    {
                        return new ExecuteAndReponse
                        {
                            Status = false,
                            Message = $"Employee not found for Ecode: {snapshot.Ecode}"
                        };
                    }
                    
                    // Get current user from session
                    var identity = _httpContextAccessor.HttpContext?.User?.Identity as ClaimsIdentity;
                    var user = AuthenticUserDetails.GetCurrentUserDetails(identity);
                    var createdBy = user?.EmployeeId ?? "System";
                    
                    // Create PaidInCash record - copy BatchNo to BatchId
                    var paidInCash = new PaidInCash
                    {
                        Ecode = snapshot.Ecode,
                        Month = snapshot.Month_Year,
                        A_C = employee.A_C_NO,
                        BankTransfer = snapshot.Monthly_Gross_CTC_Actual_After_Deduction_AND_AddONS_.ToString(),
                        BatchId = (int)snapshot.ID, // Copy BatchNo from snapshot to BatchId
                        CreatedBy = createdBy,
                        CreatedOn = DateTime.Now,
                        IsActive = true,
                        IsDeleted = false
                    };
                    
                    _context.PaidInCashes.Add(paidInCash);
                }
                
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                
                return new ExecuteAndReponse
                {
                    Status = true,
                    Message = status == 2 ? 
                        "Salary status updated and bank record created successfully." : 
                        status == 3 ?
                        "Salary status updated and paid in cash record created successfully." :
                        "Salary status updated successfully."
                };
            }
            catch (Exception ex)
            {
                return new ExecuteAndReponse
                {
                    Status = false,
                    Message = ex.Message
                };
            }
        }

        public async Task<ExecuteAndReponse> GivenToBankToPaidByBankOrReturnFromBank(long id, int statusId, string batchId)
        {
            try
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                
                // Get the GivenToBank record
                var givenToBankRecord = await _context.GivenToBanks
                    .FirstOrDefaultAsync(x => x.BankTransferId == id);
                    
                if (givenToBankRecord == null)
                {
                    return new ExecuteAndReponse
                    {
                        Status = false,
                        Message = "GivenToBank record not found with the given ID."
                    };
                }
                
                // Get current user from session
                var identity = _httpContextAccessor.HttpContext?.User?.Identity as ClaimsIdentity;
                var user = AuthenticUserDetails.GetCurrentUserDetails(identity);
                var updatedBy = user?.EmployeeId ?? "System";
                
                if (statusId == 4) // Paid by Bank
                {
                    // Copy data from GivenToBank to PaidByBank
                    var paidByBank = new PaidByBank
                    {
                        Ecode = givenToBankRecord.Ecode,
                        Month = givenToBankRecord.Month,
                        A_C = givenToBankRecord.A_C,
                        BankTransfer = givenToBankRecord.BankTransfer,
                        BatchId = givenToBankRecord.BatchId,
                        CreatedBy = updatedBy,
                        CreatedOn = DateTime.Now,
                        IsActive = true,
                        IsDeleted = false
                    };
                    
                    _context.PaidByBanks.Add(paidByBank);
                }
                else if (statusId == 5) // Return by Bank
                {
                    // Copy data from GivenToBank to ReturnByBank
                    var returnByBank = new ReturnByBankNew
                    {
                        Ecode = givenToBankRecord.Ecode,
                        Month = givenToBankRecord.Month,
                        A_C = givenToBankRecord.A_C,
                        BankTransfer = givenToBankRecord.BankTransfer, // Using BankTransfer field as ReturnByBank1
                        CreatedBy = updatedBy,
                        CreatedOn = DateTime.Now,
                        IsActive = true,
                        IsDeleted = false,
                        BatchId = givenToBankRecord.BatchId
                    };
                    
                    _context.ReturnByBankNews.Add(returnByBank);
                }
                else
                {
                    return new ExecuteAndReponse
                    {
                        Status = false,
                        Message = "Invalid status. Only status 4 (Paid by Bank) and 5 (Return by Bank) are supported."
                    };
                }

                var batchIdd = ParseBatchNoToId(batchId)??0;
                // Update EmpAttendanceViewSnapshot SalaryStatus
                var snapshot = await _context.EmpAttendanceViewSnapshots
                    .FirstOrDefaultAsync(x => x.Ecode == givenToBankRecord.Ecode && 
                                             x.Month_Year == givenToBankRecord.Month &&
                                             x.ID == batchIdd);
                                             
                if (snapshot != null)
                {
                    snapshot.SalaryStatus = statusId;
                    _context.EmpAttendanceViewSnapshots.Update(snapshot);
                }
                givenToBankRecord.IsDeleted = true;
                givenToBankRecord.IsActive = false;
                _context.GivenToBanks.Update(givenToBankRecord);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                
                return new ExecuteAndReponse
                {
                    Status = true,
                    Message = statusId == 4 ? 
                        "Record successfully moved to Paid by Bank and salary status updated." : 
                        "Record successfully moved to Return by Bank and salary status updated."
                };
            }
            catch (Exception ex)
            {
                return new ExecuteAndReponse
                {
                    Status = false,
                    Message = ex.Message
                };
            }
        }

        public async Task<ExecuteAndReponse> ProcessExcelUploadAsync(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return new ExecuteAndReponse
                    {
                        Status = false,
                        Message = "No file uploaded."
                    };
                }

                if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                {
                    return new ExecuteAndReponse
                    {
                        Status = false,
                        Message = "Only .xlsx files are supported."
                    };
                }

                using var transaction = await _context.Database.BeginTransactionAsync();
                var processedCount = 0;
                var errorMessages = new List<string>();

                using var stream = file.OpenReadStream();
                using var workbook = new XLWorkbook(stream);
                var worksheet = workbook.Worksheets.First();

                // Get current user from session
                var identity = _httpContextAccessor.HttpContext?.User?.Identity as ClaimsIdentity;
                var user = AuthenticUserDetails.GetCurrentUserDetails(identity);
                var createdBy = user?.EmployeeId ?? "System";

                // Save uploaded file using same structure as upload-recalculate-new
                // Path: wwwroot/Uploader/SalaryProcess/YYYY/MMM/DD/EmployeeId/ExcelFileName_{timestamp}.ext
                var now = DateTime.Now;
                var yearFolder = now.ToString("yyyy");
                var monthFolder = now.ToString("MMM");
                var dayFolder = now.ToString("dd");
                var basePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Uploader", "SalaryProcess", yearFolder, monthFolder, dayFolder, createdBy);
                Directory.CreateDirectory(basePath);

                var ext = Path.GetExtension(file.FileName);
                if (string.IsNullOrWhiteSpace(ext)) ext = ".xlsx";
                var fileName = $"ExcelFileName_{DateTime.Now:ddMMyyyyHHmmssfff}{ext}";
                var filePath = Path.Combine(basePath, fileName);

                // Validate header names must match exactly
                var h1 = worksheet.Cell(1, 1).GetString().Trim();
                var h2 = worksheet.Cell(1, 2).GetString().Trim();
                var h3 = worksheet.Cell(1, 3).GetString().Trim();
                if (h1 != "ProcessId" || h2 != "GivenToBank" || h3 != "PaidByCash")
                {
                    return new ExecuteAndReponse
                    {
                        Status = false,
                        Message = "Invalid header row. Expected headers: ProcessId, GivenToBank, PaidByCash"
                    };
                }

                // Skip header row (row 1)
                for (int row = 2; row <= worksheet.LastRowUsed().RowNumber(); row++)
                {
                    try
                    {
                        var processId = worksheet.Cell(row, 1).GetString().Trim();
                        var givenToBankStr = worksheet.Cell(row, 2).GetString().Trim().ToLower();
                        var paidByCashStr = worksheet.Cell(row, 3).GetString().Trim().ToLower();

                        // Validate ProcessId
                        if (string.IsNullOrWhiteSpace(processId))
                        {
                            errorMessages.Add($"Row {row}: ProcessId cannot be empty.");
                            continue;
                        }

                        // Parse ProcessId (formatted batch number) to get actual ID
                        var actualId = ParseBatchNoToId(processId);
                        if (!actualId.HasValue)
                        {
                            errorMessages.Add($"Row {row}: Invalid ProcessId format '{processId}'. Expected format: B_MMM-YY_0001");
                            continue;
                        }

                        // Validate GivenToBank (only true/false allowed)
                        if (givenToBankStr != "true" && givenToBankStr != "false")
                        {
                            errorMessages.Add($"Row {row}: GivenToBank must be 'true' or 'false' only.");
                            continue;
                        }

                        // Validate PaidByCash (only true/false allowed)
                        if (paidByCashStr != "true" && paidByCashStr != "false")
                        {
                            errorMessages.Add($"Row {row}: PaidByCash must be 'true' or 'false' only.");
                            continue;
                        }

                        // Validate that only one can be true
                        bool isGivenToBank = givenToBankStr == "true";
                        bool isPaidByCash = paidByCashStr == "true";

                        if (isGivenToBank && isPaidByCash)
                        {
                            errorMessages.Add($"Row {row}: Both GivenToBank and PaidByCash cannot be true at the same time.");
                            continue;
                        }

                        if (!isGivenToBank && !isPaidByCash)
                        {
                            errorMessages.Add($"Row {row}: At least one of GivenToBank or PaidByCash must be true.");
                            continue;
                        }

                        // Find the EmpAttendanceViewSnapshot record using parsed ID
                        var snapshot = await _context.EmpAttendanceViewSnapshots
                            .FirstOrDefaultAsync(x => x.ID == actualId.Value);

                        if (snapshot == null)
                        {
                            errorMessages.Add($"Row {row}: ProcessId '{processId}' not found in EmpAttendanceViewSnapshot.");
                            continue;
                        }

                        // Determine status based on boolean values
                        int status = isGivenToBank ? 2 : 3; // 2 for GivenToBank, 3 for PaidByCash

                        // Update SalaryStatus
                        snapshot.SalaryStatus = status;
                        _context.EmpAttendanceViewSnapshots.Update(snapshot);

                        // Create appropriate record based on status
                        if (status == 2) // GivenToBank
                        {
                            var employee = await _context.tblEmployees.AsNoTracking().AsQueryable()
                                .FirstOrDefaultAsync(x => x.Ecode == snapshot.Ecode);

                            if (employee != null)
                            {
                                var givenToBank = new GivenToBank
                                {
                                    Ecode = snapshot.Ecode,
                                    Month = snapshot.Month_Year,
                                    A_C = employee.A_C_NO,
                                    BankTransfer = snapshot.Monthly_Gross_CTC_Actual_After_Deduction_AND_AddONS_.ToString(),
                                    BatchId = (int)snapshot.ID,
                                    CreatedBy = createdBy,
                                    CreatedOn = DateTime.Now,
                                    IsActive = true,
                                    IsDeleted = false
                                };

                                _context.GivenToBanks.Add(givenToBank);
                            }
                        }
                        else if (status == 3) // PaidByCash
                        {
                            var employee = await _context.tblEmployees.AsNoTracking().AsQueryable()
                                .FirstOrDefaultAsync(x => x.Ecode == snapshot.Ecode);

                            if (employee != null)
                            {
                                var paidInCash = new PaidInCash
                                {
                                    Ecode = snapshot.Ecode,
                                    Month = snapshot.Month_Year,
                                    A_C = employee.A_C_NO,
                                    BankTransfer = snapshot.Monthly_Gross_CTC_Actual_After_Deduction_AND_AddONS_.ToString(),
                                    BatchId = (int)snapshot.ID,
                                    CreatedBy = createdBy,
                                    CreatedOn = DateTime.Now,
                                    IsActive = true,
                                    IsDeleted = false
                                };

                                _context.PaidInCashes.Add(paidInCash);
                            }
                        }

                        processedCount++;
                    }
                    catch (Exception ex)
                    {
                        errorMessages.Add($"Row {row}: Error processing row - {ex.Message}");
                    }
                }

                if (errorMessages.Any())
                {
                    await transaction.RollbackAsync();
                    return new ExecuteAndReponse
                    {
                        Status = false,
                        Message = $"Validation errors found: {string.Join("; ", errorMessages)}"
                    };
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(fileStream);
                }
                return new ExecuteAndReponse
                {
                    Status = true,
                    Message = $"Excel file processed successfully. {processedCount} records updated."
                };
            }
            catch (Exception ex)
            {
                return new ExecuteAndReponse
                {
                    Status = false,
                    Message = ex.Message
                };
            }
        }

        public async Task<ExecuteAndReponse> ProcessGivenToBankExcelUploadAsync(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return new ExecuteAndReponse
                    {
                        Status = false,
                        Message = "No file uploaded."
                    };
                }

                if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                {
                    return new ExecuteAndReponse
                    {
                        Status = false,
                        Message = "Only .xlsx files are supported."
                    };
                }

                using var transaction = await _context.Database.BeginTransactionAsync();
                var processedCount = 0;
                var errorMessages = new List<string>();

                using var stream = file.OpenReadStream();
                using var workbook = new XLWorkbook(stream);
                var worksheet = workbook.Worksheets.First();

                // Get current user from session
                var identity = _httpContextAccessor.HttpContext?.User?.Identity as ClaimsIdentity;
                var user = AuthenticUserDetails.GetCurrentUserDetails(identity);
                var updatedBy = user?.EmployeeId ?? "System";

                // Save uploaded file using same structure as upload-recalculate-new
                // Path: wwwroot/Uploader/GivenToBank/YYYY/MMM/DD/EmployeeId/ExcelFileName_{timestamp}.ext
                var now = DateTime.Now;
                var yearFolder = now.ToString("yyyy");
                var monthFolder = now.ToString("MMM");
                var dayFolder = now.ToString("dd");
                var basePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Uploader", "GivenToBank", yearFolder, monthFolder, dayFolder, updatedBy);
                Directory.CreateDirectory(basePath);

                var ext = Path.GetExtension(file.FileName);
                if (string.IsNullOrWhiteSpace(ext)) ext = ".xlsx";
                var fileName = $"ExcelFileName_{DateTime.Now:ddMMyyyyHHmmssfff}{ext}";
                var filePath = Path.Combine(basePath, fileName);

                // Validate header names must match exactly
                var gh1 = worksheet.Cell(1, 1).GetString().Trim();
                var gh2 = worksheet.Cell(1, 2).GetString().Trim();
                var gh3 = worksheet.Cell(1, 3).GetString().Trim();
                var gh4 = worksheet.Cell(1, 4).GetString().Trim();
                if (gh1 != "BatchId" || gh2 != "TransactionId" || gh3 != "PaidByBank" || gh4 != "ReturnByBank")
                {
                    return new ExecuteAndReponse
                    {
                        Status = false,
                        Message = "Invalid header row. Expected headers: BatchId, TransactionId, PaidByBank, ReturnByBank"
                    };
                }

                // Skip header row (row 1)
                for (int row = 2; row <= worksheet.LastRowUsed().RowNumber(); row++)
                {
                    try
                    {
                        var batchId = worksheet.Cell(row, 1).GetString().Trim();
                        var transactionId = worksheet.Cell(row, 2).GetString().Trim();
                        var paidByBankStr = worksheet.Cell(row, 3).GetString().Trim().ToLower();
                        var returnByBankStr = worksheet.Cell(row, 4).GetString().Trim().ToLower();

                        // Validate BatchId (ProcessId)
                        if (string.IsNullOrWhiteSpace(batchId))
                        {
                            errorMessages.Add($"Row {row}: BatchId cannot be empty.");
                            continue;
                        }

                        // Parse BatchId to get actual ID
                        var actualBatchId = ParseBatchNoToId(batchId);
                        if (!actualBatchId.HasValue)
                        {
                            errorMessages.Add($"Row {row}: Invalid BatchId format '{batchId}'. Expected format: B_MMM-YY_0001");
                            continue;
                        }

                        // Validate TransactionId
                        if (string.IsNullOrWhiteSpace(transactionId))
                        {
                            errorMessages.Add($"Row {row}: TransactionId cannot be empty.");
                            continue;
                        }

                        // Parse TransactionId to get actual ID
                        var actualTransactionId = ParseTransactionIdToId(transactionId);
                        if (!actualTransactionId.HasValue)
                        {
                            errorMessages.Add($"Row {row}: Invalid TransactionId format '{transactionId}'. Expected format: GTB_00000001");
                            continue;
                        }

                        // Validate PaidByBank (only true/false allowed)
                        if (paidByBankStr != "true" && paidByBankStr != "false")
                        {
                            errorMessages.Add($"Row {row}: PaidByBank must be 'true' or 'false' only.");
                            continue;
                        }

                        // Validate ReturnByBank (only true/false allowed)
                        if (returnByBankStr != "true" && returnByBankStr != "false")
                        {
                            errorMessages.Add($"Row {row}: ReturnByBank must be 'true' or 'false' only.");
                            continue;
                        }

                        // Validate that only one can be true
                        bool isPaidByBank = paidByBankStr == "true";
                        bool isReturnByBank = returnByBankStr == "true";

                        if (isPaidByBank && isReturnByBank)
                        {
                            errorMessages.Add($"Row {row}: Both PaidByBank and ReturnByBank cannot be true at the same time.");
                            continue;
                        }

                        if (!isPaidByBank && !isReturnByBank)
                        {
                            errorMessages.Add($"Row {row}: At least one of PaidByBank or ReturnByBank must be true.");
                            continue;
                        }

                        // Find the GivenToBank record using TransactionId
                        var givenToBankRecord = await _context.GivenToBanks
                            .FirstOrDefaultAsync(x => x.BankTransferId == actualTransactionId.Value);

                        if (givenToBankRecord == null)
                        {
                            errorMessages.Add($"Row {row}: TransactionId '{transactionId}' not found in GivenToBank table.");
                            continue;
                        }

                        // Determine status based on boolean values
                        int statusId = isPaidByBank ? 4 : 5; // 4 for PaidByBank, 5 for ReturnByBank

                        // Create appropriate record based on status
                        if (statusId == 4) // PaidByBank
                        {
                            var paidByBank = new PaidByBank
                            {
                                Ecode = givenToBankRecord.Ecode,
                                Month = givenToBankRecord.Month,
                                A_C = givenToBankRecord.A_C,
                                BankTransfer = givenToBankRecord.BankTransfer,
                                BatchId = actualBatchId.Value,
                                CreatedBy = updatedBy,
                                CreatedOn = DateTime.Now,
                                IsActive = true,
                                IsDeleted = false
                            };

                            _context.PaidByBanks.Add(paidByBank);
                        }
                        else if (statusId == 5) // ReturnByBank
                        {
                            var returnByBank = new ReturnByBankNew
                            {
                                Ecode = givenToBankRecord.Ecode,
                                Month = givenToBankRecord.Month,
                                A_C = givenToBankRecord.A_C,
                                BankTransfer = givenToBankRecord.BankTransfer,
                                CreatedBy = updatedBy,
                                CreatedOn = DateTime.Now,
                                IsActive = true,
                                IsDeleted = false,
                                BatchId= actualBatchId.Value,
                            };

                            _context.ReturnByBankNews.Add(returnByBank);
                        }

                        // Update EmpAttendanceViewSnapshot SalaryStatus
                        var snapshot = await _context.EmpAttendanceViewSnapshots
                            .FirstOrDefaultAsync(x => x.Ecode == givenToBankRecord.Ecode && 
                                                     x.Month_Year == givenToBankRecord.Month &&
                                                     x.BatchNo == actualBatchId.Value);

                        if (snapshot != null)
                        {
                            snapshot.SalaryStatus = statusId;
                            _context.EmpAttendanceViewSnapshots.Update(snapshot);
                        }

                        // Mark GivenToBank record as deleted/inactive
                        givenToBankRecord.IsDeleted = true;
                        givenToBankRecord.IsActive = false;
                        _context.GivenToBanks.Update(givenToBankRecord);

                        processedCount++;
                    }
                    catch (Exception ex)
                    {
                        errorMessages.Add($"Row {row}: Error processing row - {ex.Message}");
                    }
                }

                if (errorMessages.Any())
                {
                    await transaction.RollbackAsync();
                    return new ExecuteAndReponse
                    {
                        Status = false,
                        Message = $"Validation errors found: {string.Join("; ", errorMessages)}"
                    };
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(fileStream);
                }
                return new ExecuteAndReponse
                {
                    Status = true,
                    Message = $"GivenToBank Excel file processed successfully. {processedCount} records updated."
                };
            }
            catch (Exception ex)
            {
                return new ExecuteAndReponse
                {
                    Status = false,
                    Message = ex.Message
                };
            }
        }

        public async Task<FetchAndResponse> GetComprehensiveSalaryStatusList(string month = null, string ecode = null, int pageNumber = 1, int pageSize = 50)
        {
            try
            {
                // Validate pagination parameters
                if (pageNumber < 1) pageNumber = 1;
                if (pageSize < 1 || pageSize > 1000) pageSize = 50;

                // Validate month format if provided
                if (!string.IsNullOrWhiteSpace(month))
                {
                    if (!DateTime.TryParseExact(month, "MMM-yy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedMonth))
                    {
                        return new FetchAndResponse()
                        {
                            Status = false,
                            Message = "Invalid month format. Expected format: MMM-YY (e.g., Jul-25)"
                        };
                    }
                }

                // Call stored procedure manually with new parameters
                await using var dbConnection = _context.Database.GetDbConnection();
                await dbConnection.OpenAsync();
                
                await using var connection = (SqlConnection)dbConnection;
                using var command = new SqlCommand("GetComprehensiveSalaryStatusList", connection)
                {
                    CommandType = CommandType.StoredProcedure
                };

                // Add parameters
                command.Parameters.Add(new SqlParameter("@Month", SqlDbType.NVarChar, 10)
                {
                    Value = string.IsNullOrWhiteSpace(month) ? (object)DBNull.Value : month
                });
                command.Parameters.Add(new SqlParameter("@Ecode", SqlDbType.NVarChar, 20)
                {
                    Value = string.IsNullOrWhiteSpace(ecode) ? (object)DBNull.Value : ecode
                });
                command.Parameters.Add(new SqlParameter("@PageNumber", SqlDbType.Int) { Value = pageNumber });
                command.Parameters.Add(new SqlParameter("@PageSize", SqlDbType.Int) { Value = pageSize });
                
                var totalCountParam = new SqlParameter("@TotalCount", SqlDbType.Int) 
                { 
                    Direction = ParameterDirection.Output 
                };
                command.Parameters.Add(totalCountParam);

                var results = new List<ComprehensiveSalaryStatusResponseDto>();
                
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        results.Add(new ComprehensiveSalaryStatusResponseDto
                        {
                            Id = reader.GetInt64(reader.GetOrdinal("Id")),
                            Ecode = reader.IsDBNull(reader.GetOrdinal("Ecode")) ? null : reader.GetString(reader.GetOrdinal("Ecode")),
                            Location_Code = reader.IsDBNull(reader.GetOrdinal("Location_Code")) ? null : reader.GetString(reader.GetOrdinal("Location_Code")),
                            Location_Name = reader.IsDBNull(reader.GetOrdinal("LocationName")) ? null : reader.GetString(reader.GetOrdinal("LocationName")),
                            Employee_Name = reader.IsDBNull(reader.GetOrdinal("EmployeeName")) ? null : reader.GetString(reader.GetOrdinal("EmployeeName")),
                            Month_Year = reader.IsDBNull(reader.GetOrdinal("MonthYear")) ? null : reader.GetString(reader.GetOrdinal("MonthYear")),
                            PayableSalary = reader.GetDecimal(reader.GetOrdinal("PayableSalary")),
                            GivenToBankAmount = reader.IsDBNull(reader.GetOrdinal("GivenToBankAmount")) ? "0" : reader.GetDecimal(reader.GetOrdinal("GivenToBankAmount")).ToString(),
                            PaidByBankAmount = reader.IsDBNull(reader.GetOrdinal("PaidByBankAmount")) ? "0" : reader.GetDecimal(reader.GetOrdinal("PaidByBankAmount")).ToString(),
                            PaidByCashAmount = reader.IsDBNull(reader.GetOrdinal("PaidByCashAmount")) ? "0" : reader.GetDecimal(reader.GetOrdinal("PaidByCashAmount")).ToString(),
                            ReturnByBankAmount = reader.IsDBNull(reader.GetOrdinal("ReturnByBankAmount")) ? "0" : reader.GetDecimal(reader.GetOrdinal("ReturnByBankAmount")).ToString(),
                            Difference = reader.IsDBNull(reader.GetOrdinal("Difference")) ? 0m : reader.GetDecimal(reader.GetOrdinal("Difference")),
                            SalaryStatus = reader.GetInt32(reader.GetOrdinal("SalaryStatus")),
                            BatchId = reader.IsDBNull(reader.GetOrdinal("BatchId")) ? null : reader.GetString(reader.GetOrdinal("BatchId")),
                            FormattedId = reader.IsDBNull(reader.GetOrdinal("FormattedId")) ? null : reader.GetString(reader.GetOrdinal("FormattedId")),
                            RunAt = reader.GetDateTime(reader.GetOrdinal("RunAt"))
                        });
                    }
                }

                int totalCount = totalCountParam.Value == DBNull.Value ? 0 : Convert.ToInt32(totalCountParam.Value);

                // Check if no data found
                if (results.Count == 0)
                {
                    return new FetchAndResponse()
                    {
                        Status = false,
                        Message = "No data found for the specified criteria.",
                        Data = new ComprehensiveSalaryStatusPaginatedResponse
                        {
                            Pagination = new PaginatedResponse<ComprehensiveSalaryStatusResponseDto>
                            {
                                Data = new List<ComprehensiveSalaryStatusResponseDto>(),
                                PageNumber = pageNumber,
                                PageSize = pageSize,
                                TotalRecords = totalCount
                            },
                            Summary = new ComprehensiveSalaryStatusSummaryDto()
                        }
                    };
                }

                // Calculate summary totals from paginated results
                // Note: These are calculated from the current page only, not the entire dataset
                var summary = new ComprehensiveSalaryStatusSummaryDto
                {
                    TotalPayableSalary = results.Count(x => x.SalaryStatus == 1),
                    TotalGivenToBank = results.Count(x => x.SalaryStatus == 2),
                    TotalPaidByBank = results.Count(x => x.SalaryStatus == 4),
                    TotalReturnByBank = results.Count(x => x.SalaryStatus == 5),
                    TotalDifference = results.Sum(x => x.PayableSalary) - 
                                     results.Sum(x => decimal.TryParse(x.PaidByBankAmount, out decimal pbb) ? pbb : 0) - 
                                     results.Sum(x => decimal.TryParse(x.PaidByCashAmount, out decimal pic) ? pic : 0)
                };

                return new FetchAndResponse()
                {
                    Status = true,
                    Message = $"Found {totalCount} total records. Showing page {pageNumber} with {results.Count} records.",
                    Data = new ComprehensiveSalaryStatusPaginatedResponse
                    {
                        Pagination = new PaginatedResponse<ComprehensiveSalaryStatusResponseDto>
                        {
                            //Data = results,
                            PageNumber = pageNumber,
                            PageSize = pageSize,
                            TotalRecords = totalCount
                        },
                        Data=results,
                        Summary = summary
                    }
                };
            }
            catch (Exception ex)
            {
                return new FetchAndResponse()
                {
                    Status = false,
                    Message = ex.Message,
                    Data = new ComprehensiveSalaryStatusPaginatedResponse
                    {
                        Pagination = new PaginatedResponse<ComprehensiveSalaryStatusResponseDto>
                        {
                            Data = new List<ComprehensiveSalaryStatusResponseDto>(),
                            PageNumber = pageNumber,
                            PageSize = pageSize,
                            TotalRecords = 0
                        },
                        Summary = new ComprehensiveSalaryStatusSummaryDto()
                    }
                };
            }
        }

        public async Task<FetchAndResponse> GetEmployeesMissingOrReturnedAsync(string stCode = "RH01", string month = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(month))
                {
                    month = DateTime.Now.ToString("MMM-yy");
                }

                if (!DateTime.TryParseExact(month, "MMM-yy", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                {
                    return new FetchAndResponse()
                    {
                        Status = false,
                        Message = "Invalid month format. Expected format: MMM-YY (e.g., Oct-25)",
                        Code = System.Net.HttpStatusCode.BadRequest
                    };
                }

                var normalizedSt = (stCode ?? "RH01").Trim();

                // Call the EF Core Power Tools generated procedure wrapper
                var spRows = await _context.GetProcedures().usp_GetEligibleEmployeesForSnapshotAsync(
                    normalizedSt,
                    month
                );

                var data = spRows.Select(x => new EligibleEmployeeResponseDto
                {
                    Ecode = x.Ecode,
                    EmployeeName = x.EmployeeName,
                    STCode = x.STCode,
                    LocationName = x.LocationName,
                    IsActive = x.IsActive,
                    DepartmentName = x.DepartmentName,
                    DesignationName = x.DesignationName
                }).ToList();
                var activeCount = data.Count(d => d.IsActive == true);
                var inactiveCount = data.Count(d => d.IsActive == false);
                var totalCount = data.Count;

                if (data.Count < 1) {
                    return new FetchAndResponse()
                    {
                        Status = true,
                        Message = $"No Data Found for STCode {normalizedSt.ToUpper()} and month {month}",
                        Data = new { Data = data, ActiveCount = activeCount, InactiveCount = inactiveCount, TotalCount = totalCount },
                        Code = System.Net.HttpStatusCode.NotFound
                    };
                }

                return new FetchAndResponse()
                {
                    Status = true,
                    Message = $"Found {data.Count} employees for STCode {normalizedSt.ToUpper()} and month {month}",
                    Data = new { Data = data, ActiveCount = activeCount, InactiveCount = inactiveCount, TotalCount = totalCount },
                    Code = System.Net.HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                return new FetchAndResponse()
                {
                    Status = false,
                    Message = ex.Message,
                    Code = System.Net.HttpStatusCode.InternalServerError
                };
            }
        }

        public async Task<FetchAndResponse> GetEligibleEmployeesFastAsync(string ecode = null, string month = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(month))
                {
                    month = DateTime.Now.ToString("MMM-yy");
                }

                if (!DateTime.TryParseExact(month, "MMM-yy", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                {
                    return new FetchAndResponse()
                    {
                        Status = false,
                        Message = "Invalid month format. Expected format: MMM-YY (e.g., Oct-25)",
                        Code = System.Net.HttpStatusCode.BadRequest
                    };
                }

                var ecodeParam = new SqlParameter("@Ecode", (object)ecode ?? DBNull.Value);
                var monthParam = new SqlParameter("@MonthKey", month);

                // Use ADO.NET to get dynamic data without DTO mapping
                var data = new List<Dictionary<string, object>>();
                
                using (var connection = _context.Database.GetDbConnection())
                {
                    await connection.OpenAsync();
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "EXEC [dbo].[usp_GetEligibleEmployeesForSnapshot_Fast] @Ecode, @MonthKey";
                        command.Parameters.Add(ecodeParam);
                        command.Parameters.Add(monthParam);
                        
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var row = new Dictionary<string, object>();
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    var columnName = reader.GetName(i);
                                    var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                                    row[columnName] = value;
                                }
                                data.Add(row);
                            }
                        }
                    }
                }

                if (data.Count < 1)
                {
                    return new FetchAndResponse()
                    {
                        Status = true,
                        Message = $"No eligible employees found for ecode {ecode ?? "all"} and month {month}",
                        Data = data,
                        Code = System.Net.HttpStatusCode.NotFound
                    };
                }

                return new FetchAndResponse()
                {
                    Status = true,
                    Message = $"Found {data.Count} eligible employees for ecode {ecode ?? "all"} and month {month}",
                    Data = data,
                    Code = System.Net.HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                return new FetchAndResponse()
                {
                    Status = false,
                    Message = ex.Message,
                    Code = System.Net.HttpStatusCode.InternalServerError
                };
            }
        }

        public async Task<ExecuteAndReponse> UpdateStatusByIdAsync(long id, int status)
        {
            try
            {
                // Validate status parameter - only allow 1 (approve) or -1 (reject)
                if (status != 1 && status != -1)
                {
                    return new ExecuteAndReponse
                    {
                        Status = false,
                        Message = $"Invalid status value: {status}. Only 1 (approve) or -1 (reject) are allowed."
                    };
                }

                // Find the record by ID
                var snapshot = await _context.EmpAttendanceViewSnapshots
                    .FirstOrDefaultAsync(x => x.ID == id);

                if (snapshot == null)
                {
                    return new ExecuteAndReponse
                    {
                        Status = false,
                        Message = $"Record not found with ID: {id}"
                    };
                }

                // Update Status to the provided value (1 or -1)
                snapshot.SalaryStatus = status;
                await _context.SaveChangesAsync();

                var action = status == 1 ? "approved" : "rejected";
                return new ExecuteAndReponse
                {
                    Status = true,
                    Message = $"Status updated successfully to '{status}' ({action}) for ID: {id}"
                };
            }
            catch (Exception ex)
            {
                return new ExecuteAndReponse
                {
                    Status = false,
                    Message = $"Error updating status: {ex.Message}"
                };
            }
        }

        // ===================== PAYROLL FORMAT EXPORT =====================
        // Builds the "LOC-WISE EMP-WISE SALARY REPORT" in the 148-column PAYROLL FORMAT.xlsx
        // layout (grouped merged header banners + clean column labels), one row per employee
        // using the LATEST run for the selected month. Pulls employee-master fields from
        // tblEmployee. Columns we don't store (Bonus/Gratuity/SL leave/Salary-Paid/etc.) are
        // intentionally left blank. Read-only — does not modify any data/tables.
        public async Task<(byte[] bytes, string fileName)> ExportPayrollFormatAsync(string month, int? status)
        {
            if (string.IsNullOrWhiteSpace(month))
                month = DateTime.Now.ToString("MMM-yy");

            // All snapshot rows for the month, then keep the latest run (max RunAt) per Ecode.
            var monthRows = await _context.EmpAttendanceViewSnapshots.AsNoTracking()
                .Where(x => x.MONTH == month)
                .ToListAsync();

            var latest = monthRows
                .GroupBy(r => r.Ecode)
                .Select(g => g.OrderByDescending(r => r.RunAt).ThenByDescending(r => r.ID).First())
                .OrderBy(r => r.Location_Code).ThenBy(r => r.Ecode)
                .ToList();

            // Employee-master lookup for the fields not held on the snapshot.
            var ecodes = latest.Select(r => r.Ecode).Where(e => !string.IsNullOrEmpty(e)).Distinct().ToList();
            var emps = await _context.tblEmployees.AsNoTracking()
                .Where(e => ecodes.Contains(e.Ecode))
                .ToListAsync();
            var empMap = emps
                .GroupBy(e => e.Ecode, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            // Location lookup (by STCode = snapshot Location_Code) → LocationType + StateId.
            var stCodes = latest.Select(r => r.Location_Code).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
            var locs = await _context.tblLocations.AsNoTracking()
                .Where(l => stCodes.Contains(l.STCode))
                .Select(l => new { l.STCode, l.LocationType, l.StateId })
                .ToListAsync();
            var locMap = locs
                .GroupBy(l => l.STCode, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            // State lookup (StateId → StateName).
            var stateIds = locs.Where(l => l.StateId.HasValue).Select(l => l.StateId.Value).Distinct().ToList();
            var stateMap = (await _context.tblStates.AsNoTracking()
                    .Where(s => stateIds.Contains(s.StateId))
                    .Select(s => new { s.StateId, s.StateName })
                    .ToListAsync())
                .ToDictionary(s => s.StateId, s => s.StateName);

            // Sub-department lookup (SubDepartmentId1 → name). No EF DbSet, so read raw.
            var subDeptIds = emps.Where(e => e.SubDepartmentId1.HasValue).Select(e => e.SubDepartmentId1.Value).Distinct().ToList();
            var subDeptMap = new Dictionary<int, string>();
            if (subDeptIds.Count > 0)
            {
                var conn = _context.Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();
                using var sdCmd = conn.CreateCommand();
                sdCmd.CommandText = "SELECT SubDepartmentId, SubDepartmentName FROM tblSubDepartment WHERE SubDepartmentId IN (" +
                                    string.Join(",", subDeptIds) + ")";
                using var sdReader = await sdCmd.ExecuteReaderAsync();
                while (await sdReader.ReadAsync())
                {
                    if (!sdReader.IsDBNull(0))
                        subDeptMap[sdReader.GetInt32(0)] = sdReader.IsDBNull(1) ? null : sdReader.GetString(1);
                }
            }

            // Monthly bonus & gratuity (calculated in HRMS) from vw_Bonus_Gratuity, deduped per ECode.
            // Bonus/Gratuity = budgeted "rate"; ActualBonus/ActualGratuity = actual earned.
            var bonusGratMap = new Dictionary<string, (decimal bonus, decimal actBonus, decimal grat, decimal actGrat)>(StringComparer.OrdinalIgnoreCase);
            {
                var conn2 = _context.Database.GetDbConnection();
                if (conn2.State != System.Data.ConnectionState.Open) await conn2.OpenAsync();
                using var bgCmd = conn2.CreateCommand();
                bgCmd.CommandText = @"SELECT ECode, MAX(Bonus) AS Bonus, MAX(ActualBonus) AS ActualBonus,
                                             MAX(Gratuity) AS Gratuity, MAX(ActualGratuity) AS ActualGratuity
                                      FROM vw_Bonus_Gratuity WHERE Month = @m AND ECode IS NOT NULL
                                      GROUP BY ECode";
                var mp = bgCmd.CreateParameter(); mp.ParameterName = "@m"; mp.Value = month; bgCmd.Parameters.Add(mp);
                using var bgReader = await bgCmd.ExecuteReaderAsync();
                while (await bgReader.ReadAsync())
                {
                    var ec = bgReader.GetString(0);
                    decimal D(int i) => bgReader.IsDBNull(i) ? 0m : Convert.ToDecimal(bgReader.GetValue(i));
                    bonusGratMap[ec] = (D(1), D(2), D(3), D(4));
                }
            }

            // Column labels (148) in exact reference order.
            string[] labels = PayrollFormatLabels();

            // Grouped header banners: (startCol, endCol, text) — 1-based.
            var banners = new (int s, int e, string t)[]
            {
                (1, 38, "PART-1 (LOC & EMP DETAIL)"),
                (39, 50, "SALARY (BGT VS ACT)"),
                (51, 58, "BONUS EARNED RATE VS ACT"),
                (59, 70, "REIM-BREKUP DETAIL"),
                (71, 76, "EXTRA-GROSS-DETAIL"),
                (77, 85, "SALARY PAYABLE"),
                (86, 101, "PART-8 DEDUCTION ( PF/ESIC/TDS/LOAN/BONUS)"),
                (102, 122, "PART 4 ( LEAVE REPORT)"),
                (123, 141, "PART-7 : PAYABLE DAYS WORKING"),
                (142, 148, "SALARY PAID"),
            };

            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("SALARY WORKING-FINAL");

            const int bannerRow = 1;
            const int labelRow = 2;
            const int firstDataRow = 3;
            int totalCols = labels.Length; // 148

            // Banner row (merged groups).
            foreach (var b in banners)
            {
                var rng = ws.Range(bannerRow, b.s, bannerRow, b.e).Merge();
                rng.Value = b.t;
                rng.Style.Font.Bold = true;
                rng.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                rng.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                rng.Style.Fill.BackgroundColor = XLColor.FromArgb(0xD9, 0xE1, 0xF2);
                rng.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }

            // Column-label row.
            for (int c = 1; c <= totalCols; c++)
            {
                var cell = ws.Cell(labelRow, c);
                cell.Value = labels[c - 1] ?? string.Empty;
                cell.Style.Font.Bold = true;
                cell.Style.Alignment.WrapText = true;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                cell.Style.Fill.BackgroundColor = XLColor.FromArgb(0xF2, 0xF2, 0xF2);
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }

            // Data rows.
            int r = firstDataRow;
            int sr = 1;
            foreach (var snap in latest)
            {
                empMap.TryGetValue(snap.Ecode ?? string.Empty, out var emp);

                // Resolve portal-sourced fields for this row.
                string locType = null, stateName = null, subDeptName = null;
                if (!string.IsNullOrEmpty(snap.Location_Code) && locMap.TryGetValue(snap.Location_Code, out var locInfo))
                {
                    locType = locInfo.LocationType;
                    if (locInfo.StateId.HasValue) stateMap.TryGetValue(locInfo.StateId.Value, out stateName);
                }
                if (emp?.SubDepartmentId1 != null) subDeptMap.TryGetValue(emp.SubDepartmentId1.Value, out subDeptName);

                decimal? bonusRate = null, bonusEarned = null, gratRate = null, gratEarned = null;
                if (!string.IsNullOrEmpty(snap.Ecode) && bonusGratMap.TryGetValue(snap.Ecode, out var bg))
                {
                    bonusRate = bg.bonus; bonusEarned = bg.actBonus; gratRate = bg.grat; gratEarned = bg.actGrat;
                }

                var vals = BuildPayrollRow(sr, snap, emp, month, locType, stateName, subDeptName,
                    bonusRate, bonusEarned, gratRate, gratEarned);
                for (int c = 1; c <= totalCols; c++)
                {
                    var v = vals[c - 1];
                    var cell = ws.Cell(r, c);
                    switch (v)
                    {
                        case null:
                            break;
                        case DateTime dt:
                            cell.Value = dt;
                            cell.Style.DateFormat.Format = "dd-MMM-yyyy";
                            break;
                        case decimal dec:
                            cell.Value = dec;
                            break;
                        case double dbl:
                            cell.Value = dbl;
                            break;
                        case int iv:
                            cell.Value = iv;
                            break;
                        default:
                            cell.Value = v.ToString();
                            break;
                    }
                }
                r++;
                sr++;
            }

            // Freeze the two header rows only; no columns are frozen (per requirement). Size columns.
            ws.SheetView.FreezeRows(2);
            ws.Columns().Width = 14;
            ws.Column(3).Width = 26; // Location
            ws.Column(7).Width = 24; // Name
            ws.Row(labelRow).Height = 42;

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            var fileName = $"LOC_EMP_Salary_Report_{month}.xlsx";
            return (ms.ToArray(), fileName);
        }

        private static string[] PayrollFormatLabels() => new string[]
        {
            "SR NO","LOC CD","LOCATION","LOC-TYPE","STATE","E.CODE","NAME","GENDER","D.O.B","JOINING DATE",
            "MOBILE NO.","LEAVING DT","DEPARTMENT","SUB-DEPT.","DESIGNATION","STATUS","Name of Bank","IFSC Code","A/c No.","U.A.N NO",
            "P.F.NO.","E.S.I NO","PAN NO","AADHAR NO","P.F. Applicable?","E.S.I. Applicable?","F.P.F. Applicable?","O.T. Applicable?","P.TAX. Applicable?","BONUS TYPE",
            "BONUS Applicable?","WK-OFF PAY APPLICABLE?","AUTO REMARKS","LP REMARKS","DEPT. REMARKS","HR REMARKS","MTH","",
            "Basic Salary Rate","BASIC SALARY","H.R.A. Rate","H.R.A.","D.A. RATE","D.A","C.C.A. Rate","C.C.A.","SPECIAL ALLOWANCE RATE","SPECIAL ALLOWANCE","REIMB RATE","REIMB",
            "BONUS EARNED RATE","BONUS EARNED","GRATUITY EARNED RATE","GRATUITY EARNED","RETENTION GROSS SALARY","RET. BONUS %","RET. BONUS RATE","RET. BONUS EARNED",
            "Fuel and Maintenance","Fuel and Maintenance (REIMB)","Books and Periodicals","Books and Periodicals (REIMB)","Professional Attire","Professional Attire (REIMB)","Driver Wages","Driver Wages (REIMB)","Mobile Bill","Mobile Bill (REIMB)","Meal Voucher","Meal Voucher (REIMB)",
            "OVERTIME","INCENTIVE AMT","FOODING ALL","MOBILE BILL","ARRERS","EXTRA DAYS ALLOWANCE",
            "CTC-MTH-AS-PER-OFFER-LETTER- SALARY RATE","MTH-AS-PER-ACTUAL- SALARY","CTC SALARY","GROSS SALARY","","TTL GROSS-EARNING","PAYABLE (WITH REIMBUS)","PAYABLE","",
            "TOTAL DEDUCTION","PF","ESI","TDS","P-TAX","CASH SHORT","DIESEL","PENALTY","LOAN","MONTHLY ADVANCE","LWF","","PF (EMPLOYER)","ESIC (EMPLOYER)","LWF (EMPLOYER)","",
            "TOTAL LEAVE","","","","EL LEAVE OP_BAL","EL LEAVE CLS_BAL","EL LEAVE EARN","EL LEAVE AVAILED","CL LEAVE OP_BAL","CL LEAVE CLS_BAL","CL LEAVE EARN","CL LEAVE AVAILED","CO LEAVE OP_BAL","CO LEAVE CLS_BAL","CO LEAVE EARN","CO LEAVE AVAILED","SL LEAVE OP_BAL","SL LEAVE CLS_BAL","SL LEAVE EARN","SL LEAVE AVAILED","",
            "TTL PRESENT DAYS","MACHINE PRESENT DAYS","MANUAL PRESENT DAYS","GEO FENCE ATTEND-DAYS","BGT-PAYABLE DAYS","PAYABLE DAYS","TTL PAYABLE DAYS","ABSENT","EXTRA DAY CNT","BGT DAYS WKL-OF","ACT DAYS WK-OFF (WO LEAVE)","HLD","POW","TOTAL LEAVE AVAILED","EL","CL","CO","SL","",
            "SALARY PAID-1","REIMB","INCENTIVE","SALARY PAID-2","SALARY PAID-TTL","DIFF","",
        };

        // Numeric-looking strings -> double so Excel treats them as numbers; otherwise raw string.
        private static object NumOrStr(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            return double.TryParse(s.Trim(), out var d) ? (object)d : s.Trim();
        }

        private static string YesNo(bool? b) => b == true ? "Yes" : (b == false ? "No" : null);

        private object[] BuildPayrollRow(int sr, EmpAttendanceViewSnapshot s, tblEmployee e, string month,
            string locType = null, string stateName = null, string subDeptName = null,
            decimal? bonusRate = null, decimal? bonusEarned = null, decimal? gratRate = null, decimal? gratEarned = null)
        {
            // Bonus applicable Yes/No derived from BonusApplicable (Stat/Ctc = Yes, No/blank = No).
            var bonusApp = e?.BonusApplicable?.Trim();
            string bonusApplicableYesNo = string.IsNullOrEmpty(bonusApp) ? null
                : (bonusApp.Equals("No", StringComparison.OrdinalIgnoreCase) ? "No" : "Yes");

            // 148 values in exact column order matching PayrollFormatLabels().
            return new object[]
            {
                /*1  SR NO*/ sr,
                /*2  LOC CD*/ s.Location_Code,
                /*3  LOCATION*/ s.Location_Name,
                /*4  LOC-TYPE*/ locType,
                /*5  STATE*/ stateName,
                /*6  E.CODE*/ s.Ecode,
                /*7  NAME*/ s.Employee_Name,
                /*8  GENDER*/ e?.GENDER,
                /*9  D.O.B*/ e?.DOB,
                /*10 JOINING DATE*/ e?.DOJ,
                /*11 MOBILE NO.*/ e?.MOBILE,
                /*12 LEAVING DT*/ e?.DateOfLeft,
                /*13 DEPARTMENT*/ s.department,
                /*14 SUB-DEPT.*/ subDeptName,
                /*15 DESIGNATION*/ s.designation,
                /*16 STATUS*/ s.Status,
                /*17 Name of Bank*/ e?.BANK_NAME,
                /*18 IFSC Code*/ e?.BANK_IFSC_CODE,
                /*19 A/c No.*/ e?.A_C_NO,
                /*20 U.A.N NO*/ e?.UAN_NO,
                /*21 P.F.NO.*/ e?.UAN_NO,   // PF No. = UAN number
                /*22 E.S.I NO*/ e?.ESICNO,
                /*23 PAN NO*/ e?.PAN_NO,
                /*24 AADHAR NO*/ e?.AADHAR_NO,
                /*25 P.F. Applicable?*/ YesNo(e?.PFApplicable),
                /*26 E.S.I. Applicable?*/ YesNo(e?.ESICApplicable),
                /*27 F.P.F. Applicable?*/ null,
                /*28 O.T. Applicable?*/ null,
                /*29 P.TAX. Applicable?*/ null,
                /*30 BONUS TYPE*/ e?.BonusApplicable,
                /*31 BONUS Applicable?*/ bonusApplicableYesNo,
                /*32 WK-OFF PAY APPLICABLE?*/ null,
                /*33 AUTO REMARKS*/ null,
                /*34 LP REMARKS*/ null,
                /*35 DEPT. REMARKS*/ null,
                /*36 HR REMARKS*/ null,
                /*37 MTH*/ s.MONTH ?? month,
                /*38 (blank)*/ null,
                /*39 Basic Salary Rate*/ s.BasicSalary_Bud__,
                /*40 BASIC SALARY*/ s.BasicSalary_Actual_,
                /*41 H.R.A. Rate*/ s.HRA_Bud__,
                /*42 H.R.A.*/ s.HRA_Actual_,
                /*43 D.A. RATE*/ s.DA_Bud__,
                /*44 D.A*/ s.DA_Actual_,
                /*45 C.C.A. Rate*/ s.CCA_Bud__,
                /*46 C.C.A.*/ s.CCA_Actual_,
                /*47 SPECIAL ALLOWANCE RATE*/ s.SpecialAllowance_Bud__,
                /*48 SPECIAL ALLOWANCE*/ s.SpecialAllowance_Actual_,
                /*49 REIMB RATE*/ s.Reimbersment_Bud__,
                /*50 REIMB*/ s.Reimbersment_Actual_,
                /*51 BONUS EARNED RATE*/ bonusRate,
                /*52 BONUS EARNED*/ bonusEarned,
                /*53 GRATUITY EARNED RATE*/ gratRate,
                /*54 GRATUITY EARNED*/ gratEarned,
                /*55 RETENTION GROSS SALARY*/ null,
                /*56 RET. BONUS %*/ null,
                /*57 RET. BONUS RATE*/ null,
                /*58 RET. BONUS EARNED*/ null,
                /*59 Fuel and Maintenance*/ s.Fuel_and_Maintenance_Bud__,
                /*60 Fuel (REIMB)*/ s.Fuel_and_Maintenance_Actual_,
                /*61 Books and Periodicals*/ s.Books_and_Periodicals_Bud__,
                /*62 Books (REIMB)*/ s.Books_and_Periodicals_Actual_,
                /*63 Professional Attire*/ s.Professional_Attire_Bud__,
                /*64 Professional Attire (REIMB)*/ s.Professional_Attire_Actual_,
                /*65 Driver Wages*/ s.Driver_Wages_Bud__,
                /*66 Driver Wages (REIMB)*/ s.Driver_Wages_Actual_,
                /*67 Mobile Bill*/ s.Mobile_Bill_Bud__,
                /*68 Mobile Bill (REIMB)*/ s.Mobile_Bill_Actual_,
                /*69 Meal Voucher*/ s.Meal_Voucher_Bud__,
                /*70 Meal Voucher (REIMB)*/ s.Meal_Voucher_Actual_,
                /*71 OVERTIME*/ s.Overtime,
                /*72 INCENTIVE AMT*/ NumOrStr(s.Incentive),
                /*73 FOODING ALL*/ s.Fooding_Allowance,
                /*74 MOBILE BILL*/ s.Mobile_Bill,
                /*75 ARRERS*/ NumOrStr(s.ARREAR),
                /*76 EXTRA DAYS ALLOWANCE*/ NumOrStr(s.ExtraDayAllowance),
                /*77 CTC-OFFER RATE*/ s.Monthly_Gross_CTC_Bud__,
                /*78 MTH-ACTUAL SALARY*/ s.Monthly_Gross_CTC_Actual_,
                /*79 CTC SALARY*/ null,
                /*80 GROSS SALARY*/ s.Monthly_Gross_CTC_Actual_,
                /*81 (blank)*/ null,
                /*82 TTL GROSS-EARNING*/ s.Monthly_Gross_CTC_Actual_,
                /*83 PAYABLE (WITH REIMBUS)*/ s.Monthly_Gross_CTC_Actual_After_Deduction_AND_AddONS_,
                /*84 PAYABLE*/ s.Monthly_Gross_CTC_Actual_After_Deduction_AND_AddONS_,
                /*85 (blank)*/ null,
                /*86 TOTAL DEDUCTION*/ s.TotalDeductions,
                /*87 PF*/ s.PF_Employee_,
                /*88 ESI*/ s.ESIC_Employee_,
                /*89 TDS*/ NumOrStr(s.TDS),
                /*90 P-TAX*/ NumOrStr(s.PTax),
                /*91 CASH SHORT*/ NumOrStr(s.CashShort),
                /*92 DIESEL*/ NumOrStr(s.DieselDeduction),
                /*93 PENALTY*/ NumOrStr(s.Penality),
                /*94 LOAN*/ NumOrStr(s.Loan),
                /*95 MONTHLY ADVANCE*/ null,
                /*96 LWF*/ NumOrStr(s.Lwf),
                /*97 (blank)*/ null,
                /*98 PF (EMPLOYER)*/ s.PF_Employeer_,
                /*99 ESIC (EMPLOYER)*/ s.ESIC_Employeer_,
                /*100 LWF (EMPLOYER)*/ null,
                /*101 (blank)*/ null,
                /*102 TOTAL LEAVE*/ s.Leave_Used,
                /*103 (blank)*/ null,
                /*104 (blank)*/ null,
                /*105 (blank)*/ null,
                /*106 EL OP_BAL*/ s.Opening_EL,
                /*107 EL CLS_BAL*/ s.EarnedLeaveBalance,
                /*108 EL EARN*/ s.EarnedLeaveAcquired,
                /*109 EL AVAILED*/ s.EarnedLeaveUsed,
                /*110 CL OP_BAL*/ s.Opening_CL,
                /*111 CL CLS_BAL*/ s.CasualLeaveBalance,
                /*112 CL EARN*/ s.CasualLeaveAcquired,
                /*113 CL AVAILED*/ s.CasualLeaveUsed,
                /*114 CO OP_BAL*/ s.Opening_CompoOff,
                /*115 CO CLS_BAL*/ s.CompoOffBalance,
                /*116 CO EARN*/ s.CompoOffAcquired,
                /*117 CO AVAILED*/ s.CompoOffUsed,
                /*118 SL OP_BAL*/ null,
                /*119 SL CLS_BAL*/ null,
                /*120 SL EARN*/ null,
                /*121 SL AVAILED*/ null,
                /*122 (blank)*/ null,
                /*123 TTL PRESENT DAYS*/ s.actualttl_days,
                /*124 MACHINE PRESENT DAYS*/ NumOrStr(s.Machine),
                /*125 MANUAL PRESENT DAYS*/ NumOrStr(s.MANUAL),
                /*126 GEO FENCE ATTEND-DAYS*/ s.GF,
                /*127 BGT-PAYABLE DAYS*/ s.ttl_bgt_days,
                /*128 PAYABLE DAYS*/ s.paybledays,
                /*129 TTL PAYABLE DAYS*/ s.Payble_Days,
                /*130 ABSENT*/ s.Absent,
                /*131 EXTRA DAY CNT*/ s.extradays,
                /*132 BGT DAYS WKL-OF*/ null,
                /*133 ACT DAYS WK-OFF*/ s.actualweekly,
                /*134 HLD*/ s.HolidayOff,
                /*135 POW*/ s.presentweeklyoff,
                /*136 TOTAL LEAVE AVAILED*/ s.Leave_Used,
                /*137 EL*/ s.EarnedLeaveUsed,
                /*138 CL*/ s.CasualLeaveUsed,
                /*139 CO*/ s.CompoOffUsed,
                /*140 SL*/ null,
                /*141 (blank)*/ null,
                /*142 SALARY PAID-1*/ null,
                /*143 REIMB*/ null,
                /*144 INCENTIVE*/ null,
                /*145 SALARY PAID-2*/ null,
                /*146 SALARY PAID-TTL*/ null,
                /*147 DIFF*/ null,
                /*148 (blank)*/ null,
            };
        }
    }
}

