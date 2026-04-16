using HRMSAPI.Data;
using HRMSAPI.DTO;
using Microsoft.AspNetCore.Http;
using Roomsy.DTOS.GenericsResponses;
using GetEmployeeDetailsResult = HRMSAPI.DTO.GetEmployeeDetailsResult;

namespace HRMSAPI.Interfaces
{
    public interface IEmployeeService
    {

        Task<(List<GetEmployeeDetailsResult> Employees, long TotalCount, int CurrentPageNumber)> EmployeeList(int pageNumber, int pageSize, string searchTerm = "");
        Task<(List<GetEmployeeDetailsResult> Employees, int TotalEmployees, int CurrentPageNumber)> EmployeeSearchList(string searchTerm, string? email = null, string? designationName = null);

        Task<(List<GetEmployeeDetailsResultNew_Hold> EmployeesHold, long TotalCount, int CurrentPageNumber)> GetEmployee_HoldList(int pageNumber, int pageSize, string searchTerm = "");
        Task<(bool Success, string Message)> UpsertEmployeeAsync(DCEmployeeDto employeeDto, EmployeeDocs files);
        Task<(bool Success, string Message)> DeleteEmployeeAsync(long id, string deletedBy);
        Task<string> SaveFileAsync(IFormFile file, string folderName, string candidateId);
        Task SaveEmployeeAttachmentsAsync(long empId, string email, EmployeeDocs files, string updatedBy);
        Task<(bool Success, DCEmployeeDto? Employee, string Message)> GetEmployeeByIdAsync(long employeeId);
        Task<tblEmployee> GetEmployeeByEcodeAsync(string ecode);
        Task<List<OfferLetterDto>> GetOfferLettersOnMail(string employeeIds);
        Task SendOfferLetters(string employeeIds);
        Task<List<tblEmployee>> GetActiveEmployeesWithFaceDataAsync();
        Task UpdateEmployeeAsync(tblEmployee employee);
         Task<EmployeeSalarySlip?> GetSalaryDetailsByEcode(string ecode, string month);
   
        Task<List<EmployeeSalarySlipDto>> GetAllSalarySlipsDetails(string month, int pageNumber, int pageSize, string? searchTerm = "");

        Task SaveEmailStatus(int applicantId, string email, bool isSent, string errorMessage = "");
        //Task<ExecuteAndReponse> upsertMarketingEmpChecklistAsync(MarketingEmpChecklistDto EmpDto);
        //Task<List<EmployeeResignationChecklistMasterDTO>>GetEmployeeResignationChecklistMasterAsync();
        Task<List<GetEmployeeResignationChecklist>> GetEmployeeResignationChecklistByECodeAsync(string ECode);
        Task<bool> SaveChecklistAsync(ResignationChecklistResponseDto dto, string EmployeeId);
        Task<bool> SaveChecklistListAsync(List<ResignationChecklistItemDto> items, List<IFormFile> files, string EmployeeId);        
    }
}
