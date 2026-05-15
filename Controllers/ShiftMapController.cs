
using HRMSAPI.DTO;
using HRMSAPI.Extension;
using HRMSAPI.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;

[Route("api/[controller]")]
[ApiController]
public class ShiftMapController : ControllerBase
{
    private readonly IShiftMapService _shiftMapService;

    public ShiftMapController(IShiftMapService shiftMapService)
    {
        _shiftMapService = shiftMapService ?? throw new ArgumentNullException(nameof(shiftMapService));
    }

    [HttpPost("upload")]
    public async Task<IActionResult> UploadShiftMap([FromForm] IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { Message = "No file uploaded" });

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension != ".xlsx" && extension != ".xls")
                return BadRequest(new { Message = "Only Excel files (.xlsx, .xls) are allowed" });
            var identity = HttpContext.User.Identity as ClaimsIdentity;
            var loginDetail = AuthenticUserDetails.GetCurrentUserDetails(identity);
            var (success, message) = await _shiftMapService.UploadShiftMapDataAsync(file, loginDetail.EmployeeId.ToString());

            if (success)
                return Ok(new { Status = true, Message = message });
            else
                return BadRequest(new { Status = false, Message = message });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error uploading shift map data: {ex.Message}");
            return StatusCode(500, new { Message = "An error occurred while uploading shift map data." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetShiftMapRecords(
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? ecode = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            var (records, totalRecords) = await _shiftMapService.GetShiftMapRecordsAsync(searchTerm, ecode, page, pageSize);
            return Ok(new { Records = records, TotalRecords = totalRecords });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error retrieving shift map records: {ex.Message}");
            return StatusCode(500, new { Message = "An error occurred while retrieving shift map records." });
        }
    }

    [HttpGet("employee-shift-history")]
    public async Task<IActionResult> GetEmployeeShiftAndHistory(
        [FromQuery] int? employeeId = null,
        [FromQuery] string? ecode = null)
    {
        try
        {
            if (employeeId == null && string.IsNullOrWhiteSpace(ecode))
            {
                return BadRequest(new { Status = false, Message = "Either employeeId or ecode must be provided." });
            }

            var result = await _shiftMapService.GetEmployeeShiftAndHistoryAsync(employeeId, ecode);
            return Ok(new { Status = true, Data = result });
        }
        catch (InvalidOperationException ex)
        {
            // Handle stored procedure errors (e.g., employee not found)
            return BadRequest(new { Status = false, Message = ex.Message });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error retrieving employee shift and history: {ex.Message}");
            return StatusCode(500, new { Status = false, Message = "An error occurred while retrieving employee shift and history." });
        }
    }

    [HttpPost("assign-shift-bulk"), Authorize]
    public async Task<IActionResult> BulkAssignEmployeeShift([FromForm] BulkAssignShiftRequest request)
    {
        try
        {
            if (request == null)
                return BadRequest(new { Status = false, Message = "Request body is required." });
            if (request.ShiftId <= 0)
                return BadRequest(new { Status = false, Message = "ShiftId is required." });
            if (request.EffectiveFrom == default)
                return BadRequest(new { Status = false, Message = "EffectiveFrom is required." });

            var identity = HttpContext.User.Identity as ClaimsIdentity;
            if (identity == null)
                return Unauthorized(new { Status = false, Message = "Authentication required." });

            var loginDetail = AuthenticUserDetails.GetCurrentUserDetails(identity);
            if (string.IsNullOrWhiteSpace(request.AssignedBy))
                request.AssignedBy = loginDetail?.EmployeeId ?? "System";

            var result = await _shiftMapService.BulkAssignEmployeeShiftAsync(request);

            return Ok(new
            {
                Status = true,
                Message = $"Processed {result.Processed} of {result.TotalSubmitted}. " +
                          $"Already on shift: {result.AlreadyOnShift}. " +
                          $"Not found: {result.NotFoundEcodes.Count}. Errors: {result.Errors.Count}.",
                Data = result
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Status = false, Message = ex.Message });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in bulk assign shift: {ex.Message}");
            return StatusCode(500, new { Status = false, Message = $"An error occurred: {ex.Message}" });
        }
    }

    [HttpPost("assign-shift"), Authorize]
    public async Task<IActionResult> AssignEmployeeShift([FromBody] AssignEmployeeShiftRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { Status = false, Message = "Invalid request data.", Errors = ModelState });
            }

            var identity = HttpContext.User.Identity as ClaimsIdentity;
            if (identity == null)
            {
                return Unauthorized(new { Status = false, Message = "Authentication required." });
            }

            var loginDetail = AuthenticUserDetails.GetCurrentUserDetails(identity);
            
            // Set AssignedBy from authenticated user if not provided
            if (string.IsNullOrWhiteSpace(request.AssignedBy))
            {
                request.AssignedBy = loginDetail?.EmployeeId ?? "System";
            }

            var (success, message) = await _shiftMapService.AssignEmployeeShiftAsync(request);

            if (success)
            {
                return Ok(new { Status = true, Message = message });
            }
            else
            {
                return BadRequest(new { Status = false, Message = message });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error assigning employee shift: {ex.Message}");
            return StatusCode(500, new { Status = false, Message = "An error occurred while assigning employee shift." });
        }
    }

   
}
