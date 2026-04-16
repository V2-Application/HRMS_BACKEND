using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Roomsy.DTOS.GenericsResponses;

namespace HRMSAPI.Interfaces
{
    public interface IBgtSeatMasterService
    {
        Task<FetchAndResponse> UploadBgtSeatMasterExcelAsync(IFormFile file);
        Task<FetchAndResponse> GetAllBgtSeatMasterAsync(bool isExcel = false);
        Task<ExecuteAndReponse> DeleteSeatsBySeriesAsync(string locCode, int deptSno, int desgSno, int deleteCount);
    }
}
