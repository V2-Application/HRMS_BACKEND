using HRMSAPI.DTO;
using Roomsy.DTOS.GenericsResponses;
using System.Threading.Tasks;

namespace HRMSAPI.Interfaces
{
    public interface IEcodeWiseBonusProvisioningPolicyMappingService
    {
        Task<FetchAndResponse> GetAllEcodeWiseBonusProvisioningPolicyMappingsAsync();
        Task<FetchAndResponse> GetAllBonusProvisioningPoliciesAsync();
        Task<ExecuteAndReponse> UpsertEcodeWiseBonusProvisioningPolicyMappingAsync(EcodeWiseBonusProvisioningPolicyMappingUpsertDto dto, string userId);
        Task<ExecuteAndReponse> DeleteEcodeWiseBonusProvisioningPolicyMappingAsync(Guid id);
    }
}

