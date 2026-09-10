using HRMSAPI.DTO;
using HRMSAPI.Extension;
using HRMSAPI.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Roomsy.DTOS.GenericsResponses;
using System.Security.Claims;
using System.Threading.Tasks;

namespace HRMSAPI.Controllers
{
    /// <summary>
    /// Role Master (Masters -> Role Master). Shaped after ShiftMasterController so
    /// the two master pages behave the same way.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [RequirePageAccess("/master/role")]
    public class RoleMasterController : ControllerBase
    {
        private readonly IRoleMasterService _roleMasterService;

        public RoleMasterController(IRoleMasterService roleMasterService)
        {
            _roleMasterService = roleMasterService;
        }

        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAllRoles()
        {
            var result = await _roleMasterService.GetAllRolesAsync();
            return StatusCode((int)result.Code, new ApiFetchAndResponse
            {
                Status = result.Status,
                Message = result.Message,
                Data = result.Data
            });
        }

        /// <summary>
        /// Active roles only. The Role dropdowns use /api/DropDown/GetRoleMaster
        /// instead, since this controller is gated on the Role Master page.
        /// </summary>
        [HttpGet("GetActive")]
        public async Task<IActionResult> GetActiveRoles()
        {
            var result = await _roleMasterService.GetActiveRolesAsync();
            return StatusCode((int)result.Code, new ApiFetchAndResponse
            {
                Status = result.Status,
                Message = result.Message,
                Data = result.Data
            });
        }

        [HttpGet("GetById/{roleId}")]
        public async Task<IActionResult> GetRoleById(int roleId)
        {
            var result = await _roleMasterService.GetRoleByIdAsync(roleId);
            return StatusCode((int)result.Code, new ApiFetchAndResponse
            {
                Status = result.Status,
                Message = result.Message,
                Data = result.Data
            });
        }

        [HttpPost("Create"), Authorize]
        public async Task<IActionResult> CreateRole([FromBody] RoleMasterUpsertDto roleDto)
        {
            var employeeId = GetCallerEmployeeId();
            if (employeeId == null)
            {
                return BadRequest(new { Status = false, Message = "Invalid user credentials." });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _roleMasterService.CreateRoleAsync(roleDto, employeeId);
            return StatusCode((int)result.Code, new ApiExecuteAndReponse
            {
                Status = result.Status,
                Message = result.Message
            });
        }

        [HttpPut("Update/{roleId}"), Authorize]
        public async Task<IActionResult> UpdateRole(int roleId, [FromBody] RoleMasterUpsertDto roleDto)
        {
            var employeeId = GetCallerEmployeeId();
            if (employeeId == null)
            {
                return BadRequest(new { Status = false, Message = "Invalid user credentials." });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _roleMasterService.UpdateRoleAsync(roleId, roleDto, employeeId);
            return StatusCode((int)result.Code, new ApiExecuteAndReponse
            {
                Status = result.Status,
                Message = result.Message
            });
        }

        /// <summary>
        /// Bulk create/update roles from an Excel sheet. Matches on role name, so a
        /// re-upload updates instead of duplicating, and never deletes anything.
        /// </summary>
        [HttpPost("BulkUpload"), Authorize]
        public async Task<IActionResult> BulkUploadRoles([FromForm] IFormFile file)
        {
            var employeeId = GetCallerEmployeeId();
            if (employeeId == null)
            {
                return BadRequest(new { Status = false, Message = "Invalid user credentials." });
            }

            var result = await _roleMasterService.BulkUploadRolesAsync(file, employeeId);
            return StatusCode((int)result.Code, new ApiFetchAndResponse
            {
                Status = result.Status,
                Message = result.Message,
                Data = result.Data
            });
        }

        /// <summary>Upload template (headers + examples).</summary>
        [HttpGet("Template")]
        public IActionResult DownloadTemplate()
        {
            var bytes = _roleMasterService.BuildUploadTemplate();
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"RoleMaster_Template_{DateTime.Now:yyyyMMdd}.xlsx");
        }

        [HttpGet("ToggleStatus/{roleId}"), Authorize]
        public async Task<IActionResult> ToggleRoleStatus(int roleId)
        {
            var employeeId = GetCallerEmployeeId();
            if (employeeId == null)
            {
                return BadRequest(new { Status = false, Message = "Invalid user credentials." });
            }

            var result = await _roleMasterService.ToggleRoleStatusAsync(roleId, employeeId);
            return StatusCode((int)result.Code, new ApiExecuteAndReponse
            {
                Status = result.Status,
                Message = result.Message
            });
        }

        /// <summary>Caller's EmployeeId from the token, or null when it is missing.</summary>
        private string? GetCallerEmployeeId()
        {
            var identity = HttpContext.User.Identity as ClaimsIdentity;
            if (identity == null) return null;

            var userClaims = AuthenticUserDetails.GetCurrentUserDetails(identity);
            return string.IsNullOrEmpty(userClaims?.EmployeeId) ? null : userClaims.EmployeeId;
        }
    }
}
