using ClosedXML.Excel;
using HRMSAPI.Data;
using HRMSAPI.DTO;
using HRMSAPI.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HRMSAPI.Implementation
{
    public class LocationDesignationPolicyService : ILocationDesignationPolicyService
    {
        private readonly HRMSContext _context;

        public LocationDesignationPolicyService(HRMSContext context)
        {
            _context = context;
        }

        public async Task<(bool Success, string Message)> UploadPolicyDataAsync(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return (false, "No file uploaded");

                using (var stream = file.OpenReadStream())
                {
                    using (var workbook = new XLWorkbook(stream))
                    {
                        var worksheet = workbook.Worksheet(1);
                        var rows = worksheet.RowsUsed().Skip(1); // Skip header

                        foreach (var row in rows)
                        {
                            var locationCategoryName = row.Cell(1).GetValue<string>()?.Trim() ?? "NA";
                            var designationName = row.Cell(2).GetValue<string>()?.Trim() ?? "NA";
                            var totalAttendance = row.Cell(3).GetValue<string>()?.Trim() ?? "NA";
                            var weeklyOff = row.Cell(4).IsEmpty() ? 0.0m : row.Cell(4).GetValue<decimal>();

                            // Validate LocationCategoryName
                            var locationCategory = await _context.LocationCategories
                                .AsNoTracking()
                                .FirstOrDefaultAsync(lc => lc.LocationCategoryName == locationCategoryName);
                            if (locationCategory == null)
                                return (false, $"Location category not found: {locationCategoryName}");

                            // Validate DesignationName
                            var designation = await _context.tblDesignations.AsNoTracking()
                                .AsNoTracking()
                                .FirstOrDefaultAsync(d => d.DesignationName == designationName);
                            if (designation == null)
                                return (false, $"Designation not found: {designationName}");

                            // Check for existing record
                            var existingPolicy = await _context.tblLocationDesignationPolicies.AsNoTracking()
                                .FirstOrDefaultAsync(p => p.LocationCategoryId == locationCategory.LocationCategoryId.ToString()
                                                       && p.DesignationId == designation.DesignationId);

                            if (existingPolicy != null)
                            {
                                // Update existing record
                                existingPolicy.LocationCategoryName = locationCategoryName;
                                existingPolicy.DesignationName = designationName;
                                existingPolicy.TotalAttendance = totalAttendance;
                                existingPolicy.WeeklyOff = weeklyOff;
                            }
                            else
                            {
                                // Create new record
                                var policyRecord = new tblLocationDesignationPolicy
                                {
                                    LocationCategoryId = locationCategory.LocationCategoryId.ToString(),
                                    LocationCategoryName = locationCategoryName,
                                    DesignationId = designation.DesignationId,
                                    DesignationName = designationName,
                                    TotalAttendance = totalAttendance,
                                    WeeklyOff = weeklyOff
                                };
                                _context.tblLocationDesignationPolicies.Add(policyRecord);
                            }
                        }

                        await _context.SaveChangesAsync();
                        return (true, "Policy data uploaded successfully");
                    }
                }
            }
            catch (Exception ex)
            {
                return (false, $"Error uploading policy data: {ex.Message}");
            }
        }
        public async Task<(List<LocationDesignationPolicyDTO> Records, int TotalRecords)> GetPolicyRecordsAsync(
           string? searchTerm = null,
           string? locationCategoryName = null,
           int page = 1,
           int pageSize = 10)
        {
            if (page < 1 || pageSize < 1)
            {
                
                throw new ArgumentException("Page and pageSize must be greater than 0.");
            }

            try
            {
                var query = _context.tblLocationDesignationPolicies
                    .AsNoTracking()
                    .Select(p => new LocationDesignationPolicyDTO
                    {
                        LocationDesignationPolicyId = p.LocationDesignationPolicyId,
                        LocationCategoryId = p.LocationCategoryId,
                        LocationCategoryName = p.LocationCategoryName,
                        DesignationId = (int)p.DesignationId,
                        DesignationName = p.DesignationName,
                        TotalAttendance = p.TotalAttendance,
                        WeeklyOff = (int)p.WeeklyOff
                    });

                // Apply search across all columns
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    searchTerm = searchTerm.Trim().ToLower();
                    query = query.Where(p =>
                        (p.LocationCategoryName != null && p.LocationCategoryName.ToLower().Contains(searchTerm)) ||
                        (p.DesignationName != null && p.DesignationName.ToLower().Contains(searchTerm)) ||
                        (p.TotalAttendance != null && p.TotalAttendance.ToLower().Contains(searchTerm)) ||
                        p.WeeklyOff.ToString().Contains(searchTerm) ||
                        p.LocationDesignationPolicyId.ToString().Contains(searchTerm) ||
                        p.LocationCategoryId.ToString().Contains(searchTerm) ||
                        p.DesignationId.ToString().Contains(searchTerm));
                }

                // Apply specific locationCategoryName filter
                if (!string.IsNullOrWhiteSpace(locationCategoryName))
                {
                    query = query.Where(p => p.LocationCategoryName != null &&
                                            p.LocationCategoryName.Contains(locationCategoryName, StringComparison.OrdinalIgnoreCase));
                }

                var totalRecords = await query.CountAsync();
                var records = await query
                    .OrderByDescending(p => p.LocationDesignationPolicyId)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                return (records, totalRecords);
            }
            catch (Exception ex)
            {
                throw;
            }
        }
    }
}
