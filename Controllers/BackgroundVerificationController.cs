using ASN.Controllers;
using HRMSAPI.DTO;
using HRMSAPI.Extension;
using HRMSAPI.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace HRMSAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    [RequirePageAccess("/BGV")]
    public class BackgroundVerificationController : ControllerBase
    {
        private readonly IBackgroundVerificationService _service;
        private readonly ILogger<BackgroundVerificationController> _logger;

        public BackgroundVerificationController(IBackgroundVerificationService backgroundVerificationService, ILogger<BackgroundVerificationController> logger)
        {
            _service = backgroundVerificationService;
            _logger = logger;
        }

        [HttpGet("GetBgvCandidateList")]
        public async Task<IActionResult> GetBgvCandidateList([FromQuery] int status = 4)
        {
            try
            {
                var identity = HttpContext.User.Identity as ClaimsIdentity;
                var loginDetail = AuthenticUserDetails.GetCurrentUserDetails(identity);
                var role = loginDetail.role;
                var empId = long.Parse(loginDetail.EmployeeId);
                if (role == "Audit")
                {
                    var response = await _service.GetBgvListAudit(empId, status);
                    return Ok(new { Status = true, Message = "Success", Data = response });
                }
                else
                {
                    var response = await _service.GetBgvList(status);
                    return Ok(new { Status = true, Message = "Success", Data = response });
                }

            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    Status = false,
                    Message = ex.Message ?? "Error fetching candidate list."
                });
            }
        }

        [HttpGet("GetEmployeesWithAuditRole")]
        public async Task<IActionResult> GetEmployeesWithAuditRole()
        {
            try
            {
                var response = await _service.GetAuditEmployees();
                return Ok(new { Status = true, Message = "Success", Data = response });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    Status = false,
                    Message = ex.Message ?? "Error fetching candidate list."
                });
            }
        }

        [HttpPost("AssignAuditor")]
        public async Task<IActionResult> AssignAuditorToBackgroundVerification([FromBody] AssignAuditorDTO request)
        {
            try
            {
                if (request.AuditorId == 0) return BadRequest(new { Status = false, Message = "Auditor cannot be null." });
                if (request.CandidateId == 0) return BadRequest(new { Status = false, Message = "Candidate Id cannot be null." });

                var response = await _service.AssignAuditor(request);
                return Ok(new { Status = response.Status, Message = response.Message, Data = response.Data });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = false, Message = ex.Message ?? "Failed to assign auditor." });
            }
        }

        [HttpPost("AuditorFeedback")]
        public async Task<IActionResult> AuditorFeedback([FromBody] AuditorBgvFeedbackDTO request)
        {
            try
            {
                var response = await _service.AuditorFeedback(request);
                return Ok(new { Status = response.Status, Message = response.Message, Data = response.Data });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = false, Message = ex.Message ?? "Failed to update feedback." });
            }
        }

        [HttpGet("FetchBgvCandidateDetails/{bgvid}")]
        public async Task<IActionResult> FetchBgvCandidateDetails(long bgvid)
        {
            try
            {
                if(bgvid <= 0)
                {
                    return BadRequest("Invalid Id");
                }
                var details = await _service.GetBgvCandidateDetails(bgvid);
                return Ok(new { Status = true, Message = "Candidate details fetched successfully.", Data = details });
            }
            catch(Exception ex)
            {
                return BadRequest(new { Status = false, Message = ex.Message ?? "Failed to update feedback." });
            }
        }
    }
}
