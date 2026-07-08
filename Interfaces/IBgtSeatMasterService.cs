using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Roomsy.DTOS.GenericsResponses;
using HRMSAPI.DTO;

namespace HRMSAPI.Interfaces
{
    public interface IBgtSeatMasterService
    {
        Task<FetchAndResponse> UploadBgtSeatMasterExcelAsync(IFormFile file, string? uploadedBy = null);
        Task<FetchAndResponse> GetAllBgtSeatMasterAsync(bool isExcel = false);
        Task<ExecuteAndReponse> DeleteSeatsBySeriesAsync(string locCode, int deptSno, int desgSno, int deleteCount, string? deletedBy = null);
        // Precise delete of specific seat entries (single row or bulk) by Loc+Dept+Desg+SeatNo.
        Task<ExecuteAndReponse> DeleteSeatsAsync(List<BgtSeatDeleteItem> items, string? deletedBy = null);
        // Delete ALL budget seats for one or more stores (LOC_CODE). Backs up the affected rows first.
        Task<ExecuteAndReponse> DeleteSeatsByStoreAsync(List<string> locCodes, string? deletedBy = null);
        // Delete EVERY budget seat (whole table). Backs up the full table first. Requires explicit confirm.
        Task<ExecuteAndReponse> DeleteAllSeatsAsync(string? deletedBy = null);
    }
}
