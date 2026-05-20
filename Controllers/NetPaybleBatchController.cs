using HRMSAPI.Extension;
using HRMSAPI.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Roomsy.DTOS.GenericsResponses;

namespace HRMSAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [RequirePageAccess("/month")]
    public class NetPaybleBatchController : ControllerBase
    {
        private readonly INetPaybleBatchService _netPaybleBatchService;

        public NetPaybleBatchController(INetPaybleBatchService netPaybleBatchService)
        {
            _netPaybleBatchService = netPaybleBatchService;
        }

        /// <summary>
        /// Get Net Payable Batch list with optional filtering and pagination
        /// </summary>
        /// <param name="ecode">Employee code (optional)</param>
        /// <param name="asExcel">Export as Excel file (optional)</param>
        /// <param name="page">Page number for pagination (optional)</param>
        /// <param name="pageSize">Page size for pagination (optional)</param>
        /// <returns>Net Payable Batch data or Excel file</returns>
        [HttpGet("GetNetPaybleBatchList")]
        public async Task<IActionResult> GetNetPaybleBatchList(
            [FromQuery] string? ecode = null, 
            [FromQuery] bool asExcel = false, 
            [FromQuery] int? page = null, 
            [FromQuery] int? pageSize = null)
        {
            try
            {
                var result = await _netPaybleBatchService.GetNetPaybleBatchListAsync(ecode, asExcel, page, pageSize);
                
                if (asExcel && result.Status == true && result.Data is byte[] bytes)
                {
                    return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "NetPaybleBatch.xlsx");
                }

                return StatusCode((int)result.Code, new FetchAndResponse
                {
                    Status = result.Status,
                    Message = result.Message,
                    Data = result.Data
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new FetchAndResponse
                {
                    Status = false,
                    Message = "An error occurred while processing the request",
                    Data = null
                });
            }
        }

        /// <summary>
        /// Export Net Payable Batch data to Excel
        /// </summary>
        /// <param name="ecode">Employee code (optional)</param>
        /// <returns>Excel file</returns>
        [HttpGet("ExportNetPaybleBatchToExcel")]
        public async Task<IActionResult> ExportNetPaybleBatchToExcel([FromQuery] string? ecode = null)
        {
            try
            {
                var bytes = await _netPaybleBatchService.ExportNetPaybleBatchToExcelAsync(ecode);
                return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "NetPaybleBatch.xlsx");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new FetchAndResponse
                {
                    Status = false,
                    Message = "An error occurred while exporting data to Excel",
                    Data = null
                });
            }
        }

        /// <summary>
        /// Get Net Payable Batch data as JSON
        /// </summary>
        /// <param name="ecode">Employee code (optional)</param>
        /// <param name="page">Page number for pagination (optional)</param>
        /// <param name="pageSize">Page size for pagination (optional)</param>
        /// <returns>Net Payable Batch data as JSON</returns>
        [HttpGet("GetNetPaybleBatchData")]
        public async Task<IActionResult> GetNetPaybleBatchData(
            [FromQuery] string? ecode = null, 
            [FromQuery] int? page = null, 
            [FromQuery] int? pageSize = null)
        {
            try
            {
                var data = await _netPaybleBatchService.GetNetPaybleBatchDataAsync(ecode, page, pageSize);
                
                return Ok(new FetchAndResponse
                {
                    Status = true,
                    Message = "Net Payable Batch data retrieved successfully",
                    Code = System.Net.HttpStatusCode.OK,
                    Data = data
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new FetchAndResponse
                {
                    Status = false,
                    Message = "An error occurred while retrieving data",
                    Data = null
                });
            }
        }
    }
}
