using HRMSAPI.DTO;
using static HRMSAPI.Implementation.OCRservice;

namespace HRMSAPI.Interfaces
{
    public interface IOCRservice
    {
        Task<List<OCRMasterResponseDto>> GetOCRMasterAsync(string? subject = null);
    }
}