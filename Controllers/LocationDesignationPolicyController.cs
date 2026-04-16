using ClosedXML.Excel;
using HRMSAPI.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using HRMSAPI.Interfaces;

namespace YourNamespace.Controllers
{
    public class FileUploadModel
    {
        public IFormFile File { get; set; }
    }
    [ApiController]
    [Route("api/[controller]")]
    public class LocationDesignationPolicyController : ControllerBase
    {
        private readonly ILocationDesignationPolicyService _service;

        public LocationDesignationPolicyController(ILocationDesignationPolicyService service)
        {
            _service = service;
        }

        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Upload([FromForm] FileUploadModel file)
        {
            try
            {
                if (file.File == null || file.File.Length == 0)
                    return BadRequest(new { Status = false, Message = "No file uploaded" });

                var extension = Path.GetExtension(file.File.FileName).ToLowerInvariant();
                if (extension != ".xlsx" && extension != ".xls")
                    return BadRequest(new { Status = false, Message = "Only Excel files (.xlsx, .xls) are allowed" });

                var (success, message) = await _service.UploadPolicyDataAsync(file.File);

                if (success)
                    return Ok(new { Status = true, Message = message });
                else
                    return BadRequest(new { Status = false, Message = message });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error uploading policy data: {ex.Message}");
                return StatusCode(500, new { Status = false, Message = "An error occurred while uploading policy data." });
            }
        }
        [HttpGet]
        public async Task<IActionResult> GetPolicyRecords(
          [FromQuery] string? searchTerm = null,
          [FromQuery] string? locationCategoryName = null,
          [FromQuery] int page = 1,
          [FromQuery] int pageSize = 10)
        {
            try
            {
                var (records, totalRecords) = await _service.GetPolicyRecordsAsync(searchTerm, locationCategoryName, page, pageSize);
                return Ok(new { Records = records, TotalRecords = totalRecords });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving policy records: {ex.Message}");
                return StatusCode(500, new { Message = "An error occurred while retrieving policy records." });
            }
        }
    }
}


  

   

