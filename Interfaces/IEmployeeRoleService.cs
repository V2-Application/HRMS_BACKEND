using HRMSAPI.DTO;
using Roomsy.DTOS.GenericsResponses;
using System.Threading.Tasks;

namespace HRMSAPI.Interfaces
{
    public interface IEmployeeRoleService
    {
        Task<ExecuteAndReponse> BulkUpsertEmployeeRolesAsync(EmployeeRoleBulkUpsertDto request);
        Task<FetchAndResponse> GetAllEmployeeRolesAsync();
    }
}
