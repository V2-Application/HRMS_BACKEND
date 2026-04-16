using HRMSAPI.DTO;
using Roomsy.DTOS.GenericsResponses;

namespace HRMSAPI.Interfaces
{
    public interface IAttendanceRegularizationService
    {
        Task<FetchAndResponse> GetAttendanceRegularizationAsync(string monthYear, bool asExcel = false);
    }
}

