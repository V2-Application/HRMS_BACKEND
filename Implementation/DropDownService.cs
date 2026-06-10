using DocumentFormat.OpenXml.ExtendedProperties;
using HRMSAPI.Data;
using HRMSAPI.DTO;
using HRMSAPI.Interfaces;
using HRMSAPI.Models.Candidate;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HRMSAPI.Implementation
{
    public class DropDownService : IDropDownService
    {
        private readonly IConfiguration _configuration;
        private readonly HRMSContext _context;
        private readonly ILogger<DropDownService> _logger;

        public DropDownService(HRMSContext context, IConfiguration configuration, ILogger<DropDownService> logger)
        {
            _configuration = configuration;
            _context = context;
            _logger = logger;
        }

        #region GetDesignation
        public async Task<IEnumerable<DesignationDto>> GetDesignation()
        {
            return await _context.tblDesignations
                .Where(l => (l.isActive == null || l.isActive == true) && (l.isDeleted == null || l.isDeleted == false))
                .Select(l => new DesignationDto
                {
                    DesignationId = l.DesignationId,
                    DesignationName = l.DesignationName
                }).ToListAsync();
        }

        public async Task<IEnumerable<DesignationDto>> GetDesignationsByDepartment(int? deptId)
        {
            var result = await _context.ufn_GetDesignations(deptId)
                .Select(d => new DesignationDto
                {
                    DesignationId = d.DesignationId,
                    DesignationName = d.DesignationName
                }).ToListAsync();

            return result;
        }

        #endregion

        #region GetLocation
        public async Task<IEnumerable<LocationDto>> GetLocation()
        {
            return await _context.tblLocations.AsNoTracking().Where(row=>row.IsDeleted==false)
                .Select(l => new LocationDto
                {
                    LocationId = l.LocationId,
                    LocationName = l.STCode==null?l.LocationName:l.STCode+"-" +l.LocationName
                })
                .ToListAsync();
        }
        #endregion



        #region GetStoreLocation
        public async Task<IEnumerable<LocationStoreDto>> GetStoreLocation()
        {
            return await _context.tblLocations
                
                .Select(l => new LocationStoreDto
                {
                    StCode = l.STCode,
                    StoreLocationName = l.STCode == null ? l.LocationName : l.LocationName + "_" + l.STCode,
                    Status = l.IsActive,
                    
                })
                 
                .ToListAsync();
        }
        #endregion

        #region GetLeaveType
        public async Task<IEnumerable<LeaveTypeDto>> GetLeaveTypes()
        {
            return await _context.tblLeaveTypes
                .Select(lt => new LeaveTypeDto
                {
                    LeaveTypeId = lt.LeaveTypeId,
                    LeaveTypeName = lt.LeaveTypeName
                })
                .ToListAsync();
        }
        #endregion

        #region GetDepartment
        public async Task<IEnumerable<DepartmentDto>> GetDepartments()
        {
            return await _context.tblDepartments
                .Where(d => (d.isActive == null || d.isActive == true) && (d.isDeleted == null || d.isDeleted == false))
                .Select(d => new DepartmentDto
                {
                    DepartmentId = d.DepartmentId,
                    DepartmentName = d.DepartmentName
                })
                .ToListAsync();
        }
        #endregion

        public async Task<List<Country>> GetCountriesAsync()
        {
            return await _context.Countries.ToListAsync();
        }

        public async Task<List<State>> GetStatesByCountryIdAsync(int countryId)
        {
            return await _context.States.Where(s => s.CountryId == countryId).ToListAsync();
        }

        public async Task<List<City>> GetCitiesByStateIdAsync(int stateId)
        {
            return await _context.Cities.Where(c => c.StateId == stateId).ToListAsync();
        }
        public async Task<IEnumerable<CompanyDto>> GetCompany()
        {
            return await _context.tblCompanies
                .Select(d => new CompanyDto
                {
                    CompanyId = d.CompanyId,
                    CompanyName = d.CompanyName
                })
                .ToListAsync();
        }
        public async Task<IEnumerable<ReasonForLeavingDto>> ReasonForSeparation()
        {
            return await _context.ReasonForLeavings
                .Select(d => new ReasonForLeavingDto
                {
                    ReasonID = d.ReasonID,
                    ReasonForLeaving = d.ReasonForLeaving1
                })
                .ToListAsync();
        }
        public async Task<IEnumerable<ResignationTypeDto>> GetResignationType()
        {
            return await _context.tblResignationTypes
                .Select(d => new ResignationTypeDto
                {
                    ResignationTypeId = d.ResignationTypeId,
                    ResignationTypeName = d.ResignationTypeName
                })
                .ToListAsync();
        }
        public async Task<List<AbscondingReason>> GetAbscondingReasonsByResignationTypeIdAsync(int resignationTypeId)
        {
            return await _context.tblAbscondingReasons
                .Where(a => a.ResignationTypeId == resignationTypeId)
                .Select(a => new AbscondingReason
                {
                    AbscondingReasonId = a.AbscondingReasonId,
                    ResignationTypeId = a.ResignationTypeId,
                    AbscondingReasonName = a.AbscondingReasonName
                })
                .ToListAsync();
        }

        public async Task<List<BlackListReason>> GetBlackListReasonsByResignationTypeIdAsync(int resignationTypeId)
        {
            return await _context.tblBlacklistReasons
                .Where(a => a.ResignationTypeId == resignationTypeId)
                .Select(a => new BlackListReason
                {
                    BlackListReasonId = a.BlacklistReasonId,
                    ResignationTypeId = a.ResignationTypeId,
                    BlacklListReasonName = a.BlacklistReasonName
                })
                .ToListAsync();
        }

        public async Task<IEnumerable<ShiftMasterDto>> GetShiftMaster()
        {
            return await _context.tblShiftMasters

                .Select(l => new ShiftMasterDto
                {
                    ShiftID = l.ShiftID,
                    ShiftName = l.ShiftName,
                    StartTime = l.StartTime,
                    EndTime = l.EndTime
                    
                })

                .ToListAsync();
        }
        public async Task<List<LocationCategoryDropdownDto>> GetLocationDesignationPolicyCategory()
        {
            return await _context.LocationDesignationPolicyCategories
                .Where(x => x.IsActive && !x.IsDeleted)
                .OrderBy(x => x.CategoryName)
                .Select(x => new LocationCategoryDropdownDto
                {
                    Id = x.LocationCategoryId,
                    CategoryCode = x.CategoryCode,
                    CategoryName = x.CategoryName
                }).ToListAsync();

        }
    }
}