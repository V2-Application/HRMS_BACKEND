using HRMSAPI.Data;
using HRMSAPI.DTO;
using Roomsy.DTOS.GenericsResponses;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HRMSAPI.Interfaces
{
    public interface IGroupService
    {
        Task<ExecuteAndReponse> UpsertGroupAsync(GroupUpsertDto groupDto);
        Task<ExecuteAndReponse> DeleteGroupAsync(int id);
        Task<FetchAndResponse> GetAllGroupsAsync();
        //Task<GroupMaster> GetGroupByIdAsync(int id);
    }
}
