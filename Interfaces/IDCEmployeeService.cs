using HRMSAPI.DTO;
using Microsoft.AspNetCore.Identity.Data;

namespace HRMSAPI.Interfaces
{
    public interface IDCEmployeeService
    {
        Task<List<DCEmployeeDTO>> DCLoginAsync(DCLoginRequest request);
    }
}
