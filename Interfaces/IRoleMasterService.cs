using HRMSAPI.DTO;
using Microsoft.AspNetCore.Http;
using Roomsy.DTOS.GenericsResponses;
using System.Threading.Tasks;

namespace HRMSAPI.Interfaces
{
    /// <summary>
    /// Role Master page (Masters -> Role Master): the HR-maintained role list.
    /// List every role including inactive ones, create, rename, and switch active
    /// or inactive.
    ///
    /// Separate from the V2 Parivar portal/RBAC roles (tblRole), which drive page
    /// access and are not editable here.
    /// </summary>
    public interface IRoleMasterService
    {
        Task<FetchAndResponse> GetAllRolesAsync();
        Task<FetchAndResponse> GetActiveRolesAsync();
        Task<FetchAndResponse> GetRoleByIdAsync(int roleId);
        Task<ExecuteAndReponse> CreateRoleAsync(RoleMasterUpsertDto roleDto, string createdBy);
        Task<ExecuteAndReponse> UpdateRoleAsync(int roleId, RoleMasterUpsertDto roleDto, string updatedBy);
        Task<ExecuteAndReponse> ToggleRoleStatusAsync(int roleId, string updatedBy);

        /// <summary>Bulk create/update roles from an Excel sheet.</summary>
        Task<FetchAndResponse> BulkUploadRolesAsync(IFormFile file, string uploadedBy);

        /// <summary>The upload template: headers plus a couple of example rows.</summary>
        byte[] BuildUploadTemplate();
    }
}
