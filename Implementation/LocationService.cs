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

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Bulk fetch all existing data
                var zones = await _context.tblZones.ToListAsync();
                var clusters = await _context.Clusters.ToListAsync();
                var regions = await _context.tblRegions.ToListAsync();
                var states = await _context.tblStates.ToListAsync();
                var locations = await _context.tblLocations.ToListAsync();

                // 2. Prepare in-memory dictionaries for fast lookup
                var zoneDict = zones.ToDictionary(z => z.ZoneName, z => z);
                var clusterDict = clusters.ToDictionary(c => c.ClusterName, c => c);
                var regionDict = regions.ToDictionary(r => r.RegionName, r => r);
                var stateDict = states.ToDictionary(s => s.StateName, s => s);
                var locationDict = locations.ToDictionary(l => (l.LocationName, l.STCode), l => l);

                // 3. Track new entities to add
                var newZones = new List<tblZone>();
                var newClusters = new List<Cluster>();
                var newRegions = new List<tblRegion>();
                var newStates = new List<tblState>();
                var newLocations = new List<tblLocation>();
                var updatedLocations = new List<tblLocation>();

                // Track STCodes seen in this Excel to catch duplicates
                var seenStCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var row in rows)
                {
                    var locCode = row.Cell(1).GetValue<string>()?.Trim();
                    var locationName = row.Cell(2).GetValue<string>()?.Trim();
                    var zoneName = row.Cell(3).GetValue<string>()?.Trim();
                    var regionName = row.Cell(4).GetValue<string>()?.Trim();
                    var clusterName = row.Cell(5).GetValue<string>()?.Trim();
                    var stateName = row.Cell(6).GetValue<string>()?.Trim();
                    var status = row.Cell(7).GetValue<string>()?.Trim();
                    var openingDate = row.Cell(8).GetValue<string>()?.Trim();

                    int? zoneId = null, clusterId = null, regionId = null, stateId = null;

                    // Check for duplicate STCode in Excel
                    if (!string.IsNullOrEmpty(locCode))
                    {
                        if (!seenStCodes.Add(locCode))
                        {
                            return BuildFetchErrorResponse($"Duplicate STCode '{locCode}' found in Excel.", HttpStatusCode.BadRequest);
                        }
                    }

                    // Validate Status column - only allow UPC or Active
                    if (!string.IsNullOrEmpty(status))
                    {
                        if (!string.Equals(status, "UPC", StringComparison.OrdinalIgnoreCase) && 
                            !string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase))
                        {
                            return BuildFetchErrorResponse($"Invalid status '{status}' for STCode '{locCode}'. Only 'UPC' or 'Active' are allowed.", HttpStatusCode.BadRequest);
                        }
                    }

                    // Zone
                    tblZone zone = null;
                    if (!string.IsNullOrEmpty(zoneName))
                    {
                        if (!zoneDict.TryGetValue(zoneName, out zone))
                        {
                            zone = new tblZone { ZoneName = zoneName, IsActive = true, CreatedOn = DateTime.UtcNow };
                            newZones.Add(zone);
                            zoneDict[zoneName] = zone;
                        }
                        zoneId = zone.Id;
                    }

                    // Cluster
                    Cluster cluster = null;
                    if (!string.IsNullOrEmpty(clusterName))
                    {
                        if (!clusterDict.TryGetValue(clusterName, out cluster))
                        {
                            cluster = new Cluster { ClusterName = clusterName, IsActive = true, CreatedOn = DateTime.UtcNow };
                            newClusters.Add(cluster);
                            clusterDict[clusterName] = cluster;
                        }
                        clusterId = cluster.Id;
                    }

                    // Region
                    tblRegion region = null;
                    if (!string.IsNullOrEmpty(regionName))
                    {
                        if (!regionDict.TryGetValue(regionName, out region))
                        {
                            region = new tblRegion { RegionName = regionName, CreatedOn = DateTime.UtcNow };
                            newRegions.Add(region);
                            regionDict[regionName] = region;
                        }
                        regionId = region.RegionId;
                    }

                    // State
                    tblState state = null;
                    if (!string.IsNullOrEmpty(stateName))
                    {
                        if (!stateDict.TryGetValue(stateName, out state))
                        {
                            state = new tblState { StateName = stateName, RegionId = Convert.ToInt32(regionId), CreatedOn = DateTime.UtcNow };
                            newStates.Add(state);
                            stateDict[stateName] = state;
                        }
                        else if (regionId.HasValue && state.RegionId != regionId)
                        {
                            state.RegionId = Convert.ToInt32(regionId);
                        }
                        stateId = state.StateId;
                    }

                    // Location: Check by STCode only
                    tblLocation location = null;
                    if (!string.IsNullOrEmpty(locCode))
                    {
                        location = await _context.tblLocations.FirstOrDefaultAsync(l => l.STCode == locCode);
                        if (location == null)
                        {
                            location = new tblLocation
                            {
                                LocationName = locationName,
                                STCode = locCode,
                                ZoneId = zoneId,
                                ClusterId = clusterId,
                                RegionId = regionId,
                                StateId = stateId,
                                IsActive = string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase),
                                IsDeleted=false,
                                OpeningDate = openingDate,
                                CreatedOn = DateTime.UtcNow
                            };
                            newLocations.Add(location);
                            locationDict[(locationName, locCode)] = location;
                        }
                        else
                        {
                            // Update existing location
                            location.LocationName = locationName; // Optionally update name
                            location.ZoneId = zoneId;
                            location.ClusterId = clusterId;
                            location.RegionId = regionId;
                            location.StateId = stateId;
                            location.IsActive = string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase);
                            location.IsDeleted = false;
                            location.OpeningDate = openingDate;
                            updatedLocations.Add(location);
                        }
                    }
                }

                // 4. Bulk insert new entities
                if (newZones.Any()) _context.tblZones.AddRange(newZones);
                if (newClusters.Any()) _context.Clusters.AddRange(newClusters);
                if (newRegions.Any()) _context.tblRegions.AddRange(newRegions);
                if (newStates.Any()) _context.tblStates.AddRange(newStates);

                await _context.SaveChangesAsync();

                // Now that new entities have IDs, update foreign keys for new locations
                foreach (var loc in newLocations)
                {
                    if (!string.IsNullOrEmpty(loc.ZoneId?.ToString()))
                        loc.ZoneId = zoneDict.Values.FirstOrDefault(z => z.ZoneName == zones.FirstOrDefault(zz => zz.Id == loc.ZoneId)?.ZoneName)?.Id;
                    if (!string.IsNullOrEmpty(loc.ClusterId?.ToString()))
                        loc.ClusterId = clusterDict.Values.FirstOrDefault(c => c.ClusterName == clusters.FirstOrDefault(cc => cc.Id == loc.ClusterId)?.ClusterName)?.Id;
                    if (!string.IsNullOrEmpty(loc.RegionId?.ToString()))
                        loc.RegionId = regionDict.Values.FirstOrDefault(r => r.RegionName == regions.FirstOrDefault(rr => rr.RegionId == loc.RegionId)?.RegionName)?.RegionId;
                    if (!string.IsNullOrEmpty(loc.StateId?.ToString()))
                        loc.StateId = stateDict.Values.FirstOrDefault(s => s.StateName == states.FirstOrDefault(ss => ss.StateId == loc.StateId)?.StateName)?.StateId;
                }

                if (newLocations.Any()) _context.tblLocations.AddRange(newLocations);
                if (updatedLocations.Any()) _context.tblLocations.UpdateRange(updatedLocations);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return BuildFetchSuccessResponse("Locations uploaded successfully", null);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
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