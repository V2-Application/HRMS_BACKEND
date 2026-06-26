using HRMSAPI.DTO;
using Roomsy.DTOS.GenericsResponses;
using System.Collections.Generic;

namespace HRMSAPI.Interfaces
{
    public interface IEmpAttendanceViewSnapshotService
    {
        Task<FetchAndResponse> GetEmpAttendanceViewSnapshotsAsync(string month = null, int? status = null, string ecode = null, string batch = null, int? page = null, int? pageSize = null, string search = null);
        Task<ExecuteAndReponse> SalaryProcessToGivenToBankOrPaidByCash(long id, int status);
        Task<FetchAndResponse> GetSalaryStatusList(int status, string month = null);
        Task<ExecuteAndReponse> GivenToBankToPaidByBankOrReturnFromBank(long id, int statusId, string batchId);
        Task<ExecuteAndReponse> ProcessExcelUploadAsync(IFormFile file);
        Task<ExecuteAndReponse> ProcessGivenToBankExcelUploadAsync(IFormFile file);
        Task<FetchAndResponse> GetComprehensiveSalaryStatusList(string month = null, string ecode = null, int pageNumber = 1, int pageSize = 50);
        Task<FetchAndResponse> GetEmployeesMissingOrReturnedAsync(string stCode = "RH01", string month = null);
        Task<FetchAndResponse> GetEligibleEmployeesFastAsync(string ecode = null, string month = null);
        Task<ExecuteAndReponse> UpdateStatusByIdAsync(long id, int status);
    }
}

