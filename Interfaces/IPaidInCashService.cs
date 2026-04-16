using HRMSAPI.DTOs;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HRMSAPI.Interfaces
{
    public interface IPaidInCashService
    {
        Task<(List<PaidInCashDTO> Records, int TotalRecords)> GetPaidInCashRecordsAsync(
            string? searchTerm = null,
            string? ecode = null,
            string? month = null,
            string? location = null,
            int page = 1,
            int pageSize = 10);

        Task<(bool Success, string Message)> UploadPaidInCashDataAsync(IFormFile file);
    }
}