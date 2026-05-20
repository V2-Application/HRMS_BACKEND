using HRMSAPI.Data;
using HRMSAPI.DTO;
using HRMSAPI.Implementation;
using HRMSAPI.Interfaces;
using HRMSAPI.Models;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;
using StoreLocation = HRMSAPI.Data.StoreLocation;

namespace HRMSAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Microsoft.AspNetCore.Authorization.Authorize]
    [HRMSAPI.Extension.RequirePageAccess("/master/seat")]
    public class StoreLocationsController : ControllerBase
    {
        private readonly IStoreService _storeService;

        public StoreLocationsController(IStoreService storeService)
        {
            _storeService = storeService ?? throw new ArgumentNullException(nameof(storeService));
        }

        // POST or PUT: api/StoreLocations/{id?}
        [HttpPost("{id?}")]
        public async Task<ActionResult<StoreLocation>> UpsertStoreLocation(int? id, [FromBody] StoreLocationUpsertDto storeLocationDto)
        {
            if (storeLocationDto == null)
            {
                return BadRequest("Store location data is required.");
            }

            try
            {
                var storeLocation = await _storeService.UpsertStoreLocationAsync(storeLocationDto, id);
                if (id.HasValue)
                {
                    return Ok(storeLocation);
                }
                return CreatedAtAction(nameof(GetStoreLocations), new { id = storeLocation.StoreLocationsId }, storeLocation);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // POST or PUT: api/StoreLocations/StoreBudget/{id?}
        [HttpPost("StoreBudget")]
        public async Task<ActionResult> UpsertStoreBudgets([FromBody] List<StoreBudgetUpsertDto> storeBudgetDtos)
        {
            if (storeBudgetDtos == null || !storeBudgetDtos.Any())
                return BadRequest("Store budget list is required.");

            try
            {
                var result = await _storeService.UpsertStoreBudgetAsync(storeBudgetDtos);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }


        // GET: api/StoreLocations/{storeLocationsId?}
        [HttpGet("{storeLocationsId?}")]
        public async Task<ActionResult<List<StoreDetailDto>>> GetStoreLocations(int? storeLocationsId = null)
        {
            try
            {
                var storeLocations = await _storeService.GetStoreLocationsAsync(storeLocationsId);
                return Ok(storeLocations);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // GET: api/StoreLocations/StoreBudgets
        // GET: api/StoreLocations/StoreBudgets/{id?}
        [HttpGet("StoreBudgets/{id?}")]
        public async Task<ActionResult<List<tblStoreBudget>>> GetStoreBudgets(int? id = null)
        {
            try
            {
                var storeBudgets = await _storeService.GetStoreBudgetsAsync(id);
                return Ok(storeBudgets);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
        [HttpGet]
        public async Task<IActionResult> GetStoreLocations([FromQuery] int? pageNumber, [FromQuery] int? pageSize)
        {
            try
            {
                var data = await _storeService.GetStoreLocationsBulkAsync(pageNumber, pageSize);
                return StatusCode(200, new
                {
                    Status = "Success",
                    Message = "Store data retrieved successfully",
                    Data = data
                });
            }
            catch (Exception ex)
            {
                return StatusCode(400, new
                {
                    Status = "Error",
                    Message = $"An error occurred: {ex.Message}",
                    Data = (PaginatedResponse<StoreMasterBulk>?)null
                });
            }
        }

        [HttpGet("GetByRecords/{records}")]
        public async  Task<IActionResult> GetStores(int records)
        {
            try
            {
                var res = await _storeService.GetStoresByRecord(records);
                return Ok(res);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Status = "Error",
                    Message = $"An error occurred: {ex.Message}",
                    Data = (PaginatedResponse<StoreMasterBulk>?)null
                });
            }
        }
        [HttpGet("GetStoresByMonth")]
        public async Task<IActionResult> GetStoresByMonth( [FromQuery] int? records = null)
        {
            try
            {
                var result = await _storeService.GetStoresByMonthAsync(records);


                return Ok(new { 
                    Status = true,
                    Message = "Feteched Successfully",
                    Data = result
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new
                {
                    Status = false,
                    Message = ex.Message,
                    
                });
            }
        }
    }
}