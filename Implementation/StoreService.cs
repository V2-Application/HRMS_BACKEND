using HRMSAPI.Data;
using HRMSAPI.DTO;
using HRMSAPI.Interfaces;
using HRMSAPI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using HRMSContext = HRMSAPI.Data.HRMSContext;
using MyStoreLocation = HRMSAPI.Data.StoreLocation;

namespace HRMSAPI.Implementation
{
    public class StoreService : IStoreService
    {
        private readonly Data.HRMSContext _context;
        private readonly ILogger<StoreService> _logger;

        public StoreService(HRMSContext context, ILogger<StoreService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Data.StoreLocation> UpsertStoreLocationAsync(StoreLocationUpsertDto storeLocationDto, int? id = null)
        {
            if (storeLocationDto == null)
            {
                throw new ArgumentNullException(nameof(storeLocationDto), "Store location data is required.");
            }

            try
            {
                Data.StoreLocation storeLocation;

                if (id.HasValue && id > 0)
                {
                    storeLocation = await _context.StoreLocations.FindAsync(id.Value);
                    if (storeLocation == null && id != 0)
                    {
                        throw new ArgumentException($"Store location with ID {id.Value} not found.");
                    }

                    storeLocation.NameOfLocation = storeLocationDto.NameOfLocation ?? storeLocation.NameOfLocation;
                    storeLocation.LocationIncharge = storeLocationDto.LocationIncharge ?? storeLocation.LocationIncharge;
                    storeLocation.Address = storeLocationDto.Address ?? storeLocation.Address;
                    storeLocation.SAPCode = storeLocationDto.SAPCode ?? storeLocation.SAPCode;
                    storeLocation.Zone = storeLocationDto.Zone ?? storeLocation.Zone;
                    storeLocation.BillingOver50Lac = storeLocationDto.BillingOver50Lac ?? storeLocation.BillingOver50Lac;
                    storeLocation.PFCode = storeLocationDto.PFCode ?? storeLocation.PFCode;
                    storeLocation.ESICode = storeLocationDto.ESICode ?? storeLocation.ESICode;
                    storeLocation.Type = storeLocationDto.Type ?? storeLocation.Type;
                    storeLocation.StateId = storeLocationDto.StateId ?? storeLocation.StateId;
                    storeLocation.WeeklyOff = storeLocationDto.WeeklyOff ?? storeLocation.WeeklyOff;
                    storeLocation.EmailID = storeLocationDto.EmailID ?? storeLocation.EmailID;
                    storeLocation.ERPSiteNameCode = storeLocationDto.ERPSiteNameCode ?? storeLocation.ERPSiteNameCode;
                    storeLocation.Udf1 = storeLocationDto.Udf1 ?? storeLocation.Udf1;
                    storeLocation.Udf2 = storeLocationDto.Udf2 ?? storeLocation.Udf2;
                    storeLocation.Udf3 = storeLocationDto.Udf3 ?? storeLocation.Udf3;
                    storeLocation.LastupdatedBy = "System";
                    storeLocation.CreatedOn = storeLocation.CreatedOn;
                    storeLocation.CreatedBy = storeLocation.CreatedBy;
                }
                else
                {
                    storeLocation = new Data.StoreLocation
                    {
                        NameOfLocation = storeLocationDto.NameOfLocation,
                        LocationIncharge = storeLocationDto.LocationIncharge,
                        Address = storeLocationDto.Address,
                        SAPCode = storeLocationDto.SAPCode,
                        Zone = storeLocationDto.Zone,
                        BillingOver50Lac = storeLocationDto.BillingOver50Lac,
                        PFCode = storeLocationDto.PFCode,
                        ESICode = storeLocationDto.ESICode,
                        Type = storeLocationDto.Type,
                        StateId = (int)storeLocationDto.StateId,
                        WeeklyOff = storeLocationDto.WeeklyOff,
                        EmailID = storeLocationDto.EmailID,
                        ERPSiteNameCode = storeLocationDto.ERPSiteNameCode,
                        Udf1 = storeLocationDto.Udf1,
                        Udf2 = storeLocationDto.Udf2,
                        Udf3 = storeLocationDto.Udf3,
                        CreatedBy = "System",
                        CreatedOn = DateTime.UtcNow,
                        LastupdatedBy = "System"
                    };

                    if (!await _context.States.AnyAsync(s => s.StateId == storeLocation.StateId))
                    {
                        throw new ArgumentException("Invalid StateId");
                    }

                    _context.StoreLocations.Add(storeLocation);
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("Store location {Action} successfully with ID: {Id}", id.HasValue ? "updated" : "created", storeLocation.StoreLocationsId);
                return storeLocation;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error while {Action} store location", id.HasValue ? "updating" : "creating");
                throw new Exception($"Error {(id.HasValue ? "updating" : "creating")} store location: {ex.InnerException?.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error {Action} store location", id.HasValue ? "updating" : "creating");
                throw;
            }
        }

        //public async Task<List<tblStoreBudget>> UpsertStoreBudgetAsync(List<StoreBudgetUpsertDto> storeBudgetDtos)
        //{
        //    if (storeBudgetDtos == null || !storeBudgetDtos.Any())
        //        throw new ArgumentException("Store budget list is required.");

        //    var results = new List<tblStoreBudget>();

        //    foreach (var dto in storeBudgetDtos)
        //    {
        //        if (dto == null)
        //            continue;

        //        tblStoreBudget storeBudget;

        //        // If ID is present, update
        //        if (dto.StoreBudgetId.HasValue && dto.StoreBudgetId > 0)
        //        {
        //            storeBudget = await _context.tblStoreBudgets.FindAsync(dto.StoreBudgetId.Value);
        //            if (storeBudget == null)
        //                throw new ArgumentException($"Store budget with ID {dto.StoreBudgetId.Value} not found.");

        //            storeBudget.StoreLocationsId = dto.StoreLocationsId ?? storeBudget.StoreLocationsId;
        //            storeBudget.DesignationId = dto.DesignationId ?? storeBudget.DesignationId;
        //            storeBudget.BudgetManpowerCount = dto.BudgetManpowerCount ?? storeBudget.BudgetManpowerCount;
        //            storeBudget.BudgetAmount = dto.BudgetAmount ?? storeBudget.BudgetAmount;

        //            storeBudget.LastupdatedBy = "System";
        //            storeBudget.CreatedOn = storeBudget.CreatedOn;
        //            storeBudget.CreatedBy = storeBudget.CreatedBy;
        //        }
        //        else // Insert new
        //        {
        //            storeBudget = new tblStoreBudget
        //            {
        //                StoreLocationsId = (int)dto.StoreLocationsId,
        //                DesignationId = (int)dto.DesignationId,
        //                BudgetManpowerCount = (int)dto.BudgetManpowerCount,
        //                BudgetAmount = (decimal)dto.BudgetAmount,

        //                CreatedBy = "System",
        //                CreatedOn = DateTime.UtcNow,
        //                LastupdatedBy = "System"
        //            };

        //            // Optional validations
        //            if (!await _context.StoreLocations.AnyAsync(sl => sl.StoreLocationsId == storeBudget.StoreLocationsId))
        //                throw new ArgumentException($"Invalid StoreLocationsId {storeBudget.StoreLocationsId}");

        //            if (!await _context.tblDesignations.AnyAsync(d => d.DesignationId == storeBudget.DesignationId))
        //                throw new ArgumentException($"Invalid DesignationId {storeBudget.DesignationId}");

        //            _context.tblStoreBudgets.Add(storeBudget);
        //        }

        //        results.Add(storeBudget);
        //    }

        //    await _context.SaveChangesAsync();
        //    return results;
        //}

        public async Task<List<tblStoreBudget>> UpsertStoreBudgetAsync(List<StoreBudgetUpsertDto> storeBudgetDtos)
        {
            if (storeBudgetDtos == null || !storeBudgetDtos.Any())
                throw new ArgumentException("Store budget list is required.");

            var results = new List<tblStoreBudget>();

            foreach (var dto in storeBudgetDtos)
            {
                if (dto == null)
                    continue;

                tblStoreBudget storeBudget;

                // Update
                if (dto.StoreBudgetId.HasValue && dto.StoreBudgetId > 0)
                {
                    storeBudget = await _context.tblStoreBudgets.FindAsync(dto.StoreBudgetId.Value);
                    if (storeBudget == null)
                        throw new ArgumentException($"Store budget with ID {dto.StoreBudgetId.Value} not found.");

                    storeBudget.StoreLocationsId = dto.StoreLocationsId ?? storeBudget.StoreLocationsId;
                    storeBudget.DesignationId = dto.DesignationId ?? storeBudget.DesignationId;
                    storeBudget.BudgetManpowerCount = dto.BudgetManpowerCount ?? storeBudget.BudgetManpowerCount;
                    storeBudget.BudgetAmount = dto.BudgetAmount ?? storeBudget.BudgetAmount;

                    storeBudget.LastupdatedBy = "System";
                }
                else // Insert
                {
                    storeBudget = new tblStoreBudget
                    {
                        StoreLocationsId = (int)dto.StoreLocationsId,
                        DesignationId = (int)dto.DesignationId,
                        BudgetManpowerCount = (int)dto.BudgetManpowerCount,
                        BudgetAmount = (decimal)dto.BudgetAmount,
                        CreatedBy = "System",
                        CreatedOn = DateTime.UtcNow,
                        LastupdatedBy = "System"
                    };

                    if (!await _context.StoreLocations.AnyAsync(sl => sl.StoreLocationsId == storeBudget.StoreLocationsId))
                        throw new ArgumentException($"Invalid StoreLocationsId {storeBudget.StoreLocationsId}");

                    if (!await _context.tblDesignations.AnyAsync(d => d.DesignationId == storeBudget.DesignationId))
                        throw new ArgumentException($"Invalid DesignationId {storeBudget.DesignationId}");

                    _context.tblStoreBudgets.Add(storeBudget);
                    await _context.SaveChangesAsync(); // Save to get StoreBudgetId

                    // 🔹 Generate Job Positions
                    var location = await (from sl in _context.StoreLocations
                                          join loc in _context.tblLocations on sl.LocationId equals loc.LocationId
                                          where sl.StoreLocationsId == storeBudget.StoreLocationsId
                                          select new { loc.STCode }).FirstOrDefaultAsync();

                    if (location == null)
                        throw new ArgumentException($"Location not found for StoreLocationsId {storeBudget.StoreLocationsId}");

                    var jobPositions = new List<tblJobPosition>();
                    for (int i = 1; i <= storeBudget.BudgetManpowerCount; i++)
                    {
                        var jobId = $"{location.STCode}-{storeBudget.DesignationId:D2}-{storeBudget.StoreLocationsId:D2}-{i:D2}";
                        jobPositions.Add(new tblJobPosition
                        {
                            StoreBudgetId = storeBudget.StoreBudgetId,
                            JobId = jobId,
                            IsFilled = false,
                            CreatedBy = "System",
                            CreatedOn = DateTime.UtcNow
                        });
                    }

                    _context.tblJobPositions.AddRange(jobPositions);
                }

                results.Add(storeBudget);
            }

            await _context.SaveChangesAsync();
            return results;
        }

        public async Task<List<StoreDetailDto>> GetStoreLocationsAsync(int? storeLocationsId = null)
        {
            return await _context.StoreLocations
                .Include(sl => sl.tblStoreBudgets)
                .Where(sl => !storeLocationsId.HasValue || sl.StoreLocationsId == storeLocationsId)
                .Select(sl => new StoreDetailDto
                {
                    StoreLocationsId = sl.StoreLocationsId,
                    NameOfLocation = sl.NameOfLocation,
                    LocationIncharge = sl.LocationIncharge,
                    Address = sl.Address,
                    SAPCode = sl.SAPCode,
                    Zone = sl.Zone,
                    BillingOver50Lac = sl.BillingOver50Lac ?? false,
                    PFCode = sl.PFCode,
                    ESICode = sl.ESICode,
                    Type = sl.Type,
                    StateId = sl.StateId,
                    StateName = _context.States
                                    .Where(s => s.StateId == sl.StateId)
                                    .Select(s => s.StateName)
                                    .FirstOrDefault(),
                    WeeklyOff = sl.WeeklyOff,
                    EmailID = sl.EmailID,
                    ERPSiteNameCode = sl.ERPSiteNameCode,
                    CreatedBy = sl.CreatedBy,
                    CreatedOn = (DateTime)sl.CreatedOn,
                    LastupdatedBy = sl.LastupdatedBy,
                    StoreBudgets = sl.tblStoreBudgets.Select(b => new StoreBudgetUpsertDto
                    {
                        StoreBudgetId = b.StoreBudgetId,
                        StoreLocationsId = b.StoreLocationsId,
                        DesignationId = b.DesignationId,
                        DesignationName = _context.tblDesignations
                                                .Where(d => d.DesignationId == b.DesignationId)
                                                .Select(d => d.DesignationName)
                                                .FirstOrDefault(),
                        BudgetManpowerCount = b.BudgetManpowerCount,
                        BudgetAmount = b.BudgetAmount,

                    }).ToList()
                }).ToListAsync();
        }
        public async Task<List<StoreBudgetUpsertDto>> GetStoreBudgetsAsync(int? id = null)
        {
            return await _context.tblStoreBudgets
                .Where(b => !id.HasValue || b.StoreBudgetId == id.Value)
                .Select(b => new StoreBudgetUpsertDto
                {
                    StoreBudgetId = b.StoreBudgetId,
                    StoreLocationsId = b.StoreLocationsId,
                    DesignationId = b.DesignationId,
                    DesignationName = b.Designation.DesignationName,
                    BudgetManpowerCount = b.BudgetManpowerCount,
                    BudgetAmount = b.BudgetAmount,

                })
                .ToListAsync();
        }
        public async Task<PaginatedResponse<StoreMasterBulk>> GetStoreLocationsBulkAsync(int? pageNumber, int? pageSize)
        {
            IQueryable<StoreMasterBulk> query = _context.StoreMasterBulks;
            int totalRecords = await query.CountAsync();

            var paginatedResponse = new PaginatedResponse<StoreMasterBulk>
            {
                TotalRecords = totalRecords
            };

            // If pageNumber or pageSize is null or 0, return all records
            if (!pageNumber.HasValue || !pageSize.HasValue || pageNumber.Value == 0 || pageSize.Value == 0)
            {
                paginatedResponse.Data = await query.ToListAsync();
                paginatedResponse.PageNumber = 1;
                paginatedResponse.PageSize = totalRecords; // Set pageSize to totalRecords
            }
            else
            {
                int page = pageNumber.Value;
                int size = pageSize.Value;

                // Ensure valid page and size
                if (page < 1) page = 1;
                if (size < 1) size = 10;

                paginatedResponse.Data = await query
                    .Skip((page - 1) * size)
                    .Take(size)
                    .ToListAsync();
                paginatedResponse.PageNumber = page;
                paginatedResponse.PageSize = size;
            }

            return paginatedResponse;
        }

        public async Task<IEnumerable<tblLocation>> GetStoresByRecord(int records)
        {
            return await _context.tblLocations
                .OrderByDescending(c => c.CreatedOn).Take(records)
                .ToListAsync();
        }
        public async Task<IEnumerable<vw_tblLocation_UPC>> GetStoresByMonthAsync( int? records = null)
        {
            //if (string.IsNullOrWhiteSpace(monthYear))
            //    throw new ArgumentException("Month-Year is required in format 'MMM-yy'. Example: Jan-24", nameof(monthYear));

            // Parse "MMM-yy" into DateTime
            IQueryable<vw_tblLocation_UPC> query = _context.vw_tblLocation_UPCs
        .AsNoTracking();
             query = query
       .OrderBy(x => x.CreatedOn)   // deterministic order for "first"
       .ThenBy(x => x.STCode);

            // If any 'records' value is supplied, take only the first record
            if (records.HasValue)
                query = query.Take(records.Value);

            var res = await query.ToListAsync();

            if (res==null || res.Count == 0)
                throw new ArgumentException("No data found.");

            return res;
        }
    }
}
