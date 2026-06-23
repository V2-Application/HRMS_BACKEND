using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Roomsy.DTOS.GenericsResponses;
using HRMSAPI.DTO;

namespace HRMSAPI.Interfaces
{
    // Fetch/export + UPDATE-ONLY upload (no inserts) for the PTax (PTPolicyMaster) and
    // LWF (LWFPolicyMaster) masters, plus single-row update from the UI.
    public interface IPolicyMasterService
    {
        // ----- Professional Tax (PTPolicyMaster) -----
        Task<FetchAndResponse> UploadPtaxExcelAsync(IFormFile file);
        Task<FetchAndResponse> GetAllPtaxAsync(bool isExcel = false);
        Task<ExecuteAndReponse> UpdatePtaxAsync(PtaxUpdateDto dto);
        Task<ExecuteAndReponse> CreatePtaxAsync(PtaxUpdateDto dto);

        // ----- Labour Welfare Fund (LWFPolicyMaster) -----
        Task<FetchAndResponse> UploadLwfExcelAsync(IFormFile file);
        Task<FetchAndResponse> GetAllLwfAsync(bool isExcel = false);
        Task<ExecuteAndReponse> UpdateLwfAsync(LwfUpdateDto dto);
        Task<ExecuteAndReponse> CreateLwfAsync(LwfUpdateDto dto);
    }
}
