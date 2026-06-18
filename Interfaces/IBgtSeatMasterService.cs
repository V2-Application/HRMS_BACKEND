using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Roomsy.DTOS.GenericsResponses;
using HRMSAPI.DTO;

namespace HRMSAPI.Interfaces
{
    public interface IBgtSeatMasterService
    {
        Task<FetchAndResponse> UploadBgtSeatMasterExcelAsync(IFormFile file);
        Task<FetchAndResponse> GetAllBgtSeatMasterAsync(bool isExcel = false);
        Task<ExecuteAndReponse> DeleteSeatsBySeriesAsync(string locCode, int deptSno, int desgSno, int deleteCount);
        // Precise delete of specific seat entries (single row or bulk) by Loc+Dept+Desg+SeatNo.
        Task<ExecuteAndReponse> DeleteSeatsAsync(List<BgtSeatDeleteItem> items);
    }
}
