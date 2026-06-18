using Azure.Core;
using DocumentFormat.OpenXml.Office2016.Excel;
using HRMSAPI.Data;
using HRMSAPI.DTO;
using HRMSAPI.Extension;
using HRMSAPI.Implementation;
using HRMSAPI.Helpers;
using HRMSAPI.Interfaces;
using HRMSAPI.Models.Candidate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Asn1.Ocsp;
using Roomsy.DTOS.GenericsResponses;
using Serilog.Filters;
using System.Net;
using System.Security.Claims;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using HRMSAPI.Helpers;

namespace HRMSAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    // NOTE: controller-level RequirePageAccess removed —
    // GetEmployeeOrCandidateById and similar are called by MyProfile etc.
    // Apply per-method gates to admin-only actions when needed.
    public class EmployeeNewController : ControllerBase
    {
        private readonly HRMSContext _context;
        private readonly string savePath = Path.Combine("wwwroot");
        private readonly IEmployeeServiceNew _uow;
        private readonly IEmployeeSalaryAddOnsService _salaryAddOnsService;
        private readonly IEmployeeDeductionService _deductionService;
        private readonly ILogger<EmployeeNewController> _logger;

        public EmployeeNewController(HRMSContext context, IEmployeeServiceNew uow, IEmployeeSalaryAddOnsService service, IEmployeeDeductionService deductionService, ILogger<EmployeeNewController> logger)
        {
            _uow = uow;
            _context = context;
            _salaryAddOnsService = service;
            _deductionService = deductionService;
            _logger = logger;
        }

        [HttpGet("GetEmployee_New")]
        public async Task<IActionResult> Get(int pageNumber = 1, int pageSize = 10, string searchTerm = "")
        {
            _logger.LogInformation("Fetching employee list with pageNumber: {PageNumber}, pageSize: {PageSize}, searchTerm: {SearchTerm}", pageNumber, pageSize, searchTerm);
            try
            {
                var identity = HttpContext.User.Identity as ClaimsIdentity;
                if (identity == null)
                {
                    _logger.LogWarning("Authentication failed: No identity provided");
                    return BadRequest("Authentication Fails");
                }

                var (employees, totalCount, currentPageNumber) = await _uow.EmployeeList(pageNumber, pageSize, searchTerm);
                _logger.LogInformation("Employee list fetched successfully. TotalCount: {TotalCount}, CurrentPageNumber: {CurrentPageNumber}", totalCount, currentPageNumber);

                return Ok(new
                {
                    Employees = employees,
                    TotalCount = totalCount,
                    CurrentPageNumber = currentPageNumber
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching employee list");
                return StatusCode(500, new { Status = false, Message = ex.Message });
            }
        }

        [HttpGet]
        [Route("GetEmployeeOrCandidateById"), Authorize]
        public async Task<IActionResult> GetEmployeeOrCandidateById([FromQuery] int candidateId, [FromQuery] bool isCandidate)
        {
            _logger.LogInformation("Fetching details for ID: {Id}, isCandidate: {IsCandidate}", candidateId, isCandidate);
            try
            {
                var userIdentity = User.Identity as ClaimsIdentity;
                if (userIdentity == null || !userIdentity.IsAuthenticated)
                {
                    _logger.LogWarning("Unauthorized access attempt for ID: {Id}", candidateId);
                    return Unauthorized(new
                    {
                        Status = false,
                        Message = "User is not authenticated"
                    });
                }

                var loginDetail = AuthenticUserDetails.GetCurrentUserDetails(userIdentity);
                var updatedBy = userIdentity.FindFirst("EmployeeId")?.Value;
                _logger.LogInformation("Processing GetEmployeeOrCandidateById for ID: {Id} by employee ID: {EmployeeId}", candidateId, updatedBy);

                var res = await _uow.GetEmployeeOrCandidateById(candidateId, isCandidate);
                _logger.LogInformation("GetEmployeeOrCandidateById completed for ID: {Id} with status: {Status}, StatusCode: {StatusCode}",
                    candidateId, res.Status, res.Code);

                return StatusCode((int)res.Code, new
                {
                    Status = res.Status,
                    Message = res.Message,
                    Data = res.Data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching details for ID: {Id}", candidateId);
                return BadRequest(new
                {
                    Status = false,
                    Message = ex.Message,
                    Data = ex.Data
                });
            }
        }

        [HttpPost]
        [Route("UpdateEmployee"), Authorize]
        public async Task<IActionResult> UpdateEmployee([FromForm] CandidateUpdate details, [FromForm] CandidateDocs files)
        {
            _logger.LogInformation("Updating employee");
            try
            {
                var userIdentity = User.Identity as ClaimsIdentity;
                if (userIdentity == null || !userIdentity.IsAuthenticated)
                {
                    _logger.LogWarning("Unauthorized access attempt for UpdateEmployee");
                    return Unauthorized(new
                    {
                        Status = false,
                        Message = "User is not authenticated"
                    });
                }

                var loginDetail = AuthenticUserDetails.GetCurrentUserDetails(userIdentity);
                var updatedBy = userIdentity.FindFirst("EmployeeId")?.Value;
                _logger.LogInformation("Processing UpdateEmployee for employee ID: {EmployeeId}", updatedBy);

                if (details == null)
                {
                    _logger.LogWarning("UpdateEmployee failed: Employee details are required");
                    return BadRequest(new { Status = false, Message = "Employee details are required" });
                }

                // Validate required fields: account, ifsc, storecode, aadhar, pan
                var validationResult = await EmployeeValidationHelper.ValidateEmployeeFieldsAsync(details, _context, isInitialPost: false);
                
                if (!validationResult.IsValid)
                {
                    _logger.LogWarning("Validation failed for field {FieldName}: {Message}", validationResult.FieldName, validationResult.Message);
                    return BadRequest(new
                    {
                        Status = false,
                        Message = validationResult.Message,
                        FieldName = validationResult.FieldName
                    });
                }

                var validationError = DocumentValidationHelper.ValidateCandidateDocuments(files);
                if (validationError != null)
                {
                    _logger.LogWarning("Document validation failed while updating employee ID: {EmployeeId}. Error: {Message}", updatedBy, validationError.Message);
                    return BuildFileValidationErrorResponse(validationError);
                }

                var result = await _uow.UpdateEmployee(details, files, updatedBy);
                _logger.LogInformation("UpdateEmployee completed for employee ID: {EmployeeId} with status: {Status}, StatusCode: {StatusCode}",
                    updatedBy, result.Status, result.Code);

                return StatusCode((int)result.Code, new
                {
                    Status = result.Status,
                    Message = result.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating employee for employee ID: {EmployeeId}", HttpContext.User.FindFirst("EmployeeId")?.Value);
                return StatusCode(500, new { Status = false, Message = ex.Message });
            }
        }

        [HttpPost]
        [Route("ValidateEmployeeOnInitialPost"), Authorize]
        public async Task<IActionResult> ValidateEmployeeOnInitialPost([FromBody] CandidateUpdate details)
        {
            _logger.LogInformation("Validating employee on initial post");
            try
            {
                var userIdentity = User.Identity as ClaimsIdentity;
                if (userIdentity == null || !userIdentity.IsAuthenticated)
                {
                    _logger.LogWarning("Unauthorized access attempt for ValidateEmployeeOnInitialPost");
                    return Unauthorized(new
                    {
                        Status = false,
                        Message = "User is not authenticated"
                    });
                }

                if (details == null)
                {
                    _logger.LogWarning("ValidateEmployeeOnInitialPost failed: Employee details are required");
                    return BadRequest(new { Status = false, Message = "Employee details are required" });
                }

                // Perform validation for initial post
                var validationResult = await EmployeeValidationHelper.ValidateEmployeeFieldsAsync(details, _context, isInitialPost: true);
                
                if (!validationResult.IsValid)
                {
                    _logger.LogWarning("Validation failed for field {FieldName}: {Message}", validationResult.FieldName, validationResult.Message);
                    return BadRequest(new
                    {
                        Status = false,
                        Message = validationResult.Message,
                        FieldName = validationResult.FieldName
                    });
                }

                _logger.LogInformation("Employee validation passed successfully");
                return Ok(new
                {
                    Status = true,
                    Message = "All validations passed successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating employee on initial post");
                return StatusCode(500, new { Status = false, Message = "An error occurred while validating employee data" });
            }
        }

        [HttpPost]
        [Route("UpdateEmployeeStatus"), Authorize]
        public async Task<IActionResult> UpdateEmployeeStatus([FromBody] EmployeeStatusUpdateRequest request)
        {
            _logger.LogInformation("Updating employee status for request ID: {Id}", request?.id);
            try
            {
                var userIdentity = User.Identity as ClaimsIdentity;
                if (userIdentity == null || !userIdentity.IsAuthenticated)
                {
                    _logger.LogWarning("Unauthorized access attempt for UpdateEmployeeStatus");
                    return Unauthorized(new
                    {
                        Status = false,
                        Message = "User is not authenticated"
                    });
                }

                var loginDetail = AuthenticUserDetails.GetCurrentUserDetails(userIdentity);
                var updatedBy = userIdentity.FindFirst("EmployeeId")?.Value;
                _logger.LogInformation("Processing UpdateEmployeeStatus for employee ID: {EmployeeId}", updatedBy);

                request.lastUpdatedBy = updatedBy;
                var result = await _uow.UpdateEmployeeStatus(request);
                _logger.LogInformation("UpdateEmployeeStatus completed for employee ID: {EmployeeId} with status: {Status}, StatusCode: {StatusCode}",
                    updatedBy, result.Status, result.Code);

                return StatusCode((int)result.Code, new
                {
                    Status = result.Status,
                    Message = result.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating employee status for employee ID: {EmployeeId}", HttpContext.User.FindFirst("EmployeeId")?.Value);
                return StatusCode(500, new { Status = false, Message = ex.Message });
            }
        }

        [HttpGet, Route("GetInActiveStatusList")]
        public async Task<IActionResult> GetInActiveStatusList()
        {
            _logger.LogInformation("Fetching inactive status list");
            try
            {
                var result = await _uow.GetInActiveStatusList();
                _logger.LogInformation("GetInActiveStatusList completed with status: {Status}, StatusCode: {StatusCode}", result.Status, result.Code);

                return StatusCode((int)result.Code, new
                {
                    Status = result.Status,
                    Message = result.Message,
                    Data = result.Data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching inactive status list");
                return StatusCode(500, new { Status = false, Message = ex.Message });
            }
        }

        [HttpPost]
        [Route("UpdateEmployeeWithExcel"), Authorize]
        public async Task<IActionResult> UpdateEmployeeWithExcel([FromForm] IFormFile file)
        {
            _logger.LogInformation("Updating employee with Excel file");
            try
            {
                var userIdentity = User.Identity as ClaimsIdentity;
                if (userIdentity == null || !userIdentity.IsAuthenticated)
                {
                    _logger.LogWarning("Unauthorized access attempt for UpdateEmployeeWithExcel");
                    return Unauthorized(new
                    {
                        Status = false,
                        Message = "User is not authenticated"
                    });
                }

                var loginDetail = AuthenticUserDetails.GetCurrentUserDetails(userIdentity);
                var updatedBy = userIdentity.FindFirst("EmployeeId")?.Value;
                _logger.LogInformation("Processing UpdateEmployeeWithExcel for employee ID: {EmployeeId}", updatedBy);

                var result = await _uow.UpdateEmployeeWithExcel(file, updatedBy);
                _logger.LogInformation("UpdateEmployeeWithExcel completed for employee ID: {EmployeeId} with status: {Status}, StatusCode: {StatusCode}",
                    updatedBy, result.Status, result.Code);

                return StatusCode((int)result.Code, new
                {
                    Status = result.Status,
                    Message = result.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating employee with Excel for employee ID: {EmployeeId}", HttpContext.User.FindFirst("EmployeeId")?.Value);
                return StatusCode(500, new { Status = false, Message = ex.Message });
            }
        }

        [HttpPost]
        [Route("BulkInsertEmployees"), Authorize]
        public async Task<IActionResult> BulkInsertEmployees([FromForm] IFormFile file)
        {
            _logger.LogInformation("Bulk inserting employees from Excel file");
            try
            {
                var userIdentity = User.Identity as ClaimsIdentity;
                if (userIdentity == null || !userIdentity.IsAuthenticated)
                {
                    return Unauthorized(new { Status = false, Message = "User is not authenticated" });
                }

                var createdBy = userIdentity.FindFirst("EmployeeId")?.Value;
                var result = await _uow.BulkInsertEmployeesWithExcel(file, createdBy);

                return StatusCode((int)result.Code, new
                {
                    Status = result.Status,
                    Message = result.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bulk inserting employees");
                return StatusCode(500, new { Status = false, Message = ex.Message });
            }
        }

        [HttpPost("BulkInactivateEmployees"), Authorize]
        public async Task<IActionResult> BulkInactivateEmployees([FromForm] BulkInactivateRequest request)
        {
            _logger.LogInformation("BulkInactivateEmployees invoked");
            try
            {
                var identity = User.Identity as ClaimsIdentity;
                if (identity == null || !identity.IsAuthenticated)
                {
                    return Unauthorized(new { Status = false, Message = "User is not authenticated" });
                }

                if (string.IsNullOrWhiteSpace(request.LastUpdatedBy))
                {
                    request.LastUpdatedBy = identity.FindFirst("EmployeeId")?.Value;
                }

                var result = await _uow.BulkInactivateEmployees(request);
                _logger.LogInformation("BulkInactivateEmployees completed with Status={Status} Code={Code}", result.Status, result.Code);

                return StatusCode((int)result.Code, new
                {
                    Status = result.Status,
                    Message = result.Message,
                    Data = (object?)null
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in BulkInactivateEmployees");
                return StatusCode((int)HttpStatusCode.InternalServerError, new
                {
                    Status = false,
                    Message = "An unexpected error occurred.",
                    Data = (object?)null
                });
            }
        }

        [HttpPost("UpdateEmployeeStatusWithAttachment"), Authorize]
        public async Task<IActionResult> UpdateEmployeeStatus(EmployeeStatusUpdateWithReasonAndAttachmentRequest request)
        {
            _logger.LogInformation("Updating employee status with attachment for employee ID: {Id}", request?.id);
            try
            {
                if (request == null || request.id <= 0)
                {
                    _logger.LogWarning("Invalid request: Employee ID is required for UpdateEmployeeStatusWithAttachment");
                    return StatusCode((int)HttpStatusCode.BadRequest, new
                    {
                        Status = false,
                        Message = "Invalid request: Employee ID is required.",
                        Data = (object?)null
                    });
                }

                if (request.inactiveattachment != null && request.inactiveattachment.Any(f => f.Length > 10 * 1024 * 1024))
                {
                    _logger.LogWarning("File size exceeds 10MB limit for employee ID: {Id}", request.id);
                    return StatusCode((int)HttpStatusCode.BadRequest, new
                    {
                        Status = false,
                        Message = "File size exceeds 10MB limit.",
                        Data = (object?)null
                    });
                }

                var result = await _uow.UpdateEmployeeStatusWithReasonAndAttachment(request);
                _logger.LogInformation("UpdateEmployeeStatusWithAttachment completed for employee ID: {Id} with status: {Status}, StatusCode: {StatusCode}",
                    request.id, result.Status, result.Code);

                return StatusCode((int)result.Code, new
                {
                    Status = result.Status,
                    Message = result.Message,
                    Data = (object?)null
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating employee status with attachment for employee ID: {Id}", request?.id);
                return StatusCode((int)HttpStatusCode.InternalServerError, new
                {
                    Status = false,
                    Message = "An unexpected error occurred.",
                    Data = (object?)null
                });
            }
        }

        [HttpPost("upload_salaryaddoons")]
        public async Task<IActionResult> UploadSalaryAddOns([FromForm] IFormFile file)
        {
            _logger.LogInformation("Uploading salary add-ons");
            try
            {
                var userIdentity = User.Identity as ClaimsIdentity;
                if (userIdentity == null || !userIdentity.IsAuthenticated)
                {
                    _logger.LogWarning("Unauthorized access attempt for UploadSalaryAddOns");
                    return Unauthorized(new
                    {
                        Status = false,
                        Message = "User is not authenticated"
                    });
                }

                var loginDetail = AuthenticUserDetails.GetCurrentUserDetails(userIdentity);
                var updatedBy = userIdentity.FindFirst("EmployeeId")?.Value;
                _logger.LogInformation("Processing UploadSalaryAddOns for employee ID: {EmployeeId}", updatedBy);

                var res = await _salaryAddOnsService.UploadSalaryAddOnsExcel(file, updatedBy);
                _logger.LogInformation("UploadSalaryAddOns completed for employee ID: {EmployeeId} with status: {Status}, StatusCode: {StatusCode}",
                    updatedBy, res.Status, res.Code);

                return StatusCode((int)res.Code, new ApiExecuteAndReponse
                {
                    Status = res.Status,
                    Message = res.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading salary add-ons for employee ID: {EmployeeId}", HttpContext.User.FindFirst("EmployeeId")?.Value);
                return StatusCode(500, new { Status = false, Message = ex.Message });
            }
        }

        [HttpPost("upload_employeededuction")]
        public async Task<IActionResult> UploadEmployeeDeductionExcel([FromForm] IFormFile file)
        {
            _logger.LogInformation("Uploading employee deduction Excel");
            try
            {
                var userIdentity = User.Identity as ClaimsIdentity;
                if (userIdentity == null || !userIdentity.IsAuthenticated)
                {
                    _logger.LogWarning("Unauthorized access attempt for UploadEmployeeDeductionExcel");
                    return Unauthorized(new
                    {
                        Status = false,
                        Message = "User is not authenticated"
                    });
                }

                var loginDetail = AuthenticUserDetails.GetCurrentUserDetails(userIdentity);
                var updatedBy = userIdentity.FindFirst("EmployeeId")?.Value;
                _logger.LogInformation("Processing UploadEmployeeDeductionExcel for employee ID: {EmployeeId}", updatedBy);

                var res = await _deductionService.UploadEmployeeDeductionExcel(file, updatedBy);
                _logger.LogInformation("UploadEmployeeDeductionExcel completed for employee ID: {EmployeeId} with status: {Status}, StatusCode: {StatusCode}",
                    updatedBy, res.Status, res.Code);

                return StatusCode((int)res.Code, new ApiExecuteAndReponse
                {
                    Status = res.Status,
                    Message = res.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading employee deduction Excel for employee ID: {EmployeeId}", HttpContext.User.FindFirst("EmployeeId")?.Value);
                return StatusCode(500, new { Status = false, Message = ex.Message });
            }
        }

        [HttpGet("employeesbymanager")]
        public async Task<ActionResult<(List<GetEmployeeDetailsResultNew> Employees, long TotalCount, int CurrentPageNumber)>> GetEmployeeDetailsByManagerId(
            [FromQuery] long managerId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string searchTerm = null)
        {
            _logger.LogInformation("Fetching employees by manager ID: {ManagerId}, pageNumber: {PageNumber}, pageSize: {PageSize}, searchTerm: {SearchTerm}",
                managerId, pageNumber, pageSize, searchTerm);
            try
            {
                if (pageNumber < 1 || pageSize < 1)
                {
                    _logger.LogWarning("Invalid page number ({PageNumber}) or page size ({PageSize}) for manager ID: {ManagerId}", pageNumber, pageSize, managerId);
                    return BadRequest("Page number and page size must be greater than 0.");
                }

                var (employees, totalCount, currentPageNumber) = await _uow.GetEmployeeDetailsByManagerIdAsync(managerId, pageNumber, pageSize, searchTerm);
                _logger.LogInformation("GetEmployeeDetailsByManagerId completed for manager ID: {ManagerId}. TotalCount: {TotalCount}, CurrentPageNumber: {CurrentPageNumber}",
                    managerId, totalCount, currentPageNumber);

                return Ok(new { Employees = employees, TotalCount = totalCount, CurrentPageNumber = currentPageNumber });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching employees for manager ID: {ManagerId}", managerId);
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }



        [HttpGet("GetEmployeeDetailsWithCards_Test")]
        public async Task<IActionResult> GetEmployeeDetailsWithCards_Test(string? managerId,int pageNumber = 1, int pageSize = 10, string searchTerm = "", string mode = "all")
        {
            _logger.LogInformation("Fetching employee list with cards (test version) - pageNumber: {PageNumber}, pageSize: {PageSize}, searchTerm: {SearchTerm}, mode: {Mode}", pageNumber, pageSize, searchTerm, mode);
            try
            {
                var identity = HttpContext.User.Identity as ClaimsIdentity;
                if (identity == null)
                {
                    _logger.LogWarning("Authentication failed: No identity provided");
                    return BadRequest("Authentication Fails");
                }

                // Validate mode parameter ('mainview' = active+absconded only, for the Employee Master active view)
                if (!new[] { "active", "inactive", "all", "mainview" }.Contains(mode.ToLower()))
                {
                    _logger.LogWarning("Invalid mode parameter: {Mode}", mode);
                    return BadRequest("Invalid mode. Use 'active', 'inactive', 'all', or 'mainview'.");
                }

                var (employees, totalCount, currentPageNumber, activeCount, inactiveCount, abscondCnt, locCountt) = await _uow.EmployeeListWithCards_Test(managerId,pageNumber, pageSize, searchTerm, mode );
                _logger.LogInformation("Employee list with cards (test version) fetched successfully. TotalCount: {TotalCount}, CurrentPageNumber: {CurrentPageNumber}, ActiveCount: {ActiveCount}, InactiveCount: {InactiveCount}", totalCount, currentPageNumber, activeCount, inactiveCount);

                return Ok(new
                {
                    Employees = employees,
                    TotalCount = totalCount,
                    CurrentPageNumber = currentPageNumber,
                    Cards = new
                    {
                        ActiveCount = activeCount,
                        InactiveCount = inactiveCount,
                        TotalCount = totalCount,
                        abscondCnt = abscondCnt,
                        locCountt = locCountt
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching employee list with cards (test version) - pageNumber: {PageNumber}, pageSize: {PageSize}, searchTerm: {SearchTerm}, mode: {Mode}", pageNumber, pageSize, searchTerm, mode);
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }

        [HttpGet("GetEmployeeDetailsWithCards")]
        public async Task<IActionResult> GetEmployeeDetailsWithCards(int pageNumber = 1, int pageSize = 10, string searchTerm = "", string mode = "all")
        {
            _logger.LogInformation("Fetching employee list with cards - pageNumber: {PageNumber}, pageSize: {PageSize}, searchTerm: {SearchTerm}, mode: {Mode}", pageNumber, pageSize, searchTerm, mode);
            try
            {
                var identity = HttpContext.User.Identity as ClaimsIdentity;
                if (identity == null)
                {
                    _logger.LogWarning("Authentication failed: No identity provided");
                    return BadRequest("Authentication Fails");
                }

                // Validate mode parameter
                if (!new[] { "active", "inactive", "all" }.Contains(mode.ToLower()))
                {
                    _logger.LogWarning("Invalid mode parameter: {Mode}", mode);
                    return BadRequest("Invalid mode. Use 'active', 'inactive', or 'all'.");
                }

                var (employees, totalCount, currentPageNumber, activeCount, inactiveCount, abscondCnt, locCountt) = await _uow.EmployeeListWithCards(pageNumber, pageSize, searchTerm, mode);
                _logger.LogInformation("Employee list with cards fetched successfully. TotalCount: {TotalCount}, CurrentPageNumber: {CurrentPageNumber}, ActiveCount: {ActiveCount}, InactiveCount: {InactiveCount}", totalCount, currentPageNumber, activeCount, inactiveCount);

                return Ok(new
                {
                    Employees = employees,
                    TotalCount = totalCount,
                    CurrentPageNumber = currentPageNumber,
                    Cards = new
                    {
                        ActiveCount = activeCount,
                        InactiveCount = inactiveCount,
                        TotalCount = totalCount,
                        abscondCnt= abscondCnt,
                        locCountt= locCountt
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching employee list with cards");
                return StatusCode(500, new { Status = false, Message = ex.Message });
            }
        }

        [HttpGet("GetEmployee_HoldList")]
        public async Task<IActionResult> GetEmployee_HoldList(int pageNumber = 1, int pageSize = 10, string searchTerm = "")
        {
            _logger.LogInformation("Fetching employee list with pageNumber: {PageNumber}, pageSize: {PageSize}, searchTerm: {SearchTerm}", pageNumber, pageSize, searchTerm);
            try
            {
                var identity = HttpContext.User.Identity as ClaimsIdentity;
                if (identity == null)
                {
                    _logger.LogWarning("Authentication failed: No identity provided");
                    return BadRequest("Authentication Fails");
                }

                var (employees, totalCount, currentPageNumber) = await _uow.EmployeeList(pageNumber, pageSize, searchTerm);
                _logger.LogInformation("Employee list fetched successfully. TotalCount: {TotalCount}, CurrentPageNumber: {CurrentPageNumber}", totalCount, currentPageNumber);

                return Ok(new
                {
                    Employees = employees,
                    TotalCount = totalCount,
                    CurrentPageNumber = currentPageNumber
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching employee list");
                return StatusCode(500, new { Status = false, Message = ex.Message });
            }
        }

        [HttpGet("RefreshEmpDetails")]
        public async Task<IActionResult> RefreshEmpDetails(string eCode)
        {
            var res = await _uow.RefreshEmpDetails(eCode);

            return StatusCode((int)res.Code, new ApiExecuteAndReponse
            {
                Status = res.Status,
                Message = res.Message
            });
        }

        [HttpPost]
        [Route("UpdateEmployeeDetails"), Authorize]
        public async Task<IActionResult> UpdateEmployeeDetails([FromBody] CandidateRequest empUpdateDetails)
        {
            _logger.LogInformation("Updating employee details");
            try
            {
                var userIdentity = User.Identity as ClaimsIdentity;
                if (userIdentity == null || !userIdentity.IsAuthenticated)
                {
                    _logger.LogWarning("Unauthorized access attempt for UpdateEmployeeDetails");
                    return Unauthorized(new
                    {
                        Status = false,
                        Message = "User is not authenticated"
                    });
                }

                var loginDetail = AuthenticUserDetails.GetCurrentUserDetails(userIdentity);
                var updatedBy = userIdentity.FindFirst("EmployeeId")?.Value;
                _logger.LogInformation("Processing UpdateEmployee for employee ID: {EmployeeId}", updatedBy);

                var result = await _uow.UpdateEmployeeDetails(empUpdateDetails, updatedBy);
                _logger.LogInformation("UpdateEmployee completed for employee ID: {EmployeeId} with status: {Status}, StatusCode: {StatusCode}",
                    updatedBy, result.Status, result.Code);

                return StatusCode((int)result.Code, new
                {
                    Status = result.Status,
                    Message = result.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating employee for employee ID: {EmployeeId}", HttpContext.User.FindFirst("EmployeeId")?.Value);
                return StatusCode(500, new { Status = false, Message = ex.Message });
            }
        }
        /*
        [HttpGet]
        [Route("GetAllEmployeeDetails")]
        [Authorize]
        public async Task<IActionResult> GetAllEmployeeDetails()
        {
            try
            {
                _logger.LogInformation("Fetching all employee details (original and updated)");
                var userIdentity = User.Identity as ClaimsIdentity;
                if (userIdentity == null || !userIdentity.IsAuthenticated)
                {
                    _logger.LogWarning("Unauthorized access attempt for UpdateEmployeeDetails");
                    return Unauthorized(new
                    {
                        Status = false,
                        Message = "User is not authenticated"
                    });
                }

                var loginDetail = AuthenticUserDetails.GetCurrentUserDetails(userIdentity);
                var updatedBy = userIdentity.FindFirst("EmployeeId")?.Value;
                _logger.LogInformation("Processing Fetching all employee details (original and updated) by employee ID: {EmployeeId}", updatedBy);
                var result = await _uow.GetAllEmployeeUpdateComparisonsAsync();
                _logger.LogInformation("Fetching all employee details (original and updated) is completted by employee ID: {EmployeeId} with status: {Status}, StatusCode: {StatusCode}",
                    updatedBy);

                return Ok(new
                {
                    Status = true,
                    Message = "Employee details fetched successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching employee details");
                return StatusCode(500, new { Status = false, Message = "Internal Server Error", Error = ex.Message });
            }
        }
        */

        [HttpGet]
        [Route("GetPendingUpdateEmployees")]
        [Authorize]
        public async Task<IActionResult> GetPendingUpdateEmployees()
        {
            try
            {
                _logger.LogInformation("Fetching pending employee update requests...");
                var userIdentity = User.Identity as ClaimsIdentity;
                if (userIdentity == null || !userIdentity.IsAuthenticated)
                {
                    _logger.LogWarning("Unauthorized access attempt for UpdateEmployeeDetails");
                    return Unauthorized(new
                    {
                        Status = false,
                        Message = "User is not authenticated"
                    });
                }

                var loginDetail = AuthenticUserDetails.GetCurrentUserDetails(userIdentity);
                var updatedBy = userIdentity.FindFirst("EmployeeId")?.Value;
                _logger.LogInformation("Processing Fetching pending employee update requests by employee ID: {EmployeeId}", updatedBy);
                var result = await _uow.GetPendingEmployeeUpdateListAsync();

                _logger.LogInformation("Fetching pending employee update requests is completted by employee ID: {EmployeeId} with status: {Status}, StatusCode: {StatusCode}",
                    updatedBy);

                return Ok(new
                {
                    Status = true,
                    Message = "Pending update employees fetched successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching pending employee updates");
                return StatusCode(500, new { Status = false, Message = "Internal Server Error", Error = ex.Message });
            }
        }

        [HttpGet]
        [Route("GetEmployeeDetailsUpdateView")]
        [Authorize]
        public async Task<IActionResult> GetEmployeeDetailsUpdateView([FromQuery] long EmployeeId)
        {
            try
            {
                _logger.LogInformation("Fetching employee Details update view requests...");
                var userIdentity = User.Identity as ClaimsIdentity;
                if (userIdentity == null || !userIdentity.IsAuthenticated)
                {
                    _logger.LogWarning("Unauthorized access attempt for GetEmployeeDetailsUpdateView");
                    return Unauthorized(new
                    {
                        Status = false,
                        Message = "User is not authenticated"
                    });
                }

                var loginDetail = AuthenticUserDetails.GetCurrentUserDetails(userIdentity);
                var updatedBy = userIdentity.FindFirst("EmployeeId")?.Value;
                _logger.LogInformation("Processing Fetching employee update requests by employee ID: {EmployeeId}", updatedBy);
                var result = await _uow.GetChangedFieldsForEmployeeAsync(EmployeeId);

                _logger.LogInformation("Fetching employee update requests is completted by employee ID: {EmployeeId} with status: {Status}, StatusCode: {StatusCode}",
                    updatedBy);

                return Ok(new
                {
                    Status = true,
                    Message = "employee Details update view fetched successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching employee Details update view");
                return StatusCode(500, new { Status = false, Message = "Internal Server Error", Error = ex.Message });
            }
        }

        [HttpPost]
        [Route("UpdateEmployeeApprovedDetails"), Authorize]
        public async Task<IActionResult> UpdateEmployeeApprovedDetails([FromBody] EmployeeDetailsUpdateView employeeDetailsUpdateView)
        {
            _logger.LogInformation("Updating employee Approved details");
            try
            {
                var userIdentity = User.Identity as ClaimsIdentity;
                if (userIdentity == null || !userIdentity.IsAuthenticated)
                {
                    _logger.LogWarning("Unauthorized access attempt for UpdateEmployeeApprovedDetails");
                    return Unauthorized(new
                    {
                        Status = false,
                        Message = "User is not authenticated"
                    });
                }

                var loginDetail = AuthenticUserDetails.GetCurrentUserDetails(userIdentity);
                var updatedBy = userIdentity.FindFirst("EmployeeId")?.Value;
                _logger.LogInformation("Processing UpdateEmployee Approved Details for employee ID: {EmployeeId}", updatedBy);

                var result = await _uow.UpdateEmployeeApprovedDetails(employeeDetailsUpdateView, employeeDetailsUpdateView.EmployeeId, updatedBy);
                _logger.LogInformation("UpdateEmployee Approved Details completed for employee ID: {EmployeeId} with status: {Status}, StatusCode: {StatusCode}",
                    updatedBy, result.Status, result.Code);

                return StatusCode((int)result.Code, new
                {
                    Status = result.Status,
                    Message = result.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UpdateEmployee Approved Details for employee ID: {EmployeeId}", HttpContext.User.FindFirst("EmployeeId")?.Value);
                return StatusCode(500, new { Status = false, Message = ex.Message });
            }
        }

        private IActionResult BuildFileValidationErrorResponse(FileValidationError error)
        {
            var statusCode = error.IsFileSizeViolation ? StatusCodes.Status413PayloadTooLarge : StatusCodes.Status400BadRequest;

            return StatusCode(statusCode, new
            {
                Status = false,
                Message = error.Message,
                AllowedFileTypes = DocumentValidationHelper.AllowedDocumentExtensions,
                MaxFileSizeInBytes = DocumentValidationHelper.MaxFileSizeBytes,
                MaxFileSize = DocumentValidationHelper.MaxFileSizeDisplay
            });
        }
    }
}