using ClosedXML.Excel;
using HRMSAPI.DTO;
using HRMSAPI.Extension;
using HRMSAPI.Implementation;
using HRMSAPI.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using static HRMSAPI.Implementation.HolidayMasterService;

namespace HRMSAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [RequirePageAccess("/holiday-master/holidays")]
    public class HolidayMasterController : ControllerBase
    {
        private readonly IHolidayMasterService _holidayService;
        private readonly ILogger<HolidayMasterController> _logger;

        public HolidayMasterController(IHolidayMasterService holidayService, ILogger<HolidayMasterController> logger)
        {
            _holidayService = holidayService ?? throw new ArgumentNullException(nameof(holidayService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Create a new holiday (error if already exists)
        /// </summary>
        /// <param name="holidayDto">Holiday data</param>
        /// <returns>Success status</returns>
        [HttpPost("upsert")]
        public async Task<IActionResult> UpsertHoliday([FromBody] HolidayMasterUpsertDto holidayDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var result = await _holidayService.UpsertHolidayAsync(holidayDto);
                return StatusCode((int)result.Code, new
                {
                    Status = result.Status,
                    Message = result.Message
                });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Validation error in UpsertHoliday");
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in UpsertHoliday");
                return StatusCode(500, new { success = false, message = "An error occurred while processing the request" });
            }
        }

        /// <summary>
        /// Delete a holiday (soft delete)
        /// </summary>
        /// <param name="id">Holiday ID to delete</param>
        /// <returns>Success status</returns>
        [HttpGet("DeleteHoliday")]
        public async Task<IActionResult> DeleteHoliday([FromQuery] int id)
        {
            try
            {
                if (id <= 0)
                {
                    return BadRequest(new { success = false, message = "Invalid holiday ID" });
                }

                var result = await _holidayService.DeleteHolidayAsync(id);
                return StatusCode((int)result.Code, new
                {
                    Status = result.Status,
                    Message = result.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in DeleteHoliday for ID: {Id}", id);
                return StatusCode(500, new { success = false, message = "An error occurred while processing the request" });
            }
        }

        /// <summary>
        /// Get all holidays with optional filters
        /// </summary>
        /// <param name="storeCodeOrGroupName">Optional filter for StoreCode or GroupName</param>
        /// <param name="month">Optional filter for month (1-12)</param>
        /// <returns>List of holidays</returns>
        [HttpGet("all")]
        public async Task<IActionResult> GetAllHolidays([FromQuery] string storeCodeOrGroupName = null, [FromQuery] int? month = null)
        {
            try
            {
                var holidays = await _holidayService.GetAllHolidaysAsync(storeCodeOrGroupName, month);
                return StatusCode((int)holidays.Code, new
                {
                    Status = holidays.Status,
                    Message = holidays.Message,
                    Data = holidays.Data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in GetAllHolidays");
                return StatusCode(500, new { success = false, message = "An error occurred while processing the request" });
            }
        }

        /// <summary>
        /// Upload holidays from Excel file
        /// </summary>
        /// <param name="file">Excel file with LocationTypeName, LocationValue, HolidayName, and HolidayDate columns</param>
        /// <returns>Upload result</returns>
        [HttpPost("upload")]
        public async Task<IActionResult> UploadHolidays([FromForm] IFormFile file)
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

                var result = await _holidayService.UploadHolidaysAsync(file);
                return StatusCode((int)result.Code, new
                {
                    Status = result.Status,
                    Message = result.Message,
                    Data = result.Data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in UploadHolidays");
                return StatusCode(500, new { success = false, message = "An error occurred while processing the request" });
            }
        }


        [HttpPost("UpsertPolicyDesignation")]
        public async Task<IActionResult> UpsertPolicyDesignation([FromBody] List<LocationDesignationPolicyDto> request)
        {
            if (request == null || request.Count == 0)
                return BadRequest(new
                {
                    Status = false,
                    Message = "Request body is empty"
                });

            try
            {
                var identity = HttpContext.User.Identity as ClaimsIdentity;
                var userClaims = AuthenticUserDetails.GetCurrentUserDetails(identity);

                if (userClaims == null || string.IsNullOrEmpty(userClaims.EmployeeId))
                {
                    return BadRequest(new
                    {
                        Status = false,
                        Message = "Invalid user credentials."
                    });
                }

                await _holidayService.UpsertPolicyDesignation(request, userClaims);

                return Ok(new
                {
                    Status = true,
                    Message = "Location Designation Policy saved successfully"
                });
            }
            catch (SqlException ex)
            {
                return BadRequest(new
                {
                    Status = false,
                    Message = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UpsertPolicyDesignation");

                return StatusCode(500, new
                {
                    Status = false,
                    Message = "Internal server error"
                });
            }
        }

        //[HttpGet("GetPolicyDesignationByMonthYear")]
        //public async Task<IActionResult> GetPolicyDesignationByMonthYear([FromQuery] string monthYear,[FromQuery] int pageNumber = 1,[FromQuery] int pageSize = 10,[FromQuery] string? searchTerm = null)
        //{
        //    try
        //    {
        //        if (string.IsNullOrWhiteSpace(monthYear))
        //        {
        //            return Ok(new
        //            {
        //                Status = true,
        //                message = "Month year is not valid",
        //                Data = new List<LocationDesignationPolicyResponseDto>(),
        //                TotalCount = 0
        //            });
        //        }

        //        var result = await _holidayService.GetByMonthYearAsync(
        //            monthYear, pageNumber, pageSize, searchTerm);

        //        return Ok(new
        //        {
        //            Status = true,
        //            message = result.Data.Any()
        //                ? "Location Designation Retrieved successfully"
        //                : "No data found",
        //            Data = result.Data,
        //            TotalCount = result.TotalRecords,
        //            PageNumber = pageNumber,
        //            PageSize = pageSize
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(
        //            ex,
        //            "Error occurred in GetPolicyDesignationByMonthYear for MonthYear: {MonthYear}",
        //            monthYear);

        //        return StatusCode(500, new
        //        {
        //            Status = false,
        //            message = "An error occurred while processing the request"
        //        });
        //    }
        //}

        [HttpGet("GetPolicyDesignationByMonthYear")]
        public async Task<IActionResult> GetPolicyDesignationByMonthYear([FromQuery] string monthYear,[FromQuery] string? searchTerm = null,[FromQuery] bool isExcel = false)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(monthYear))
                {
                    return Ok(new
                    {
                        Status = true,
                        Message = "Month year is not valid",
                        Data = new List<LocationDesignationPolicyResponseDto>(),
                        TotalCount = 0
                    });
                }

                // 🔹 EXCEL DOWNLOAD
                if (isExcel)
                {
                    var excelData =
                        await _holidayService.GetByMonthYearForExcelAsync(
                            monthYear, searchTerm);

                    return ExcelHelper.GeneratePolicyExcel(excelData,$"LocationDesignationPolicy_{monthYear}_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
                }

                // 🔹 NORMAL PAGED LIST
                var result =
                    await _holidayService.GetByMonthYearAsync(
                        monthYear, searchTerm);

                return Ok(new
                {
                    Status = true,
                    Message = result.Data.Any()
                        ? "Location Designation Retrieved successfully"
                        : "No data found",
                    Data = result.Data,
                    TotalCount = result.TotalRecords,
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error in GetPolicyDesignationByMonthYear for MonthYear: {MonthYear}",
                    monthYear);

                return StatusCode(500, new
                {
                    Status = false,
                    Message = "An error occurred while processing the request"
                });
            }
        }

        [HttpPost("ImportPolicyDesignation")]
        public async Task<IActionResult> ImportPolicyDesignation(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { Status = false, Message = "File is empty" });

            try
            {
                var identity = HttpContext.User.Identity as ClaimsIdentity;
                var userClaims = AuthenticUserDetails.GetCurrentUserDetails(identity);

                if (userClaims == null || string.IsNullOrEmpty(userClaims.EmployeeId))
                {
                    return BadRequest(new
                    {
                        Status = false,
                        Message = "Invalid user credentials."
                    });
                }

                var importedCount = await _holidayService.ImportPolicyDesignationAsync(file, userClaims);

                return Ok(new
                {
                    Status = true,
                    Message = "Import completed successfully",
                    ImportedCount = importedCount
                });
            }
            catch (SqlException ex)
            {
                return BadRequest(new
                {
                    Status = false,
                    Message = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during ImportPolicyDesignation");
                return StatusCode(500, new
                {
                    Status = false,
                    Message = "Internal server error"
                });
            }
        }

        [HttpPut("ToggleActive")]
        public async Task<IActionResult> ToggleActive([FromBody] ToggleLocationDesignationPolicyStatusDto request)
        {
            if (request?.LocationDesignationPolicyIds == null ||
                !request.LocationDesignationPolicyIds.Any())
            {
                return BadRequest("No IDs provided");
            }

            try
            {
                var identity = HttpContext.User.Identity as ClaimsIdentity;
                var userClaims = AuthenticUserDetails.GetCurrentUserDetails(identity);

                if (userClaims == null || string.IsNullOrEmpty(userClaims.EmployeeId))
                {
                    return BadRequest(new
                    {
                        Status = false,
                        Message = "Invalid user credentials."
                    });
                }

                await _holidayService.ToggleActiveStatusAsync(request.LocationDesignationPolicyIds, request.IsActive, userClaims);

                return Ok(new
                {
                    Status = true,
                    message = request.IsActive
                        ? "Location Designation Policy activated successfully"
                        : "Location Designation Policy deactivated successfully"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    Status = false,
                    message = ex.Message
                });
            }
        }
    }
}
