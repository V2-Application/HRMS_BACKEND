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
    [RequirePageAccess("/employee-logs")]
    public class EmployeeChangeLogController : ControllerBase
    {
        private readonly IEmployeeChangeLogService _employeeChangeLogService;
        private readonly ILogger<EmployeeChangeLogController> _logger;

        public EmployeeChangeLogController(
            IEmployeeChangeLogService employeeChangeLogService,
            ILogger<EmployeeChangeLogController> logger)
        {
            _employeeChangeLogService = employeeChangeLogService;
            _logger = logger;
        }

        [HttpGet("GetEmployeeChangeLog")]
        public async Task<IActionResult> GetEmployeeChangeLog([FromQuery] string ecode)
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

                var result = await _employeeChangeLogService.GetEmployeeChangeLogAsync(ecode);

                return Ok(new
                {
                    Status = true,
                    Message = "Employee change log fetched successfully.",
                    Code = HttpStatusCode.OK,
                    Data = result,
                    Count = result.Count
                });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid argument for GetEmployeeChangeLog: {Ecode}", ecode);
                return BadRequest(new
                {
                    Status = false,
                    Message = ex.Message,
                    Code = HttpStatusCode.BadRequest
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching employee change log for Ecode: {Ecode}", ecode);
                return StatusCode((int)HttpStatusCode.InternalServerError, new
                {
                    Status = false,
                    Message = "An error occurred while fetching employee change log.",
                    Code = HttpStatusCode.InternalServerError,
                    Error = ex.Message
                });
            }
        }
    }
}

