using HRMSAPI.Extension;
using HRMSAPI.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[RequirePageAccess("/finance/paid-by-cash")]
public class PaidInCashController : ControllerBase
{
    private readonly IPaidInCashService _paidInCashService;

    public PaidInCashController(IPaidInCashService paidInCashService)
    {
        _paidInCashService = paidInCashService ?? throw new ArgumentNullException(nameof(paidInCashService));
    }

    [HttpPost("PaidInCashUploader")]
    public async Task<IActionResult> UploadPaidInCash(IFormFile file)
    {
        string createdBy = "system";
        var (success, message) = await _paidInCashService.UploadPaidInCashDataAsync(file);
        return success ? Ok(new { Message = message }) : BadRequest(new { Message = message });
    }

    [HttpGet]
    public async Task<IActionResult> GetPaidInCashList(
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? ecode = null,
        [FromQuery] string? month = null,
        [FromQuery] string? location = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            var (records, totalRecords) = await _paidInCashService.GetPaidInCashRecordsAsync(
                searchTerm, ecode, month, location, page, pageSize);
            return Ok(new { Records = records, TotalRecords = totalRecords });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error retrieving paid in cash records: {ex.Message}");
            return StatusCode(500, new { Message = "An error occurred while retrieving paid in cash records." });
        }
    }

   
    
}