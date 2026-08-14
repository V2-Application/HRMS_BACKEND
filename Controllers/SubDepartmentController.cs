using System.Security.Claims;
using HRMSAPI.DTO;
using HRMSAPI.Extension;
using HRMSAPI.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMSAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SubDepartmentController : ControllerBase
    {
        private readonly ISubDepartmentService _service;
        private readonly ILogger<SubDepartmentController> _logger;

        public SubDepartmentController(ISubDepartmentService service, ILogger<SubDepartmentController> logger)
        {
            _service = service;
            _logger = logger;
        }

        // Children at one level under one parent (department for L1, sub-dept for L2/L3).
        [HttpGet("All")]
        [RequirePageAccess("/master/sub-departments")]
        public async Task<IActionResult> GetAll(
            [FromQuery] int departmentId,
            [FromQuery] int? parentSubDepartmentId = null,
            [FromQuery] int depthLevel = 1,
            [FromQuery] bool onlyInactive = false,
            [FromQuery] string? searchTerm = null)
        {
            var result = await _service.GetAllAsync(departmentId, parentSubDepartmentId, depthLevel, onlyInactive, searchTerm);
            return StatusCode((int)result.Code, new { Status = result.Status, Message = result.Message, Data = result.Data });
        }

        // Unrestricted (any authenticated user) read for cascading dropdowns in other modules'
        // forms — e.g. Vendor Manpower onboarding — where the caller doesn't have access to the
        // Sub-Department master page itself. Always returns active-only.
        [HttpGet("Dropdown")]
        public async Task<IActionResult> GetDropdown(
            [FromQuery] int departmentId,
            [FromQuery] int? parentSubDepartmentId = null,
            [FromQuery] int depthLevel = 1)
        {
            var result = await _service.GetAllAsync(departmentId, parentSubDepartmentId, depthLevel, onlyInactive: false, searchTerm: null);
            return StatusCode((int)result.Code, new { Status = result.Status, Message = result.Message, Data = result.Data });
        }

        [HttpPost("Upsert")]
        [RequirePageAccess("/master/sub-departments")]
        public async Task<IActionResult> Upsert([FromBody] SubDepartmentUpsertDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var identity = HttpContext.User.Identity as ClaimsIdentity;
            var userClaims = AuthenticUserDetails.GetCurrentUserDetails(identity);
            if (userClaims == null || !long.TryParse(userClaims.EmployeeId, out var empId))
                return BadRequest(new { Status = false, Message = "Invalid user credentials." });

            var result = await _service.UpsertAsync(dto, empId);
            return StatusCode((int)result.Code, new { Status = result.Status, Message = result.Message });
        }

        [HttpPut("ToggleActive")]
        [RequirePageAccess("/master/sub-departments")]
        public async Task<IActionResult> ToggleActive([FromBody] ToggleActiveStatusDto dto)
        {
            var identity = HttpContext.User.Identity as ClaimsIdentity;
            var userClaims = AuthenticUserDetails.GetCurrentUserDetails(identity);
            if (userClaims == null || !long.TryParse(userClaims.EmployeeId, out var empId))
                return BadRequest(new { Status = false, Message = "Invalid user credentials." });

            var result = await _service.ToggleActiveAsync(dto.Id, dto.IsActive, empId);
            return StatusCode((int)result.Code, new { Status = result.Status, Message = result.Message });
        }

        [HttpPost("Upload")]
        [RequirePageAccess("/master/sub-departments")]
        public async Task<IActionResult> Upload([FromForm] IFormFile file)
        {
            var identity = HttpContext.User.Identity as ClaimsIdentity;
            var userClaims = AuthenticUserDetails.GetCurrentUserDetails(identity);
            if (userClaims == null || !long.TryParse(userClaims.EmployeeId, out var empId))
                return BadRequest(new { Status = false, Message = "Invalid user credentials." });

            var allowed = new[] { ".xlsx", ".xls" };
            var ext = System.IO.Path.GetExtension(file?.FileName ?? "").ToLowerInvariant();
            if (file == null || !Array.Exists(allowed, e => e == ext))
                return BadRequest(new { Status = false, Message = "Only Excel files (.xlsx, .xls) are allowed." });

            var result = await _service.BulkUploadAsync(file, empId);
            return StatusCode((int)result.Code, new { Status = result.Status, Message = result.Message, Data = result.Data });
        }
    }
}
