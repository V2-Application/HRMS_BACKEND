using HRMSAPI.DTO;
using Roomsy.DTOS.GenericsResponses;

namespace HRMSAPI.Interfaces
{
    public interface IAttendanceRegularizationService
    {
        Task<FetchAndResponse> GetAttendanceRegularizationAsync(string monthYear, bool asExcel = false);

        Task<FetchAndResponse> ExportAttendanceRegularizationByRangeAsync(
            DateTime startDate,
            DateTime endDate,
            string? status,
            string? managerStatus,
            string? lpStatus);
    }
}

