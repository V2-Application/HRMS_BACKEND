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
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [RequirePageAccess("/master/shift")]
    public class ShiftMasterController : ControllerBase
    {
        private readonly IShiftMasterService _shiftMasterService;

        public ShiftMasterController(IShiftMasterService shiftMasterService)
        {
            _shiftMasterService = shiftMasterService;
        }

        [HttpPost("Create"), Authorize]
        public async Task<IActionResult> CreateShift([FromBody] ShiftMasterUpsertDto shiftDto)
        {
            var identity = HttpContext.User.Identity as ClaimsIdentity;
            if (identity == null)
                return BadRequest("Authentication Fails");

            var userClaims = AuthenticUserDetails.GetCurrentUserDetails(identity);

            if (userClaims == null || string.IsNullOrEmpty(userClaims.EmployeeId))
            {
                return BadRequest(new
                {
                    Status = false,
                    Message = "Invalid user credentials."
                });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _shiftMasterService.CreateShiftAsync(shiftDto, userClaims.EmployeeId);
            return StatusCode((int)result.Code, new ApiExecuteAndReponse
            {
                Status = result.Status,
                Message = result.Message
            });
        }

        [HttpPut("Update/{shiftId}"), Authorize]
        public async Task<IActionResult> UpdateShift(int shiftId, [FromBody] ShiftMasterUpsertDto shiftDto)
        {
            var identity = HttpContext.User.Identity as ClaimsIdentity;
            if (identity == null)
                return BadRequest("Authentication Fails");

            var userClaims = AuthenticUserDetails.GetCurrentUserDetails(identity);

            if (userClaims == null || string.IsNullOrEmpty(userClaims.EmployeeId))
            {
                return BadRequest(new
                {
                    Status = false,
                    Message = "Invalid user credentials."
                });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _shiftMasterService.UpdateShiftAsync(shiftId, shiftDto, userClaims.EmployeeId);
            return StatusCode((int)result.Code, new ApiExecuteAndReponse
            {
                Status = result.Status,
                Message = result.Message
            });
        }

        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAllShifts()
        {
            var result = await _shiftMasterService.GetAllShiftsAsync();
            return StatusCode((int)result.Code, new ApiFetchAndResponse
            {
                Status = result.Status,
                Message = result.Message,
                Data = result.Data
            });
        }

        [HttpGet("GetById/{shiftId}")]
        public async Task<IActionResult> GetShiftById(int shiftId)
        {
            var result = await _shiftMasterService.GetShiftByIdAsync(shiftId);
            return StatusCode((int)result.Code, new ApiFetchAndResponse
            {
                Status = result.Status,
                Message = result.Message,
                Data = result.Data
            });
        }

        [HttpDelete("Delete/{shiftId}"), Authorize]
        public async Task<IActionResult> DeleteShift(int shiftId)
        {
            var identity = HttpContext.User.Identity as ClaimsIdentity;
            if (identity == null)
                return BadRequest("Authentication Fails");

            var userClaims = AuthenticUserDetails.GetCurrentUserDetails(identity);

            if (userClaims == null || string.IsNullOrEmpty(userClaims.EmployeeId))
            {
                return BadRequest(new
                {
                    Status = false,
                    Message = "Invalid user credentials."
                });
            }

            var result = await _shiftMasterService.DeleteShiftAsync(shiftId);
            return StatusCode((int)result.Code, new ApiExecuteAndReponse
            {
                Status = result.Status,
                Message = result.Message
            });
        }

        [HttpGet("ToggleStatus/{shiftId}"), Authorize]
        public async Task<IActionResult> ToggleShiftStatus(int shiftId)
        {
            var identity = HttpContext.User.Identity as ClaimsIdentity;
            if (identity == null)
                return BadRequest("Authentication Fails");

            var userClaims = AuthenticUserDetails.GetCurrentUserDetails(identity);

            if (userClaims == null || string.IsNullOrEmpty(userClaims.EmployeeId))
            {
                return BadRequest(new
                {
                    Status = false,
                    Message = "Invalid user credentials."
                });
            }

            var result = await _shiftMasterService.ToggleShiftStatusAsync(shiftId, userClaims.EmployeeId);
            return StatusCode((int)result.Code, new ApiExecuteAndReponse
            {
                Status = result.Status,
                Message = result.Message
            });
        }
    }
}

