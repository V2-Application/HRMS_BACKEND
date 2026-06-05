using HRMSAPI.DTO;
using Microsoft.AspNetCore.Http;
using Roomsy.DTOS.GenericsResponses;

namespace HRMSAPI.Interfaces
{
    public interface ISubDepartmentService
    {
        // Children at one level under one parent (department for L1, sub-dept for L2/L3).
        Task<FetchAndResponse> GetAllAsync(int departmentId, int? parentSubDepartmentId, int depthLevel, bool onlyInactive = false, string? searchTerm = null);
        Task<ExecuteAndReponse> UpsertAsync(SubDepartmentUpsertDto dto, long currentEmployeeId);
        Task<ExecuteAndReponse> ToggleActiveAsync(int id, bool isActive, long currentEmployeeId);
        Task<FetchAndResponse> BulkUploadAsync(IFormFile file, long currentEmployeeId);
    }
}
