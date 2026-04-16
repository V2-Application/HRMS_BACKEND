using Microsoft.AspNetCore.Http;
using Roomsy.DTOS.GenericsResponses;
using System.Threading.Tasks;

public interface IPaidByBankService
{
    Task<(bool Success, string Message)> UploadPaidByBankDataAsync(IFormFile file, string createdBy);

    //Task<(List<PaidByBankDTO> Records, int TotalRecords)> GetPaidByBankRecordsAsync(
    //  string? searchTerm = null,
    //  string? ecode = null,
    //  int page = 1,
    //  int pageSize = 10);
    Task<FetchAndResponse> GetPaidByBankRecordsAsync(
      string? searchTerm = null,
      string? ecode = null,
      string? monthYear = null,
      bool asExcel = false,
      int? page = null,
      int? pageSize = null);
}