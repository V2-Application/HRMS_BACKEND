using HRMSAPI.Data;
using HRMSAPI.DTO;
using HRMSAPI.Models.Candidate;
using Microsoft.AspNetCore.Mvc;
using Roomsy.DTOS.GenericsResponses;
using GetEmployeeDetailsResult = HRMSAPI.DTO.GetEmployeeDetailsResultNew;
namespace HRMSAPI.Interfaces
{
    public interface IEmployeeServiceNew
    {
        Task<ExecuteAndReponse> RefreshEmpDetails(string eCode);
        Task<(List<GetEmployeeDetailsResult> Employees, long TotalCount, int CurrentPageNumber)> EmployeeList(int pageNumber, int pageSize, string searchTerm = "");
        Task<FetchAndResponse> GetEmployeeOrCandidateById(int Id, bool isCandidate = true);
        //Task<ExecuteAndReponse> UpdateEmployee(UpdateEmployee employee, string updatedBy);
        Task<ExecuteAndReponse> UpdateEmployee(CandidateUpdate details, CandidateDocs files, string updatedBy);
        Task<ExecuteAndReponse> UpdateEmployeeStatus(EmployeeStatusUpdateRequest request);
        Task<FetchAndResponse> GetInActiveStatusList();
        Task<ExecuteAndReponse> UpdateEmployeeWithExcel(IFormFile file, string updatedBy);
        Task<ExecuteAndReponse> BulkInsertEmployeesWithExcel(IFormFile file, string createdBy);
        Task<ExecuteAndReponse> UpdateEmployeeStatusWithReasonAndAttachment(EmployeeStatusUpdateWithReasonAndAttachmentRequest request);
        Task<ExecuteAndReponse> BulkInactivateEmployees(BulkInactivateRequest request);
        Task<(List<GetEmployeeDetailsResult> Employees, long TotalCount, int CurrentPageNumber)> GetEmployeeDetailsByManagerIdAsync(
       long managerId, int pageNumber = 1, int pageSize = 10, string searchTerm = null);

        Task<(List<GetEmployeeDetailsResultNew> Employees, long TotalCount, int CurrentPageNumber, long ActiveCount, long InactiveCount, long abscondCnt, long locCountt)> EmployeeListWithCards(int pageNumber, int pageSize, string searchTerm = "", string mode = "all");

        Task<(List<GetEmployeeDetailsResultNew_Test> Employees, long TotalCount, int CurrentPageNumber, long ActiveCount, long InactiveCount, long abscondCnt, long locCountt)> EmployeeListWithCards_Test(string? managerId, int pageNumber, int pageSize, string searchTerm = "", string mode = "all");

        Task<ExecuteAndReponse> UpdateEmployeeDetails(CandidateRequest empUpdateDetails, string updatedBy);

        Task<List<EmployeeUpdateInfo>> GetPendingEmployeeUpdateListAsync();

        //Task<List<EmployeeCombinedDto>> GetAllEmployeeUpdateComparisonsAsync();

        Task<EmployeeDetailsUpdateView> GetChangedFieldsForEmployeeAsync(long employeeId);

        List<ChangedFieldDto> GetEmployeeChanges(tempTblEmployee temp, tblEmployee perm);
        Task<List<FamilyChangeDto>> GetFamilyChangesAsync(long employeeId, long? CandidateId, long? idPass);

        Task<List<ExperienceChangeDto>> GetExperienceChangesAsync(long employeeId, long? CandidateId, long? idPass);

        Task<List<QualificationChangeDto>> GetQualificationChangesAsync(long employeeId, long? CandidateId, long? idPass);

        Task<List<DocumentChangeDto>> GetDocumentChangesAsync(long employeeId, long? CandidateId, long? idPass);

        Task<ExecuteAndReponse> UpdateEmployeeApprovedDetails(EmployeeDetailsUpdateView employeeDetailsUpdateView, long EmployeeId, string updatedBy);
    }
}
