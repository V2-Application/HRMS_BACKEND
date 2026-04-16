using HRMSAPI.DTO;

namespace HRMSAPI.Interfaces
{
    public interface ILocationDesignationWeeklyOffHolidayMasterService
    {
        Task<(bool Success, string Message)> UploadMasterDataAsync(IFormFile file);
        Task<(List<LocationDesignationWeeklyOffHolidayMasterDTO> Records, int TotalRecords)> GetMasterRecordsAsync(
            string? searchTerm = null,
            string? locationCategoryName = null,
            int page = 1,
            int pageSize = 10);
    }
}
