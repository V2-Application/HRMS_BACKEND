using HRMSAPI.DTO;
using HRMSAPI.Extension;
using HRMSAPI.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace HRMSAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [RequirePageAccess("/holiday-master/groups")]
    public class GroupWiseStoreCodeMappingController : ControllerBase
    {
        private readonly IGroupWiseStoreCodeMappingService _mappingService;
        private readonly ILogger<GroupWiseStoreCodeMappingController> _logger;

        public GroupWiseStoreCodeMappingController(IGroupWiseStoreCodeMappingService mappingService, ILogger<GroupWiseStoreCodeMappingController> logger)
        {
            _mappingService = mappingService ?? throw new ArgumentNullException(nameof(mappingService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Create or update a group-wise store code mapping
        /// </summary>
        /// <param name="mappingDto">Mapping data</param>
        /// <returns>Success status</returns>
        [HttpPost("upsert")]
        public async Task<IActionResult> UpsertMapping([FromBody] GroupWiseStoreCodeMappingUpsertDto mappingDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var result = await _mappingService.UpsertGroupWiseStoreCodeMappingAsync(mappingDto);
                return StatusCode((int)result.Code, new
                {
                    Status = result.Status,
                    Message = result.Message
                });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Validation error in UpsertMapping");
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in UpsertMapping");
                return StatusCode(500, new { success = false, message = "An error occurred while processing the request" });
            }
        }

        /// <summary>
        /// Delete a group-wise store code mapping (soft delete)
        /// </summary>
        /// <param name="id">Mapping ID to delete</param>
        /// <returns>Success status</returns>
        [HttpGet("DeleteMapping")]
        public async Task<IActionResult> DeleteMapping([FromQuery] int id)
        {
            try
            {
                if (id <= 0)
                {
                    return BadRequest(new { success = false, message = "Invalid mapping ID" });
                }

                var result = await _mappingService.DeleteGroupWiseStoreCodeMappingAsync(id);
                return StatusCode((int)result.Code, new
                {
                    Status = result.Status,
                    Message = result.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in DeleteMapping for ID: {Id}", id);
                return StatusCode(500, new { success = false, message = "An error occurred while processing the request" });
            }
        }

        /// <summary>
        /// Get all active group-wise store code mappings
        /// </summary>
        /// <returns>List of all mappings</returns>
        [HttpGet("all")]
        public async Task<IActionResult> GetAllMappings()
        {
            try
            {
                var mappings = await _mappingService.GetAllGroupWiseStoreCodeMappingsAsync();
                return StatusCode((int)mappings.Code, new
                {
                    Status = mappings.Status,
                    Message = mappings.Message,
                    Data = mappings.Data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in GetAllMappings");
                return StatusCode(500, new { success = false, message = "An error occurred while processing the request" });
            }
        }

        /// <summary>
        /// Get  group store code mappings using groupId
        /// </summary>
        /// <returns>List of all group store Mappings</returns>
        [HttpGet("GroupStores")]
        public async Task<IActionResult> GetAllGroupStores([FromQuery]int id)
        {
            try
            {
                var mappings = await _mappingService.GetAllGroupCodeMappingsAsync(id);
                return StatusCode((int)mappings.Code, new
                {
                    Status = mappings.Status,
                    Message = mappings.Message,
                    Data = mappings.Data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in GetAllMappings");
                return StatusCode(500, new { success = false, message = "An error occurred while processing the request" });
            }
        }

        /// <summary>
        /// Upload group-wise store code mappings from Excel file
        /// </summary>
        /// <param name="file">Excel file with GroupName and ST_CD columns</param>
        /// <returns>Upload result</returns>
        [HttpPost("upload")]
        public async Task<IActionResult> UploadMappings([FromForm] IFormFile file)
        {
            try
            {
                if (file == null)
                {
                    return BadRequest(new { success = false, message = "No file uploaded" });
                }

                // Validate file extension
                var allowedExtensions = new[] { ".xlsx", ".xls" };
                var fileExtension = System.IO.Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!Array.Exists(allowedExtensions, ext => ext == fileExtension))
                {
                    return BadRequest(new { success = false, message = "Only Excel files (.xlsx, .xls) are allowed" });
                }

                var result = await _mappingService.UploadGroupWiseStoreCodeMappingAsync(file);
                return StatusCode((int)result.Code, new
                {
                    Status = result.Status,
                    Message = result.Message,
                    Data = result.Data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in UploadMappings");
                return StatusCode(500, new { success = false, message = "An error occurred while processing the request" });
            }
        }
    }
}
