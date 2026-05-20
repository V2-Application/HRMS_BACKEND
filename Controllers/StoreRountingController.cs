
using HRMSAPI.DTO;
using HRMSAPI.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

[Route("api/[controller]")]
[ApiController]
[Microsoft.AspNetCore.Authorization.Authorize]
[HRMSAPI.Extension.RequirePageAccess("/new-stores")]
public class StoreRoutingController : ControllerBase
{
    private readonly IStoreRoutingService _storeRoutingService;

    public StoreRoutingController(IStoreRoutingService storeRoutingTransactionService)
    {
        _storeRoutingService = storeRoutingTransactionService ?? throw new ArgumentNullException(nameof(storeRoutingTransactionService));
    }

    [HttpPost]
    public async Task<IActionResult> AddStoreRoutingTransaction([FromForm] StoreRoutingTransactionDTO model)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(new { Message = "Invalid input data." });

            var (success, message) = await _storeRoutingService.AddStoreRoutingTransactionAsync(model);

            if (success)
                return Ok(new { Status = true, Message = message });
            else
                return BadRequest(new { Status = false, Message = message });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error adding store routing transaction: {ex.Message}");
            return StatusCode(500, new { Message = "An error occurred while processing the store routing transaction." });
        }
    }
    [HttpGet]
    public async Task<IActionResult> GetStoreRoutingStatus([FromQuery] int locationId)
    {
        try
        {
            if (locationId <= 0)
                return BadRequest(new { Message = "Invalid LocationId. It must be greater than 0." });

            var records = await _storeRoutingService.GetStoreRoutingStatusAsync(locationId);
            return Ok(new { Status = true, Records = records });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error retrieving store routing status: {ex.Message}");
            return StatusCode(500, new { Message = "An error occurred while retrieving store routing status." });
        }
    }
    [HttpGet("GetStoreRoutingsByLocationId/{locationId}")]
    public async Task<ActionResult<StoreRoutingResponse>> GetStoreRoutingsByLocationId(int locationId)
    {
        try
        {
            var response = await _storeRoutingService.GetStoreRoutingStatusByLocationIdAsync(locationId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }
}
