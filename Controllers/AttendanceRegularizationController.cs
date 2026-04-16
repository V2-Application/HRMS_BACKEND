using Microsoft.AspNetCore.Mvc;
using HRMSAPI.Interfaces;
using Roomsy.DTOS.GenericsResponses;

namespace HRMSAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
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
    }
}

