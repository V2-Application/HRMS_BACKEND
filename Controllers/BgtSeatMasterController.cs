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
    [RequirePageAccess("/bgt-seat-uploader")]
    public class BgtSeatMasterController : ControllerBase
    {

        private readonly IBgtSeatMasterService _service;
        public BgtSeatMasterController(IBgtSeatMasterService service)
        {
            _service = service;
        }

        [HttpPost("UploadExcel")]
        public async Task<IActionResult> UploadExcel([FromForm] IFormFile file)
        {
            var result = await _service.UploadBgtSeatMasterExcelAsync(file);
            return StatusCode((int)result.Code, new ApiExecuteAndReponse
            {
                Status = result.Status,
                Message = result.Message
            });
        }

        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll([FromQuery] bool isExcel = false)
        {
            var result = await _service.GetAllBgtSeatMasterAsync(isExcel);
            if (isExcel && result.Status == true && result.Data is byte[] bytes)
            {
                return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "BgtSeatMaster.xlsx");
            }
            return StatusCode((int)result.Code, new ApiFetchAndResponse
            {
                Status = result.Status,
                Message = result.Message,
                Data = result.Data
            });
        }

		[HttpPost("DeleteBySeries")]
		public async Task<IActionResult> DeleteBySeries([FromQuery] string locCode, [FromQuery] int deptSno, [FromQuery] int desgSno, [FromQuery] int deleteCount = 1)
		{
			var result = await _service.DeleteSeatsBySeriesAsync(locCode, deptSno, desgSno, deleteCount);
			return StatusCode((int)result.Code, new ApiExecuteAndReponse
			{
				Status = result.Status,
				Message = result.Message
			});
		}
    
    }
} 