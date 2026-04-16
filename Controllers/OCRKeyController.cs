using HRMSAPI.Interfaces;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace HRMSAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OCRKeyController : ControllerBase
    {
        private readonly IOCRservice _ocrService;
        public OCRKeyController(IOCRservice ocrService)
        {
            _ocrService = ocrService;
        }

        [HttpGet("GetOCRMaster")]
        public async Task<IActionResult> GetOCRMaster([FromQuery] string? subject)
        {
            try
            {
                var data = await _ocrService.GetOCRMasterAsync(subject);

                return Ok(new
                {
                    Status = true,
                    Message = "OCR successfully retrieved",
                    Data = data
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Status = false,
                    Message = ex.Message
                });
            }
        }

    }
}