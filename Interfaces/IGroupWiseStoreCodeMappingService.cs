using HRMSAPI.DTO;
using Microsoft.AspNetCore.Http;
using Roomsy.DTOS.GenericsResponses;
using System.Threading.Tasks;

namespace HRMSAPI.Interfaces
{
    public interface IGroupWiseStoreCodeMappingService
    {
        Task<ExecuteAndReponse> UpsertGroupWiseStoreCodeMappingAsync(GroupWiseStoreCodeMappingUpsertDto mappingDto);
        Task<ExecuteAndReponse> DeleteGroupWiseStoreCodeMappingAsync(int id);
        Task<FetchAndResponse> GetAllGroupWiseStoreCodeMappingsAsync();
        Task<FetchAndResponse> GetAllGroupCodeMappingsAsync(int id);
        Task<FetchAndResponse> UploadGroupWiseStoreCodeMappingAsync(IFormFile file);
    }
}
