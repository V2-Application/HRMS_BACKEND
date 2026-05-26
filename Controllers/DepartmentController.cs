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
    public class DepartmentController : ControllerBase
    {
        private readonly IDepartmentService _service;
        private readonly ILogger<DepartmentController> _logger;

        public DepartmentController(IDepartmentService service, ILogger<DepartmentController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet("All")]
        [RequirePageAccess("/master/departments")]
        public async Task<IActionResult> GetAll([FromQuery] bool onlyInactive = false, [FromQuery] string? searchTerm = null)
        {
            var result = await _service.GetAllAsync(onlyInactive, searchTerm);
            return StatusCode((int)result.Code, new { Status = result.Status, Message = result.Message, Data = result.Data });
        }

        [HttpPost("Upsert")]
        [RequirePageAccess("/master/departments")]
        public async Task<IActionResult> Upsert([FromBody] DepartmentUpsertDto dto)
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
        [RequirePageAccess("/master/departments")]
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
        [RequirePageAccess("/master/departments")]
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
