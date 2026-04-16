using HRMSAPI.DTO;

namespace HRMSAPI.Interfaces
{
    public interface IStoreRoutingService
    {
        Task<List<StoreRoutingStatusDTO>> GetStoreRoutingStatusAsync(int locationId);
        Task<StoreRoutingResponse> GetStoreRoutingStatusByLocationIdAsync(int locationId);
        Task<(bool Success, string Message)> AddStoreRoutingTransactionAsync(StoreRoutingTransactionDTO model);
    }
}
