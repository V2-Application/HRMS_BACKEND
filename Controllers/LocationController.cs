using HRMSAPI.DTO;
using HRMSAPI.Extension;
using HRMSAPI.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Roomsy.DTOS.GenericsResponses;
using System.Security.Claims;
using System.Threading.Tasks;

namespace HRMSAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LocationController : ControllerBase
    {
        private readonly ILocationService _locationService;
        public LocationController(ILocationService locationService)
        {
            _locationService = locationService;
        }
        [HttpPost("UploadLocationsExcel"), Authorize]
        public async Task<IActionResult> UploadLocationsExcel([FromForm] IFormFile file)
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
            else
            {
                var result = await _locationService.UploadLocationsExcelAsync(file,userClaims.EmployeeId);
                return StatusCode((int)result.Code, new ApiExecuteAndReponse
                {
                    Status = result.Status,
                    Message = result.Message
                });
            }
        }

        [HttpGet("Delete/{locationId}"), Authorize]
        public async Task<IActionResult> SoftDeleteLocation(int locationId)
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

            var result = await _locationService.SoftDeleteLocationAsync(locationId, userClaims.EmployeeId);
            return StatusCode((int)result.Code, new ApiExecuteAndReponse
            {
                Status = result.Status,
                Message = result.Message
            });
        }

        [HttpGet("ToggleStatus/{locationId}"), Authorize]
        public async Task<IActionResult> ToggleStatus(int locationId)
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

            var result = await _locationService.ToggleLocationStatusAsync(locationId, userClaims.EmployeeId);
            return StatusCode((int)result.Code, new ApiExecuteAndReponse
            {
                Status = result.Status,
                Message = result.Message
            });
        }
        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll()
        {
            var result = await _locationService.getAllLocation();
            return StatusCode((int)result.Code, new ApiFetchAndResponse
            {
                Status = result.Status,
                Message = result.Message,
                Data = result.Data
            });
        }
        [HttpGet("GetLocationDataWithGeo")]
        public async Task<IActionResult> GetLocationDataWithGeo()
        {
            var result = await _locationService.GetAllLocationsData();
            return StatusCode((int)result.Code, new ApiFetchAndResponse
            {
                Status = result.Status,
                Message = result.Message,
                Data = result.Data
            });
        }
        [HttpPost("UpdateLocationGeo"), Authorize]
        public async Task<IActionResult> UpdateGeo([FromBody] LocationGeoUpdateRequest req)
        {
            // must provide either LocationId or STCode
            if (req.LocationId is null)
                return BadRequest("Provide either LocationId.");

            var result = await _locationService.UpdateGeoAsync(req);
            if (result == null) return NotFound("Location not found or not updated.");
            return Ok(result);
        }

        [HttpPost("GetActiveEmployeesByLocation")]
        public async Task<IActionResult> GetActiveEmployeesByLocation([FromForm] string stcode)
        {
            var result = await _locationService.GetActiveEmployeesByLocationAsync(stcode);
            return StatusCode((int)result.Code, new ApiFetchAndResponse
            {
                Status = result.Status,
                Message = result.Message,
                Data = result.Data
            });
        }
    }
}