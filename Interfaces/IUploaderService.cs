using System.Threading.Tasks;
using HRMSAPI.Data;
using HRMSAPI.DTO;
using Microsoft.AspNetCore.Http;
using Roomsy.DTOS.GenericsResponses;

namespace HRMSAPI.Interfaces
{
    public interface IUploaderService
    {
        Task<FetchAndResponse> UploadEmpAttendanceMasterAsync(IFormFile file);
        Task<FetchAndResponse> GetAllEmpAttendanceMasterAsync();
        Task<FetchAndResponse> UploadEmpTDSTableAsync(IFormFile file);
        Task<FetchAndResponse> GetAllEmpTDSTableAsync();
        Task<FetchAndResponse> UploadApplicabilityMasterAsync(IFormFile file);
        Task<FetchAndResponse> GetAllApplicabilityMasterAsync();
        Task<FetchAndResponse> UploadEmpSalaryStructureAsync(IFormFile file);
        Task<FetchAndResponse> GetAllEmpSalaryStructureAsync();
        Task<FetchAndResponse> UploadLeaveOpeningBalTableAsync(IFormFile file);
        Task<FetchAndResponse> GetAllLeaveOpeningBalTableAsync();
        Task<FetchAndResponse> UploadEmpPersonalDetailsAsync(IFormFile file);
        Task<FetchAndResponse> GetAllEmpPersonalDetailsAsync();
        Task<FetchAndResponse> UploadEmpStatutoryDetailsAsync(IFormFile file);
        Task<FetchAndResponse> GetAllEmpStatutoryDetailsAsync();
        Task<FetchAndResponse> UploadEmpDegreeQualificationAsync(IFormFile file);
        Task<FetchAndResponse> GetAllEmpDegreeQualificationAsync();
        Task<FetchAndResponse> UploadEmpPastExperienceDetailsAsync(IFormFile file);
        Task<FetchAndResponse> GetAllEmpPastExperienceDetailsAsync();
        Task<FetchAndResponse> UploadEmpJoiningReleavingDetailsAsync(IFormFile file);
        Task<FetchAndResponse> GetAllEmpJoiningReleavingDetailsAsync();
        Task<FetchAndResponse> UploadEmpRevisedDeptDesgLocDetailsAsync(IFormFile file);
        Task<FetchAndResponse> GetAllEmpRevisedDeptDesgLocDetailsAsync();
        Task<FetchAndResponse> UploadPaymentsync(IFormFile file);
        Task<FetchAndResponse> GetAllPaymentsAsync();
        Task<FetchAndResponse> UploadBonusAndGratutityOpeningAsync(IFormFile file);
        Task<FetchAndResponse> GetBonusAndGratutityOpeningByEcodeAsync(string? ecode);
        Task<FetchAndResponse> UploadEmpSalaryStatusAsync(IFormFile file);
        Task<FetchAndResponse> GetEmpSalaryStatusByEcodeAsync(string? ecode);
        Task<(bool Success, string Message)> UploadCompOffDataAsync(IFormFile file, string createdBy);
        Task<List<CompOffDto>> GetCompOffListAsync();
        Task<FetchAndResponse> UploadStoreStateLinkingAsync(IFormFile file);
        Task<FetchAndResponse> GetAllStoreStateLinkingAsync();
        Task<FetchAndResponse> GetStoreWhichCanAddAsync();
        Task<(bool Success, string Message, byte[] FileBytes, string ContentType, string FileName)> GetStoreWhichCanAddExcelAsync();
        Task<ExecuteAndReponse> UploadEcodeZoneRegionClusterMappingAsync(IFormFile file, string updatedBy);
        Task<FetchAndResponse> GetAllEcodeZoneRegionClusterMappingAsync();
        Task<(bool Success, string Message, byte[] FileBytes, string ContentType, string FileName)> GetEcodeZoneRegionClusterMappingExcelAsync();
        Task<FetchAndResponse> UploadEmpPayrolDetailsAsync(IFormFile file);
        Task<FetchAndResponse> GetEMPBonusListAsync();
        Task<ExecuteAndReponse> UploadPayrollWithChallanAsync(IFormFile excelFile, IFormFile challanPdf, string monthYear, JwtLoginDetailDto createdBy);
        Task<PagedResultNew<EmployeePayrollUploadDto>> GetEmployeePayrollAsync(string? monthYear, int pageNumber, int pageSize, string searchTerm = "");
        Task<ExecuteAndReponse> UploadESICFromExcelAsync(
         IFormFile excelFile,
         JwtLoginDetailDto createdBy);
        Task<PagedResultNew<EmployeeESICUploadDto>> GetEmployeeESICAsync(string? searchTerm, int pageNumber, int pageSize);
        Task<ExecuteAndReponse> UploadRetentionAsync(
         IFormFile excelFile,
         JwtLoginDetailDto createdBy);
        Task<(List<RetentionDTO>? Data,int TotalCount,byte[]? ExcelBytes)> GetRetentionAsync(int pageNumber,int pageSize,string searchTerm,bool isExcel);
    }
} 