//using ASN.EF_Models;
using Azure.Core;
using HRMSAPI.Data;
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
using HRMSAPI.Extension;
using HRMSAPI.DTO;
using HRMSAPI.Controllers;
using System.Security.Principal;
using System.Text.Json;
using DocumentFormat.OpenXml.InkML;
using System.Net;

namespace HRMSAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Microsoft.AspNetCore.Authorization.Authorize]
    // NOTE: controller-level RequirePageAccess removed — GetApplicantById and
    // applicantdetails/{id} are called from candidate workflows used by users
    // who may not have /applicant/list permission. Apply per-method gates to
    // admin-only actions when needed.
    public class ApplicantController : ControllerBase
    {
        public readonly ICandidateService _candidateService;
        public ApplicantController(ICandidateService candidateService)
        {
            _candidateService = candidateService;
        }

        [HttpPost]  
        public async Task<IActionResult> Insertnewcandidate([FromForm] CandidateUpdate details, [FromForm] CandidateDocs files)
        {
            try
            {
                if (details == null)
                    return BadRequest(new { Status = false, Message = "Candidate details are required" });

                // Handle lists
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
                    BankPassbookAttachment = files?.BankPassbookAttachment ?? new List<IFormFile>(),
                    EducationAttachment = files?.EducationAttachment ?? new List<IFormFile>(),
                    ResumeAttachment = files?.ResumeAttachment ?? new List<IFormFile>(),
                };
                var loginDetail = new JwtLoginDetailDto { EmployeeId = "1", role = "1" };
                string updatedBy = "1";
                var res = await _candidateService.UpdateData(details, candidateDocs, updatedBy, loginDetail);

                return StatusCode((int)res.StatusCode, new
                {
                    Status = res.Status,
                    Message = res.Message,
                    Data = res.Data
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Status = false, Message = "An error occurred while processing your request" });
            }
        }



        [HttpGet("ExportToExcelApplicant")]
        public async Task<IActionResult> ExportToExcelApplicant([FromQuery] int StatusId = 0, [FromQuery] string searchTerm = "")
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

            var fileBytes = await _candidateService.ExportApplicantListByStatusToExcelAsync(userClaims, StatusId, searchTerm);

            var fileName = $"ApplicantList_{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx";
            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
        [HttpGet("GetApplicantById")]
        public async Task<IActionResult> GetApplicantById(int candidateId)
        {
            var candidate = await _candidateService.GetApplicantByIdAsync(candidateId);
            if (candidate == null)
            {
                return NotFound("Candidate not found");
            }

            return Ok(candidate);
        }

        [HttpPost("updateapplicantstatus")]

        public async Task<IActionResult> UpdateApplicantStatus([FromBody] UpdateStatusDto dto)
        {

            var result = await _candidateService.UpdateApplicantStatusAsync(dto);
            if (!result)
            {
                return NotFound("Candidate not found or update failed");
            }

            return Ok("Status updated successfully");
        }

        [HttpGet("applicantdetails/{candidateId}")]
        public async Task<IActionResult> GetApplicantDetails(int candidateId)
        {
            var candidate = await _candidateService.GetApplicantDetailsAsync(candidateId);
            if (candidate == null)
            {
                return NotFound("Candidate not found");
            }

            var result = new
            {
                key = candidate.id,
                firstName = candidate.firstName,
                email = candidate.emailAddress,
                dob = candidate.dob?.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                designation = candidate.designation,
                phone = candidate.mobile,
                statusId = candidate.statusId,
                offer_letter = "https://morth.nic.in/sites/default/files/dd12-13_0.pdf",
                offer_letter_sent = 1,
                cv = "offerletter",
                type = "https://meet.google.com/abc-mnop-xyz",
                designationname = "Frontend Developer",
                interviewRounds = candidate.InterviewRounds?.Select(r => new
                {
                    round = r.RoundName,
                    interviewer = r.Interviewers?.Select(i => new { name = i.Name, feedback = i.Feedback }) ?? Enumerable.Empty<object>(),
                    level = r.Level,
                    status = r.Status,
                    remark = r.Remark
                }) ?? Enumerable.Empty<object>(),
                finalResult = candidate.InterviewRounds?.Any(r => r.Status == "Pending") == true ? "Pending" : "Completed"
            };

            return Ok(result);
        }
        [HttpGet("Applicantlist")]
        public async Task<IActionResult> GetApplicantlist(
     [FromQuery] int pageNumber = 1,
     [FromQuery] int pageSize = 10, [FromQuery] int StatusId = 0,
     [FromQuery] string searchTerm = "")
        {
            if (pageNumber < 1 || pageSize < 1)
            {
                return BadRequest(new Response
                {
                    Status = false,
                    Message = "Page number and page size must be greater than 0",
                    StatusCode = HttpStatusCode.BadRequest
                });
            }

            try
            {
                var response = await _candidateService.GetApplicantList(pageNumber, pageSize, StatusId, searchTerm);

                if (!response.Status)
                    return StatusCode((int)response.StatusCode, response);

                var candidates = (response.Data as dynamic)?.Candidates;

                if (candidates == null || !((IEnumerable<object>)candidates).Any())
                {
                    return NotFound(new Response
                    {
                        Status = false,
                        Message = "Candidates not found",
                        StatusCode = HttpStatusCode.NotFound
                    });
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new Response
                {
                    Status = false,
                    Message = "An error occurred while processing the request",
                    StatusCode = HttpStatusCode.InternalServerError
                });
            }
        }


        [NonAction]
        private List<T> DeserializeList<T>(string json, JsonSerializerOptions options = null)
        {
            if (string.IsNullOrEmpty(json))
                return new List<T>(); // Return empty list if no data provided

            try
            {
                // Try deserializing as a list first
                var list = JsonSerializer.Deserialize<List<T>>(json, options);
                return list ?? new List<T>();
            }
            catch (JsonException)
            {
                try
                {
                    // If list fails, try deserializing as a single object
                    var singleItem = JsonSerializer.Deserialize<T>(json, options);
                    return singleItem != null ? new List<T> { singleItem } : new List<T>();
                }
                catch (JsonException)
                {
                    // If both fail, return an empty list instead of throwing an error
                    return new List<T>();
                }
            }
        }

        public class Response
        {
            public bool Status { get; set; }
            public string Message { get; set; }
            public System.Net.HttpStatusCode StatusCode { get; set; }
            public object Data { get; set; }
        }

        [HttpGet("GetApplicantListByStatus")]
        public async Task<IActionResult> GetApplicantListByStatus([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10, [FromQuery] int StatusId = 0, [FromQuery] string searchTerm = "")
        {
            //try
            //{
            //    var response = await _candidateService.GetApplicantListByStatus(pageNumber, pageSize, StatusId, searchTerm);

            //    return Ok(new
            //    {
            //        Status = true,
            //        Message = "Data fetched successfully.",
            //        Candidates = response

            //    });
            //}
            //catch (Exception ex)
            //{

            //    return StatusCode(500, new
            //    {
            //        Status = false,
            //        Message = "An error occurred: " + ex.Message
            //    });
            //}

            if (pageNumber < 1 || pageSize < 1)
            {
                return BadRequest(new Response
                {
                    Status = false,
                    Message = "Page number and page size must be greater than 0",
                    StatusCode = HttpStatusCode.BadRequest
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

            try
            {
                var response = await _candidateService.GetApplicantListByStatus(userClaims, pageNumber, pageSize, StatusId, searchTerm);

                if (!response.Status)
                    return StatusCode((int)response.StatusCode, response);

                var candidates = (response.Data as dynamic)?.Candidates;

                if (candidates == null || !((IEnumerable<object>)candidates).Any())
                {
                    return NotFound(new Response
                    {
                        Status = false,
                        Message = "Candidates not found",
                        StatusCode = HttpStatusCode.NotFound
                    });
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new Response
                {
                    Status = false,
                    Message = "An error occurred while processing the request",
                    StatusCode = HttpStatusCode.InternalServerError
                });
            }
        }

        [HttpPost("reopen")]
        public async Task<IActionResult> ReopenCandidate([FromBody] ReopenCandidateDto dto)
        {
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

                var result = await _candidateService.ReopenCandidateAsync(dto, userClaims);

                if (!result)
                    return BadRequest("Candidate cannot be reopened");

                return Ok(new
                {
                    Status = true,
                    Message = "Candidate reopened successfully"
                });
            }

            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new Response
                {
                    Status = false,
                    Message = "An error occurred while processing the request",
                    StatusCode = HttpStatusCode.InternalServerError
                });

            }
        }


        [HttpGet("by-interviewer/{interviewerId}")]
        public async Task<IActionResult> GetByInterviewer(long interviewerId)
        {
            try
            {
                var data = await _candidateService.GetInterviewsByInterviewerAsync(interviewerId);

                return Ok(new
                {
                    Status = true,
                    Message = "Interview schedules fetched successfully",
                    Data = data
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Status = false,
                    Message = "An error occurred: " + ex.Message
                });
            }

        }
        [HttpGet("GetApplicantAssignDetails")]
        public async Task<IActionResult> GetApplicantAssignDetails()
        {
            try
            {
                var result = await _candidateService.GetApplicantAssignDetails();

                if (result == null || !result.Any())
                    return NotFound("No records found");

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "An error occurred while fetching applicant assign details",
                    error = ex.Message
                });
            }
        }
        [HttpGet("GetApplicantFeedBack")]
        public async Task<IActionResult> GetApplicantFeedBack()
        {
            try
            {
                var result = await _candidateService.GetApplicantFeedBack();

                if (result == null)
                    return NotFound("No records found");

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "An error occurred while fetching applicant feedback details",
                    error = ex.Message
                });
            }

        }
        [HttpPost("InterviewBackgroundProcess")]
        public async Task<IActionResult> Create([FromForm] InterviewBackgroundProcessDto dto, CancellationToken ct = default)
        {
            if (dto.CandidateId <= 0)
                return BadRequest("ApplicantId is required");

            var identity = HttpContext.User.Identity as ClaimsIdentity;
            var loginDetail = AuthenticUserDetails.GetCurrentUserDetails(identity);

            var result = await _candidateService.CreateBackgroundProcessAsync(dto, loginDetail, ct);

            if (!result.Status)
                return BadRequest(result);

            return Ok(result);
        }
        [HttpGet("GetInterviewBackgroundProcess")]
        public async Task<IActionResult> GetInterviewBackgroundProcess()
        {
            try
            {
                var result = await _candidateService.GetInterviewBackgroundProcess();

                if (result == null)
                    return NotFound("No records found");

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "An error occurred while fetching applicant feedback details",
                    error = ex.Message
                });
            }

        }
    }
}

