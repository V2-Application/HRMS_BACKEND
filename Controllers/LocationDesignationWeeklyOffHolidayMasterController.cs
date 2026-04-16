using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using HRMSAPI.Interfaces;

namespace YourNamespace.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LocationDesignationWeeklyOffHolidayMasterController : ControllerBase
    {
        private readonly ILocationDesignationWeeklyOffHolidayMasterService _service;

        public LocationDesignationWeeklyOffHolidayMasterController(ILocationDesignationWeeklyOffHolidayMasterService service)
        {
            _service = service;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> Upload([FromForm] FileUploadModel file)
        {
            try
            {
                if (file.File == null || file.File.Length == 0)
                    return BadRequest(new { Status = false, Message = "No file uploaded" });

                var extension = Path.GetExtension(file.File.FileName).ToLowerInvariant();
                if (extension != ".xlsx" && extension != ".xls")
                    return BadRequest(new { Status = false, Message = "Only Excel files (.xlsx, .xls) are allowed" });

                var (success, message) = await _service.UploadMasterDataAsync(file.File);

                if (success)
                    return Ok(new { Status = true, Message = message });
                else
                    return BadRequest(new { Status = false, Message = message });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error uploading master data: {ex.Message}");
                return StatusCode(500, new { Status = false, Message = "An error occurred while uploading master data." });
            }
        }
        [HttpGet]
        public async Task<IActionResult> GetMasterRecords(
            [FromQuery] string? searchTerm = null,
            [FromQuery] string? locationCategoryName = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                var (records, totalRecords) = await _service.GetMasterRecordsAsync(searchTerm, locationCategoryName, page, pageSize);
                return Ok(new { Records = records, TotalRecords = totalRecords });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving master records: {ex.Message}");
                return StatusCode(500, new { Message = "An error occurred while retrieving master records." });
            }
        }
    }
}
