using HRMSAPI.DTO;
using Microsoft.AspNetCore.Mvc;
using Roomsy.DTOS.GenericsResponses;
using System;
using System.IO;
using System.Threading.Tasks;

[Route("api/[controller]")]
[ApiController]
public class PaidByBankController : ControllerBase
{
    private readonly IPaidByBankService _paidByBankRepository;

    public PaidByBankController(IPaidByBankService paidByBankRepository)
    {
        _paidByBankRepository = paidByBankRepository;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> UploadPaidByBank([FromForm] FileDTO fileObj)
    {
        try
        {
            var file = fileObj.File;
            if (file == null || file.Length == 0)
                return BadRequest(new { Message = "No file uploaded" });

            // Validate file extension
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension != ".xlsx" && extension != ".xls")
                return BadRequest(new { Message = "Only Excel files (.xlsx, .xls) are allowed" });

            var (success, message) = await _paidByBankRepository.UploadPaidByBankDataAsync(file, "System");

            if (success)
                return Ok(new { Status = true, Message = message });
            else
                return BadRequest(new { Status = false, Message = message });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error uploading paid by bank data: {ex.Message}");
            return StatusCode(500, new { Message = "An error occurred while uploading paid by bank data." });
        }
    }
    //[HttpGet]
    //public async Task<IActionResult> GetPaidByBankRecords(
    //[FromQuery] string? searchTerm = null,
    //[FromQuery] string? ecode = null,
    //[FromQuery] int page = 1,
    //[FromQuery] int pageSize = 10)
    //{
    //    try
    //    {
    //        var (records, totalRecords) = await _paidByBankRepository.GetPaidByBankRecordsAsync(searchTerm, ecode, page, pageSize);
    //        return Ok(new { Records = records, TotalRecords = totalRecords });
    //    }
    //    catch (Exception ex)
    //    {
    //        Console.WriteLine($"Error retrieving paid by bank records: {ex.Message}");
    //        return StatusCode(500, new { Message = "An error occurred while retrieving paid by bank records." });
    //    }
    //}

    //Update by Gautam
    [HttpGet]
    public async Task<IActionResult> GetPaidByBankRecords([FromQuery] string? searchTerm = null, [FromQuery] string? ecode = null, [FromQuery] string? monthYear = null, [FromQuery] int page = 1, [FromQuery] bool asExcel = false, [FromQuery] int pageSize = 10)
    {
        try
        {
            var result = await _paidByBankRepository.GetPaidByBankRecordsAsync(searchTerm, ecode, monthYear, asExcel, page, pageSize);

            // 🔹 Excel download
            if (asExcel && result.Status == true && result.Data is byte[] bytes)
            {
                return File(
                    bytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"PaidByBank_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
            }

            // 🔹 Normal API response
            return StatusCode((int)result.Code, new ApiFetchAndResponse
            {
                Status = result.Status,
                Message = result.Message,
                Data = result.Data
            });
        }
        catch (Exception ex)
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new ApiFetchAndResponse
                {
                    Status = false,
                    Message = "An unexpected error occurred while fetching Paid By Bank records.",
                    Data = null
                });
        }
    }

}