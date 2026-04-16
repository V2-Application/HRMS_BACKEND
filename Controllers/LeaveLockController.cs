using HRMSAPI.Interfaces;
using HRMSAPI.Models.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace HRMSAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    //[Authorize]
    public class LeaveLockController : ControllerBase
    {
        private readonly ILeaveLockService _leaveLockService;

        public LeaveLockController(ILeaveLockService leaveLockService)
        {
            _leaveLockService = leaveLockService;
        }

        [HttpGet("CheckLeaveLockStatus")]
        public async Task<IActionResult> CheckLeaveLockStatus()
        {
            try
            {
                var result = await _leaveLockService.CheckLeaveLockStatusAsync();
                
                if (result.Status)
                {
                    return Ok(new
                    {
                        Status = result.Status,
                        Message = result.Message,
                        Data = result.Data
                    });
                }
                else
                {
                    return StatusCode((int)result.StatusCode, new
                    {
                        Status = result.Status,
                        Message = result.Message,
                        Data = result.Data
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Status = false,
                    Message = "An unexpected error occurred.",
                    Data = (object?)null
                });
            }
        }
    }
}

