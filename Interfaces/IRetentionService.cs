using HRMSAPI.DTO;
using Roomsy.DTOS.GenericsResponses;
using System.Threading.Tasks;

namespace HRMSAPI.Interfaces
{
    public interface IRetentionService
    {
        Task<ExecuteAndReponse> CreateRetentionBonusAsync(RetentionBonusRequestDto request, string userId);
        Task<FetchAndResponse> GetRetentionBonusesAsync(string ecode);
        Task<ExecuteAndReponse> UpdateRetentionBonusStatusAsync(RetentionBonusStatusUpdateDto request, string userId);
    }
}

