using HRMSAPI.DTO;
using HRMSAPI.Extension;
using HRMSAPI.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace HRMSAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    [RequirePageAccess("/emp-bonus-uploader")]
    public class EcodeWiseBonusProvisioningPolicyMappingController : ControllerBase
    {
        private readonly IEcodeWiseBonusProvisioningPolicyMappingService _service;
        private readonly ILogger<EcodeWiseBonusProvisioningPolicyMappingController> _logger;

        public EcodeWiseBonusProvisioningPolicyMappingController(
            IEcodeWiseBonusProvisioningPolicyMappingService service, 
            ILogger<EcodeWiseBonusProvisioningPolicyMappingController> logger)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Get all EcodeWiseBonusProvisioningPolicyMapping records where IsActive = 1 and IsDeleted = 0
        /// </summary>
        /// <returns>List of all active EcodeWiseBonusProvisioningPolicyMapping records</returns>
        [HttpGet]
        public async Task<IActionResult> GetAllEcodeWiseBonusProvisioningPolicyMappings()
        {
            try
            {
                var result = await _service.GetAllEcodeWiseBonusProvisioningPolicyMappingsAsync();
                return StatusCode((int)result.Code, new
                {
                    Status = result.Status,
                    Message = result.Message,
                    Data = result.Data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in GetAllEcodeWiseBonusProvisioningPolicyMappings");
                return StatusCode(500, new { Status = false, Message = "An error occurred while processing the request" });
            }
        }

        /// <summary>
        /// Get all BonusProvisioningPolicyMaster records where IsActive = 1 and IsDeleted = 0
        /// </summary>
        /// <returns>List of all active BonusProvisioningPolicyMaster records</returns>
        [HttpGet("bonus-policies")]
        public async Task<IActionResult> GetAllBonusProvisioningPolicies()
        {
            try
            {
                var result = await _service.GetAllBonusProvisioningPoliciesAsync();
                return StatusCode((int)result.Code, new
                {
                    Status = result.Status,
                    Message = result.Message,
                    Data = result.Data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in GetAllBonusProvisioningPolicies");
                return StatusCode(500, new { Status = false, Message = "An error occurred while processing the request" });
            }
        }

        /// <summary>
        /// Create or update an EcodeWiseBonusProvisioningPolicyMapping record
        /// </summary>
        /// <param name="dto">EcodeWiseBonusProvisioningPolicyMapping data</param>
        /// <returns>Success status</returns>
        [HttpPost("upsert")]
        public async Task<IActionResult> UpsertEcodeWiseBonusProvisioningPolicyMapping([FromBody] EcodeWiseBonusProvisioningPolicyMappingUpsertDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Get user ID from JWT claims
                var identity = HttpContext.User.Identity as ClaimsIdentity;
                var userId = identity?.FindFirst("EmployeeId")?.Value;

                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { Status = false, Message = "User is not authenticated" });
                }

                var result = await _service.UpsertEcodeWiseBonusProvisioningPolicyMappingAsync(dto, userId);
                return StatusCode((int)result.Code, new
                {
                    Status = result.Status,
                    Message = result.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in UpsertEcodeWiseBonusProvisioningPolicyMapping");
                return StatusCode(500, new { Status = false, Message = "An error occurred while processing the request" });
            }
        }

        /// <summary>
        /// Soft delete an EcodeWiseBonusProvisioningPolicyMapping record by setting IsActive to false and IsDeleted to true
        /// </summary>
        /// <param name="deleteDto">Delete request containing the Id</param>
        /// <returns>Success status</returns>
        [HttpPost("delete")]
        public async Task<IActionResult> DeleteEcodeWiseBonusProvisioningPolicyMapping([FromBody] EcodeWiseBonusProvisioningPolicyMappingDeleteDto deleteDto)
        {
            try
            {
                if (deleteDto == null || deleteDto.Id == Guid.Empty)
                {
                    return BadRequest(new { Status = false, Message = "Valid Id is required" });
                }

                var result = await _service.DeleteEcodeWiseBonusProvisioningPolicyMappingAsync(deleteDto.Id);
                return StatusCode((int)result.Code, new
                {
                    Status = result.Status,
                    Message = result.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in DeleteEcodeWiseBonusProvisioningPolicyMapping");
                return StatusCode(500, new { Status = false, Message = "An error occurred while processing the request" });
            }
        }
    }
}

