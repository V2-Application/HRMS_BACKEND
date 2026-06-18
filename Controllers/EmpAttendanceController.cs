using ClosedXML.Excel;
using HRMSAPI.Data;
using HRMSAPI.DTO;
using HRMSAPI.Extension;
using HRMSAPI.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Roomsy.DTOS.GenericsResponses;
using Serilog;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Net;
using System.Security.Claims;


namespace HRMSAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public partial class EmpAttendanceController : ControllerBase
    {
        private readonly IEmpAttendanceService _service;
        public readonly HRMSContext _context;
        private readonly IMemoryCache _memoryCache;
        
        public EmpAttendanceController(IEmpAttendanceService service, HRMSContext context, IMemoryCache memoryCache)
        {
            _service = service;
            _context = context;
            _memoryCache = memoryCache;
        }

        [HttpPost("refreshattendace"), Authorize]
        public async Task<IActionResult> RefreshAttendanceAsync()
        {
            await _service.FetchAndSaveAttendanceAsync();
            return Ok("Attendance data refreshed successfully.");
        }
        [HttpPost("refreshmultipunchattendace")]
        [AllowAnonymous]
        public async Task<IActionResult> FetchAndSavePunchesAsync()
        {
            await _service.FetchAndSavePunchesAsync();
            return Ok("Attendance data refreshed successfully.");
        }

        [HttpPost("refreshmultipunchattendacerange")]
        [AllowAnonymous]
        public async Task<IActionResult> FetchAndSavePunchesRangeAsync([FromBody] DateRangeDto dateRange)
        {
            try
            {
                if (dateRange == null || dateRange.FromDate == default || dateRange.ToDate == default)
                {
                    return BadRequest("FromDate and ToDate are required.");
                }

                if (dateRange.FromDate > dateRange.ToDate)
                {
                    return BadRequest("FromDate cannot be greater than ToDate.");
                }

                await _service.FetchAndSavePunchesRangeAsync(dateRange.FromDate, dateRange.ToDate);
                return Ok($"Attendance data refreshed successfully for date range: {dateRange.FromDate:yyyy-MM-dd} to {dateRange.ToDate:yyyy-MM-dd}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }

        [HttpGet("multipunch-by-ecode")]
        [AllowAnonymous]
        public async Task<IActionResult> GetMultiPunchByEcode([FromQuery] DateTime fromDate, [FromQuery] DateTime toDate, [FromQuery] string ecode)
        {
            if (string.IsNullOrWhiteSpace(ecode))
                return BadRequest("Ecode is required.");

            if (fromDate == default || toDate == default)
                return BadRequest("FromDate and ToDate are required.");

            if (fromDate > toDate)
                return BadRequest("FromDate cannot be greater than ToDate.");

            var data = await _service.FetchPunchesRangeByEcodeAsync(fromDate, toDate, ecode);

            return Ok(new FetchAndResponse
            {
                Status = true,
                Message = "Multi punch attendance fetched successfully.",
                Data = data
            });
        }

        [HttpGet("refreshmultipunchattendacebyecode")]
        [AllowAnonymous]
        public async Task<IActionResult> FetchAndSavePunchesRangeByEcodeAsync(
            [FromQuery] DateTime fromDate,
            [FromQuery] DateTime toDate,
            [FromQuery] string ecode)
        {
            if (string.IsNullOrWhiteSpace(ecode))
                return BadRequest("Ecode is required.");

            if (fromDate == default || toDate == default)
                return BadRequest("FromDate and ToDate are required.");

            if (fromDate > toDate)
                return BadRequest("FromDate cannot be greater than ToDate.");

            await _service.FetchAndSavePunchesRangeByEcodeAsync(fromDate, toDate, ecode);

            return Ok($"Attendance data refreshed for {ecode} between {fromDate:yyyy-MM-dd} and {toDate:yyyy-MM-dd}.");
        }

        [HttpPost("GetMonthlyAttendance"), Authorize, RequirePageAccess("/attandance/track")]
        public async Task<IActionResult> GetMonthlyAttendance([FromBody] AttendanceGetDto request)
        {
            try
            {
                // Create a unique cache key based on Month, Year, and ECode
                //string cacheKey = $"MonthlyAttendance_{request.Year}_{request.Month}_{request.ECode ?? "ALL"}";

                //// Try to get data from cache
                //if (_memoryCache.TryGetValue(cacheKey, out List<AttendanceFetchDto>? cachedResult))
                //{
                //    return Ok(cachedResult);
                //}

                // Convert month and year to date range
                DateTime fromDate = new DateTime(request.Year, request.Month, 1);
                DateTime toDate = fromDate.AddMonths(1).AddDays(-1);

                var fromDateParam = new SqlParameter("@FromDate", fromDate);
                var toDateParam = new SqlParameter("@ToDate", toDate);
                var ecodeParam = new SqlParameter("@ECode", (object?)request.ECode ?? DBNull.Value);

                // Fetch data from service (UseCycle => 26th prev month .. 25th selected month)
                var result = await _service.FetchAttendance(request.Month, request.Year, request.ECode, request.UseCycle);

                // Cache the result for 1 hour
                //var cacheOptions = new MemoryCacheEntryOptions
                //{
                //    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1),
                //    SlidingExpiration = null // Use absolute expiration only
                //};

                //_memoryCache.Set(cacheKey, result, cacheOptions);

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }

        [HttpPost("GetMonthlyAttendance_Ishu"), Authorize, RequirePageAccess("/attandance/track")]
        public async Task<IActionResult> GetMonthlyAttendance_Ishu([FromBody] AttendanceGetDto request)
        {
            try
            {
                var result = await _service.FetchAttendance_Ishu(request.Month, request.Year, request.ECode ?? string.Empty);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }

        // Self-service submit: any authenticated employee may create their own regularize request.
        // No RequirePageAccess gate — the endpoint scopes the request to the caller's own EmployeeId
        // (from the JWT), so page-level RBAC would only block legitimate employees from raising tickets.
        [HttpPost("regularization"), Authorize]
        public async Task<IActionResult> CreateAttendanceRequest([FromForm] AttendanceRegularizationRequestDto requestDto, IFormFile? attachment)
        {
            var userIdentity = User.Identity as ClaimsIdentity;
            if (userIdentity == null || !userIdentity.IsAuthenticated)
            {
                return Unauthorized(new ResponseDto
                {
                    Status = false,
                    Message = "User is not authenticated."
                });
            }

            var loginDetail = AuthenticUserDetails.GetCurrentUserDetails(userIdentity);
            var updatedBy = userIdentity.FindFirst("EmployeeId")?.Value;

            if (string.IsNullOrEmpty(updatedBy))
            {
                return Unauthorized(new ResponseDto
                {
                    Status = false,
                    Message = "Employee ID not found in user claims."
                });
            }

            try
            {
                string? fileUrl = null;

                if (attachment != null && attachment.Length > 0)
                {
                    // Create folder path
                    var folderName = $"AttendanceFiles/{DateTime.Now:yyyyMMddHHmmssfff}";
                    var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", folderName);

                    Directory.CreateDirectory(folderPath);

                    // Save file
                    var filePath = Path.Combine(folderPath, attachment.FileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await attachment.CopyToAsync(stream);
                    }

                    // Generate file URL
                    fileUrl = $"{Request.Scheme}://{Request.Host}/" + folderName + "/" + attachment.FileName;
                }

                var requestId = await _service.CreateAttendanceRequestAsync(requestDto, loginDetail, fileUrl);

                return Ok(new ResponseDto
                {
                    Status = true,
                    Message = "Attendance request created successfully.",
                    Data = new { AttendanceRequestId = requestId }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ResponseDto
                {
                    Status = false,
                    Message = GetFullErrorMessage(ex)
                });
            }
        }

        private string GetFullErrorMessage(Exception ex)
        {
            return ex.InnerException?.Message ?? ex.Message;
        }

        [HttpGet("RegularizeRequestsformanager/{managerId}"), Authorize, RequirePageAccess("/regularize-request")]
        public async Task<IActionResult> GetRegularizationRequests(
      long managerId,
      int statusId = 0,
      int pageNumber = 1,
      int pageSize = 10,
      string? searchTerm = null)
        {
            var identity = HttpContext.User.Identity as ClaimsIdentity;
            if (identity == null || !identity.IsAuthenticated)
                return Unauthorized(new { Status = false, Message = "User is not authenticated" });

            var loginDetail = AuthenticUserDetails.GetCurrentUserDetails(identity);
            string role = loginDetail.role?.Trim() ?? string.Empty;

            // You’ll need the caller’s EmployeeId for “my team” filter
            if (!long.TryParse(loginDetail.EmployeeId, out var currentEmployeeId))
                return BadRequest(new { Status = false, Message = "Invalid EmployeeId in token" });

            var data = await _service.GetRegularizationRequestsAsync(
                managerId, role, currentEmployeeId, statusId, pageNumber, pageSize, searchTerm);

            return Ok(new
            {
                Status = true,
                Message = "Data fetched successfully",
                Data = data.Data,
                TotalRecords = data.TotalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize
            });
        }


        [HttpPost("regularize/approve/{requestId}"), Authorize, RequirePageAccess("/regularize-request")]
        public async Task<IActionResult> ApproveRegularization(int requestId, [FromBody] UpdateAttendanceRequestDto dto)
        {
            var identity = User.Identity as ClaimsIdentity;
            if (identity == null || !identity.IsAuthenticated)
                return BadRequest(new ApiResponse<object>(HttpStatusCode.BadRequest, false, "User is not authenticated", null));

            var login = AuthenticUserDetails.GetCurrentUserDetails(identity);

            if (!long.TryParse(login.EmployeeId, out var callerEmployeeId))
                return BadRequest(new ApiResponse<object>(HttpStatusCode.BadRequest, false, "Invalid EmployeeId in token", null));

            var role = (login.role ?? "").Trim();

            var response = await _service.ApproveRegularizationAsync(requestId, dto, callerEmployeeId, role);

            return StatusCode((int)response.StatusCode, response);
        }

        [NonAction]
        public async Task<IActionResult> DownloadMonthlyPunchesExcelold([FromBody] AttendanceRangeGetDto request)
        {
            try
            {
                var data = await _service.FetchPunchesRange(request.FromDate, request.ToDate, request.ECode);

                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Punches");

                // Headers (unchanged)
                worksheet.Cell(1, 1).Value = "EmployeeName";
                worksheet.Cell(1, 2).Value = "ECode";
                worksheet.Cell(1, 3).Value = "DesignationName";
                worksheet.Cell(1, 4).Value = "LocationName";
                worksheet.Cell(1, 5).Value = "STCode";
                worksheet.Cell(1, 6).Value = "DepartmentName";
                worksheet.Cell(1, 7).Value = "MachineType";
                worksheet.Cell(1, 8).Value = "AttendanceDate";
                worksheet.Cell(1, 9).Value = "Punch1";
                worksheet.Cell(1, 10).Value = "Punch2";
                worksheet.Cell(1, 11).Value = "Punch3";
                worksheet.Cell(1, 12).Value = "Punch4";
                worksheet.Cell(1, 13).Value = "Punch5";
                worksheet.Cell(1, 14).Value = "Punch6";
                worksheet.Cell(1, 15).Value = "Punch7";
                worksheet.Cell(1, 16).Value = "Punch8";
                worksheet.Cell(1, 17).Value = "Punch9";
                worksheet.Cell(1, 18).Value = "Punch10";
                worksheet.Cell(1, 19).Value = "Punch11";
                worksheet.Cell(1, 20).Value = "Punch12";
                worksheet.Cell(1, 21).Value = "PunchIn";
                worksheet.Cell(1, 22).Value = "PunchOut";
                worksheet.Cell(1, 23).Value = "TotalWorkingMinutes";
                worksheet.Cell(1, 24).Value = "Status";

                // Set column formats (unchanged)
                worksheet.Column(8).Style.DateFormat.Format = "yyyy-mm-dd"; // AttendanceDate
                worksheet.Columns(9, 20).Style.DateFormat.Format = "hh:mm:ss"; // Punch1 to Punch12
                worksheet.Column(21).Style.DateFormat.Format = "hh:mm:ss"; // PunchIn
                worksheet.Column(22).Style.DateFormat.Format = "hh:mm:ss"; // PunchOut
                worksheet.Column(23).Style.NumberFormat.Format = "0.00"; // TotalWorkingHours

                int row = 2;
                foreach (var item in data)
                {
                    worksheet.Cell(row, 1).Value = item.EmployeeName ?? "NA";
                    worksheet.Cell(row, 2).Value = item.ECode ?? "";
                    worksheet.Cell(row, 3).Value = item.DesignationName ?? "N/A";
                    worksheet.Cell(row, 4).Value = item.LocationName ?? "N/A";
                    worksheet.Cell(row, 5).Value = item.STCode ?? "N/A";
                    worksheet.Cell(row, 6).Value = item.DepartmentName ?? "N/A";
                    worksheet.Cell(row, 7).Value = item.MachineType ?? "N/A";
                    worksheet.Cell(row, 8).Value = item.AttendanceDate?.ToString("yyyy-MM-dd") ?? "";
                    worksheet.Cell(row, 9).Value = item.Punch1 == "00:00:00" ? "" : item.Punch1;
                    worksheet.Cell(row, 10).Value = item.Punch2 == "00:00:00" ? "" : item.Punch2;
                    worksheet.Cell(row, 11).Value = item.Punch3 == "00:00:00" ? "" : item.Punch3;
                    worksheet.Cell(row, 12).Value = item.Punch4 == "00:00:00" ? "" : item.Punch4;
                    worksheet.Cell(row, 13).Value = item.Punch5 == "00:00:00" ? "" : item.Punch5;
                    worksheet.Cell(row, 14).Value = item.Punch6 == "00:00:00" ? "" : item.Punch6;
                    worksheet.Cell(row, 15).Value = item.Punch7 == "00:00:00" ? "" : item.Punch7;
                    worksheet.Cell(row, 16).Value = item.Punch8 == "00:00:00" ? "" : item.Punch8;
                    worksheet.Cell(row, 17).Value = item.Punch9 == "00:00:00" ? "" : item.Punch9;
                    worksheet.Cell(row, 18).Value = item.Punch10 == "00:00:00" ? "" : item.Punch10;
                    worksheet.Cell(row, 19).Value = item.Punch11 == "00:00:00" ? "" : item.Punch11;
                    worksheet.Cell(row, 20).Value = item.Punch12 == "00:00:00" ? "" : item.Punch12;
                    worksheet.Cell(row, 21).Value = item.PunchIn.HasValue ? item.PunchIn.Value.ToString(@"hh\:mm\:ss") : "";
                    worksheet.Cell(row, 22).Value = item.PunchOut.HasValue ? item.PunchOut.Value.ToString(@"hh\:mm\:ss") : "";
                    worksheet.Cell(row, 23).Value = item.TotalWorkingHours;
                    worksheet.Cell(row, 24).Value = item.Status ?? "Absent";

                    row++;
                }

                // Auto-adjust column widths
                worksheet.Columns().AdjustToContents();

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                stream.Position = 0;

                string fileName = $"Punches_{request.FromDate:yyyyMMdd}_to_{request.ToDate:yyyyMMdd}.xlsx";
                return File(stream.ToArray(),
                            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                            fileName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }
        [HttpPost("DownloadMonthlyAttendanceExcel"), Authorize, RequirePageAccess("/attandance/track")]
        public async Task<IActionResult> DownloadMonthlyPunchesExcel([FromBody] AttendanceRangeGetDto request)
        {
            try
            {
                if (request == null)
                    return BadRequest("Request cannot be null.");

                var data = await _service.FetchPunchesRangeExcel(request.FromDate, request.ToDate, request.ECode);

                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Punches");

                // -------------------------
                // HEADERS
                // -------------------------
                string[] headers =
                {
            "EmployeeName","ECode","DesignationName","LocationName","STCode","DepartmentName","MachineType",
            "AttendanceDate","Punch1","Punch2","Punch3","Punch4","Punch5","Punch6","Punch7","Punch8","Punch9",
            "Punch10","Punch11","Punch12","PunchIn","PunchOut","TotalWorkingMinutes","LateMinutes","EarlyMinutes",
            "TotalMonthlyWorkingHours","Status","RegularizePunchIn","RegularizePunchOut","IsRegularize","TotalWorkingDays"
        };

                for (int i = 0; i < headers.Length; i++)
                    worksheet.Cell(1, i + 1).Value = headers[i];

                // -------------------------
                // FORMATTING
                // -------------------------
                worksheet.Column(8).Style.DateFormat.Format = "yyyy-MM-dd";     // AttendanceDate
                worksheet.Columns(9, 20).Style.DateFormat.Format = "hh:mm:ss";  // Punch1 - Punch12
                worksheet.Column(21).Style.DateFormat.Format = "hh:mm:ss";      // PunchIn
                worksheet.Column(22).Style.DateFormat.Format = "hh:mm:ss";      // PunchOut
                worksheet.Column(24).Style.NumberFormat.Format = "0";           // LateMinutes
                worksheet.Column(25).Style.NumberFormat.Format = "0";           // EarlyMinutes
                worksheet.Column(28).Style.DateFormat.Format = "hh:mm:ss";      // RegularizePunchIn
                worksheet.Column(29).Style.DateFormat.Format = "hh:mm:ss";      // RegularizePunchOut
                worksheet.Column(31).Style.NumberFormat.Format = "0.0";         // TotalWorkingDays

                // -------------------------
                // HELPERS
                // -------------------------
                void SetTimeStringCell(IXLCell cell, string? time)
                {
                    if (string.IsNullOrWhiteSpace(time) || time == "00:00:00")
                    {
                        cell.Value = "";   // empty cell
                        return;
                    }

                    if (TimeSpan.TryParse(time, out var ts))
                    {
                        cell.Value = ts;   // Excel time
                    }
                    else
                    {
                        cell.Value = time; // fallback as string
                    }
                }

                // -------------------------
                // DATA
                // -------------------------
                int row = 2;

                foreach (var item in data ?? Enumerable.Empty<PunchFetchDto>())
                {
                    worksheet.Cell(row, 1).Value = item.EmployeeName ?? "NA";
                    worksheet.Cell(row, 2).Value = item.ECode ?? "";
                    worksheet.Cell(row, 3).Value = item.DesignationName ?? "N/A";
                    worksheet.Cell(row, 4).Value = item.LocationName ?? "N/A";
                    worksheet.Cell(row, 5).Value = item.STCode ?? "N/A";
                    worksheet.Cell(row, 6).Value = item.DepartmentName ?? "N/A";
                    worksheet.Cell(row, 7).Value = item.MachineType ?? "N/A";

                    // Attendance date
                    if (item.AttendanceDate.HasValue)
                        worksheet.Cell(row, 8).Value = item.AttendanceDate.Value.Date;
                    else
                        worksheet.Cell(row, 8).Value = "";

                    // Punch1–Punch12 (strings like "00:00:00")
                    SetTimeStringCell(worksheet.Cell(row, 9), item.Punch1);
                    SetTimeStringCell(worksheet.Cell(row, 10), item.Punch2);
                    SetTimeStringCell(worksheet.Cell(row, 11), item.Punch3);
                    SetTimeStringCell(worksheet.Cell(row, 12), item.Punch4);
                    SetTimeStringCell(worksheet.Cell(row, 13), item.Punch5);
                    SetTimeStringCell(worksheet.Cell(row, 14), item.Punch6);
                    SetTimeStringCell(worksheet.Cell(row, 15), item.Punch7);
                    SetTimeStringCell(worksheet.Cell(row, 16), item.Punch8);
                    SetTimeStringCell(worksheet.Cell(row, 17), item.Punch9);
                    SetTimeStringCell(worksheet.Cell(row, 18), item.Punch10);
                    SetTimeStringCell(worksheet.Cell(row, 19), item.Punch11);
                    SetTimeStringCell(worksheet.Cell(row, 20), item.Punch12);

                    // PunchIn / PunchOut are TimeSpan?
                    if (item.PunchIn.HasValue)
                        worksheet.Cell(row, 21).Value = item.PunchIn.Value;
                    else
                        worksheet.Cell(row, 21).Value = "";

                    if (item.PunchOut.HasValue)
                        worksheet.Cell(row, 22).Value = item.PunchOut.Value;
                    else
                        worksheet.Cell(row, 22).Value = "";

                    worksheet.Cell(row, 23).Value = item.TotalWorkingMinutes ?? "0 hours and 00 minutes";
                    worksheet.Cell(row, 24).Value = item.LateMinutes;
                    worksheet.Cell(row, 25).Value = item.EarlyMinutes;
                    worksheet.Cell(row, 26).Value = item.TotalMonthlyWorkingHours ?? "0 hours and 00 minutes";
                    worksheet.Cell(row, 27).Value = item.Status ?? "Absent";

                    // Regularize punches – they are stored as string/time in DTO
                    SetTimeStringCell(worksheet.Cell(row, 28), item.RegularizePunchIn);
                    SetTimeStringCell(worksheet.Cell(row, 29), item.RegularizePuncOut);

                    // IsRegularize (bool or bool?)
                    // if it's bool? in DTO, use: item.IsRegularize ?? false;
                    worksheet.Cell(row, 30).Value = (XLCellValue)item.IsRegularize;

                    worksheet.Cell(row, 31).Value = item.TotalWorkingDays;

                    row++;
                }

                worksheet.Columns().AdjustToContents();

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                stream.Position = 0;

                string fileName = $"Punches_{request.FromDate:yyyyMMdd}_to_{request.ToDate:yyyyMMdd}.xlsx";

                return File(
                    stream.ToArray(),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    fileName
                );
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error generating Excel file: {ex.Message}");
            }
        }


        [HttpPost("DownloadMonthlyAttendance"), Authorize, RequirePageAccess("/attandance/track")]
        public async Task<IActionResult> DownloadMonthlyAttendance([FromBody] AttendanceRangeGetDto request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest("Request cannot be null.");
                }

                var data = await _service.FetchPunchesRange(request.FromDate, request.ToDate, request.ECode);
                if (data != null && data.Count > 0) {
                    return Ok(new {
                        Status = true,
                        Message = "Fetched Successfully",
                        Data = data
                    });
                }
                return NotFound(new {
                    Status = false,
                    Message = "No Data Found",
                    Data = new { }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(400, $"Error : {ex.Message}");
            }
        }
        [HttpGet("GetEmployeeAttendanceDetails"), Authorize, RequirePageAccess("/emp-attandance-list")]
        public async Task<IActionResult> GetEmployeeAttendanceDetails(
            int pageNumber = 1,
            int pageSize = 10,
            string? searchTerm = null,
            string mode = "all",
            string? managerId = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int? month = null,
            int? year = null)
        {
            try
            {
                var (employees, total, currentPage, active, inactive, abscond, loc) = await _service.GetEmployeeAttendanceDetailsAsync(
                    pageNumber, pageSize, searchTerm, mode, managerId, fromDate, toDate, month, year);

                return Ok(new
                {
                    Status = true,
                    Message = "Fetched Successfully",
                    Employees = employees,
                    TotalCount = total,
                    CurrentPageNumber = currentPage,
                    Cards = new
                    {
                        ActiveCount = active,
                        InactiveCount = inactive,
                        AbscondCount = abscond,
                        LocCount = loc
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Status = false, Message = ex.Message });
            }
        }
        // Self-service list: returns only the caller's own regularize requests (scoped via JWT EmployeeId).
        // No RequirePageAccess gate — see comment on POST regularization above.
        [HttpGet("GetRegularizationRequestsself"), Authorize]
        public async Task<IActionResult> GetRegularizationRequestsself()
        {
            var identity = HttpContext.User.Identity as ClaimsIdentity;

            var loginDetail = AuthenticUserDetails.GetCurrentUserDetails(identity);

            long employeeid = Convert.ToInt64(loginDetail.EmployeeId);

            var data = await _service.GetRegularizationRequestsSelfAsync(employeeid);
            return Ok(new
            {
                Status = true,
                Message = "Data fetched successfully",
                Data = data
            });
        }


        // Employee punch-in/out — used by every user from their own profile,
        // not from the /Geo-fence admin page. Auth required, no page gate.
        [HttpPost("GeoLocationAttendance"), Authorize]
        public async Task<IActionResult> GeoLocationAttendancePunch([FromForm] PunchDto dto)
        {
            try
            {
                //var rec = await _service.GeoLocationAttendance(dto.EmployeeCode, dto.Type, dto.Lat, dto.Lon, dto.Device, HttpContext.Connection.RemoteIpAddress?.ToString(), dto.Address);
                var rec = await _service.GeoLocationAttendanceWithProc(dto.EmployeeCode, dto.Type, dto.Lat, dto.Lon, dto.Device, HttpContext.Connection.RemoteIpAddress?.ToString(), dto.Address, dto.Proof);
                return Ok(new
                {
                    success = true,
                    rec.Id,
                    rec.PunchType,
                    rec.PunchTimeUtc,
                    rec.WithinGeofence,
                    rec.Latitude,
                    rec.Longitude,
                    rec.EmployeeId,
                    rec.Address,
                    rec.ProofPath
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

       [HttpGet("daily-summary-geo/{managerId:long}"), Authorize, RequirePageAccess("/geofence-request")]
        public async Task<IActionResult> GetDailyAttendanceSummaryGeo(
         long managerId,
         int statusId = 0,
         int pageNumber = 1,
         int pageSize = 10,
         string? searchTerm = null,
         string timeZoneId = "UTC",
         CancellationToken ct = default)
        {
            var identity = HttpContext.User.Identity as ClaimsIdentity;
            var loginDetail = AuthenticUserDetails.GetCurrentUserDetails(identity);
            string role = loginDetail.role;

            var result = await _service.GetDailyAttendanceSummaryGeoAsync(
                managerId, role, statusId, pageNumber, pageSize, searchTerm, timeZoneId, ct);

            return Ok(new
            {
                Status = true,
                Message = "Data fetched successfully",
                Data = result.Data,
                TotalRecords = result.TotalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize
            });
        }
        [HttpPost("geo/attendance/status/{managerId:long}"), Authorize, RequirePageAccess("/geofence-request")]
        public async Task<IActionResult> SetGeoAttendanceStatus(
       long managerId,
       [FromBody] SetGeoAttendanceStatusDto body,
       CancellationToken ct = default)
        {
            if (body is null)
                return BadRequest(new { Status = false, Message = "Request body is required." });

            var identity = HttpContext.User.Identity as ClaimsIdentity;
            var loginDetail = AuthenticUserDetails.GetCurrentUserDetails(identity);
            string role = loginDetail.role;
            string employeeId = loginDetail.EmployeeId;   // <-- the logged-in user's employeeId

            if (string.IsNullOrWhiteSpace(body.TimeZoneId))
                body.TimeZoneId = "UTC";

            AttendanceStatusChangeResult result;
            try
            {
                result = await _service.SetGeoAttendanceStatusAsync(
                    managerId: managerId,
                    role: role,
                    employeeId: body.EmployeeId,
                    punchDate: body.PunchDate.Date,
                    statusId: body.StatusId,
                    remarks : body.Remarks,
                    timeZoneId: body.TimeZoneId,
                    lastUpdatedBy: employeeId,     // <-- send logged-in employee id
                    ct: ct);
            }
            catch (SqlException ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    Status = false,
                    Message = ex.Message
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Status = false, Message = ex.Message });
            }

            return Ok(new
            {
                Status = true,
                Message = result.RowsUpdated > 0
                    ? "Attendance status updated successfully."
                    : "No records were updated (check employee, date, authorization, or current status).",
                Data = result
            });
        }

        /// <summary>
        /// SuperAdmin-only export: geofence/geo-attendance approvals for a date range,
        /// optionally filtered by finalStatus / managerStatus / masterStatus.
        /// </summary>
        [HttpGet("geo/export"), Authorize, RequirePageAccess("/geofence-request")]
        public async Task<IActionResult> ExportGeoAttendance(
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate,
            [FromQuery] string? finalStatus = null,
            [FromQuery] string? managerStatus = null,
            [FromQuery] string? masterStatus = null,
            CancellationToken ct = default)
        {
            try
            {
                var identity = HttpContext.User.Identity as ClaimsIdentity;
                var userClaims = AuthenticUserDetails.GetCurrentUserDetails(identity);

                if (userClaims == null || string.IsNullOrEmpty(userClaims.EmployeeId))
                    return Unauthorized(new { Status = false, Message = "Invalid user credentials." });

                var roleLower = (userClaims.role ?? string.Empty).Trim().ToLowerInvariant();
                var isSuperAdmin = roleLower == "superadmin"
                                   || roleLower == "it superadmin"
                                   || roleLower == "master";

                if (!isSuperAdmin)
                    return StatusCode(StatusCodes.Status403Forbidden, new
                    {
                        Status = false,
                        Message = "Only SuperAdmin can export geofence requests."
                    });

                if (startDate == default || endDate == default)
                    return BadRequest(new { Status = false, Message = "StartDate and EndDate are required." });

                if (endDate < startDate)
                    return BadRequest(new { Status = false, Message = "EndDate must be >= StartDate." });

                var bytes = await _service.ExportGeoAttendanceByRangeAsync(
                    startDate, endDate, finalStatus, managerStatus, masterStatus, ct);

                var fileName = $"GeoAttendance_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}_{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx";
                return File(bytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    fileName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Status = false, Message = $"Export failed: {ex.Message}" });
            }
        }


        #region Attendance Count Approval Endpoints

        /// <summary>
        /// Create a new attendance count approval request with file upload
        /// </summary>
        [HttpPost("attendance-count-approval"), Authorize, RequirePageAccess("/attandance/track")]
        [Authorize]
        [RequestSizeLimit(52428800)] // 50 MB limit
        public async Task<IActionResult> CreateAttendanceCountApproval([FromForm] CreateAttendanceCountApprovalWithFilesDto dto)
        {
            try
            {
                var loginDetail = AuthenticUserDetails.GetCurrentUserDetails(HttpContext.User.Identity as ClaimsIdentity);
                if (loginDetail == null)
                {
                    return Unauthorized(new { success = false, message = "User not authenticated" });
                }

                // Validate file types (optional)
                if (dto.Files != null && dto.Files.Any())
                {
                    var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".doc", ".docx", ".xls", ".xlsx" };
                    foreach (var file in dto.Files)
                    {
                        var extension = Path.GetExtension(file.FileName).ToLower();
                        if (!allowedExtensions.Contains(extension))
                        {
                            return BadRequest(new { success = false, message = $"File type {extension} is not allowed. Allowed types: {string.Join(", ", allowedExtensions)}" });
                        }
                    }
                }

                // Create the DTO for service
                var createDto = new CreateAttendanceCountApprovalDto
                {
                    ECode = dto.ECode,
                    MonthYear = dto.MonthYear,
                    AttendanceCount = dto.AttendanceCount,
                    EmployeeRemarks = dto.EmployeeRemarks,
                    Attachments = new List<AttachmentDto>()
                };

                var approvalId = await _service.CreateAttendanceCountApprovalWithFilesAsync(createDto, dto.Files, loginDetail.EmployeeId);
                
                return Ok(new 
                { 
                    success = true, 
                    message = "Attendance count approval request created successfully", 
                    approvalId = approvalId 
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "An error occurred while creating the request", error = ex.Message });
            }
        }

        /// <summary>
        /// CM (Cluster Manager) approves or rejects attendance count approval
        /// </summary>
        [HttpPost("attendance-count-approval/cm-approve"), Authorize, RequirePageAccess("/attandance/track")]
        [Authorize]
        public async Task<IActionResult> CMApproveAttendanceCount([FromBody] CMApprovalDto dto)
        {
            try
            {
                var loginDetail = AuthenticUserDetails.GetCurrentUserDetails(HttpContext.User.Identity as ClaimsIdentity);
                if (loginDetail == null)
                {
                    return Unauthorized(new { success = false, message = "User not authenticated" });
                }

                var result = await _service.CMApproveAttendanceCountAsync(dto, loginDetail.EmployeeId);
                
                if (!result)
                {
                    return NotFound(new { success = false, message = "Attendance count approval not found" });
                }

                return Ok(new 
                { 
                    success = true, 
                    message = dto.IsApproved ? "Attendance count approved by CM successfully" : "Attendance count rejected by CM"
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "An error occurred while processing CM approval", error = ex.Message });
            }
        }

        /// <summary>
        /// RM (Regional Manager) approves or rejects attendance count approval
        /// RM is upper level and can override CM's decision
        /// </summary>
        [HttpPost("attendance-count-approval/rm-approve"), Authorize, RequirePageAccess("/attandance/track")]
        [Authorize]
        public async Task<IActionResult> RMApproveAttendanceCount([FromBody] RMApprovalDto dto)
        {
            try
            {
                var loginDetail = AuthenticUserDetails.GetCurrentUserDetails(HttpContext.User.Identity as ClaimsIdentity);
                if (loginDetail == null)
                {
                    return Unauthorized(new { success = false, message = "User not authenticated" });
                }

                var result = await _service.RMApproveAttendanceCountAsync(dto, loginDetail.EmployeeId);
                
                if (!result)
                {
                    return NotFound(new { success = false, message = "Attendance count approval not found" });
                }

                return Ok(new 
                { 
                    success = true, 
                    message = dto.IsApproved ? "Attendance count approved by RM successfully" : "Attendance count rejected by RM"
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "An error occurred while processing RM approval", error = ex.Message });
            }
        }

        /// <summary>
        /// Get paginated list of attendance count approvals with filtering
        /// </summary>
        [HttpGet("attendance-count-approval"), Authorize, RequirePageAccess("/attandance/track")]
        [Authorize]
        public async Task<IActionResult> GetAttendanceCountApprovals(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? searchTerm = null,
            [FromQuery] int? statusId = null,
            [FromQuery] string? ecode = null,
            [FromQuery] string? approverRole = null)
        {
            try
            {
                var loginDetail = AuthenticUserDetails.GetCurrentUserDetails(HttpContext.User.Identity as ClaimsIdentity);
                if (loginDetail == null)
                {
                    return Unauthorized(new { success = false, message = "User not authenticated" });
                }

                var result = await _service.GetAttendanceCountApprovalsAsync(
                    pageNumber, 
                    pageSize, 
                    searchTerm, 
                    statusId, 
                    ecode, 
                    approverRole, 
                    loginDetail.EmployeeId);

                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "An error occurred while retrieving approvals", error = ex.Message });
            }
        }

        /// <summary>
        /// Get attendance count approval by ID
        /// </summary>
        [HttpGet("attendance-count-approval/{approvalId}"), Authorize, RequirePageAccess("/attandance/track")]
        [Authorize]
        public async Task<IActionResult> GetAttendanceCountApprovalById([FromRoute] long approvalId)
        {
            try
            {
                var result = await _service.GetAttendanceCountApprovalByIdAsync(approvalId);
                return Ok(new { success = true, data = result });
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "An error occurred while retrieving approval", error = ex.Message });
            }
        }

        #endregion

        #region Employee Attendance Request List

        [HttpGet("employee-attendance-requests/{employeeId}")]
        //[Authorize]
        public async Task<IActionResult> GetEmployeeAttendanceRequestList([FromRoute] long employeeId, [FromQuery] DateTime? date = null)
        {
            try
            {
                var result = await _service.GetEmployeeAttendanceRequestListAsync(employeeId, date);
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "An error occurred while retrieving attendance requests", error = ex.Message });
            }
        }

        #endregion

        #region Employee Attendance Snapshot

        /// <summary>
        /// Get employee attendance snapshot with optional filters
        /// </summary>
        /// <param name="ecode">Employee code (optional)</param>
        /// <param name="month">Month-Year in format "MMM-YY" (optional)</param>
        /// <param name="batchNo">Batch number (optional)</param>
        /// <returns>List of attendance snapshot records</returns>
        // Called by /emp-final-data page and possibly others — not exclusive
        // to /attandance/track, so don't gate to that single page.
        [HttpGet("attendance-snapshot"), Authorize]
        [Authorize]
        public async Task<IActionResult> GetEmpAttendanceSnapshot(
            [FromQuery] string? ecode = null,
            [FromQuery] string? month = null,
            [FromQuery] int? batchNo = null)
        {
            try
            {
                var result = await _service.GetEmpAttendanceSnapshotAsync(ecode, month, batchNo);
                return Ok(new { 
                    success = true, 
                    data = result,
                    count = result.Count,
                    filters = new { ecode, month, batchNo }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    success = false, 
                    message = "An error occurred while retrieving attendance snapshot", 
                    error = ex.Message 
                });
            }
        }

        #endregion

        #region Merge Monthly Punches Range

        /// <summary>
        /// Merge monthly punches range for a specific employee
        /// </summary>
        /// <param name="request">Request containing FromDate, ToDate, and Ecode (all mandatory)</param>
        /// <returns>List of merged punch records</returns>
        [HttpPost("merge-monthly-punches-range"), Authorize, RequirePageAccess("/attandance/track")]
        [Authorize]
        public async Task<IActionResult> MergeMonthlyPunchesRange([FromBody] MergeMonthlyPunchesRangeDto request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(new { 
                        success = false, 
                        message = "Request body is required." 
                    });
                }

                if (string.IsNullOrWhiteSpace(request.Ecode))
                {
                    return BadRequest(new { 
                        success = false, 
                        message = "Ecode is required and cannot be empty." 
                    });
                }

                if (request.FromDate == default)
                {
                    return BadRequest(new { 
                        success = false, 
                        message = "FromDate is required." 
                    });
                }

                if (request.ToDate == default)
                {
                    return BadRequest(new { 
                        success = false, 
                        message = "ToDate is required." 
                    });
                }

                if (request.FromDate > request.ToDate)
                {
                    return BadRequest(new { 
                        success = false, 
                        message = "FromDate cannot be greater than ToDate." 
                    });
                }

                var rowsAffected = await _service.MergeMonthlyPunchesRangeAsync(
                    request.FromDate, 
                    request.ToDate, 
                    request.Ecode);

                return Ok(new { 
                    success = true, 
                    message = "Monthly punches merged successfully.", 
                    rowsAffected = rowsAffected
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { 
                    success = false, 
                    message = ex.Message 
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    success = false, 
                    message = "An error occurred while merging monthly punches range", 
                    error = ex.Message 
                });
            }
        }

        #endregion

        #region Refresh Attendance by Ecode List

        /// <summary>
        /// Refresh attendance for multiple employees based on mode (table or machine)
        /// </summary>
        /// <param name="request">Request containing Mode, Ecodes list, FromDate, and ToDate</param>
        /// <returns>Summary of processed employees</returns>
        [HttpPost("refreshattendanceemployeebasedonecodelist")]
        [Authorize]
        public async Task<IActionResult> RefreshAttendanceEmployeeBasedOnEcodeList([FromBody] RefreshAttendanceByEcodeListDto request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(new { 
                        success = false, 
                        message = "Request body is required." 
                    });
                }

                if (string.IsNullOrWhiteSpace(request.Mode))
                {
                    return BadRequest(new { 
                        success = false, 
                        message = "Mode is required. Must be 'table' or 'machine'." 
                    });
                }

                if (request.Ecodes == null || request.Ecodes.Count == 0)
                {
                    return BadRequest(new { 
                        success = false, 
                        message = "Ecode list is required and cannot be empty." 
                    });
                }

                if (request.FromDate == default)
                {
                    return BadRequest(new { 
                        success = false, 
                        message = "FromDate is required." 
                    });
                }

                if (request.ToDate == default)
                {
                    return BadRequest(new { 
                        success = false, 
                        message = "ToDate is required." 
                    });
                }

                if (request.FromDate > request.ToDate)
                {
                    return BadRequest(new { 
                        success = false, 
                        message = "FromDate cannot be greater than ToDate." 
                    });
                }

                var mode = request.Mode.ToLower().Trim();
                if (mode != "table" && mode != "machine")
                {
                    return BadRequest(new { 
                        success = false, 
                        message = "Mode must be either 'table' or 'machine'." 
                    });
                }

                var results = new List<object>();
                var successCount = 0;
                var failureCount = 0;
                var errors = new List<string>();

                foreach (var ecode in request.Ecodes)
                {
                    if (string.IsNullOrWhiteSpace(ecode))
                    {
                        failureCount++;
                        errors.Add($"Empty ecode skipped.");
                        continue;
                    }

                    try
                    {
                        if (mode == "table")
                        {
                            // Call merge-monthly-punches-range logic
                            var rowsAffected = await _service.MergeMonthlyPunchesRangeAsync(
                                request.FromDate, 
                                request.ToDate, 
                                ecode);
                            
                            results.Add(new
                            {
                                Ecode = ecode,
                                Mode = "table",
                                Success = true,
                                RowsAffected = rowsAffected,
                                Message = $"Successfully merged monthly punches for {ecode}"
                            });
                            successCount++;
                        }
                        else if (mode == "machine")
                        {
                            // Call refreshmultipunchattendacebyecode logic
                            await _service.FetchAndSavePunchesRangeByEcodeAsync(
                                request.FromDate, 
                                request.ToDate, 
                                ecode);
                            
                            results.Add(new
                            {
                                Ecode = ecode,
                                Mode = "machine",
                                Success = true,
                                Message = $"Successfully refreshed multi-punch attendance for {ecode}"
                            });
                            successCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        failureCount++;
                        var errorMsg = $"Error processing {ecode}: {ex.Message}";
                        errors.Add(errorMsg);
                        results.Add(new
                        {
                            Ecode = ecode,
                            Mode = mode,
                            Success = false,
                            Message = errorMsg
                        });
                    }
                }

                return Ok(new { 
                    success = true, 
                    message = $"Processed {request.Ecodes.Count} employee(s). Success: {successCount}, Failed: {failureCount}",
                    totalProcessed = request.Ecodes.Count,
                    successCount = successCount,
                    failureCount = failureCount,
                    results = results,
                    errors = errors.Count > 0 ? errors : null
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { 
                    success = false, 
                    message = ex.Message 
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    success = false, 
                    message = "An error occurred while processing attendance refresh", 
                    error = ex.Message 
                });
            }
        }

        #endregion
    
    }
}

