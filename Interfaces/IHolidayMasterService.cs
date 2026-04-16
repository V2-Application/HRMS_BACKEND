using HRMSAPI.DTO;
using Microsoft.AspNetCore.Http;
using Roomsy.DTOS.GenericsResponses;
using System.Threading.Tasks;

namespace HRMSAPI.Interfaces
{
    public interface IHolidayMasterService
    {
        Task<ExecuteAndReponse> UpsertHolidayAsync(HolidayMasterUpsertDto holidayDto);
        Task<ExecuteAndReponse> DeleteHolidayAsync(int id);
        Task<FetchAndResponse> GetAllHolidaysAsync(string storeCodeOrGroupName = null, int? month = null);
        Task<FetchAndResponse> UploadHolidaysAsync(IFormFile file);
        Task ToggleActiveStatusAsync(List<int> ids,bool isActive,JwtLoginDetailDto updatedBy);
        Task UpsertPolicyDesignation(List<LocationDesignationPolicyDto> policies,JwtLoginDetailDto createdBy);
        //Task<PagedResult<LocationDesignationPolicyResponseDto>>GetByMonthYearAsync(string monthYear,int pageNumber,int pageSize,string? searchTerm);        
        Task<PagedResult<LocationDesignationPolicyResponseDto>>GetByMonthYearAsync(string monthYear,string? searchTerm);
        Task<List<LocationDesignationPolicyResponseDto>>GetByMonthYearForExcelAsync(string monthYear,string? searchTerm);
        Task<int> ImportPolicyDesignationAsync(IFormFile file,JwtLoginDetailDto createdBy);
    }
}
