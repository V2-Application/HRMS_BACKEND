using ClosedXML.Excel;
using HRMSAPI.Data;
using HRMSAPI.DTO;
using HRMSAPI.Extension;
using HRMSAPI.Implementation;
using HRMSAPI.Interfaces;
using HRMSAPI.Models.Candidate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NLog.Targets;
using System;
using System.IO;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace HRMSAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PayrollController : ControllerBase
    {
        private readonly IPayrollService _payrollRepository;
        private readonly IWebHostEnvironment _env;

        public PayrollController(IPayrollService payrollRepository, IWebHostEnvironment env)
        {
            _payrollRepository = payrollRepository;
            _env = env;
        }

        [HttpGet("list"), RequirePageAccess("/payroll")]
        public async Task<IActionResult> ListPayroll(
    [FromQuery] string? searchTerm = null,
    [FromQuery] string? ecode = null,
    [FromQuery] long? employeeId = null,
    [FromQuery] string? location = null,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 10)
        {
            try
            {
                var (records, totalRecords) = await _payrollRepository.GetPayrollRecordsAsync(
                    searchTerm, ecode, employeeId, location, page, pageSize);

                var response = new
                {
                    TotalRecords = totalRecords,
                    Page = page,
                    PageSize = pageSize,
                    Data = records
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error listing payroll: {ex.Message}");
                return StatusCode(500, new { Message = "An error occurred while retrieving payroll records." });
            }
        }

        [HttpPost("upload"), RequirePageAccess("/payroll")]
        public async Task<IActionResult> UploadPayroll([FromForm] IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return BadRequest(new { Message = "No file uploaded" });


                // Validate file extension
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (extension != ".xlsx" && extension != ".xls")
                    return BadRequest(new { Message = "Only Excel files (.xlsx, .xls) are allowed" });

                var (success, message) = await _payrollRepository.UploadPayrollDataAsync(file, "0");

                if (success)
                    return Ok(new { Status =true,Message = message });
                else
                    return BadRequest(new { Status = false,Message = message });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error uploading payroll: {ex.Message}");
                return StatusCode(500, new { Message = "An error occurred while uploading payroll data." });
            }
        }
        [HttpGet("download-excel"), RequirePageAccess("/payroll")]
        public async Task<IActionResult> DownloadPayrollExcel(
         [FromQuery] string? searchTerm = null,
         [FromQuery] string? ecode = null,
         [FromQuery] long? employeeId = null,
         [FromQuery] string? location = null)
        {
            try
            {
                // Fetch all records without pagination
                var (records, _) = await _payrollRepository.GetPayrollRecordsAsync(
                    searchTerm, ecode, employeeId, location, fetchAll: true);

                // Create Excel workbook
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Payroll Records");

                // Define headers
                worksheet.Cell(1, 1).Value = "Employee Payroll ID";
                worksheet.Cell(1, 2).Value = "Ecode";
                worksheet.Cell(1, 3).Value = "Location";
                worksheet.Cell(1, 4).Value = "Employee ID";
                worksheet.Cell(1, 5).Value = "Full Name";
                worksheet.Cell(1, 6).Value = "Email Address";
                worksheet.Cell(1, 7).Value = "Month Year";
                worksheet.Cell(1, 8).Value = "BGT Salary";
                worksheet.Cell(1, 9).Value = "Payable Days";
                worksheet.Cell(1, 10).Value = "Gross Salary";
                worksheet.Cell(1, 11).Value = "Total Deduction";
                worksheet.Cell(1, 12).Value = "Payable Salary";
                worksheet.Cell(1, 13).Value = "PF";
                worksheet.Cell(1, 14).Value = "ESI";
                worksheet.Cell(1, 15).Value = "TDS";
                worksheet.Cell(1, 16).Value = "P TAX";
                worksheet.Cell(1, 17).Value = "Cash Short";
                worksheet.Cell(1, 18).Value = "Diesel";
                worksheet.Cell(1, 19).Value = "Penalty";
                worksheet.Cell(1, 20).Value = "Loan";
                worksheet.Cell(1, 21).Value = "OT Amount";
                worksheet.Cell(1, 22).Value = "Incentive Amount";
                worksheet.Cell(1, 23).Value = "Fooding Allowance";
                worksheet.Cell(1, 24).Value = "Arrears";
                worksheet.Cell(1, 25).Value = "Extra Days Allowance";
                worksheet.Cell(1, 26).Value = "Created On";
                worksheet.Cell(1, 27).Value = "Created By";
                worksheet.Cell(1, 28).Value = "Last Updated On";
                worksheet.Cell(1, 29).Value = "Last Updated By";

                // Populate data
                for (int i = 0; i < records.Count; i++)
                {
                    var record = records[i];
                    worksheet.Cell(i + 2, 1).Value = record.EmployeePayRollId;
                    worksheet.Cell(i + 2, 2).Value = record.Ecode;
                    worksheet.Cell(i + 2, 3).Value = record.Location;
                    worksheet.Cell(i + 2, 4).Value = record.EmployeeId;
                    worksheet.Cell(i + 2, 5).Value = record.FullName;
                    worksheet.Cell(i + 2, 6).Value = record.EmailAddress;
                    worksheet.Cell(i + 2, 7).Value = record.MonthYear;
                    worksheet.Cell(i + 2, 8).Value = record.BGT_Salary;
                    worksheet.Cell(i + 2, 9).Value = record.Payable_Days;
                    worksheet.Cell(i + 2, 10).Value = record.Gross_Salary;
                    worksheet.Cell(i + 2, 11).Value = record.Total_Deduction;
                    worksheet.Cell(i + 2, 12).Value = record.Payable_Salary;
                    worksheet.Cell(i + 2, 13).Value = record.PF;
                    worksheet.Cell(i + 2, 14).Value = record.ESI;
                    worksheet.Cell(i + 2, 15).Value = record.TDS;
                    worksheet.Cell(i + 2, 16).Value = record.P_TAX;
                    worksheet.Cell(i + 2, 17).Value = record.CASH_SHORT;
                    worksheet.Cell(i + 2, 18).Value = record.DIESEL;
                    worksheet.Cell(i + 2, 19).Value = record.PENALTY;
                    worksheet.Cell(i + 2, 20).Value = record.LOAN;
                    worksheet.Cell(i + 2, 21).Value = record.OT_AMT;
                    worksheet.Cell(i + 2, 22).Value = record.INCENTIVE_AMT;
                    worksheet.Cell(i + 2, 23).Value = record.FOODING_ALL;
                    worksheet.Cell(i + 2, 24).Value = record.ARRERS;
                    worksheet.Cell(i + 2, 25).Value = record.EXTRA_DAYS_ALLOWANCE;
                    worksheet.Cell(i + 2, 26).Value = record.CretedOn;
                    worksheet.Cell(i + 2, 27).Value = record.CreatedBy;
                    worksheet.Cell(i + 2, 28).Value = record.LastUpdatedOn;
                    worksheet.Cell(i + 2, 29).Value = record.LastUpdatedBy;
                }

                // Auto-fit columns for better readability
                worksheet.Columns().AdjustToContents();

                // Save workbook to memory stream
                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                stream.Position = 0;

                // Return Excel file
                var fileName = $"Payroll_Records_{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx";
                return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating payroll Excel: {ex.Message}");
                return StatusCode(500, new { Message = "An error occurred while generating the payroll Excel file." });
            }
        }
        [HttpGet("payroll-summary"), RequirePageAccess("/payroll-summary")]
        public async Task<IActionResult> GetPayrollSummary(
          [FromQuery] DateTime startDate,
          [FromQuery] DateTime endDate,
          [FromQuery] int pageNumber = 1,
          [FromQuery] int pageSize = 10)
        {
            try
            {
                var result = await _payrollRepository.GetPayrollSummaryAsync(startDate, endDate, pageNumber, pageSize);
                return Ok(new
                {
                    Status = true,
                    Message = "Payroll summary retrieved successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    Status = false,
                    Message = "An error occurred while retrieving payroll summary",
                    Error = ex.Message
                });
            }
        }
        [HttpPost("upsertPFApproval"), RequirePageAccess("/payroll")]
        public async Task<IActionResult> UpsertPFApproval([FromForm] PFApprovalRequest dto, CancellationToken ct)
        {
            var userIdentity = User.Identity as ClaimsIdentity;
            if (userIdentity == null || !userIdentity.IsAuthenticated)
            {
                return Unauthorized(new
                {
                    Status = false,
                    Message = "User is not authenticated"
                });
            }

            var loginDetail = AuthenticUserDetails.GetCurrentUserDetails(userIdentity);
            var updatedBy = userIdentity.FindFirst("EmployeeId")?.Value;
            if (dto == null)
                return BadRequest("Request is required.");

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (dto.Attachment == null || dto.Attachment.Length == 0)
                return BadRequest("Attachment file is required.");

            // basic file checks (customize)
            var allowedExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".pdf", ".jpg", ".jpeg", ".png" };
            var ext = Path.GetExtension(dto.Attachment.FileName);

            if (!allowedExt.Contains(ext))
                return BadRequest("Invalid file type. Allowed: PDF, JPG, PNG.");

            const long maxBytes = 5 * 1024 * 1024; // 5MB
            if (dto.Attachment.Length > maxBytes)
                return BadRequest("File too large (max 5MB).");

            var webRoot = _env.WebRootPath;
            if (string.IsNullOrWhiteSpace(webRoot))
                webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            
            var uploadsDir = Path.Combine(webRoot, "PFUploads");
            Directory.CreateDirectory(uploadsDir);

            var fileName = $"{Guid.NewGuid():N}{ext}";
            var filePath = Path.Combine(uploadsDir, fileName);

            await using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await dto.Attachment.CopyToAsync(stream, ct);
            }

            var entity = new tblPF_Approval
            {
                E_Code = dto.E_Code?.Trim(),
                _Month = dto._Month?.Trim(),
                Challan_No = dto.Challan_No?.Trim(),
                Attachment = $"/PFUploads/{fileName}",
                createdBy = updatedBy,
                updatedBy = updatedBy
            };

            var result = await _payrollRepository.UpsertPFApprovalAsync(entity, ct);

            return StatusCode((int)result.Code, new
            {
                Status = result.Status,
                Message = result.Message
            });
        }
        [HttpGet("process-salary-list"), RequirePageAccess("/process-salary")]
        public async Task<IActionResult> GetProcessSalaryList(
            [FromQuery] string? searchTerm = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                var (records, totalRecords) = await _payrollRepository.GetSalaryProcessListAsync(searchTerm, page, pageSize);
                return Ok(new
                {
                    TotalRecords = totalRecords,
                    Page = page,
                    PageSize = pageSize,
                    Data = records
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An error occurred while retrieving salary process data.", Error = ex.Message });
            }
        }

        [HttpPost("process-salary-upload"), RequirePageAccess("/process-salary")]
        public async Task<IActionResult> UploadProcessSalary(IFormFile file)
        {
            try
            {
                //var identity = User.Identity as ClaimsIdentity;
                //var userId = identity?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "System";
                //// Or use specific claim if needed, e.g. EmployeeId
                //var empId = identity?.FindFirst("EmployeeId")?.Value ?? userId;
                var identity = HttpContext.User.Identity as ClaimsIdentity;
                var userClaims = AuthenticUserDetails.GetCurrentUserDetails(identity);

                if (userClaims == null || string.IsNullOrEmpty(userClaims.EmployeeId))
                {
                    return BadRequest(new
                    {
                        Status = false,
                        Message = "Invalid user credentials."
                    });
                }
                var (success, message) = await _payrollRepository.UploadSalaryProcessAsync(file, userClaims.EmployeeId);
                
                if (success)
                    return Ok(new { Status = true, Message = message });
                else
                    return BadRequest(new { Status = false, Message = message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An error occurred while uploading salary process data.", Error = ex.Message });
            }
        }

        [HttpGet("process-salary-sample"), RequirePageAccess("/process-salary")]
        public IActionResult DownloadProcessSalarySample()
        {
            try
            {
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("SalaryProcessSample");
                
                // Headers 
                var headers = new[]
                {
                    "Ecode", "Location_Code", "Location Name", "Employee Name", "designation", "department", "Month-Year",
                    "ttl bgt days", "actualttl days", "GF", "Machine", "MachineWP", "MANUAL", "actualweekly", "presentweeklyoff",
                    "HolidayOff", "paybledays", "extradays", "Absent", "LWP", "AdjustedDays", "Status",
                    "BasicSalary(Bud.)", "HRA(Bud.)", "CCA(Bud.)", "SpecialAllowance(Bud.)", "DA(Bud.)", "Reimbersment(Bud.)",
                    "Fuel and Maintenance(Bud.)", "Books and Periodicals(Bud.)", "Professional Attire(Bud.)", "Driver Wages(Bud.)",
                    "Mobile Bill(Bud.)", "Meal Voucher(Bud.)", "Monthly Gross CTC(Bud.)", "BasicSalary(Actual)", "HRA(Actual)",
                    "CCA(Actual)", "SpecialAllowance(Actual)", "DA(Actual)", "ExtraDayAllowance", "Reimbersment(Actual)",
                    "Fuel and Maintenance(Actual)", "Books and Periodicals(Actual)", "Professional Attire(Actual)", "Driver Wages(Actual)",
                    "Mobile Bill(Actual)", "Meal Voucher(Actual)", "PF(Employee)", "PF(Employeer)", "PF(Total)", "ESIC(Employee)",
                    "ESIC(Employeer)", "ESIC(Total)", "TDS", "PTax", "Loan", "CashShort", "DieselDeduction", "Penality", "Lwf",
                    "TotalDeductions", "Incentive", "ARREAR", "Overtime", "Fooding_Allowance", "Mobile_Bill", "Monthly Gross CTC(Actual)",
                    "Monthly Gross CTC(Actual After Deduction AND AddONS)", "Payble_Days", "Leave-Used", "Opening EL", "EarnedLeaveAcquired",
                    "EarnedLeaveUsed", "EarnedLeaveBalance", "Opening CL", "CasualLeaveAcquired", "CasualLeaveUsed", "CasualLeaveBalance",
                    "Opening CompoOff", "CompoOffAcquired", "CompoOffUsed", "CompoOffBalance", "MONTH", "BatchNo", "RunAt", "SalaryStatus", "ID"
                };

                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(1, i + 1).Value = headers[i];
                }

                // Add sample row
                worksheet.Cell(2, 1).Value = "RTN"; // Ecode
                worksheet.Cell(2, 2).Value = "RH010";// Location_Code
                worksheet.Cell(2, 3).Value = "HO"; // Location Name
                worksheet.Cell(2, 4).Value = "NIKHILSHARMA"; // Employee Name
                worksheet.Cell(2, 7).Value = "Jan-2"; // Month-Year
                worksheet.Cell(2, 8).Value = 31.00; // ttl bgt days
                worksheet.Cell(2, 73).Value = "Oct-25"; // MONTH
                worksheet.Cell(2, 75).Value = DateTime.Now.ToString(); // RunAt (Sample)
                worksheet.Cell(2, 77).Value = 43294; // ID

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                
                return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "SalaryProcessSample.xlsx");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Error generating sample file.", Error = ex.Message });
            }
        }

        [HttpGet("ExportProcessedSalary"), RequirePageAccess("/processed-salary")]
        public async Task<IActionResult> ExportProcessedSalary([FromQuery] string? searchTerm = null)
        {
            try
            {
                var data = await _payrollRepository.GetSalaryProcessExportDataAsync(searchTerm);
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("ProcessedSalary");

                // Headers 
                var headers = new[]
                {
                    "Ecode", "Location_Code", "Location Name", "Employee Name", "Designation", "Department", "Month-Year",
                    "Ttl Bgt Days", "Actual Ttl Days", "GF", "Machine", "MachineWP", "MANUAL", "Actual Weekly", "Present Weekly Off",
                    "Holiday Off", "Payble Days", "Extra Days", "Absent", "LWP", "Adjusted Days", "Status",
                    "BasicSalary(Bud.)", "HRA(Bud.)", "CCA(Bud.)", "SpecialAllowance(Bud.)", "DA(Bud.)", "Reimbersment(Bud.)",
                    "Fuel and Maintenance(Bud.)", "Books and Periodicals(Bud.)", "Professional Attire(Bud.)", "Driver Wages(Bud.)",
                    "Mobile Bill(Bud.)", "Meal Voucher(Bud.)", "Monthly Gross CTC(Bud.)", "BasicSalary(Actual)", "HRA(Actual)",
                    "CCA(Actual)", "SpecialAllowance(Actual)", "DA(Actual)", "ExtraDayAllowance", "Reimbersment(Actual)",
                    "Fuel and Maintenance(Actual)", "Books and Periodicals(Actual)", "Professional Attire(Actual)", "Driver Wages(Actual)",
                    "Mobile Bill(Actual)", "Meal Voucher(Actual)", "PF(Employee)", "PF(Employeer)", "PF(Total)", "ESIC(Employee)",
                    "ESIC(Employeer)", "ESIC(Total)", "TDS", "PTax", "Loan", "CashShort", "DieselDeduction", "Penality", "Lwf",
                    "TotalDeductions", "Incentive", "ARREAR", "Overtime", "Fooding_Allowance", "Mobile_Bill", "Monthly Gross CTC(Actual)",
                    "Monthly Gross CTC(Actual After Deduction AND AddONS)", "Payble_Days", "Leave-Used", "Opening EL", "EarnedLeaveAcquired",
                    "EarnedLeaveUsed", "EarnedLeaveBalance", "Opening CL", "CasualLeaveAcquired", "CasualLeaveUsed", "CasualLeaveBalance",
                    "Opening CompoOff", "CompoOffAcquired", "CompoOffUsed", "CompoOffBalance", "MONTH", "BatchNo", "RunAt", "SalaryStatus", "ID"
                };

                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(1, i + 1).Value = headers[i];
                    worksheet.Cell(1, i + 1).Style.Font.Bold = true;
                }

                int row = 2;
                foreach (var item in data)
                {
                    int c = 1;
                    worksheet.Cell(row, c++).Value = item.Ecode;
                    worksheet.Cell(row, c++).Value = item.Location_Code;
                    worksheet.Cell(row, c++).Value = item.LocationName;
                    worksheet.Cell(row, c++).Value = item.EmployeeName;
                    worksheet.Cell(row, c++).Value = item.Designation;
                    worksheet.Cell(row, c++).Value = item.Department;
                    worksheet.Cell(row, c++).Value = item.MonthYear;
                    worksheet.Cell(row, c++).Value = item.TtlBgtDays;
                    worksheet.Cell(row, c++).Value = item.ActualTtlDays;
                    worksheet.Cell(row, c++).Value = item.GF;
                    worksheet.Cell(row, c++).Value = item.Machine;
                    worksheet.Cell(row, c++).Value = item.MachineWP;
                    worksheet.Cell(row, c++).Value = item.MANUAL;
                    worksheet.Cell(row, c++).Value = item.ActualWeekly;
                    worksheet.Cell(row, c++).Value = item.PresentWeeklyOff;
                    worksheet.Cell(row, c++).Value = item.HolidayOff;
                    worksheet.Cell(row, c++).Value = item.PaybleDays;
                    worksheet.Cell(row, c++).Value = item.ExtraDays;
                    worksheet.Cell(row, c++).Value = item.Absent;
                    worksheet.Cell(row, c++).Value = item.LWP;
                    worksheet.Cell(row, c++).Value = item.AdjustedDays;
                    worksheet.Cell(row, c++).Value = item.Status;
                    worksheet.Cell(row, c++).Value = item.BasicSalaryBud;
                    worksheet.Cell(row, c++).Value = item.HRABud;
                    worksheet.Cell(row, c++).Value = item.CCABud;
                    worksheet.Cell(row, c++).Value = item.SpecialAllowanceBud;
                    worksheet.Cell(row, c++).Value = item.DABud;
                    worksheet.Cell(row, c++).Value = item.ReimbersmentBud;
                    worksheet.Cell(row, c++).Value = item.FuelAndMaintenanceBud;
                    worksheet.Cell(row, c++).Value = item.BooksAndPeriodicalsBud;
                    worksheet.Cell(row, c++).Value = item.ProfessionalAttireBud;
                    worksheet.Cell(row, c++).Value = item.DriverWagesBud;
                    worksheet.Cell(row, c++).Value = item.MobileBillBud;
                    worksheet.Cell(row, c++).Value = item.MealVoucherBud;
                    worksheet.Cell(row, c++).Value = item.MonthlyGrossCTCBud;
                    worksheet.Cell(row, c++).Value = item.BasicSalaryActual;
                    worksheet.Cell(row, c++).Value = item.HRAActual;
                    worksheet.Cell(row, c++).Value = item.CCAActual;
                    worksheet.Cell(row, c++).Value = item.SpecialAllowanceActual;
                    worksheet.Cell(row, c++).Value = item.DAActual;
                    worksheet.Cell(row, c++).Value = item.ExtraDayAllowance;
                    worksheet.Cell(row, c++).Value = item.ReimbersmentActual;
                    worksheet.Cell(row, c++).Value = item.FuelAndMaintenanceActual;
                    worksheet.Cell(row, c++).Value = item.BooksAndPeriodicalsActual;
                    worksheet.Cell(row, c++).Value = item.ProfessionalAttireActual;
                    worksheet.Cell(row, c++).Value = item.DriverWagesActual;
                    worksheet.Cell(row, c++).Value = item.MobileBillActual;
                    worksheet.Cell(row, c++).Value = item.MealVoucherActual;
                    worksheet.Cell(row, c++).Value = item.PFEmployee;
                    worksheet.Cell(row, c++).Value = item.PFEmployer;
                    worksheet.Cell(row, c++).Value = item.PFTotal;
                    worksheet.Cell(row, c++).Value = item.ESICEmployee;
                    worksheet.Cell(row, c++).Value = item.ESICEmployer;
                    worksheet.Cell(row, c++).Value = item.ESICTotal;
                    worksheet.Cell(row, c++).Value = item.TDS;
                    worksheet.Cell(row, c++).Value = item.PTax;
                    worksheet.Cell(row, c++).Value = item.Loan;
                    worksheet.Cell(row, c++).Value = item.CashShort;
                    worksheet.Cell(row, c++).Value = item.DieselDeduction;
                    worksheet.Cell(row, c++).Value = item.Penality;
                    worksheet.Cell(row, c++).Value = item.Lwf;
                    worksheet.Cell(row, c++).Value = item.TotalDeductions;
                    worksheet.Cell(row, c++).Value = item.Incentive;
                    worksheet.Cell(row, c++).Value = item.ARREAR;
                    worksheet.Cell(row, c++).Value = item.Overtime;
                    worksheet.Cell(row, c++).Value = item.FoodingAllowance;
                    worksheet.Cell(row, c++).Value = item.MobileBill;
                    worksheet.Cell(row, c++).Value = item.MonthlyGrossCTCActual;
                    worksheet.Cell(row, c++).Value = item.MonthlyGrossCTCActualAfterDeductionAndAddOns;
                    worksheet.Cell(row, c++).Value = item.Payble_Days2;
                    worksheet.Cell(row, c++).Value = item.LeaveUsed;
                    worksheet.Cell(row, c++).Value = item.OpeningEL;
                    worksheet.Cell(row, c++).Value = item.EarnedLeaveAcquired;
                    worksheet.Cell(row, c++).Value = item.EarnedLeaveUsed;
                    worksheet.Cell(row, c++).Value = item.EarnedLeaveBalance;
                    worksheet.Cell(row, c++).Value = item.OpeningCL;
                    worksheet.Cell(row, c++).Value = item.CasualLeaveAcquired;
                    worksheet.Cell(row, c++).Value = item.CasualLeaveUsed;
                    worksheet.Cell(row, c++).Value = item.CasualLeaveBalance;
                    worksheet.Cell(row, c++).Value = item.OpeningCompoOff;
                    worksheet.Cell(row, c++).Value = item.CompoOffAcquired;
                    worksheet.Cell(row, c++).Value = item.CompoOffUsed;
                    worksheet.Cell(row, c++).Value = item.CompoOffBalance;
                    worksheet.Cell(row, c++).Value = item.MONTH;
                    worksheet.Cell(row, c++).Value = item.BatchNo;
                    worksheet.Cell(row, c++).Value = item.RunAt;
                    worksheet.Cell(row, c++).Value = item.SalaryStatus;
                    worksheet.Cell(row, c++).Value = item.ID;

                    row++;
                }
                
                worksheet.Columns().AdjustToContents();

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                
                return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"ProcessedSalary_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Error exporting data.", Error = ex.Message });
            }
        }
    }
}