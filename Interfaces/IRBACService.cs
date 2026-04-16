using HRMSAPI.DTO;
using Roomsy.DTOS.GenericsResponses;

namespace HRMSAPI.Interfaces
{
    public interface IRBACService
    {
        Task<ExecuteAndReponse> UpsertModules(List<ModuleDto> moduleDtos);
        Task<FetchAndResponse> GetRbacHierarchyAsync();
        Task<ExecuteAndReponse> UpsertRbacNodes(List<RolePermissionPost> postrequest);
        Task<FetchAndResponse> GetModulesForUpsertAsync();
        Task<ExecuteAndReponse> DeleteModuleAsync(int id);
        Task<ExecuteAndReponse> DeleteSubModuleAsync(int id);
        Task<ExecuteAndReponse> DeleteActionAsync(int id);
        Task<ExecuteAndReponse> DeleteFurtherPartAsync(int id);
        Task<ExecuteAndReponse> UpsertRoleAsync(RoleDto roleDto);
        Task<ExecuteAndReponse> DeleteRoleAsync(int id);
        Task<ExecuteAndReponse> UpsertEmployeeRoleAsync(EmployeeRoleDto employeeRoleDto);
        Task<FetchAndResponse> GetEmployeeRolesAsync(long employeeId);
        Task<ExecuteAndReponse> DeleteEmployeeRoleAsync(long employeeId, int roleId);
    }
}
