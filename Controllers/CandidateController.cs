using HRMSAPI.DTO;
using HRMSAPI.Extension;
using HRMSAPI.Interfaces;
using HRMSAPI.Models.Candidate;
using Candidate = HRMSAPI.Models.Candidate.Candidate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using HRMSAPI.Models.EvalutionForm;
using Microsoft.Extensions.Configuration;
using System.Data;
using HRMSAPI.Implementation;
using Microsoft.AspNetCore.Http.HttpResults;
using System.Net;
using HRMSAPI.Helpers;

namespace ASN.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    // NOTE: controller-level RequirePageAccess removed —
    // GetCandidateDetails and Insertnewcandidate/Updatecandidate are called
    // from MyProfile (user editing own candidate record). Apply per-method
    // gates to admin-only actions when needed.
    public class CandidateController : ControllerBase
    {
        private readonly ICandidateService _candidateService;
        private readonly ILogger<CandidateController> _logger;

        public CandidateController(ICandidateService candidateService, ILogger<CandidateController> logger)
        {
            _candidateService = candidateService;
            _logger = logger;
        }

        [HttpPost("InsertCandidateWithDocs")]
        public async Task<IActionResult> InsertCandidateWithDocs([FromForm] Candidate candidate, [FromForm] CandidateDocs files)
        {
            _logger.LogInformation("Inserting candidate with docs for employee ID: {EmployeeId}", HttpContext.User.FindFirst("EmployeeId")?.Value);
            try
            {
                var fileValidationError = DocumentValidationHelper.ValidateCandidateDocuments(files);
                if (fileValidationError != null)
                {
                    _logger.LogWarning("Document validation failed while inserting candidate: {Message}", fileValidationError.Message);
                    return BuildFileValidationErrorResponse(fileValidationError);
                }

                var identity = HttpContext.User.Identity as ClaimsIdentity;
                var loginDetail = AuthenticUserDetails.GetCurrentUserDetails(identity);
                _logger.LogInformation("Processing InsertCandidateWithDocs for employee ID: {EmployeeId}", loginDetail.EmployeeId);

                var res = await _candidateService.InsertCandidateWithDocs(candidate, files, loginDetail.EmployeeId);
                _logger.LogInformation("InsertCandidateWithDocs completed for employee ID: {EmployeeId} with status: {Status}, StatusCode: {StatusCode}",
                    loginDetail.EmployeeId, res.Status, res.StatusCode);

                return StatusCode((int)res.StatusCode, new
                {
                    Status = res.Status,
                    Message = res.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inserting candidate with docs for employee ID: {EmployeeId}", HttpContext.User.FindFirst("EmployeeId")?.Value);
                return BadRequest(new
                {
                    Status = false,
                    Message = ex.Message
                });
            }
        }
        [HttpGet("GetScheduleInterviewDetailsById")]
        public async Task<IActionResult> GetScheduleInterviewDetailsById(int scheduleId)
        {
            _logger.LogInformation("Fetching interview schedule data for Schedule ID: {ScheduleId}", scheduleId);
            try
            {
                var result = await _candidateService.GetScheduleInterviewDetailsById(scheduleId);

                if (result == null)
                {
                    _logger.LogWarning("Interview schedule data not found for Schedule ID: {ScheduleId}", scheduleId);
                    return NotFound(new { message = $"Schedule ID {scheduleId} not found." });
                }

                _logger.LogInformation("Interview schedule data fetched successfully for Schedule ID: {ScheduleId}", scheduleId);
                return Ok(result);
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Database error fetching interview schedule data for Schedule ID: {ScheduleId}", scheduleId);
                return StatusCode(500, new { message = "Database error occurred.", error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching interview schedule data for Schedule ID: {ScheduleId}", scheduleId);
                return StatusCode(500, new { message = "An unexpected error occurred.", error = ex.Message });
            }
        }
        [HttpGet("GetCandidateList"), Authorize]
        public async Task<IActionResult> GetCandidateList([FromQuery] int pageNumber, [FromQuery] int pageSize, string searchTerm = "")
        {
            _logger.LogInformation("Fetching candidate list with pageNumber: {PageNumber}, pageSize: {PageSize}, searchTerm: {SearchTerm}", pageNumber, pageSize, searchTerm);
            try
            {
                var identity = HttpContext.User.Identity as ClaimsIdentity;
                var loginDetail = AuthenticUserDetails.GetCurrentUserDetails(identity);
                var employeeId = loginDetail.EmployeeId;
                var role = loginDetail.role;

                if (pageNumber < 1 || pageSize < 1)
                {
                    _logger.LogWarning("Invalid page number ({PageNumber}) or page size ({PageSize})", pageNumber, pageSize);
                    return BadRequest(new
                    {
                        Status = false,
                        Message = "Invalid page number or page size. Both must be greater than zero.",
                        Data = new List<string>()
                    });
                }

                var res = await _candidateService.GetCandidateList(pageNumber, pageSize, searchTerm, Convert.ToInt64(employeeId), role);
                _logger.LogInformation("GetCandidateList completed for employee ID: {EmployeeId} with status: {Status}, StatusCode: {StatusCode}",
                    employeeId, res.Status, res.StatusCode);

                return StatusCode((int)res.StatusCode, new
                {
                    Status = res.Status,
                    Message = res.Message,
                    Data = res.Data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching candidate list for employee ID: {EmployeeId}", HttpContext.User.FindFirst("EmployeeId")?.Value);
                return BadRequest(new
                {
                    Status = false,
                    Message = ex.Message,
                    Data = ex.Data
                });
            }
        }

        [HttpGet]
        [Route("GetCandidateDetails"), Authorize]
        public async Task<IActionResult> GetCandidateDetails([FromQuery] int candidateId)
        {
            _logger.LogInformation("Fetching candidate details for candidate ID: {CandidateId}", candidateId);
            try
            {
                var userIdentity = User.Identity as ClaimsIdentity;
                if (userIdentity == null || !userIdentity.IsAuthenticated)
                {
                    _logger.LogWarning("Unauthorized access attempt for candidate ID: {CandidateId}", candidateId);
                    return Unauthorized(new
                    {
                        Status = false,
                        Message = "User is not authenticated"
                    });
                }

                var loginDetail = AuthenticUserDetails.GetCurrentUserDetails(userIdentity);
                var updatedBy = userIdentity.FindFirst("EmployeeId")?.Value;
                _logger.LogInformation("Processing GetCandidateDetails for candidate ID: {CandidateId} by employee ID: {EmployeeId}", candidateId, updatedBy);

                var res = await _candidateService.GetCandidateInfo(candidateId);
                _logger.LogInformation("GetCandidateDetails completed for candidate ID: {CandidateId} with status: {Status}, StatusCode: {StatusCode}",
                    candidateId, res.Status, res.StatusCode);

                return StatusCode((int)res.StatusCode, new
                {
                    Status = res.Status,
                    Message = res.Message,
                    Data = res.Data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching candidate details for candidate ID: {CandidateId}", candidateId);
                return BadRequest(new
                {
                    Status = false,
                    Message = ex.Message,
                    Data = ex.Data
                });
            }
        }

        [NonAction]
        private List<T> DeserializeList<T>(string json, JsonSerializerOptions options = null)
        {
            _logger.LogInformation("Deserializing JSON list for type: {Type}", typeof(T).Name);
            try
            {
                if (string.IsNullOrEmpty(json))
                {
                    _logger.LogWarning("JSON string is empty for type: {Type}", typeof(T).Name);
                    return new List<T>();
                }

                try
                {
                    var list = JsonSerializer.Deserialize<List<T>>(json, options);
                    _logger.LogInformation("Successfully deserialized JSON list for type: {Type}", typeof(T).Name);
                    return list ?? new List<T>();
                }
                catch (JsonException)
                {
                    try
                    {
                        var singleItem = JsonSerializer.Deserialize<T>(json, options);
                        _logger.LogInformation("Deserialized JSON as single item for type: {Type}", typeof(T).Name);
                        return singleItem != null ? new List<T> { singleItem } : new List<T>();
                    }
                    catch (JsonException)
                    {
                        _logger.LogWarning("Failed to deserialize JSON for type: {Type}", typeof(T).Name);
                        return new List<T>();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deserializing JSON for type: {Type}", typeof(T).Name);
                return new List<T>();
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

        // "Freeze the budget": is there an unfilled BGT seat for this Store + Department +
        // Sub-Department(1/2/3) + Designation? Called while filling the candidate form (on
        // selection change) and re-checked server-side on submit — AllowAnonymous to match
        // Insertnewcandidate below, since candidates can apply via the public form.
        [HttpGet]
        [Route("CheckSeatAvailability")]
        [AllowAnonymous]
        public async Task<IActionResult> CheckSeatAvailability(
            [FromQuery] int locationId,
            [FromQuery] int departmentId,
            [FromQuery] int? subDepartmentId1,
            [FromQuery] int? subDepartmentId2,
            [FromQuery] int? subDepartmentId3,
            [FromQuery] int designationId,
            [FromQuery] decimal? salary,
            [FromQuery] long? excludeCandidateId)
        {
            try
            {
                var result = await _candidateService.CheckSeatAvailabilityAsync(
                    locationId, departmentId, subDepartmentId1, subDepartmentId2, subDepartmentId3,
                    designationId, salary, excludeCandidateId);

                return StatusCode((int)result.StatusCode, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking seat availability");
                return StatusCode(500, new { Status = false, Message = "An error occurred while checking seat availability" });
            }
        }

        [HttpPost]
        [Route("Insertnewcandidate")]
        [AllowAnonymous]
        [RequestFormLimits(MultipartBodyLengthLimit = 524288000)]
        public async Task<IActionResult> Insertnewcandidate([FromForm] CandidateUpdate details, [FromForm] CandidateDocs files)
        {
            _logger.LogInformation("Inserting new candidate");
            try
            {
                if (details == null)
                {
                    _logger.LogWarning("Insertnewcandidate failed: Candidate details are required");
                    return BadRequest(new { Status = false, Message = "Candidate details are required" });
                }

                _logger.LogInformation("Deserializing candidate lists");
                details.familyMembersList = DeserializeList<CandidateUpdateFamilyMember>(details.FamilyMembersListJson);
                details.experienceList = DeserializeList<CandidateUpdateExperience>(details.ExperienceListJson);
                details.qualificationList = DeserializeList<CandidateUpdateQualification>(details.QualificationListJson);
                details.assignLocations = DeserializeList<AssignLocationHistoryrecord>(details.AssignLocationsListJson);

                var candidateDocs = new CandidateDocs
                {
                    PassportPhoto = files?.PassportPhoto,
                    Last3SalarySlip = files?.Last3SalarySlip ?? new List<IFormFile>(),
                    Last3BankStatement = files?.Last3BankStatement,
                    PrevOfferLetter = files?.PrevOfferLetter,
                    PanAttachment = files?.PanAttachment ?? new List<IFormFile>(),
                    AadharAttachment = files?.AadharAttachment ?? new List<IFormFile>(),
                    AadharBackAttachment = files?.AadharBackAttachment ?? new List<IFormFile>(),
                    BankPassbookAttachment = files?.BankPassbookAttachment ?? new List<IFormFile>(),
                    EducationAttachment = files?.EducationAttachment ?? new List<IFormFile>(),
                    ResumeAttachment = files?.ResumeAttachment ?? new List<IFormFile>(),
                    EvaluationAttachment = files?.EvaluationAttachment ?? new List<IFormFile>(),
                    OfferLetterAttachment = files?.OfferLetterAttachment ?? new List<IFormFile>(),
                    InterviewVideo = files?.InterviewVideo ?? new List<IFormFile>(),
                    OtherAttachment = files?.OtherAttachment ?? new List<IFormFile>(),
                    BankStatementVideo = files?.BankStatementVideo ?? new List<IFormFile>(),
                    Form11Attachment = files?.Form11Attachment ?? new List<IFormFile>(),
                    Form2Attachment = files?.Form2Attachment ?? new List<IFormFile>(),
                    GratuityFormAttachment = files?.GratuityFormAttachment ?? new List<IFormFile>(),
                    UanCardAttachment = files?.UanCardAttachment ?? new List<IFormFile>(),
                };

                var validationError = DocumentValidationHelper.ValidateCandidateDocuments(candidateDocs);
                if (validationError != null)
                {
                    _logger.LogWarning("Document validation failed while inserting new candidate: {Message}", validationError.Message);
                    return BuildFileValidationErrorResponse(validationError);
                }

                var loginDetail = new JwtLoginDetailDto { EmployeeId = "1", role = "1" };
                string updatedBy = "1";
                _logger.LogInformation("Processing Insertnewcandidate for employee ID: {EmployeeId}", updatedBy);

                var res = await _candidateService.UpdateData(details, candidateDocs, updatedBy, loginDetail);
                _logger.LogInformation("Insertnewcandidate completed with status: {Status}, StatusCode: {StatusCode}", res.Status, res.StatusCode);

                return StatusCode((int)res.StatusCode, new
                {
                    Status = res.Status,
                    Message = res.Message,
                    Data = res.Data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inserting new candidate");
                return StatusCode(500, new { Status = false, Message = "An error occurred while processing your request" });
            }
        }

        [HttpPost, Authorize]
        [Route("Updatecandidate")]
        [RequestFormLimits(MultipartBodyLengthLimit = 524288000)]
        public async Task<IActionResult> Updatecandidate([FromForm] CandidateUpdate details, [FromForm] CandidateDocs files)
        {
            _logger.LogInformation("Updating candidate");
            try
            {
                var userIdentity = User.Identity as ClaimsIdentity;
                if (userIdentity == null || !userIdentity.IsAuthenticated)
                {
                    _logger.LogWarning("Unauthorized access attempt for Updatecandidate");
                    return Unauthorized(new { Status = false, Message = "User is not authenticated" });
                }

                var updatedBy = userIdentity.FindFirst("EmployeeId")?.Value;
                if (string.IsNullOrEmpty(updatedBy))
                {
                    _logger.LogWarning("Updatecandidate failed: Unable to determine user identity from token");
                    return BadRequest(new { Status = false, Message = "Unable to determine user identity from token" });
                }

                if (details == null)
                {
                    _logger.LogWarning("Updatecandidate failed: Candidate details are required");
                    return BadRequest(new { Status = false, Message = "Candidate details are required" });
                }

                _logger.LogInformation("Deserializing candidate lists for employee ID: {EmployeeId}", updatedBy);
                details.familyMembersList = DeserializeList<CandidateUpdateFamilyMember>(details.FamilyMembersListJson);
                details.experienceList = DeserializeList<CandidateUpdateExperience>(details.ExperienceListJson);
                details.qualificationList = DeserializeList<CandidateUpdateQualification>(details.QualificationListJson);
                details.assignLocations = DeserializeList<AssignLocationHistoryrecord>(details.AssignLocationsListJson);

                var candidateDocs = new CandidateDocs
                {
                    PassportPhoto = files?.PassportPhoto,
                    Last3SalarySlip = files?.Last3SalarySlip ?? new List<IFormFile>(),
                    Last3BankStatement = files?.Last3BankStatement,
                    PrevOfferLetter = files?.PrevOfferLetter,
                    PanAttachment = files?.PanAttachment ?? new List<IFormFile>(),
                    AadharAttachment = files?.AadharAttachment ?? new List<IFormFile>(),
                    AadharBackAttachment = files?.AadharBackAttachment ?? new List<IFormFile>(),
                    BankPassbookAttachment = files?.BankPassbookAttachment ?? new List<IFormFile>(),
                    EducationAttachment = files?.EducationAttachment ?? new List<IFormFile>(),
                    ResumeAttachment = files?.ResumeAttachment ?? new List<IFormFile>(),
                    EvaluationAttachment = files?.EvaluationAttachment ?? new List<IFormFile>(),
                    OfferLetterAttachment = files?.OfferLetterAttachment ?? new List<IFormFile>(),
                    InterviewVideo = files?.InterviewVideo ?? new List<IFormFile>(),
                    OtherAttachment = files?.OtherAttachment ?? new List<IFormFile>(),
                    BankStatementVideo = files?.BankStatementVideo ?? new List<IFormFile>(),
                    Form11Attachment = files?.Form11Attachment ?? new List<IFormFile>(),
                    Form2Attachment = files?.Form2Attachment ?? new List<IFormFile>(),
                    GratuityFormAttachment = files?.GratuityFormAttachment ?? new List<IFormFile>(),
                    UanCardAttachment = files?.UanCardAttachment ?? new List<IFormFile>(),
                };

                var validationError = DocumentValidationHelper.ValidateCandidateDocuments(candidateDocs);
                if (validationError != null)
                {
                    _logger.LogWarning("Document validation failed while updating candidate {CandidateId}: {Message}", details?.cid, validationError.Message);
                    return BuildFileValidationErrorResponse(validationError);
                }

                var loginDetail = AuthenticUserDetails.GetCurrentUserDetails(userIdentity);
                _logger.LogInformation("Processing Updatecandidate for employee ID: {EmployeeId}", updatedBy);

                var res = await _candidateService.UpdateData(details, candidateDocs, updatedBy, loginDetail);
                _logger.LogInformation("Updatecandidate completed for employee ID: {EmployeeId} with status: {Status}, StatusCode: {StatusCode}",
                    updatedBy, res.Status, res.StatusCode);

                return StatusCode((int)res.StatusCode, new
                {
                    Status = res.Status,
                    Message = res.Message,
                    Data = res.Data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating candidate for employee ID: {EmployeeId}", HttpContext.User.FindFirst("EmployeeId")?.Value);
                return StatusCode(500, new { Status = false, Message = "An error occurred while processing your request" });
            }
        }

        [HttpPost]
        [Route("CandidateApproval")]
        public async Task<IActionResult> CandidateApproval([FromBody] CandidateApprovalDto obj)
        {
            _logger.LogInformation("Processing candidate approval");
            try
            {
                var identity = HttpContext.User.Identity as ClaimsIdentity;
                var loginDetail = AuthenticUserDetails.GetCurrentUserDetails(identity);
                _logger.LogInformation("Processing CandidateApproval for employee ID: {EmployeeId}", loginDetail.EmployeeId);

                var res = await _candidateService.CandidateInitiate(obj, loginDetail);
                _logger.LogInformation("CandidateApproval completed for employee ID: {EmployeeId} with status: {Status}, StatusCode: {StatusCode}",
                    loginDetail.EmployeeId, res.Status, res.StatusCode);

                return StatusCode((int)res.StatusCode, new
                {
                    Status = res.Status,
                    Message = res.Message,
                    Data = res.Data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing candidate approval for employee ID: {EmployeeId}", HttpContext.User.FindFirst("EmployeeId")?.Value);
                return BadRequest(new
                {
                    Status = false,
                    Message = ex.Message,
                    Data = ex.Data
                });
            }
        }

        [HttpPost("search"), Authorize]
        public async Task<IActionResult> SearchCandidates([FromBody] SearchCandidatesRequest request)
        {
            _logger.LogInformation("Searching candidates with request: {Request}", JsonSerializer.Serialize(request));
            try
            {
                var candidates = await _candidateService.SearchCandidatesAsync(
                    request.StartDate,
                    request.EndDate,
                    request.LocationIds,
                    request.DesignationIds,
                    request.DepartmentIds,
                    request.StatusIds,
                    request.HrApprovalStatuses,
                    request.AuditApprovalStatuses,
                    request.ClusterManagerApprovalStatuses);
                _logger.LogInformation("SearchCandidates completed successfully");

                return Ok(candidates);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching candidates");
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }

        [HttpPost("InsertInterviewForm")]
        public async Task<IActionResult> InsertInterviewForm([FromBody] InterviewFormRequest request)
        {
            _logger.LogInformation("Inserting interview form for applicant code: {ApplicantCode}", request?.ApplicantCode);
            try
            {
                if (request == null)
                {
                    _logger.LogWarning("InsertInterviewForm failed: Request is null");
                    return BadRequest("Request cannot be null");
                }

                await _candidateService.InsertInterviewForm(
                    request.PositionAppliedId,
                    request.ApplicantCode,
                    request.PreferredWorkLocationIds,
                    request.Name,
                    request.MaritalStatus,
                    request.PresentAddress,
                    request.DeclarationConfirmed,
                    request.Place,
                    request.Ques1,
                    request.Ques2,
                    request.Ques3,
                    request.BiggestChallenges,
                    request.Strength1,
                    request.Strength2,
                    request.weakness1,
                    request.weakness2,
                    request.DateOfFilling,
                    request.FamilyInfo,
                    request.ExperienceInfo,
                    request.KRAKPIInfo,
                    request.ReferenceInfo
                );
                _logger.LogInformation("Interview form inserted successfully for applicant code: {ApplicantCode}", request.ApplicantCode);

                return Ok(new { Message = "Interview form inserted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inserting interview form for applicant code: {ApplicantCode}", request?.ApplicantCode);
                return StatusCode(500, "Server Error");
            }
        }

        [HttpGet("GetApplicantById")]
        public async Task<IActionResult> GetApplicantById(int applicantId)
        {
            _logger.LogInformation("Fetching applicant by ID: {ApplicantId}", applicantId);
            try
            {
                var result = await _candidateService.GetApplicantById(applicantId);

                if (result == null)
                {
                    _logger.LogWarning("Applicant not found for ID: {ApplicantId}", applicantId);
                    return NotFound(new { message = $"Applicant Id {applicantId} not found." });
                }

                _logger.LogInformation("Applicant fetched successfully for ID: {ApplicantId}", applicantId);
                return Ok(result);
            }
            catch (SqlException sqlEx)
            {
                _logger.LogError(sqlEx, "Database error fetching applicant by ID: {ApplicantId}", applicantId);
                return StatusCode(500, new { message = "Database error occurred.", error = sqlEx.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching applicant by ID: {ApplicantId}", applicantId);
                return StatusCode(500, new { message = "An unexpected error occurred.", error = ex.Message });
            }
        }

        [HttpGet("GetInterviewFormDataById")]
        public async Task<IActionResult> GetInterviewFormDataById(int applicantId)
        {
            _logger.LogInformation("Fetching interview form data for applicant ID: {ApplicantId}", applicantId);
            try
            {
                var result = await _candidateService.GetInterviewFormDataById(applicantId);

                if (result == null)
                {
                    _logger.LogWarning("Interview form data not found for applicant ID: {ApplicantId}", applicantId);
                    return NotFound(new { message = $"Applicant ID {applicantId} not found." });
                }

                _logger.LogInformation("Interview form data fetched successfully for applicant ID: {ApplicantId}", applicantId);
                return Ok(result);
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Database error fetching interview form data for applicant ID: {ApplicantId}", applicantId);
                return StatusCode(500, new { message = "Database error occurred.", error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching interview form data for applicant ID: {ApplicantId}", applicantId);
                return StatusCode(500, new { message = "An unexpected error occurred.", error = ex.Message });
            }
        }

        [HttpPost, Authorize]
        [Route("InsertOfferLetter")]
        [RequestFormLimits(MultipartBodyLengthLimit = 524288000)]
        public async Task<IActionResult> InsertOfferLetter([FromForm] CandidateOfferLetter details, [FromForm] CandidateOfferLetterDoc files)
        {
            _logger.LogInformation("Inserting offer letter");
            try
            {
                var userIdentity = User.Identity as ClaimsIdentity;
                if (userIdentity == null || !userIdentity.IsAuthenticated)
                {
                    _logger.LogWarning("Unauthorized access attempt for InsertOfferLetter");
                    return Unauthorized(new { Status = false, Message = "User is not authenticated" });
                }

                var updatedBy = userIdentity.FindFirst("EmployeeId")?.Value;
                if (string.IsNullOrEmpty(updatedBy))
                {
                    _logger.LogWarning("InsertOfferLetter failed: Unable to determine user identity from token");
                    return BadRequest(new { Status = false, Message = "Unable to determine user identity from token" });
                }

                if (details == null)
                {
                    _logger.LogWarning("InsertOfferLetter failed: Candidate details are required");
                    return BadRequest(new { Status = false, Message = "Candidate details are required" });
                }

                var candidateDocs = new CandidateOfferLetterDoc
                {
                    OfferLetterAttachment = files?.OfferLetterAttachment ?? new List<IFormFile>()
                };

                _logger.LogInformation("Processing InsertOfferLetter for employee ID: {EmployeeId}", updatedBy);
                var res = await _candidateService.InsertOfferLetter(details, candidateDocs, updatedBy);
                _logger.LogInformation("InsertOfferLetter completed for employee ID: {EmployeeId} with status: {Status}, StatusCode: {StatusCode}",
                    updatedBy, res.Status, res.StatusCode);

                return StatusCode((int)res.StatusCode, new
                {
                    Status = res.Status,
                    Message = res.Message,
                    Data = res.Data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inserting offer letter for employee ID: {EmployeeId}", HttpContext.User.FindFirst("EmployeeId")?.Value);
                return StatusCode(500, new { Status = false, Message = "An error occurred while processing your request" });
            }
        }


        [HttpGet("GetApplicantStatusType")]
        public async Task<IActionResult> GetApplicantStatusType()
        {
            try
            {
                var result = await _candidateService.GetApplicantStatusType();
                return Ok(new
                {
                    Status = true,
                    Message = "ApplicantStatusType retrieved successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    Status = false,
                    Message = "An error occurred while fetching ApplicantStatusType",
                    Error = ex.Message
                });
            }
        }

        [HttpPost("UpdateApplicantStatus")]
        public async Task<IActionResult> UpdateApplicantStatus([FromBody] UpdateStatusRequest obj)
        {
            try
            {
                var identity = HttpContext.User.Identity as ClaimsIdentity;
                var loginDetail = AuthenticUserDetails.GetCurrentUserDetails(identity);
                _logger.LogInformation("Processing UpdateApplicantStatus for employee ID: {EmployeeId}", loginDetail.EmployeeId);
                var result = await _candidateService.UpdateApplicantStatus(obj, loginDetail);


                return Ok(new
                {
                    Status = true,
                    Message = result,

                });

            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    Status = false,
                    Message = "An error occurred while Updating Status",
                    Error = ex.Message
                });
            }
        }

        [HttpPost("InsertInterviewSchedule")]
        public async Task<IActionResult> InsertInterviewSchedule([FromBody] ScheduleInterviewDto dto)
        {
            _logger.LogInformation("Inserting Schedule interview form for applicant code: {ApplicantId}", dto?.ApplicantId);
            try
            {
                if (dto == null || dto.Interviewers == null || dto.Interviewers.Count == 0)
                {
                    _logger.LogWarning("InsertScheduleInterview failed: Request is null");
                    return BadRequest("Request cannot be null");
                }

                var identity = HttpContext.User.Identity as ClaimsIdentity;
                var loginDetail = AuthenticUserDetails.GetCurrentUserDetails(identity);

                if (loginDetail != null && loginDetail.EmployeeId != null)
                {
                    dto.CreatedBy = loginDetail.EmployeeId;
                }

                await _candidateService.InsertScheduleInterview(dto);
                _logger.LogInformation("Interview form inserted successfully for applicant code: {ApplicantId}", dto.ApplicantId);


                return Ok(new
                {
                    Status = true,
                    Message = "Interview form inserted successfully",

                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inserting interview form for applicant code: {ApplicantId}", dto?.ApplicantId);
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    Status = false,
                    Message = "An error occurred while Updating Status",
                    Error = ex.Message
                });
            }
        }

        [HttpGet("GetAllEmployeeTransferListByManagerId"),]
        public async Task<IActionResult> GetAllEmployeeTransferListByManagerId()
        {
            _logger.LogInformation("Data request initiated for user");
            try
            {
                var identity = HttpContext.User.Identity as ClaimsIdentity;
                var userClaims = AuthenticUserDetails.GetCurrentUserDetails(identity);



                var user = await _candidateService.GetAllEmployeeTransferListByManagerId(userClaims);



                return Ok(new
                {
                    Status = user.Status,
                    Message = user.Message,
                    Data = user.Data
                });

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Getting Data for user");
                return BadRequest(new
                {
                    Status = false,
                    Message = ex.Message
                });
            }
        }

        [HttpPost("UpdateTransferApproval")]
        public async Task<IActionResult> UpdateTransferApproval([FromBody] TransferApprovalRequestDto model)
        {
            _logger.LogInformation("Transfer approval update initiated.");

            try
            {
                if (model == null || model.CandidateId <= 0)
                {
                    return BadRequest(new
                    {
                        Status = false,
                        Message = "Invalid request data."
                    });
                }

                var identity = HttpContext.User.Identity as ClaimsIdentity;
                var userClaims = AuthenticUserDetails.GetCurrentUserDetails(identity);

                //if (userClaims == null || string.IsNullOrEmpty(userClaims.EmployeeId))
                //{
                //    return BadRequest(new
                //    {
                //        Status = false,
                //        Message = "Invalid user credentials."
                //    });
                //}

                var result = await _candidateService.UpdateTransferApproval(model, userClaims);

                return Ok(new
                {
                    Status = result.Status,
                    Message = result.Message,
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while updating transfer approval.");
                return BadRequest(new
                {
                    Status = false,
                    Message = "An error occurred: " + ex.Message
                });
            }
        }
        [HttpGet("checklist/{candidateId}")]
        public async Task<IActionResult> GetCheckListByCandidateId(int candidateId)
        {
            var res = await _candidateService.GetCheckListByCandidateIdAsync(candidateId);
            if (res == null)
            {
                return StatusCode((int)HttpStatusCode.NotFound, new
                {
                    Status = "Error",
                    Message = $"Candidate with ID {candidateId} not found"
                });
            }

            return StatusCode((int)res.StatusCode, new
            {
                Data = res.Data,
                Status = res.Status,
                Message = res.Message
            });
        }
        [HttpPost("UpdateInterviewerFeedBack")]
        public async Task<IActionResult> UpdateInterviewerFeedBack([FromBody] UpdateInterviewerFeedbackRequest model)
        {
            _logger.LogInformation("Update interviewer feedback initiated.");

            try
            {
                if (model == null || model.ScheduleId <= 0)
                {
                    return BadRequest(new
                    {
                        Status = false,
                        Message = "Invalid request data."
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

                var result = await _candidateService.UpdateInterviewerFeedBack(
                    model.ScheduleId,
                    model.Feedback,
                    model.StatusName,
                    userClaims
                );


                return StatusCode((int)result.StatusCode, new
                {
                    result.Status,
                    result.Message,
                    result.Data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while updating interviewer feedback.");
                return StatusCode(500, new
                {
                    Status = false,
                    Message = "An error occurred: " + ex.Message
                });
            }
        }

        [HttpPost("candidate-doc/delete"), Authorize]
        public async Task<IActionResult> DeleteCandidateDoc(
    [FromBody] DeleteCandidateDocRequest req,
    CancellationToken ct = default)
        {
            if (req == null)
                return BadRequest(new { Status = false, Message = "Request body is required." });

            try
            {
                var result = await _candidateService.DeleteCandidateDocAsync(req, ct);
                return Ok(new
                {
                    Status = true,
                    Message = result.HardDelete
                        ? "Document hard-deleted successfully."
                        : "Document soft-deleted successfully.",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    Status = false,
                    Message = ex.Message
                });
            }
        }

        [HttpPost("move-candidate-to-background-verification/{candidateId}"), Authorize]
        public async Task<IActionResult> MoveCandidateToBackgroundVerification(long candidateId)
        {
            try
            {
                if (candidateId <= 0) return BadRequest(new { Status = false, Message = "Failed to move candidate to background verification", Error = "Invalid Candidate Id." });
                var response = await _candidateService.MoveCandidateToBackgroundVerification(candidateId);
                if(response.Status == false)
                {
                    return BadRequest(response);
                }
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = false, Message = "Failed to move candidate to background verification", Error = ex.Message });
            }
        }
    }
}