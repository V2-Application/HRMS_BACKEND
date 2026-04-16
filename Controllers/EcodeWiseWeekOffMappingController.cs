using HRMSAPI.DTO;
using HRMSAPI.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace HRMSAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EcodeWiseWeekOffMappingController : ControllerBase
    {
        private readonly IEcodeWiseWeekOffMappingService _service;
        private readonly ILogger<EcodeWiseWeekOffMappingController> _logger;

        public EcodeWiseWeekOffMappingController(IEcodeWiseWeekOffMappingService service, ILogger<EcodeWiseWeekOffMappingController> logger)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Get all EcodeWiseWeekOffMapping records
        /// </summary>
        /// <returns>List of all EcodeWiseWeekOffMapping records</returns>
        [HttpGet]
        public async Task<IActionResult> GetAllEcodeWiseWeekOffMappings()
        {
            try
            {
                var result = await _service.GetAllEcodeWiseWeekOffMappingsAsync();
                return StatusCode((int)result.Code, new
                {
                    Status = result.Status,
                    Message = result.Message,
                    Data = result.Data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in GetAllEcodeWiseWeekOffMappings");
                return StatusCode(500, new { Status = false, Message = "An error occurred while processing the request" });
            }
        }

        /// <summary>
        /// Create or update an EcodeWiseWeekOffMapping record
        /// </summary>
        /// <param name="dto">EcodeWiseWeekOffMapping data</param>
        /// <returns>Success status</returns>
        [HttpPost("upsert")]
        public async Task<IActionResult> UpsertEcodeWiseWeekOffMapping([FromBody] EcodeWiseWeekOffMappingUpsertDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var result = await _service.UpsertEcodeWiseWeekOffMappingAsync(dto);
                return StatusCode((int)result.Code, new
                {
                    Status = result.Status,
                    Message = result.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in UpsertEcodeWiseWeekOffMapping");
                return StatusCode(500, new { Status = false, Message = "An error occurred while processing the request" });
            }
        }

        /// <summary>
        /// Soft delete an EcodeWiseWeekOffMapping record by setting IsDeleted to true and IsActive to false
        /// </summary>
        /// <param name="deleteDto">Delete request containing the Id</param>
        /// <returns>Success status</returns>
        [HttpPost("delete")]
        public async Task<IActionResult> DeleteEcodeWiseWeekOffMapping([FromBody] EcodeWiseWeekOffMappingDeleteDto deleteDto)
        {
            try
            {
                if (deleteDto == null || deleteDto.Id <= 0)
                {
                    return BadRequest(new { Status = false, Message = "Valid Id is required" });
                }

                var result = await _service.DeleteEcodeWiseWeekOffMappingAsync(deleteDto.Id);
                return StatusCode((int)result.Code, new
                {
                    Status = result.Status,
                    Message = result.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in DeleteEcodeWiseWeekOffMapping");
                return StatusCode(500, new { Status = false, Message = "An error occurred while processing the request" });
            }
        }

        /// <summary>
        /// Upload EcodeWiseWeekOffMapping data from Excel file
        /// </summary>
        /// <param name="file">Excel file containing Ecode, Month, TotalAttendance, WeeklyOFF columns</param>
        /// <returns>Success status with count of records added/updated</returns>
        [HttpPost("upload")]
        public async Task<IActionResult> UploadEcodeWiseWeekOffMapping([FromForm] FileDTO filedto)
        {
            try
            {
                var file = filedto.File;
                if (file == null || file.Length == 0)
                {
                    return BadRequest(new { Status = false, Message = "No file uploaded" });
                }

                // Validate file extension
                var extension = System.IO.Path.GetExtension(file.FileName).ToLowerInvariant();
                if (extension != ".xlsx" && extension != ".xls")
                {
                    return BadRequest(new { Status = false, Message = "Only Excel files (.xlsx, .xls) are allowed" });
                }

                var result = await _service.UploadEcodeWiseWeekOffMappingAsync(file);
                return StatusCode((int)result.Code, new
                {
                    Status = result.Status,
                    Message = result.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in UploadEcodeWiseWeekOffMapping");
                return StatusCode(500, new { Status = false, Message = "An error occurred while processing the request" });
            }
        }
    }
}

