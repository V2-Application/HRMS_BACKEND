using HRMSAPI.DTO;
using Microsoft.AspNetCore.Http;
using Roomsy.DTOS.GenericsResponses;

namespace HRMSAPI.Interfaces
{
    public interface IDesignationService
    {
        Task<FetchAndResponse> GetAllAsync(bool onlyInactive = false, string? searchTerm = null);
        Task<ExecuteAndReponse> UpsertAsync(DesignationUpsertDto dto, long currentEmployeeId);
        Task<ExecuteAndReponse> ToggleActiveAsync(int id, bool isActive, long currentEmployeeId);
        Task<FetchAndResponse> BulkUploadAsync(IFormFile file, long currentEmployeeId);
    }
}
