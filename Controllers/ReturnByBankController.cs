using HRMSAPI.Extension;
using HRMSAPI.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[RequirePageAccess("/finance/return-by-bank")]
public class ReturnByBankController : ControllerBase
{
    private readonly IReturnByBankService _returnByBankService;

    public ReturnByBankController(IReturnByBankService returnByBankService)
    {
        _returnByBankService = returnByBankService ?? throw new ArgumentNullException(nameof(returnByBankService));
    }

    [HttpPost("ReturnByBankUploader")]
    public async Task<IActionResult> UploadReturnByBank(IFormFile file)
    {
        string createdBy = "system";
        var (success, message) = await _returnByBankService.UploadReturnByBankDataAsync(file);
        return success ? Ok(new { Message = message }) : BadRequest(new { Message = message });
    }

    [HttpGet]
    public async Task<IActionResult> GetReturnByBankList(
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? ecode = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            var (records, totalRecords) = await _returnByBankService.GetPaidByBankRecordsAsync(searchTerm, ecode, page, pageSize);
            return Ok(new { Records = records, TotalRecords = totalRecords });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error retrieving return by bank records: {ex.Message}");
            return StatusCode(500, new { Message = "An error occurred while retrieving return by bank records." });
        }
    }
}