using System.Threading.Tasks;
using Roomsy.DTOS.GenericsResponses;

namespace HRMSAPI.Interfaces
{
    public interface IViewService
    {
        Task<byte[]> ExportEmpAttendanceFormatToExcelAsync(string ecode = null);
        Task<byte[]> ExportBgtSalaryStructWithEmpDetailsToExcelAsync(string ecode = null);
        Task<byte[]> ExportLeaveMasterToExcelAsync(string ecode = null);
        Task<byte[]> ExportPfMasterToExcelAsync(string ecode = null);
        Task<byte[]> ExportEsicMasterToExcelAsync(string ecode = null);
        Task<byte[]> ExportTotalDeductionToExcelAsync(string ecode = null);

        Task<FetchAndResponse> GetTotalDeductionListAsync(string ecode = null, bool asExcel = false, int? page = null, int? pageSize = null);
        Task<FetchAndResponse> GetEsicMaster(string ecode = null, bool asExcel = false, int? page = null, int? pageSize = null);
        Task<FetchAndResponse> GetLeaveMaster(string ecode = null, bool asExcel = false, int? page = null, int? pageSize = null);
        Task<FetchAndResponse> GetPfMaster(string ecode = null, bool asExcel = false, int? page = null, int? pageSize = null);
        Task<FetchAndResponse> GetBgtSalaryWithEmpDetails(string ecode = null, bool asExcel = false, int? page = null, int? pageSize = null);
        Task<FetchAndResponse> GetEmpAttendanceFormat(string ecode = null, bool asExcel = false, int? page = null, int? pageSize = null);
        Task<ExecuteAndReponse> UploadEmployeeDeductionsExcelAsync(IFormFile file);
        Task<FetchAndResponse> GetNetPaybleListAsync(string ecode = null, bool asExcel = false, int? page = null, int? pageSize = null);
        Task<FetchAndResponse> GetSalaryFormatAsync(string ecode = null, bool asExcel = false, int page = 1, int pageSize = 20);
        Task<FetchAndResponse> GetPaybleDaysAsync(string ecode = null, bool asExcel = false, int? page = null, int? pageSize = null);
    }
} 