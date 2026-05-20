using HRMSAPI.Extension;
using HRMSAPI.Interfaces;
using HRMSAPI.Models.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace HRMSAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    [RequirePageAccess("/fnf")]
    public class FnfDetailsController : ControllerBase
    {
        private readonly IFnfDetailsService _fnfDetailsService;

        public FnfDetailsController(IFnfDetailsService fnfDetailsService)
        {
            _fnfDetailsService = fnfDetailsService;
        }

        [HttpGet("GetFnfDetailsByEcode/{ecode}")]
        public async Task<IActionResult> GetFnfDetailsByEcode(string ecode)
        {
            try
            {
                var result = await _fnfDetailsService.GetFnfDetailsByEcodeAsync(ecode);

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



