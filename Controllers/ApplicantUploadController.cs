using HRMSAPI.DTO;
using HRMSAPI.Extension;
using HRMSAPI.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;

namespace HRMSAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    [RequirePageAccess("/applicant/list")]
    public class ApplicantUploadController : ControllerBase
    {
        private readonly IApplicantUploadService _applicantUploadService;

        public ApplicantUploadController(IApplicantUploadService applicantUploadService)
        {
            _applicantUploadService = applicantUploadService;
        }

        [HttpPost]
        [Route("upload")]
        public async Task<IActionResult> UploadApplicants([FromForm] FileDTO fileDto)
        {
            var file = fileDto.File;
            if (file == null)
            {
                return BadRequest(new
                {
                    Status = false,
                    Message = "File is required."
                });
            }

            var identity = HttpContext.User.Identity as ClaimsIdentity;
            var userClaims = AuthenticUserDetails.GetCurrentUserDetails(identity);

            if (userClaims == null || string.IsNullOrWhiteSpace(userClaims.EmployeeId))
            {
                return StatusCode((int)HttpStatusCode.Unauthorized, new
                {
                    Status = false,
                    Message = "Unable to determine logged in user."
                });
            }

            var response = await _applicantUploadService.UploadApplicantsAsync(file, userClaims.EmployeeId);
            return StatusCode((int)response.Code, response);
        }
    }
}

