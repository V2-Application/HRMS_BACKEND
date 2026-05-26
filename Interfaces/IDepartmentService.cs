using HRMSAPI.DTO;
using Microsoft.AspNetCore.Http;
using Roomsy.DTOS.GenericsResponses;

namespace HRMSAPI.Interfaces
{
    public interface IDepartmentService
    {
        Task<FetchAndResponse> GetAllAsync(bool onlyInactive = false, string? searchTerm = null);
        Task<ExecuteAndReponse> UpsertAsync(DepartmentUpsertDto dto, long currentEmployeeId);
        Task<ExecuteAndReponse> ToggleActiveAsync(int id, bool isActive, long currentEmployeeId);
        Task<FetchAndResponse> BulkUploadAsync(IFormFile file, long currentEmployeeId);
    }
}
