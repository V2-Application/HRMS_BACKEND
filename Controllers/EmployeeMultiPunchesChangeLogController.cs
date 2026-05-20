using HRMSAPI.Extension;
using HRMSAPI.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace HRMSAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    [RequirePageAccess("/attendance-logs")]
    public class EmployeeMultiPunchesChangeLogController : ControllerBase
    {
        private readonly IEmployeeMultiPunchesChangeLogService _employeeMultiPunchesChangeLogService;
        private readonly ILogger<EmployeeMultiPunchesChangeLogController> _logger;

        public EmployeeMultiPunchesChangeLogController(
            IEmployeeMultiPunchesChangeLogService employeeMultiPunchesChangeLogService,
            ILogger<EmployeeMultiPunchesChangeLogController> logger)
        {
            _employeeMultiPunchesChangeLogService = employeeMultiPunchesChangeLogService;
            _logger = logger;
        }

        [HttpGet("GetEmployeeMultiPunchesChangeLog")]      
        public async Task<IActionResult> GetEmployeeMultiPunchesChangeLog([FromQuery] string ecode, [FromQuery] string month)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ecode))
                {
                    return BadRequest(new
                    {
                        Status = false,
                        Message = "Ecode is required.",
                        Code = HttpStatusCode.BadRequest
                    });
                }

                if (string.IsNullOrWhiteSpace(month))
                {
                    return BadRequest(new
                    {
                        Status = false,
                        Message = "Month is required.",
                        Code = HttpStatusCode.BadRequest
                    });
                }

                var result = await _employeeMultiPunchesChangeLogService.GetEmployeeMultiPunchesChangeLogAsync(ecode, month);

                return Ok(new
                {
                    Status = true,
                    Message = "Employee multi punches change log fetched successfully.",
                    Code = HttpStatusCode.OK,
                    Data = result,
                    Count = result.Count
                });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid argument for GetEmployeeMultiPunchesChangeLog: Ecode={Ecode}, Month={Month}", ecode, month);
                return BadRequest(new
                {
                    Status = false,
                    Message = ex.Message,
                    Code = HttpStatusCode.BadRequest
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching employee multi punches change log for Ecode: {Ecode}, Month: {Month}", ecode, month);
                return StatusCode((int)HttpStatusCode.InternalServerError, new
                {
                    Status = false,
                    Message = "An error occurred while fetching employee multi punches change log.",
                    Code = HttpStatusCode.InternalServerError,
                    Error = ex.Message
                });
            }
        }
    }
}

