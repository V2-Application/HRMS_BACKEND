using HRMSAPI.DTO;
using HRMSAPI.Extension;
using HRMSAPI.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace HRMSAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RetentionBonusController : ControllerBase
    {
        private readonly IRetentionService _retentionService;
        private readonly ILogger<RetentionBonusController> _logger;

        public RetentionBonusController(IRetentionService retentionService, ILogger<RetentionBonusController> logger)
        {
            _retentionService = retentionService ?? throw new ArgumentNullException(nameof(retentionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpPost]
        public async Task<IActionResult> CreateRetentionBonus([FromBody] RetentionBonusRequestDto request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var identity = HttpContext.User.Identity as ClaimsIdentity;
                var loginDetail = AuthenticUserDetails.GetCurrentUserDetails(identity);
                var userId = loginDetail?.EmployeeId;

                var result = await _retentionService.CreateRetentionBonusAsync(request, userId);
                return StatusCode((int)result.Code, new
                {
                    Status = result.Status,
                    Message = result.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while creating retention bonus.");
                return StatusCode(500, new { Status = false, Message = "An error occurred while processing the request." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetRetentionBonuses([FromQuery] string ecode)
        {
            try
            {
                var result = await _retentionService.GetRetentionBonusesAsync(ecode);
                return StatusCode((int)result.Code, new
                {
                    Status = result.Status,
                    Message = result.Message,
                    Data = result.Data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching retention bonuses.");
                return StatusCode(500, new { Status = false, Message = "An error occurred while processing the request." });
            }
        }

        [HttpPost("status")]
        public async Task<IActionResult> UpdateRetentionBonusStatus([FromBody] RetentionBonusStatusUpdateDto request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var identity = HttpContext.User.Identity as ClaimsIdentity;
                var loginDetail = AuthenticUserDetails.GetCurrentUserDetails(identity);
                var userId = loginDetail?.EmployeeId;

                var result = await _retentionService.UpdateRetentionBonusStatusAsync(request, userId);
                return StatusCode((int)result.Code, new
                {
                    Status = result.Status,
                    Message = result.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while updating retention bonus status.");
                return StatusCode(500, new { Status = false, Message = "An error occurred while processing the request." });
            }
        }
    }
}

