using HRMSAPI.DTO;
using Roomsy.DTOS.GenericsResponses;
using System.Threading.Tasks;

namespace HRMSAPI.Interfaces
{
    public interface IShiftMasterService
    {
        Task<ExecuteAndReponse> CreateShiftAsync(ShiftMasterUpsertDto shiftDto, string createdBy);
        Task<ExecuteAndReponse> UpdateShiftAsync(int shiftId, ShiftMasterUpsertDto shiftDto, string updatedBy);
        Task<FetchAndResponse> GetAllShiftsAsync();
        Task<FetchAndResponse> GetShiftByIdAsync(int shiftId);
        Task<ExecuteAndReponse> DeleteShiftAsync(int shiftId);
        Task<ExecuteAndReponse> ToggleShiftStatusAsync(int shiftId, string updatedBy);
    }
}

