using HRMSAPI.DTO;
using Microsoft.AspNetCore.Http;
using Roomsy.DTOS.GenericsResponses;
using System.Threading.Tasks;

namespace HRMSAPI.Interfaces
{
    public interface IEcodeWiseWeekOffMappingService
    {
        Task<FetchAndResponse> GetAllEcodeWiseWeekOffMappingsAsync();
        Task<ExecuteAndReponse> UpsertEcodeWiseWeekOffMappingAsync(EcodeWiseWeekOffMappingUpsertDto dto);
        Task<ExecuteAndReponse> DeleteEcodeWiseWeekOffMappingAsync(long id);
        Task<ExecuteAndReponse> UploadEcodeWiseWeekOffMappingAsync(IFormFile file);
    }
}

