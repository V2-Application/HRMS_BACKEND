using ClosedXML.Excel;
using HRMSAPI.Data;
using HRMSAPI.DTO;
using HRMSAPI.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using Roomsy.DTOS.GenericsResponses;
using System.Data;
using System.IO;
using System.Net;

namespace HRMSAPI.Implementation
{
    public class LocationService : BaseService, ILocationService
    {
        private readonly IConfiguration _configuration;
        private readonly HRMSContext _context;
        private readonly ILogger<LocationService> _logger;

        public LocationService(HRMSContext context, IConfiguration configuration, ILogger<LocationService> logger) : base(context)
        {
            _configuration = configuration;
            _context = context;
            _logger = logger;
        }

        public async Task<FetchAndResponse> UploadLocationsExcelAsync(IFormFile file,string? updatedBy)
        {
            var expectedHeaders = new[] { "LOC CODE", "LOCATION", "ZONE", "REGION", "CLUSTER", "STATE", "STATUS", "OPENING DATE" };
            if (file == null || file.Length == 0)
                return BuildFetchErrorResponse("No file uploaded", HttpStatusCode.BadRequest);

            using var stream = file.OpenReadStream();
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheet(1);

            // Validate headers
            for (int i = 0; i < expectedHeaders.Length; i++)
            {
                var cellValue = worksheet.Cell(1, i + 1).GetValue<string>().Trim();
                if (!string.Equals(cellValue, expectedHeaders[i], StringComparison.OrdinalIgnoreCase))
                    return BuildFetchErrorResponse($"Header mismatch at column {i + 1}: Expected '{expectedHeaders[i]}', found '{cellValue}'", HttpStatusCode.BadRequest);
            }

            // Save uploaded file to wwwroot folder
            try
            {
                var uploadPath = await SaveUploadedFileAsync(file, updatedBy);
                _logger.LogInformation($"File saved to: {uploadPath}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error saving file: {ex.Message}");
                return BuildFetchErrorResponse($"Error saving uploaded file: {ex.Message}", HttpStatusCode.InternalServerError);
            }

            var rows = worksheet.RowsUsed().Skip(1).ToList();

            try
            {
                // ── PASS 1: validate all rows + collect parsed data (no DB writes yet) ──

                var seenStCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var parsedRows  = new List<(string LocCode, string LocationName, string ZoneName, string RegionName, string ClusterName, string StateName, string Status, string OpeningDate)>();

                foreach (var row in rows)
                {
                    var locCode      = row.Cell(1).GetValue<string>()?.Trim();
                    var locationName = row.Cell(2).GetValue<string>()?.Trim();
                    var zoneName     = row.Cell(3).GetValue<string>()?.Trim();
                    var regionName   = row.Cell(4).GetValue<string>()?.Trim();
                    var clusterName  = row.Cell(5).GetValue<string>()?.Trim();
                    var stateName    = row.Cell(6).GetValue<string>()?.Trim();
                    var status       = row.Cell(7).GetValue<string>()?.Trim();
                    var openingDate  = row.Cell(8).GetValue<string>()?.Trim();

                    if (string.IsNullOrEmpty(locCode))
                        continue;

                    if (!seenStCodes.Add(locCode))
                        return BuildFetchErrorResponse($"Duplicate STCode '{locCode}' found in Excel.", HttpStatusCode.BadRequest);

                    if (!string.IsNullOrEmpty(status) &&
                        !string.Equals(status, "UPC", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase))
                        return BuildFetchErrorResponse($"Invalid status '{status}' for STCode '{locCode}'. Only 'UPC' or 'Active' are allowed.", HttpStatusCode.BadRequest);

                    parsedRows.Add((locCode, locationName, zoneName, regionName, clusterName, stateName, status, openingDate));
                }

                if (parsedRows.Count == 0)
                    return BuildFetchErrorResponse("No valid data rows found in Excel.", HttpStatusCode.BadRequest);

                // ── PASS 2: create any missing master entities, save to get real DB IDs ──

                // First-wins: some lookup tables have duplicate names (e.g. tblState has two
                // rows for "delhi"). Group + First() keeps the dictionary build resilient
                // without mutating the underlying data.
                var zoneDict = (await _context.tblZones.ToListAsync())
                    .Where(z => !string.IsNullOrWhiteSpace(z.ZoneName))
                    .GroupBy(z => z.ZoneName.Trim(), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
                var clusterDict = (await _context.Clusters.ToListAsync())
                    .Where(c => !string.IsNullOrWhiteSpace(c.ClusterName))
                    .GroupBy(c => c.ClusterName.Trim(), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
                var regionDict = (await _context.tblRegions.ToListAsync())
                    .Where(r => !string.IsNullOrWhiteSpace(r.RegionName))
                    .GroupBy(r => r.RegionName.Trim(), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
                var stateDict = (await _context.tblStates.ToListAsync())
                    .Where(s => !string.IsNullOrWhiteSpace(s.StateName))
                    .GroupBy(s => s.StateName.Trim(), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                foreach (var (_, _, zoneName, regionName, clusterName, stateName, _, _) in parsedRows)
                {
                    if (!string.IsNullOrEmpty(zoneName) && !zoneDict.ContainsKey(zoneName))
                    {
                        var z = new tblZone { ZoneName = zoneName, IsActive = true, CreatedOn = DateTime.UtcNow };
                        _context.tblZones.Add(z);
                        zoneDict[zoneName] = z;
                    }
                    if (!string.IsNullOrEmpty(clusterName) && !clusterDict.ContainsKey(clusterName))
                    {
                        var c = new Cluster { ClusterName = clusterName, IsActive = true, CreatedOn = DateTime.UtcNow };
                        _context.Clusters.Add(c);
                        clusterDict[clusterName] = c;
                    }
                    if (!string.IsNullOrEmpty(regionName) && !regionDict.ContainsKey(regionName))
                    {
                        var r = new tblRegion { RegionName = regionName, CreatedOn = DateTime.UtcNow };
                        _context.tblRegions.Add(r);
                        regionDict[regionName] = r;
                    }
                    if (!string.IsNullOrEmpty(stateName) && !stateDict.ContainsKey(stateName))
                    {
                        var s = new tblState { StateName = stateName, CreatedOn = DateTime.UtcNow };
                        _context.tblStates.Add(s);
                        stateDict[stateName] = s;
                    }
                }

                await _context.SaveChangesAsync(); // master entities now have real DB IDs

                // ── PASS 3: delete all existing locations ──────────────────────────────
                var oldCount = await _context.tblLocations.CountAsync();

                // 1. Disable the 4 FK constraints from StoreRoutingTransaction
                await _context.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.StoreRoutingTransaction NOCHECK CONSTRAINT FK__StoreRout__Locat__1C281490");
                await _context.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.StoreRoutingTransaction NOCHECK CONSTRAINT FK__StoreRout__Locat__24BD5A91");
                await _context.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.StoreRoutingTransaction NOCHECK CONSTRAINT FK__StoreRout__Locat__2799C73C");
                await _context.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.StoreRoutingTransaction NOCHECK CONSTRAINT FK__StoreRout__Locat__2C5E7C59");

                // 2. Turn off temporal versioning (required for DELETE on temporal tables)
                await _context.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.tblLocation SET (SYSTEM_VERSIONING = OFF)");

                // 3. Delete all rows
                await _context.Database.ExecuteSqlRawAsync("DELETE FROM dbo.tblLocation");

                // 4. Re-enable temporal versioning
                await _context.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.tblLocation SET (SYSTEM_VERSIONING = ON (HISTORY_TABLE = dbo.tblLocation_History, DATA_CONSISTENCY_CHECK = OFF))");

                // 5. Re-enable FK constraints (WITH NOCHECK to skip validating existing orphaned rows)
                await _context.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.StoreRoutingTransaction WITH NOCHECK CHECK CONSTRAINT FK__StoreRout__Locat__1C281490");
                await _context.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.StoreRoutingTransaction WITH NOCHECK CHECK CONSTRAINT FK__StoreRout__Locat__24BD5A91");
                await _context.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.StoreRoutingTransaction WITH NOCHECK CHECK CONSTRAINT FK__StoreRout__Locat__2799C73C");
                await _context.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.StoreRoutingTransaction WITH NOCHECK CHECK CONSTRAINT FK__StoreRout__Locat__2C5E7C59");

                // ── PASS 4: insert new locations with correct FK IDs ───────────────────
                var newLocations = new List<tblLocation>();

                foreach (var (locCode, locationName, zoneName, regionName, clusterName, stateName, status, openingDate) in parsedRows)
                {
                    int? zoneId    = !string.IsNullOrEmpty(zoneName)    && zoneDict.TryGetValue(zoneName, out var z)       ? z.Id       : null;
                    int? clusterId = !string.IsNullOrEmpty(clusterName) && clusterDict.TryGetValue(clusterName, out var c) ? c.Id       : null;
                    int? regionId  = !string.IsNullOrEmpty(regionName)  && regionDict.TryGetValue(regionName, out var r)   ? r.RegionId : null;
                    int? stateId   = !string.IsNullOrEmpty(stateName)   && stateDict.TryGetValue(stateName, out var s)     ? s.StateId  : null;

                    newLocations.Add(new tblLocation
                    {
                        LocationName = locationName,
                        STCode       = locCode,
                        ZoneId       = zoneId,
                        ClusterId    = clusterId,
                        RegionId     = regionId,
                        StateId      = stateId,
                        IsActive     = string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase),
                        IsDeleted    = false,
                        OpeningDate  = openingDate,
                        CreatedOn    = DateTime.UtcNow
                    });
                }

                await _context.tblLocations.AddRangeAsync(newLocations);
                await _context.SaveChangesAsync();

                return BuildFetchSuccessResponse($"Location hierarchy replaced. {oldCount} old records deleted, {newLocations.Count} new records inserted.", null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UploadLocationsExcelAsync");
                return BuildFetchErrorResponse($"Error uploading locations: {ex.Message}", HttpStatusCode.BadRequest);
            }
        }
                      
        public async Task<FetchAndResponse> getAllLocation()
        {
            try {
                var res = _context.LocationMasters.AsNoTracking().AsQueryable().Where(row =>row.IsDeleted==false).ToList();
                if (res == null || res.Count < 1)
                    return BuildFetchErrorResponse("No Data Found", HttpStatusCode.NotFound);
                return BuildFetchSuccessResponse("Data Fetched", res);
            }
            catch (Exception ex)
            {
                return BuildFetchErrorResponse(ex.Message, HttpStatusCode.BadRequest);
            }
        }

        public async Task<FetchAndResponse> SoftDeleteLocationAsync(int locationId, string updatedBy)
        {
            try
            {
                // Find the location by ID
                var location = await _context.tblLocations
                    .FirstOrDefaultAsync(l => l.LocationId == locationId);

                if (location == null)
                {
                    return BuildFetchErrorResponse($"Location with ID {locationId} not found", HttpStatusCode.NotFound);
                }

                // Check if location is already soft deleted
                if (location.IsDeleted == true)
                {
                    return BuildFetchErrorResponse($"Location with ID {locationId} is already deleted", HttpStatusCode.BadRequest);
                }

                // Perform soft delete
                location.IsDeleted = true;
                location.IsActive = false;
                //location.UpdatedOn = DateTime.UtcNow;
                // Note: If you have an UpdatedBy field in tblLocation, you can set it here
                // location.UpdatedBy = updatedBy;

                await _context.SaveChangesAsync();

                return BuildFetchSuccessResponse($"Location '{location.LocationName}' (ID: {locationId}) has been successfully deleted", null);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error soft deleting location with ID {locationId}: {ex.Message}");
                return BuildFetchErrorResponse($"Error deleting location: {ex.Message}", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<FetchAndResponse> ToggleLocationStatusAsync(int locationId, string updatedBy)
        {
            try
            {
                var location = await _context.tblLocations
                    .FirstOrDefaultAsync(l => l.LocationId == locationId);

                if (location == null)
                {
                    return BuildFetchErrorResponse($"Location with ID {locationId} not found", HttpStatusCode.NotFound);
                }

                if (location.IsDeleted == true)
                {
                    return BuildFetchErrorResponse($"Location with ID {locationId} is deleted. Cannot toggle status.", HttpStatusCode.BadRequest);
                }

                // Toggle IsActive: if null treat as false
                var current = location.IsActive ?? false;
                location.IsActive = !current;
                //location.UpdatedOn = DateTime.UtcNow;
                // location.UpdatedBy = updatedBy;

                await _context.SaveChangesAsync();

                var newStatus = location.IsActive == true ? "Active" : "UPC";
                return BuildFetchSuccessResponse($"Location '{location.LocationName}' (ID: {locationId}) status set to {newStatus}", null);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error toggling status for location with ID {locationId}: {ex.Message}");
                return BuildFetchErrorResponse($"Error toggling location status: {ex.Message}", HttpStatusCode.InternalServerError);
            }
        }

        private async Task<string> SaveUploadedFileAsync(IFormFile file, string? updatedBy)
        {
            var now = DateTime.UtcNow;
            var year = now.Year.ToString();
            var month = now.Month.ToString("D2");
            var day = now.Day.ToString("D2");
            var uploader = string.IsNullOrEmpty(updatedBy) ? "Unknown" : updatedBy;

            var uploadPath = Path.Combine("wwwroot", "LocationUploader", year, month, day, uploader);
            
            // Create directory if it doesn't exist
            Directory.CreateDirectory(uploadPath);

            var fileName = $"{Path.GetFileNameWithoutExtension(file.FileName)}_{now:yyyyMMdd_HHmmss}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(uploadPath, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return filePath;
        }
        public async Task<LocationforgeoDto?> UpdateGeoAsync(LocationGeoUpdateRequest request)
        {
            if (request.LocationId is null)
                throw new ArgumentException("LocationId is required.", nameof(request.LocationId));

            await using var sqlConn = (SqlConnection)_context.Database.GetDbConnection();
            await sqlConn.OpenAsync();

            await using var cmd = sqlConn.CreateCommand();
            cmd.CommandText = "dbo.usp_Location_UpdateGeo";
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandTimeout = 30;

            static void AddIfNotNull(
                SqlCommand c,
                string name,
                SqlDbType type,
                object? val,
                byte? precision = null,
                byte? scale = null,
                int? size = null)
            {
                if (val == null) return;
                if (val is string s && string.IsNullOrWhiteSpace(s)) return;

                var p = new SqlParameter(name, type) { Value = val };
                if (precision.HasValue) p.Precision = precision.Value;
                if (scale.HasValue) p.Scale = scale.Value;
                if (size.HasValue) p.Size = size.Value;
                c.Parameters.Add(p);
            }

            // only the fields you care about
            AddIfNotNull(cmd, "@LocationId", SqlDbType.Int, request.LocationId);
            AddIfNotNull(cmd, "@StoreLong", SqlDbType.Decimal, request.StoreLong, precision: 9, scale: 6);
            AddIfNotNull(cmd, "@StoreLat", SqlDbType.Decimal, request.StoreLat, precision: 9, scale: 6);
            AddIfNotNull(cmd, "@ADDRESS", SqlDbType.NVarChar, request.ADDRESS, size: 255);
            AddIfNotNull(cmd, "@AllowedRadiusMeters", SqlDbType.Int, request.AllowedRadiusMeters);
            AddIfNotNull(cmd, "@IsGeofenceEnabled", SqlDbType.Bit, request.IsGeofenceEnabled); // NEW

            LocationforgeoDto? dto = null;

            await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
            if (await reader.ReadAsync())
            {
                int Ord(string n) => reader.GetOrdinal(n);
                int? GetInt(string n) => reader.IsDBNull(Ord(n)) ? (int?)null : reader.GetInt32(Ord(n));
                decimal? GetDec(string n) => reader.IsDBNull(Ord(n)) ? (decimal?)null : reader.GetDecimal(Ord(n));
                string? GetStr(string n) => reader.IsDBNull(Ord(n)) ? null : reader.GetString(Ord(n));
                bool? GetBool(string n) => reader.IsDBNull(Ord(n)) ? (bool?)null : reader.GetBoolean(Ord(n));

                dto = new LocationforgeoDto
                {
                    LocationId = GetInt("LocationId"),
                    StoreLong = GetDec("StoreLong"),
                    StoreLat = GetDec("StoreLat"),
                    ADDRESS = GetStr("ADDRESS"),
                    AllowedRadiusMeters = GetInt("AllowedRadiusMeters"),
                    IsGeofenceEnabled = GetBool("IsGeofenceEnabled") // NEW
                };
            }

            return dto;
        }


        public async Task<FetchAndResponse> GetAllLocationsData()
        {
            try
            {
                var res = _context.tblLocations.AsNoTracking().AsQueryable().ToList();
                if (res == null || res.Count < 1)
                    return BuildFetchErrorResponse("No Data Found", HttpStatusCode.NotFound);
                return BuildFetchSuccessResponse("Data Fetched", res);
            }
            catch (Exception ex)
            {
                return BuildFetchErrorResponse(ex.Message, HttpStatusCode.BadRequest);
            }
        }

        public async Task<FetchAndResponse> GetActiveEmployeesByLocationAsync(string stcode)
        {
            try
            {
                int locationid = _context.tblLocations.Where(a => a.STCode == stcode && a.IsActive == true)
                                    .Select(a => a.LocationId).FirstOrDefault();

                var employees = await _context.tblEmployees
                    .AsNoTracking()
                    .Where(e =>
                        e.LocationId == locationid &&
                        e.IsActive == true &&
                        (e.IsDeleted == null || e.IsDeleted == false))
                    .Select(e => new
                    {
                        e.EmployeeId,
                        e.Ecode,
                        e.FirstName,
                        e.LastName,
                        e.FULL_NAME,
                        stcode
                    })
                    .ToListAsync();

                if (employees == null || employees.Count < 1)
                    return BuildFetchErrorResponse("No active employees found for this location", HttpStatusCode.NotFound);

                return BuildFetchSuccessResponse("Data Fetched", employees);
            }
            catch (Exception ex)
            {
                return BuildFetchErrorResponse(ex.Message, HttpStatusCode.BadRequest);
            }
        }
    }

    internal static class DataRecordExtensions
    {
        public static bool IsDBNull(this IDataRecord r, string name)
            => r.IsDBNull(r.GetOrdinal(name));

    }

  
    }