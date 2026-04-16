using HRMSAPI.Data;
using HRMSAPI.DTO;
using HRMSAPI.Extension;
using HRMSAPI.Implementation;
using HRMSAPI.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace HRMSAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LeaveController : ControllerBase
    {
        public readonly HRMSContext _context;
        private readonly string savePath = Path.Combine("wwwroot");
        private readonly ILeaveService _uow;

        public LeaveController(HRMSContext context, ILeaveService uow)
        {
            _uow = uow;
            _context = context;
        }
        [HttpPost("ApplyLeave")]
        public async Task<IActionResult> Post([FromBody] LeaveRequestDto DtoObject)
        {
            var identity = HttpContext.User.Identity as ClaimsIdentity;
            if (identity == null)
            {
                return BadRequest(new
                {
                    Status = false,
                    Message = "Authentication failed",
                    Data = (object)null
                });
            }

            try
            {
                var result = await _uow.LeaveRequest(DtoObject);
                return Ok(new
                {
                    Status = true,
                    Message = "Leave request processed successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    Status = false,
                    Message = ex.Message,
                    Data = (object)null
                });
            }
        }
        [HttpGet("GetLeave/{id}")]
        public async Task<IActionResult> Get(long id)
        {
            var identity = HttpContext.User.Identity as ClaimsIdentity;
            if (identity == null)
                return BadRequest("Authentication Fails");

            var result = await _uow.GetList(id);
            if (result == null)
                return NotFound("Leave record not found");

            return Ok(result);
        }


        [HttpGet("GetEmployeeLeaveBalance/{employeeId}")]
        public async Task<IActionResult> GetEmployeeLeaveBalance(long employeeId)
        {
            var identity = HttpContext.User.Identity as ClaimsIdentity;
            if (identity == null)
            {
                return BadRequest(new
                {
                    Status = false,
                    Message = "Authentication failed",
                    Data = (object)null
                });
            }

            try
            {
                var result = await _uow.GetEmployeeLeaveBalanceAsync(employeeId);
                return Ok(new
                {
                    Status = true,
                    Message = "Leave balances fetched successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    Status = false,
                    Message = ex.Message,
                    Data = (object)null
                });
            }
        }

        [HttpGet("LeaveRequestsformanager"), Authorize]
        public async Task<IActionResult> GetLeaveRequests(
    
     int statusId = 0,
     int pageNumber = 1,
     int pageSize = 10,
     string? searchTerm = null)
        {
            var identity = HttpContext.User.Identity as ClaimsIdentity;
            var loginDetail = AuthenticUserDetails.GetCurrentUserDetails(identity);
            string role = loginDetail.role;
            long managerId = Convert.ToInt64(loginDetail.EmployeeId); 
            var data = await _uow.GetLeaveRequestsAsync(managerId, role, statusId, pageNumber, pageSize, searchTerm);
            return Ok(new
            {
                Status = true,
                Message = "Leave requests fetched successfully",
                Data = data.Data,
                TotalRecords = data.TotalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize
            });
        }
    
    [HttpPost("UpdateLeaveRequestStatus/{requestId}"), Authorize]
        public async Task<IActionResult> UpdateLeaveRequestStatus(long requestId, [FromBody] UpdateLeaveRequestDto updateDto)
        {
            var userIdentity = User.Identity as ClaimsIdentity;
            if (userIdentity == null || !userIdentity.IsAuthenticated)
                return Unauthorized(new { Status = false, Message = "User is not authenticated" });

            var updatedBy = userIdentity.FindFirst("EmployeeId")?.Value;
            if (string.IsNullOrEmpty(updatedBy))
                return BadRequest(new { Status = false, Message = "Unable to determine user identity from token" });

            try
            {
                var result = await _uow.UpdateLeaveRequestStatusAsync(requestId, updateDto, updatedBy);

                if (!result)
                {
                    return NotFound(new
                    {
                        status = "error",
                        message = "Leave request not found or could not be updated",
                        data = (object)null
                    });
                }

                return Ok(new
                {
                    status = "success",
                    message = "Leave request updated successfully",
                    data = new
                    {
                        requestId = requestId,
                        statusId = updateDto.StatusId
                    }
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new
                {
                    status = "error",
                    message = ex.Message,
                    data = (object)null
                });
            }
            catch (Exception ex)
            {
                // Log error details here if not already logged in service
                return StatusCode(500, new
                {
                    status = "error",
                    message = "An error occurred while updating the leave request",
                    data = ex.Message
                });
            }



        

        }


        [HttpGet("GetEmployeeLeaveBalanceById/{employeeId}")]
        public async Task<IActionResult> GetEmployeeLeaveBalanceById(long employeeId)
        {
            var leaveBalance = await _uow.GetEmployeeLeaveBalanceById(employeeId);
            if (leaveBalance == null)
            {
                return NotFound("No leave balance found for the given employee.");
            }
            return Ok(leaveBalance);
        }

    }
}