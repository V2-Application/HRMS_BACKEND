using HRMSAPI.DTO;
using HRMSAPI.Extension;
using HRMSAPI.Implementation;
using HRMSAPI.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Roomsy.DTOS.GenericsResponses;
using System.Globalization;
using System.Security.Claims;
using System.Threading.Tasks;

namespace HRMSAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UploaderController : ControllerBase
    {
        private readonly ILogger<UploaderController> _logger;
        private readonly IUploaderService _service;
        public UploaderController(IUploaderService service, ILogger<UploaderController> logger)
        {
            _logger = logger;
            _service = service;
        }

        [HttpPost("UploadEmpAttendanceMaster")]
        public async Task<IActionResult> UploadEmpAttendanceMaster([FromForm] FileDTO fileD)
        {
            var file = fileD.File;
            var result = await _service.UploadEmpAttendanceMasterAsync(file);
            return StatusCode((int)result.Code, new ApiExecuteAndReponse { Status = result.Status, Message = result.Message });
        }

        [HttpGet("GetAllEcodeZoneRegionClusterMapping")]
        public async Task<IActionResult> GetAllEcodeZoneRegionClusterMapping([FromQuery] bool isExcel = false)
        {
            if (isExcel)
            {
                var (success, message, fileBytes, contentType, fileName) = await _service.GetEcodeZoneRegionClusterMappingExcelAsync();
                if (!success || fileBytes == null || fileBytes.Length == 0)
                {
                    return BadRequest(new { Status = false, Message = message ?? "Failed to generate Excel" });
                }
                return File(fileBytes, contentType, fileName);
            }
            else
            {
                var result = await _service.GetAllEcodeZoneRegionClusterMappingAsync();
                return StatusCode((int)result.Code, new ApiFetchAndResponse { Status = result.Status, Message = result.Message, Data = result.Data });
            }
        }
        [HttpPost("UploadEcodeZoneRegionClusterMapping"), Authorize]
        public async Task<IActionResult> UploadEcodeZoneRegionClusterMapping([FromForm] IFormFile file)
        {
            var identity = HttpContext.User.Identity as ClaimsIdentity;
            if (identity == null)
                return BadRequest("Authentication Fails");

            var userClaims = AuthenticUserDetails.GetCurrentUserDetails(identity);

            if (userClaims == null || string.IsNullOrEmpty(userClaims.EmployeeId))
            {
                return BadRequest(new
                {
                    Status = false,
                    Message = "Invalid user credentials."
                });
            }

            var result = await _service.UploadEcodeZoneRegionClusterMappingAsync(file, userClaims.EmployeeId);
            return StatusCode((int)result.Code, new ApiExecuteAndReponse
            {
                Status = result.Status,
                Message = result.Message
            });
        }

        [HttpGet("GetAllEmpAttendanceMaster")]
        public async Task<IActionResult> GetAllEmpAttendanceMaster()
        {
            var result = await _service.GetAllEmpAttendanceMasterAsync();
            return StatusCode((int)result.Code, new ApiFetchAndResponse { Status = result.Status, Message = result.Message, Data = result.Data });
        }

        [HttpPost("UploadEmpTDSTable")]
        public async Task<IActionResult> UploadEmpTDSTable([FromForm] IFormFile file)
        {
            var result = await _service.UploadEmpTDSTableAsync(file);
            return StatusCode((int)result.Code, new ApiExecuteAndReponse { Status = result.Status, Message = result.Message });
        }

        [HttpGet("GetAllEmpTDSTable")]
        public async Task<IActionResult> GetAllEmpTDSTable()
        {
            var result = await _service.GetAllEmpTDSTableAsync();
            return StatusCode((int)result.Code, new ApiFetchAndResponse { Status = result.Status, Message = result.Message, Data = result.Data });
        }

        [HttpPost("UploadApplicabilityMaster")]
        public async Task<IActionResult> UploadApplicabilityMaster([FromForm] IFormFile file)
        {
            var result = await _service.UploadApplicabilityMasterAsync(file);
            return StatusCode((int)result.Code, new ApiExecuteAndReponse { Status = result.Status, Message = result.Message });
        }

        [HttpGet("GetAllApplicabilityMaster")]
        public async Task<IActionResult> GetAllApplicabilityMaster()
        {
            var result = await _service.GetAllApplicabilityMasterAsync();
            return StatusCode((int)result.Code, new ApiFetchAndResponse { Status = result.Status, Message = result.Message, Data = result.Data });
        }
        [HttpPost("UploadEmpSalaryStructure")]
        public async Task<IActionResult> UploadEmpSalaryStructure([FromForm] IFormFile file)
        {
            var result = await _service.UploadEmpSalaryStructureAsync(file);
            return StatusCode((int)result.Code, new ApiExecuteAndReponse { Status = result.Status, Message = result.Message });
        }

        [HttpGet("GetAllEmpSalaryStructure")]
        public async Task<IActionResult> GetAllEmpSalaryStructure()
        {
            var result = await _service.GetAllEmpSalaryStructureAsync();
            return StatusCode((int)result.Code, new ApiFetchAndResponse { Status = result.Status, Message = result.Message, Data = result.Data });
        }

        [HttpPost("UploadLeaveOpeningBalTable")]
        public async Task<IActionResult> UploadLeaveOpeningBalTable([FromForm] IFormFile file)
        {
            var result = await _service.UploadLeaveOpeningBalTableAsync(file);
            return StatusCode((int)result.Code, new ApiExecuteAndReponse { Status = result.Status, Message = result.Message });
        }

        [HttpGet("GetAllLeaveOpeningBalTable")]
        public async Task<IActionResult> GetAllLeaveOpeningBalTable()
        {
            var result = await _service.GetAllLeaveOpeningBalTableAsync();
            return StatusCode((int)result.Code, new ApiFetchAndResponse { Status = result.Status, Message = result.Message, Data = result.Data });
        }

        [HttpPost("UploadEmpPersonalDetails")]
        public async Task<IActionResult> UploadEmpPersonalDetails([FromForm] IFormFile file)
        {
            var result = await _service.UploadEmpPersonalDetailsAsync(file);
            return StatusCode((int)result.Code, new ApiExecuteAndReponse { Status = result.Status, Message = result.Message });
        }

        [HttpGet("GetAllEmpPersonalDetails")]
        public async Task<IActionResult> GetAllEmpPersonalDetails()
        {
            var result = await _service.GetAllEmpPersonalDetailsAsync();
            return StatusCode((int)result.Code, new ApiFetchAndResponse { Status = result.Status, Message = result.Message, Data = result.Data });
        }

        [HttpPost("UploadEmpStatutoryDetails")]
        public async Task<IActionResult> UploadEmpStatutoryDetails([FromForm] IFormFile file)
        {
            var result = await _service.UploadEmpStatutoryDetailsAsync(file);
            return StatusCode((int)result.Code, new ApiExecuteAndReponse { Status = result.Status, Message = result.Message });
        }

        [HttpGet("GetAllEmpStatutoryDetails")]
        public async Task<IActionResult> GetAllEmpStatutoryDetails()
        {
            var result = await _service.GetAllEmpStatutoryDetailsAsync();
            return StatusCode((int)result.Code, new ApiFetchAndResponse { Status = result.Status, Message = result.Message, Data = result.Data });
        }

        [HttpPost("UploadEmpDegreeQualification")]
        public async Task<IActionResult> UploadEmpDegreeQualification([FromForm] IFormFile file)
        {
            var result = await _service.UploadEmpDegreeQualificationAsync(file);
            return StatusCode((int)result.Code, new ApiExecuteAndReponse { Status = result.Status, Message = result.Message });
        }

        [HttpGet("GetAllEmpDegreeQualification")]
        public async Task<IActionResult> GetAllEmpDegreeQualification()
        {
            var result = await _service.GetAllEmpDegreeQualificationAsync();
            return StatusCode((int)result.Code, new ApiFetchAndResponse { Status = result.Status, Message = result.Message, Data = result.Data });
        }

        [HttpPost("UploadEmpPastExperienceDetails")]
        public async Task<IActionResult> UploadEmpPastExperienceDetails([FromForm] IFormFile file)
        {
            var result = await _service.UploadEmpPastExperienceDetailsAsync(file);
            return StatusCode((int)result.Code, new ApiExecuteAndReponse { Status = result.Status, Message = result.Message });
        }

        [HttpGet("GetAllEmpPastExperienceDetails")]
        public async Task<IActionResult> GetAllEmpPastExperienceDetails()
        {
            var result = await _service.GetAllEmpPastExperienceDetailsAsync();
            return StatusCode((int)result.Code, new ApiFetchAndResponse { Status = result.Status, Message = result.Message, Data = result.Data });
        }

        [HttpPost("UploadEmpJoiningReleavingDetails")]
        public async Task<IActionResult> UploadEmpJoiningReleavingDetails([FromForm] IFormFile file)
        {
            var result = await _service.UploadEmpJoiningReleavingDetailsAsync(file);
            return StatusCode((int)result.Code, new ApiExecuteAndReponse { Status = result.Status, Message = result.Message });
        }

        [HttpGet("GetAllEmpJoiningReleavingDetails")]
        public async Task<IActionResult> GetAllEmpJoiningReleavingDetails()
        {
            var result = await _service.GetAllEmpJoiningReleavingDetailsAsync();
            return StatusCode((int)result.Code, new ApiFetchAndResponse { Status = result.Status, Message = result.Message, Data = result.Data });
        }

        [HttpPost("UploadEmpRevisedDeptDesgLocDetails")]
        public async Task<IActionResult> UploadEmpRevisedDeptDesgLocDetails([FromForm] IFormFile file)
        {
            var result = await _service.UploadEmpRevisedDeptDesgLocDetailsAsync(file);
            return StatusCode((int)result.Code, new ApiExecuteAndReponse { Status = result.Status, Message = result.Message });
        }

        [HttpGet("GetAllEmpRevisedDeptDesgLocDetails")]
        public async Task<IActionResult> GetAllEmpRevisedDeptDesgLocDetails()
        {
            var result = await _service.GetAllEmpRevisedDeptDesgLocDetailsAsync();
            return StatusCode((int)result.Code, new ApiFetchAndResponse { Status = result.Status, Message = result.Message, Data = result.Data });
        }
        [HttpPost("UploadPayment")]
        public async Task<IActionResult> UploadPayment([FromForm] FileDTO file)
        {
            var result = await _service.UploadPaymentsync(file.File);
            return StatusCode((int)result.Code, new ApiExecuteAndReponse { Status = result.Status, Message = result.Message });
        }

        [HttpGet("GetAllUploadPaymentDetails")]
        public async Task<IActionResult> GetAllUploadPaymentDetails()
        {
            var result = await _service.GetAllPaymentsAsync();
            return StatusCode((int)result.Code, new ApiFetchAndResponse { Status = result.Status, Message = result.Message, Data = result.Data });
        }

        [HttpPost("UploadBonusAndGratutityOpening")]
        public async Task<IActionResult> UploadBonusAndGratutityOpening([FromForm] IFormFile file)
        {
            var result = await _service.UploadBonusAndGratutityOpeningAsync(file);
            return StatusCode((int)result.Code, new ApiExecuteAndReponse { Status = result.Status, Message = result.Message });
        }

        [HttpGet("GetBonusAndGratutityOpening")]
        public async Task<IActionResult> GetBonusAndGratutityOpening([FromQuery] string? ecode)
        {
            var result = await _service.GetBonusAndGratutityOpeningByEcodeAsync(ecode);
            return StatusCode((int)result.Code, new ApiFetchAndResponse { Status = result.Status, Message = result.Message, Data = result.Data });
        }

        [HttpPost("UploadEmpSalaryStatus")]
        public async Task<IActionResult> UploadEmpSalaryStatus([FromForm] IFormFile file)
        {
            var result = await _service.UploadEmpSalaryStatusAsync(file);
            return StatusCode((int)result.Code, new ApiExecuteAndReponse { Status = result.Status, Message = result.Message });
        }

        [HttpGet("GetEmpSalaryStatus")]
        public async Task<IActionResult> GetEmpSalaryStatus([FromQuery] string? ecode)
        {
            var result = await _service.GetEmpSalaryStatusByEcodeAsync(ecode);
            return StatusCode((int)result.Code, new ApiFetchAndResponse { Status = result.Status, Message = result.Message, Data = result.Data });
        }
        [HttpPost("UploadCompOff")]
        public async Task<IActionResult> UploadCompOff([FromForm] IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    _logger.LogWarning("No file uploaded in CompOff upload request.");
                    return BadRequest(new { Message = "No file uploaded" });
                }

                // Validate file extension
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (extension != ".xlsx" && extension != ".xls")
                {
                    _logger.LogWarning("Invalid file extension {Extension} in CompOff upload.", extension);
                    return BadRequest(new { Message = "Only Excel files (.xlsx, .xls) are allowed" });
                }

                var (success, message) = await _service.UploadCompOffDataAsync(file, "0");

                if (success)
                {
                    _logger.LogInformation("CompOff data uploaded successfully.");
                    return Ok(new { Status = true, Message = message });
                }
                else
                {
                    _logger.LogError("Failed to upload CompOff data: {Message}", message);
                    return BadRequest(new { Status = false, Message = message });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading comp off data.");
                return StatusCode(500, new { Message = "An error occurred while uploading comp off data." });
            }
        }
        [HttpGet("GetCompOffList")]
        public async Task<IActionResult> GetCompOffList()
        {
            try
            {
                var compOffList = await _service.GetCompOffListAsync();
                _logger.LogInformation("Retrieved {Count} CompOff records.", compOffList.Count);
                return Ok(new { Status = true, Data = compOffList });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving CompOff list.");
                return StatusCode(500, new { Message = "An error occurred while retrieving CompOff list." });
            }
        }

        [HttpPost("UploadStoreStateLinking")]
        public async Task<IActionResult> UploadStoreStateLinking([FromForm] IFormFile file)
        {
            var result = await _service.UploadStoreStateLinkingAsync(file);
            return StatusCode((int)result.Code, new ApiExecuteAndReponse { Status = result.Status, Message = result.Message });
        }

        [HttpGet("GetAllStoreStateLinking")]
        public async Task<IActionResult> GetAllStoreStateLinking()
        {
            var result = await _service.GetAllStoreStateLinkingAsync();
            return StatusCode((int)result.Code, new ApiFetchAndResponse { Status = result.Status, Message = result.Message, Data = result.Data });
        }

        [HttpGet("GetStoreWhichCanAdd")]
        public async Task<IActionResult> GetStoreWhichCanAdd([FromQuery] bool isExcel = true)
        {
            if (isExcel)
            {
                var (success, message, fileBytes, contentType, fileName) = await _service.GetStoreWhichCanAddExcelAsync();
                if (!success || fileBytes == null)
                {
                    return BadRequest(new { Status = false, Message = message ?? "Failed to generate Excel" });
                }
                return File(fileBytes, contentType, fileName);
            }
            else
            {
                var result = await _service.GetStoreWhichCanAddAsync();
                return StatusCode((int)result.Code, new ApiFetchAndResponse { Status = result.Status, Message = result.Message, Data = result.Data });
            }
        }
        [HttpPost("UploadEmpBonusDetails")]
        public async Task<IActionResult> UploadEmpPayrolDetails([FromForm] FileDTO fileDTO)
        {
            var file = fileDTO.File;
            var result = await _service.UploadEmpPayrolDetailsAsync(file);
            return StatusCode((int)result.Code, new ApiExecuteAndReponse { Status = result.Status, Message = result.Message });
        }
        [HttpGet("GetEMPBonusList")]
        public async Task<IActionResult> GetEMPBonusList()
        {
            var result = await _service.GetEMPBonusListAsync();
            return StatusCode((int)result.Code, new ApiFetchAndResponse { Status = result.Status, Message = result.Message, Data = result.Data });
        }


        [HttpPost("UploadPayrollWithChallan")]
        public async Task<IActionResult> UploadPayrollWithChallan(IFormFile excelFile, IFormFile challanPdf, string monthYear)
        {
            if (!DateTime.TryParseExact(
                    monthYear?.Trim(),
                    "MMM-yy",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out _))
            {
                return BadRequest(new
                {
                    Status = false,
                    Message = "Invalid MonthYear format. Expected MMM-yy (e.g. Jun-26)"
                });
            }

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


            var result = await _service.UploadPayrollWithChallanAsync(excelFile, challanPdf, monthYear.Trim(), userClaims);

            return StatusCode((int)result.Code, result);
        }

        [HttpGet("GetEmployeePayroll")]
        public async Task<IActionResult> GetEmployeePayroll([FromQuery] string? monthYear, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10, [FromQuery] string searchTerm = "")
        {
            if (!string.IsNullOrWhiteSpace(monthYear))
            {
                if (!DateTime.TryParseExact(
                        monthYear,
                        "MMM-yy",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out _))
                {
                    return BadRequest(new
                    {
                        Status = false,
                        Message = "Invalid MonthYear format. Expected MMM-yy (e.g. Jun-26)"
                    });
                }
            }

            var result = await _service.GetEmployeePayrollAsync(monthYear, pageNumber, pageSize, searchTerm);

            return Ok(new
            {
                Status = true,
                result.TotalCount,
                result.PageNumber,
                result.PageSize,
                Data = result.Data
            });
        }


        [HttpPost("UploadEmployeeESIC")]
        public async Task<IActionResult> UploadEmployeeESIC([FromForm] IFormFile excelFile)
        {
            if (excelFile == null || excelFile.Length == 0)
            {
                return BadRequest(new
                {
                    Status = false,
                    Message = "Excel file is required"
                });
            }

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
            var result = await _service.UploadESICFromExcelAsync(excelFile, userClaims);

            return StatusCode((int)result.Code, new
            {
                Status = result.Status,
                Message = result.Message
            });
        }

        [HttpGet("GetEmployeeESIC")]
        public async Task<IActionResult> GetEmployeeESIC([FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var result = await _service.GetEmployeeESICAsync(
                searchTerm,
                pageNumber,
                pageSize
            );

            return Ok(new
            {
                Status = true,
                TotalCount = result.TotalCount,
                PageNumber = result.PageNumber,
                PageSize = result.PageSize,
                Data = result.Data
            });
        }
        [HttpPost("UploadRetention")]
        public async Task<IActionResult> UploadRetention([FromForm] IFormFile excelFile)
        {
            if (excelFile == null || excelFile.Length == 0)
            {
                return BadRequest(new
                {
                    Status = false,
                    Message = "Excel file is required"
                });
            }

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
            var result = await _service.UploadRetentionAsync(excelFile,userClaims);
            return StatusCode((int)result.Code, new
            {
                Status = result.Status,
                Message = result.Message
            });
        }
        [HttpGet("GetRetention")]
        public async Task<IActionResult> GetRetention(int pageNumber = 1,int pageSize = 10,string searchTerm = null,bool isExcel = false)
        {
            var result = await _service.GetRetentionAsync(
                pageNumber, pageSize, searchTerm, isExcel);

            if (isExcel)
            {
                return File(
                    result.ExcelBytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "Retention.xlsx");
            }

            return Ok(new
            {
                Retentions = result.Data,
                TotalCount = result.TotalCount
            });
        }

    }
} 