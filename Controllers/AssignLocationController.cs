using HRMSAPI.DTO;
using HRMSAPI.Extension;
using HRMSAPI.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Roomsy.DTOS.GenericsResponses;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

[Route("api/[controller]")]
[ApiController]
[Authorize]
[RequirePageAccess("/emp-store-assignment")]
public class AssignLocationController : ControllerBase
{
    private readonly IAssignLocationService _assignLocationService;

    public AssignLocationController(IAssignLocationService assignLocationService)
    {
        _assignLocationService = assignLocationService ?? throw new ArgumentNullException(nameof(assignLocationService));
    }

    [HttpPost]
    public async Task<IActionResult> CreateLocationAssignment([FromBody] List<AssignLocationsDto> assignLocations, [FromHeader(Name = "X-Created-By")] string createdBy)
    {
        try
        {
            var result = await _assignLocationService.CreateLocationAssignmentAsync(assignLocations, createdBy);
            return Ok(new { Success = result, Message = "Location assignments created successfully." });
        }
        
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { Success = false, Message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(500, new { Success = false, Message = ex.Message });
        }
    }

    [HttpGet("GetLocationAssignments")]
    public async Task<IActionResult> GetLocationAssignments(long? employeeId = null, bool isHR = false,[FromQuery] bool activeOnly = false)
    {
        try
        {
            var identity = HttpContext.User.Identity as ClaimsIdentity;
            var loginDetail = AuthenticUserDetails.GetCurrentUserDetails(identity);

            var result = await _assignLocationService.GetLocationAssignmentsAsync(loginDetail, activeOnly, employeeId, isHR);

            if (result == null || !result.Any())
            {
                return Ok(new
                {
                    Success = true,
                    Message = "No location assignments found.",
                    Data = new List<AssignLocationsDto>()
                });
            }

            return Ok(new
            {
                Success = true,
                Message = $"Found {result.Count} location assignment(s).",
                Data = result
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                Success = false,
                Message = ex.Message,
                Data = (object)null
            });
        }
        catch (ApplicationException ex)
        {
            return StatusCode(500, new
            {
                Success = false,
                Message = ex.Message,
                Data = (object)null
            });
        }
        catch (Exception ex)
        {
            // Log unexpected exceptions
            // _logger.LogError(ex, "Unexpected error in GetLocationAssignments for EmployeeId: {EmployeeId}, ActiveOnly: {ActiveOnly}", employeeId, activeOnly);

            return StatusCode(500, new
            {
                Success = false,
                Message = "An unexpected error occurred while retrieving location assignments.",
                Data = (object)null
            });
        }
    }
    [HttpPost("Approveassignlocation")]
    public async Task<IActionResult> ApproveLocationAssignment([FromBody] AssignLocationApprovalDto approvalDto, [FromHeader(Name = "X-Updated-By")] string updatedBy)
    {
        try
        {
            var result = await _assignLocationService.ApproveLocationAssignmentAsync(approvalDto, updatedBy);
            return Ok(new ApiFetchAndResponse
            {
                Status = result,
                Message = result ? "Location assignment approval updated successfully." : "Failed to update location assignment approval.",
                Data = new { AssignLocationHistoryId = approvalDto.AssignLocationHistoryId }
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiFetchAndResponse
            {
                Status = false,
                Message = ex.Message,
                Data = null
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiFetchAndResponse
            {
                Status = false,
                Message = ex.Message,
                Data = null
            });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(500, new ApiFetchAndResponse
            {
                Status = false,
                Message = ex.Message,
                Data = null
            });
        }
    }

}