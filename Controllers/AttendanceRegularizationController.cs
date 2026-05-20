using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HRMSAPI.Interfaces;
using Roomsy.DTOS.GenericsResponses;
using HRMSAPI.Extension;
using System.Security.Claims;

namespace HRMSAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [RequirePageAccess("/attendance-regularization")]
    public class AttendanceRegularizationController : ControllerBase
    {
        private readonly IAttendanceRegularizationService _attendanceRegularizationService;

        public AttendanceRegularizationController(IAttendanceRegularizationService attendanceRegularizationService)
        {
            _attendanceRegularizationService = attendanceRegularizationService;
        }

        /// <summary>
        /// Get Attendance Regularization data for a specific month-year
        /// </summary>
        /// <param name="monthYear">Month-Year in format MMM-YY (e.g., Nov-25)</param>
        /// <param name="asExcel">Export as Excel file (default: false)</param>
        /// <returns>Attendance Regularization data as JSON or Excel file</returns>
        [HttpGet("GetAttendanceRegularization")]
        public async Task<IActionResult> GetAttendanceRegularization(
            [FromQuery] string monthYear,
            [FromQuery] bool asExcel = false)
        {
            try
            {
                var result = await _attendanceRegularizationService.GetAttendanceRegularizationAsync(monthYear, asExcel);

                if (asExcel && result.Status == true && result.Data is byte[] bytes)
                {
                    var fileName = $"AttendanceRegularization_{monthYear}_{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx";
                    return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
                }

                return StatusCode((int)result.Code, new FetchAndResponse
                {
                    Status = result.Status,
                    Message = result.Message,
                    Data = result.Data
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new FetchAndResponse
                {
                    Status = false,
                    Message = "An error occurred while processing the request",
                    Data = null
                });
            }
        }

        /// <summary>
        /// SuperAdmin-only export: regularize requests for a date range, optionally filtered by status.
        /// Combine managerStatus=Approved + lpStatus=Pending for "Approved by Manager, Pending by LP".
        /// </summary>
        [HttpGet("ExportAttendanceRegularization")]
        public async Task<IActionResult> ExportAttendanceRegularization(
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate,
            [FromQuery] string? status = null,
            [FromQuery] string? managerStatus = null,
            [FromQuery] string? lpStatus = null)
        {
            try
            {
                var identity = HttpContext.User.Identity as ClaimsIdentity;
                var userClaims = AuthenticUserDetails.GetCurrentUserDetails(identity);

                if (userClaims == null || string.IsNullOrEmpty(userClaims.EmployeeId))
                {
                    return Unauthorized(new FetchAndResponse
                    {
                        Status = false,
                        Message = "Invalid user credentials.",
                        Data = null
                    });
                }

                var roleLower = (userClaims.role ?? string.Empty).Trim().ToLowerInvariant();
                var isSuperAdmin = roleLower == "superadmin"
                                   || roleLower == "it superadmin"
                                   || roleLower == "master";

                if (!isSuperAdmin)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new FetchAndResponse
                    {
                        Status = false,
                        Message = "Only SuperAdmin can export regularize requests.",
                        Data = null
                    });
                }

                var result = await _attendanceRegularizationService.ExportAttendanceRegularizationByRangeAsync(
                    startDate, endDate, status, managerStatus, lpStatus);

                if (result.Status == true && result.Data is byte[] bytes)
                {
                    var fileName = $"AttendanceRegularization_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}_{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx";
                    return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
                }

                return StatusCode((int)result.Code, new FetchAndResponse
                {
                    Status = result.Status,
                    Message = result.Message,
                    Data = result.Data
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new FetchAndResponse
                {
                    Status = false,
                    Message = "An error occurred while processing the request",
                    Data = null
                });
            }
        }
    }
}

