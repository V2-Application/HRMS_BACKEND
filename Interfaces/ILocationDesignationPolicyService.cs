using HRMSAPI.DTO;

namespace HRMSAPI.Interfaces
{
    public interface ILocationDesignationPolicyService
    {
        Task<(bool Success, string Message)> UploadPolicyDataAsync(IFormFile file);
        Task<(List<LocationDesignationPolicyDTO> Records, int TotalRecords)> GetPolicyRecordsAsync(
           string? searchTerm = null,
           string? locationCategoryName = null,
           int page = 1,
           int pageSize = 10);
    }
}
