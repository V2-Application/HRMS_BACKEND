using Roomsy.DTOS.GenericsResponses;

namespace HRMSAPI.Interfaces
{
    public interface IJDService
    {
        Task<ExecuteAndReponse> UpsertJDsAsync(List<JDUpsertDto> jdList);
        Task<ExecuteAndReponse> DeleteJDAsync(int jdId);
        Task<FetchAndResponse> GetAllJDsAsync();
    }
}
