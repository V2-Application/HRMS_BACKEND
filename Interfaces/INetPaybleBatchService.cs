using HRMSAPI.DTO;
using Roomsy.DTOS.GenericsResponses;

namespace HRMSAPI.Interfaces
{
    public interface INetPaybleBatchService
    {
        Task<FetchAndResponse> GetNetPaybleBatchListAsync(string? ecode = null, bool asExcel = false, int? page = null, int? pageSize = null);
        Task<byte[]> ExportNetPaybleBatchToExcelAsync(string? ecode = null);
        Task<List<NetPaybleBatchDto>> GetNetPaybleBatchDataAsync(string? ecode = null, int? page = null, int? pageSize = null);
    }
}
