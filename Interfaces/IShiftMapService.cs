using HRMSAPI.DTO;

namespace HRMSAPI.Interfaces
{
    public interface IShiftMapService
    {
        Task<(List<ShiftMapDTO> Records, int TotalRecords)> GetShiftMapRecordsAsync(
            string? searchTerm = null,
            string? ecode = null,
            int page = 1,
            int pageSize = 10);

        Task<(bool Success, string Message)> UploadShiftMapDataAsync(IFormFile file, string createdBy);

        Task<EmployeeShiftAndHistoryResponse> GetEmployeeShiftAndHistoryAsync(int? employeeId = null, string? ecode = null);

        Task<(bool Success, string Message)> AssignEmployeeShiftAsync(AssignEmployeeShiftRequest request);

        Task<(bool Success, string Message)> ApplyScheduledShiftsAsync();
    }

}
