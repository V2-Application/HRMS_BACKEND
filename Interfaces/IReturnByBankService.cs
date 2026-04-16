using HRMSAPI.DTO;

namespace HRMSAPI.Interfaces
{
    public interface IReturnByBankService
    {
        Task<(bool Success, string Message)> UploadReturnByBankDataAsync(IFormFile file);
        Task<(List<ReturnByBankDTO> Records, int TotalRecords)> GetPaidByBankRecordsAsync(
       string? searchTerm = null,
       string? ecode = null,
       int page = 1,
       int pageSize = 10);
    }
}
