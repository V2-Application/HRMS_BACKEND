using HRMSAPI.DTO;

namespace HRMSAPI.Interfaces
{
    public interface IBankTransferService
    {
        Task<(bool Success, string Message)> UploadBankTransferDataAsync(IFormFile file);
        Task<(List<BankTransferDTO> Records, int TotalRecords)> GetBankTransferList(
    string? searchTerm = null,
    string? ecode = null,
    int page = 1,
    int pageSize = 10);
    }
}