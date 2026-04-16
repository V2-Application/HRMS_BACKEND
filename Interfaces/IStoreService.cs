// Interfaces/IStoreService.cs
using HRMSAPI.Data;
using HRMSAPI.DTO;
using HRMSAPI.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HRMSAPI.Interfaces
{
    public interface IStoreService
    {
        Task<StoreLocation> UpsertStoreLocationAsync(StoreLocationUpsertDto storeLocationDto, int? id = null);
        Task<List<tblStoreBudget>> UpsertStoreBudgetAsync(List<StoreBudgetUpsertDto> storeBudgetDtos);

        Task<List<StoreBudgetUpsertDto>> GetStoreBudgetsAsync(int? id = null);

        Task<List<StoreDetailDto>> GetStoreLocationsAsync(int? storeLocationsId = null);
        Task<PaginatedResponse<StoreMasterBulk>> GetStoreLocationsBulkAsync(int? pageNumber, int? pageSize);
        Task<IEnumerable<tblLocation>> GetStoresByRecord(int records);
        Task<IEnumerable<vw_tblLocation_UPC>> GetStoresByMonthAsync(int? records = null);
    }
}