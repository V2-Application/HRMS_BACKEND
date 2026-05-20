using HRMSAPI.Extension;
using HRMSAPI.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Roomsy.DTOS.GenericsResponses;
using System.Threading.Tasks;

namespace HRMSAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [RequirePageAccess("/emp-store-assignment")]
    public class BgtSeatAssignmentController : ControllerBase
    {
        private readonly IBgtSeatAssignmentService _service;
        public BgtSeatAssignmentController(IBgtSeatAssignmentService service)
        {
            _service = service;
        }

        [HttpPost("UploadExcel")]
        public async Task<IActionResult> UploadExcel([FromForm] IFormFile file)
        {
            var result = await _service.UploadBgtSeatAssignmentExcelAsync(file);
            return StatusCode((int)result.Code, new ApiExecuteAndReponse
            {
                Status = result.Status,
                Message = result.Message
            });
        }

        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll()
        {
            var result = await _service.GetAllBgtSeatAssignmentAsync();
            return StatusCode((int)result.Code, new ApiFetchAndResponse
            {
                Status = result.Status,
                Message = result.Message,
                Data = result.Data
            });
        }
    }
} 