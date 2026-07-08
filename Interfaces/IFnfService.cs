using HRMSAPI.DTO;
using HRMSAPI.Models.Auth;

namespace HRMSAPI.Interfaces
{
    public interface IFnfService
    {
        Task<PaginatedResponse<FnfEmployeeDropdownDto>> FetchEmployeesForFNF(string? ecode, string? globalSearch, DateTime? fromDate, DateTime? toDate, int page = 1, int pageSize = 20);
        Task<FnfIdResponse> SaveAdditionsAsync(FnfAdditionsDto dto);
        Task<FnfIdResponse> SaveDeductionsAsync(FnfDeductionsDto dto);
        Task<PaymentIdResponse> SavePaymentAsync(FnfPaymentDto dto);
        Task<FnfSaveAllResponse> SaveAllAsync(FnfSaveAllDto dto);
        Task<FnfAccountsListResponseDto> GetAccountsListAsync(string? search, DateTime? from, DateTime? to, string? paymentStatus, int page, int pageSize);
        Task<FnfAccountsListResponseDto> GetProcessedListAsync(string? search, DateTime? from, DateTime? to, string? paymentStatus, int page, int pageSize);
        Task<(List<Dictionary<string, object>> Rows, Dictionary<string, object>? Totals)> CalculateBonusAsync(BonusCalcRequestDto dto);
        Task<Dictionary<string, object>> CalculateLeaveEncashmentAsync(LeaveEncashmentRequestDto dto);
        Task<Dictionary<string, object>> CalculateGratuityAsync(GratuityRequestDto dto);
        Task<FnfBulkUploadResponseDto> BulkUploadAsync(FnfBulkUploadRequestDto request);
        Task<bool> BulkUploadFromExcelAsync(IFormFile file, string user);
        Task<FnfBulkUploadResponseDto> BulkUploadProcessedFromExcelAsync(IFormFile file, string user);
        Task<FnfBulkUploadResponseDto> UploadCompletedFNFExcelAsync(IFormFile file, string user);
        Task<int> UpdatePaymentStatusAsync(long fnfId, string status, string remarks);
        Task<byte[]> ExportToExcelAsync(string? search, DateTime? from, DateTime? to, string? paymentStatus);
        Task<byte[]> ExportAllFnfAsync(string? search, DateTime? from, DateTime? to, string? status);
        Task<byte[]> ExportPendingToExcelAsync();
        Task<Response> FnfPendingToProcessing(long employeeid);
        Task<string?> LocateTabByEcodeAsync(string ecode);

    }
}
