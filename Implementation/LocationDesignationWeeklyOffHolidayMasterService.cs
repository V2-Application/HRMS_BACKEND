using ClosedXML.Excel;
using HRMSAPI.Data;
using HRMSAPI.DTO;
using HRMSAPI.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace HRMSAPI.Implementation
{
    public class LocationDesignationWeeklyOffHolidayMasterService : ILocationDesignationWeeklyOffHolidayMasterService
    {
        private readonly HRMSContext _context;

        public LocationDesignationWeeklyOffHolidayMasterService(HRMSContext context)
        {
            _context = context;
        }

        public async Task<(bool Success, string Message)> UploadMasterDataAsync(IFormFile file)
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
                            var month = row.Cell(1).GetValue<string>()?.Trim() ?? "NA";
                            var locationCategoryName = row.Cell(2).GetValue<string>()?.Trim() ?? "NA";
                            var designationName = row.Cell(3).GetValue<string>()?.Trim() ?? "NA";
                            var budgetWeeklyOff = row.Cell(4).IsEmpty() ? 0.0m : row.Cell(4).GetValue<decimal>();
                            var budgetHoliday = row.Cell(5).IsEmpty() ? 0.0m : row.Cell(5).GetValue<decimal>();

                            // Validate Month format (MMM-YY)
                            if (!System.Text.RegularExpressions.Regex.IsMatch(month, @"^[A-Za-z]{3}-\d{2}$", RegexOptions.IgnoreCase))
                                return (false, $"Invalid month format: {month}. Expected format: MMM-YY (e.g., May-25)");

                            // Validate LocationCategoryName
                            var locationCategory = await _context.LocationCategories
                                .AsNoTracking()
                                .FirstOrDefaultAsync(lc => lc.LocationCategoryName == locationCategoryName);
                            if (locationCategory == null)
                                return (false, $"Location category not found: {locationCategoryName}");

                            // Validate DesignationName
                            var designation = await _context.tblDesignations
                                .AsNoTracking()
                                .FirstOrDefaultAsync(d => d.DesignationName == designationName);
                            if (designation == null)
                                return (false, $"Designation not found: {designationName}");

                            // Check for existing record
                            var existingMaster = await _context.LocationDesignationWeeklyOffHolidayMasters
                                .FirstOrDefaultAsync(m => m.Month == month
                                                       && m.LocationCategoryId == locationCategory.LocationCategoryId
                                                       && m.DesignationID == designation.DesignationId);

                            if (existingMaster != null)
                            {
                                // Update existing record
                                existingMaster.BudgetWeeklyOff = budgetWeeklyOff;
                                existingMaster.BudgetHoliday = budgetHoliday;
                            }
                            else
                            {
                                // Create new record
                                var masterRecord = new LocationDesignationWeeklyOffHolidayMaster
                                {
                                    Month = month,
                                    LocationCategoryId = locationCategory.LocationCategoryId,
                                    DesignationID = designation.DesignationId,
                                    BudgetWeeklyOff = budgetWeeklyOff,
                                    BudgetHoliday = budgetHoliday
                                };
                                _context.LocationDesignationWeeklyOffHolidayMasters.Add(masterRecord);
                            }
                        }

                        await _context.SaveChangesAsync();
                        return (true, "Master data uploaded successfully");
                    }
                }
            }
            catch (Exception ex)
            {
                return (false, $"Error uploading master data: {ex.Message}");
            }
        }

        public async Task<(List<LocationDesignationWeeklyOffHolidayMasterDTO> Records, int TotalRecords)> GetMasterRecordsAsync(
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
                var query = from m in _context.LocationDesignationWeeklyOffHolidayMasters
                            join lc in _context.LocationCategories on m.LocationCategoryId equals lc.LocationCategoryId
                            join d in _context.tblDesignations on m.DesignationID equals d.DesignationId
                            select new LocationDesignationWeeklyOffHolidayMasterDTO
                            {
                                LocationDesignationWeeklyOffHolidayMasterId = m.LocationDesignationWeeklyOffHolidayMasterId,
                                Month = m.Month,
                                LocationCategoryId = m.LocationCategoryId,
                                LocationCategoryName = lc.LocationCategoryName,
                                DesignationId = (int)m.DesignationID,
                                DesignationName = d.DesignationName,
                                BudgetWeeklyOff = (int)m.BudgetWeeklyOff,
                                BudgetHoliday = (int)m.BudgetHoliday
                            };

                // Apply search across all columns
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    searchTerm = searchTerm.Trim().ToLower();
                    query = query.Where(m =>
                        (m.Month != null && m.Month.ToLower().Contains(searchTerm)) ||
                        (m.LocationCategoryName != null && m.LocationCategoryName.ToLower().Contains(searchTerm)) ||
                        (m.DesignationName != null && m.DesignationName.ToLower().Contains(searchTerm)) ||
                        m.BudgetWeeklyOff.ToString().Contains(searchTerm) ||
                        m.BudgetHoliday.ToString().Contains(searchTerm) ||
                        m.LocationDesignationWeeklyOffHolidayMasterId.ToString().Contains(searchTerm) ||
                        m.LocationCategoryId.ToString().Contains(searchTerm) ||
                        m.DesignationId.ToString().Contains(searchTerm));
                }

                // Apply specific locationCategoryName filter
                if (!string.IsNullOrWhiteSpace(locationCategoryName))
                {
                    query = query.Where(m => m.LocationCategoryName != null &&
                                            m.LocationCategoryName.Contains(locationCategoryName, StringComparison.OrdinalIgnoreCase));
                }

                var totalRecords = await query.CountAsync();
                var records = await query
                    .OrderByDescending(m => m.LocationDesignationWeeklyOffHolidayMasterId)
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
