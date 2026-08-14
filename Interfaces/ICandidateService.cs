using ASN.Controllers;
using HRMSAPI.Data;
using HRMSAPI.DTO;
using HRMSAPI.Models.Auth;
using HRMSAPI.Models.Candidate;
using HRMSAPI.Models.EvalutionForm;
using Microsoft.AspNetCore.Mvc;
using System.Security.Principal;
using System.Threading.Tasks;
using Candidate = HRMSAPI.Models.Candidate.Candidate;
using CandidateDocs = HRMSAPI.Models.Candidate.CandidateDocs;
namespace HRMSAPI.Interfaces
{
        public interface ICandidateService
        {
                Task<Response> InsertCandidateWithDocs(Candidate candidate, CandidateDocs files, string createdBy);
                Task<Response> GetCandidateList(int pageNumber, int pageSize, string searchTerm = "", long employeeId = 0, string role = "");
                Task<Response> GetCandidateInfo(int candidateID);
                Task<Response> CandidateInitiate(CandidateApprovalDto obj, JwtLoginDetailDto loginDetail);
                Task<Response> UpdateData(CandidateUpdate details, CandidateDocs candidateDocs, string updatedBy, JwtLoginDetailDto loginDetail);
                Task<Response> CheckSeatAvailabilityAsync(int locationId, int departmentId, int? subDepartmentId1, int? subDepartmentId2, int? subDepartmentId3, int designationId, decimal? salary, long? excludeCandidateId);
                Task<List<CandidateSearchResult>> SearchCandidatesAsync(
                      DateTime? startDate,
                      DateTime? endDate,
                      List<string> locationIds,
                      List<string> designationIds,
                      List<string> departmentIds,
                      List<int> statusIds,
                      List<int> hrApprovalStatuses,
                      List<int> auditApprovalStatuses,
                      List<int> clusterManagerApprovalStatuses);



                Task<Candidate> GetApplicantByIdAsync(int candidateId);
                Task<bool> UpdateApplicantStatusAsync(UpdateStatusDto dto);
                Task<Candidate> GetApplicantDetailsAsync(int candidateId);

                Task<Response> GetApplicantList(int pageNumber, int pageSize,int StatusId, string searchTerm = "");

                Task InsertInterviewForm(int? positionAppliedId, int? applicantCode, string? preferredWorkLocationIds, string? name, string? maritalStatus, string? presentAddress, bool? declarationConfirmed, string? place, string? Ques1, string? Ques2, string? Ques3, string? BiggestChallenges, string? Strength1, string? Strength2, string? weakness1, string? weakness2, DateTime? dateOfFilling, List<FamilyDto>? familyList, List<ExperienceDto>? experienceList, List<KRAKPIDto>? kraKpiList, List<ReferenceDto>? refList);

                Task<InterviewFormRequest> GetInterviewFormDataById(int applicantId);

                Task<ApplicantDto> GetApplicantById(int applicantId);
                Task<Response> InsertOfferLetter(CandidateOfferLetter details, CandidateOfferLetterDoc candidateDocs, string updatedBy);
 
                Task<IEnumerable<ApplicantStatusTypeDto>> GetApplicantStatusType();

                 Task<Response> UpdateApplicantStatus(UpdateStatusRequest obj, JwtLoginDetailDto loginDetail);




                Task<ResponseWithList<TransferEmployeeDto>> GetAllEmployeeTransferListByManagerId(JwtLoginDetailDto loginDetail);

                Task<ResponseWithList<TransferApprovalRequestDto>> UpdateTransferApproval(TransferApprovalRequestDto request, JwtLoginDetailDto loginDetail);
              //Task InsertInterviewSchedule(int applicantId, string candidateName, DateTime interviewDateTime, string interviewMode, string interviewLocation, List<Interviewers> allInterviewRounds);
                Task<Response> GetCheckListByCandidateIdAsync(int candidateId);
        
              Task<ScheduleInterview> GetScheduleInterviewDetailsById(int ScheduleId);

             Task<Response> InsertScheduleInterview(ScheduleInterviewDto dto);

        Task<Response> UpdateInterviewerFeedBack(int scheduleId, string feedback, string StatusName, JwtLoginDetailDto loginDetail);
        Task<Response> GetApplicantListByStatus(JwtLoginDetailDto loginDetail,int pageNumber = 1, int pageSize = 10, int statusId = 0,string searchTerm="");

        Task<byte[]> ExportApplicantListByStatusToExcelAsync(JwtLoginDetailDto loginDetail,int statusId = 0,string searchTerm="");

        Task<DeleteCandidateDocResult> DeleteCandidateDocAsync(DeleteCandidateDocRequest req, CancellationToken ct = default);
        Task<bool> ReopenCandidateAsync(ReopenCandidateDto dto, JwtLoginDetailDto loginDetail);
        Task<List<InterviewScheduleDto>> GetInterviewsByInterviewerAsync(long interviewerId);
        Task<List<InterviewAssignedDto>> GetApplicantAssignDetails();
        Task<List<ApplicantFeedbackDto>> GetApplicantFeedBack();
        Task<Response> CreateBackgroundProcessAsync(InterviewBackgroundProcessDto dto, JwtLoginDetailDto loginDetail, CancellationToken ct = default);
        Task<List<InterviewBackgroundProcessResponseDto>> GetInterviewBackgroundProcess();

        Task<Response> MoveCandidateToBackgroundVerification(long candidateId);

    }
}
