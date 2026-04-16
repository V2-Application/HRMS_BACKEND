using HRMSAPI.Data;
using HRMSAPI.DTO;
using Microsoft.AspNetCore.Mvc;

namespace HRMSAPI.Interfaces
{
    public interface IDropDownService
    {
        Task<IEnumerable<DesignationDto>> GetDesignation();
        Task<IEnumerable<DesignationDto>> GetDesignationsByDepartment(int? deptId);
        Task<IEnumerable<DepartmentDto>> GetDepartments();
        Task<IEnumerable<CompanyDto>> GetCompany();
        Task<IEnumerable<ReasonForLeavingDto>> ReasonForSeparation();
        Task<IEnumerable<ResignationTypeDto>> GetResignationType();


        Task<IEnumerable<LocationDto>> GetLocation();
        Task<IEnumerable<LocationStoreDto>> GetStoreLocation();
        Task<IEnumerable<LeaveTypeDto>> GetLeaveTypes();
        Task<List<Country>> GetCountriesAsync();
        Task<List<State>> GetStatesByCountryIdAsync(int countryId);
        Task<List<City>> GetCitiesByStateIdAsync(int stateId);
        Task<List<AbscondingReason>> GetAbscondingReasonsByResignationTypeIdAsync(int resignationTypeId);
        Task<List<BlackListReason>> GetBlackListReasonsByResignationTypeIdAsync(int resignationTypeId);
        Task<IEnumerable<ShiftMasterDto>> GetShiftMaster();
        Task<List<LocationCategoryDropdownDto>> GetLocationDesignationPolicyCategory();


    }
}
