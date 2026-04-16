using ClosedXML.Excel;
using HRMSAPI.Data;
using HRMSAPI.DTO;
using HRMSAPI.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Roomsy.DTOS.GenericsResponses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace HRMSAPI.Implementation
{
    public class EcodeWiseWeekOffMappingService : IEcodeWiseWeekOffMappingService
    {
        private readonly HRMSContext _context;
        private readonly ILogger<EcodeWiseWeekOffMappingService> _logger;

        public EcodeWiseWeekOffMappingService(HRMSContext context, ILogger<EcodeWiseWeekOffMappingService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<FetchAndResponse> GetAllEcodeWiseWeekOffMappingsAsync()
        {
            try
            {
                var mappings = await _context.EcodeWiseWeekOffMappings
                    .AsNoTracking()
                    .AsQueryable()
                    .Where(m => m.IsDeleted != true)
                    .OrderBy(m => m.Ecode)
                    .ThenBy(m => m.MONTH)
                    .ToListAsync();

                if (mappings == null || !mappings.Any())
                {
                    return new FetchAndResponse
                    {
                        Status = false,
                        Message = "No data found",
                        Code = HttpStatusCode.NotFound,
                        Data = null
                    };
                }

                _logger.LogInformation("Retrieved {Count} EcodeWiseWeekOffMapping records", mappings.Count);
                return new FetchAndResponse
                {
                    Status = true,
                    Message = "Fetched successfully",
                    Data = mappings,
                    Code = HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving EcodeWiseWeekOffMappings");
                return new FetchAndResponse
                {
                    Status = false,
                    Message = ex.Message,
                    Code = HttpStatusCode.InternalServerError,
                    Data = null
                };
            }
        }

        public async Task<ExecuteAndReponse> UpsertEcodeWiseWeekOffMappingAsync(EcodeWiseWeekOffMappingUpsertDto dto)
        {
            if (dto == null)
            {
                return new ExecuteAndReponse
                {
                    Status = false,
                    Message = "EcodeWiseWeekOffMapping data is required",
                    Code = HttpStatusCode.BadRequest
                };
            }

            try
            {
                // Validate required fields
                if (string.IsNullOrWhiteSpace(dto.Ecode))
                {
                    return new ExecuteAndReponse
                    {
                        Status = false,
                        Message = "Ecode is required",
                        Code = HttpStatusCode.BadRequest
                    };
                }

                if (string.IsNullOrWhiteSpace(dto.MONTH))
                {
                    return new ExecuteAndReponse
                    {
                        Status = false,
                        Message = "MONTH is required",
                        Code = HttpStatusCode.BadRequest
                    };
                }

                var trimmedEcode = dto.Ecode.Trim();
                var trimmedMonth = dto.MONTH.Trim();

                EcodeWiseWeekOffMapping mapping;

                if (dto.Id.HasValue && dto.Id.Value > 0)
                {
                    // Update existing record
                    mapping = await _context.EcodeWiseWeekOffMappings
                        .AsQueryable()
                        .FirstOrDefaultAsync(m => m.Id == dto.Id.Value);

                    if (mapping == null)
                    {
                        return new ExecuteAndReponse
                        {
                            Status = false,
                            Message = $"EcodeWiseWeekOffMapping with ID {dto.Id.Value} not found",
                            Code = HttpStatusCode.NotFound
                        };
                    }

                    // Check if updating to an ecode and month combination that already exists (excluding current record)
                    var existingMapping = await _context.EcodeWiseWeekOffMappings
                        .AsNoTracking()
                        .AsQueryable()
                        .Where(m => m.Ecode == trimmedEcode 
                            && m.MONTH == trimmedMonth 
                            && m.Id != dto.Id.Value 
                            && m.IsDeleted != true)
                        .FirstOrDefaultAsync();

                    if (existingMapping != null)
                    {
                        return new ExecuteAndReponse
                        {
                            Status = false,
                            Message = $"Data already exists for Ecode '{trimmedEcode}' and Month '{trimmedMonth}'",
                            Code = HttpStatusCode.BadRequest
                        };
                    }

                    // Update properties
                    mapping.Ecode = trimmedEcode;
                    mapping.MONTH = trimmedMonth;
                    mapping.TotalAttendance = dto.TotalAttendance;
                    mapping.WeeklyOff = dto.WeeklyOff;
                    mapping.UpdatedBy = "System";
                    mapping.UpdatedOn = DateTime.UtcNow;

                    _logger.LogInformation("Updating EcodeWiseWeekOffMapping with ID: {Id}", dto.Id.Value);
                }
                else
                {
                    // Create new record
                    // Check if ecode and month combination already exists
                    var existingMapping = await _context.EcodeWiseWeekOffMappings
                        .AsNoTracking()
                        .AsQueryable()
                        .Where(m => m.Ecode == trimmedEcode 
                            && m.MONTH == trimmedMonth 
                            && m.IsDeleted != true)
                        .FirstOrDefaultAsync();

                    if (existingMapping != null)
                    {
                        return new ExecuteAndReponse
                        {
                            Status = false,
                            Message = $"Data already exists for Ecode '{trimmedEcode}' and Month '{trimmedMonth}'",
                            Code = HttpStatusCode.BadRequest
                        };
                    }

                    mapping = new EcodeWiseWeekOffMapping
                    {
                        Ecode = trimmedEcode,
                        MONTH = trimmedMonth,
                        TotalAttendance = dto.TotalAttendance,
                        WeeklyOff = dto.WeeklyOff,
                        CreatedBy = "System",
                        CreatedOn = DateTime.UtcNow,
                        IsActive = true,
                        IsDeleted = false
                    };

                    await _context.EcodeWiseWeekOffMappings.AddAsync(mapping);
                    _logger.LogInformation("Creating new EcodeWiseWeekOffMapping for Ecode: {Ecode}, Month: {Month}", trimmedEcode, trimmedMonth);
                }

                await _context.SaveChangesAsync();

                return new ExecuteAndReponse
                {
                    Status = true,
                    Message = "Upserted successfully",
                    Code = HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while upserting EcodeWiseWeekOffMapping");
                return new ExecuteAndReponse
                {
                    Status = false,
                    Message = ex.Message,
                    Code = HttpStatusCode.InternalServerError
                };
            }
        }

        public async Task<ExecuteAndReponse> DeleteEcodeWiseWeekOffMappingAsync(long id)
        {
            if (id <= 0)
            {
                return new ExecuteAndReponse
                {
                    Status = false,
                    Message = "Invalid ID",
                    Code = HttpStatusCode.BadRequest
                };
            }

            try
            {
                var mapping = await _context.EcodeWiseWeekOffMappings
                    .AsQueryable()
                    .FirstOrDefaultAsync(m => m.Id == id);

                if (mapping == null)
                {
                    return new ExecuteAndReponse
                    {
                        Status = false,
                        Message = $"EcodeWiseWeekOffMapping with ID {id} not found",
                        Code = HttpStatusCode.NotFound
                    };
                }

                // Soft delete - set IsDeleted to true and IsActive to false
                mapping.IsDeleted = true;
                mapping.IsActive = false;
                mapping.UpdatedBy = "System";
                mapping.UpdatedOn = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                _logger.LogInformation("Soft deleted EcodeWiseWeekOffMapping with ID: {Id}", id);

                return new ExecuteAndReponse
                {
                    Status = true,
                    Message = "Deleted successfully",
                    Code = HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while deleting EcodeWiseWeekOffMapping with ID: {Id}", id);
                return new ExecuteAndReponse
                {
                    Status = false,
                    Message = ex.Message,
                    Code = HttpStatusCode.InternalServerError
                };
            }
        }

        public async Task<ExecuteAndReponse> UploadEcodeWiseWeekOffMappingAsync(IFormFile file)
        {
            var expectedHeaders = new[] { "Ecode", "Month", "TotalAttendance", "WeeklyOFF" };
            
            if (file == null || file.Length == 0)
            {
                return new ExecuteAndReponse
                {
                    Status = false,
                    Message = "No file uploaded",
                    Code = HttpStatusCode.BadRequest
                };
            }

            try
            {
                using var stream = file.OpenReadStream();
                using var workbook = new XLWorkbook(stream);
                var worksheet = workbook.Worksheet(1);

                // Validate headers
                for (int i = 0; i < expectedHeaders.Length; i++)
                {
                    var cellValue = worksheet.Cell(1, i + 1).GetValue<string>()?.Trim() ?? string.Empty;
                    if (!string.Equals(cellValue, expectedHeaders[i], StringComparison.OrdinalIgnoreCase))
                    {
                        return new ExecuteAndReponse
                        {
                            Status = false,
                            Message = $"Header mismatch at column {i + 1}: Expected '{expectedHeaders[i]}', found '{cellValue}'",
                            Code = HttpStatusCode.BadRequest
                        };
                    }
                }

                // Validate header length
                if (worksheet.Row(1).CellsUsed().Count() != expectedHeaders.Length)
                {
                    return new ExecuteAndReponse
                    {
                        Status = false,
                        Message = "Header count mismatch",
                        Code = HttpStatusCode.BadRequest
                    };
                }

                var rows = worksheet.RowsUsed().Skip(1).ToList();

                // Check for duplicate (Ecode, Month) in Excel
                var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var row in rows)
                {
                    var ecode = row.Cell(1).GetValue<string>()?.Trim();
                    var month = row.Cell(2).GetValue<string>()?.Trim();
                    
                    if (string.IsNullOrWhiteSpace(ecode) || string.IsNullOrWhiteSpace(month))
                        continue;

                    //var key = $"{ecode}|{month}";
                    //if (!seenKeys.Add(key))
                    //{
                    //    return new ExecuteAndReponse
                    //    {
                    //        Status = false,
                    //        Message = $"Duplicate combination of Ecode '{ecode}' and Month '{month}' found in Excel.",
                    //        Code = HttpStatusCode.BadRequest
                    //    };
                    //}
                }

                // Extract all Ecode and Month combinations from Excel
                var excelKeys = rows
                    .Where(r => !string.IsNullOrWhiteSpace(r.Cell(1).GetValue<string>()?.Trim()) 
                        && !string.IsNullOrWhiteSpace(r.Cell(2).GetValue<string>()?.Trim()))
                    .Select(r => new
                    {
                        Ecode = r.Cell(1).GetValue<string>()?.Trim(),
                        Month = r.Cell(2).GetValue<string>()?.Trim()
                    })
                    .ToList();

                if (!excelKeys.Any())
                {
                    return new ExecuteAndReponse
                    {
                        Status = false,
                        Message = "No valid data rows found in Excel",
                        Code = HttpStatusCode.BadRequest
                    };
                }

                var ecodes = excelKeys.Select(k => k.Ecode).Distinct().ToList();
                var months = excelKeys.Select(k => k.Month).Distinct().ToList();

                // Fetch all existing records from DB that match the Excel data
                var existing = await _context.EcodeWiseWeekOffMappings
                    .AsQueryable()
                    .Where(x => ecodes.Contains(x.Ecode) 
                        && months.Contains(x.MONTH) 
                        && x.IsDeleted != true)
                    .ToListAsync();

                // Build dictionary with composite key (Ecode|Month)
                var existingDict = existing.ToDictionary(
                    x => $"{x.Ecode}|{x.MONTH}", 
                    StringComparer.OrdinalIgnoreCase);

                var newRows = new List<EcodeWiseWeekOffMapping>();
                var updatedRows = new List<EcodeWiseWeekOffMapping>();

                foreach (var row in rows)
                {
                    var ecode = row.Cell(1).GetValue<string>()?.Trim();
                    var month = row.Cell(2).GetValue<string>()?.Trim();
                    
                    if (string.IsNullOrWhiteSpace(ecode) || string.IsNullOrWhiteSpace(month))
                        continue;

                    var key = $"{ecode}|{month}";

                    if (existingDict.TryGetValue(key, out var existingRow))
                    {
                        // Update existing record
                        existingRow.TotalAttendance = row.Cell(3).GetValue<string>()?.Trim();
                        
                        // Handle WeeklyOFF - can be decimal or string
                        var weeklyOffValue = row.Cell(4).GetValue<string>()?.Trim();
                        if (!string.IsNullOrWhiteSpace(weeklyOffValue))
                        {
                            if (decimal.TryParse(weeklyOffValue, out var weeklyOffDecimal))
                            {
                                existingRow.WeeklyOff = weeklyOffDecimal;
                            }
                        }

                        existingRow.UpdatedBy = "System";
                        existingRow.UpdatedOn = DateTime.UtcNow;
                        updatedRows.Add(existingRow);
                    }
                    else
                    {
                        // Insert new record
                        var totalAttendance = row.Cell(3).GetValue<string>()?.Trim();
                        var weeklyOffValue = row.Cell(4).GetValue<string>()?.Trim();
                        decimal? weeklyOff = null;

                        if (!string.IsNullOrWhiteSpace(weeklyOffValue))
                        {
                            if (decimal.TryParse(weeklyOffValue, out var weeklyOffDecimal))
                            {
                                weeklyOff = weeklyOffDecimal;
                            }
                        }

                        newRows.Add(new EcodeWiseWeekOffMapping
                        {
                            Ecode = ecode,
                            MONTH = month,
                            TotalAttendance = totalAttendance,
                            WeeklyOff = weeklyOff,
                            CreatedBy = "System",
                            CreatedOn = DateTime.UtcNow,
                            IsActive = true,
                            IsDeleted = false
                        });
                    }
                }

                // Save changes
                if (newRows.Any())
                {
                    await _context.EcodeWiseWeekOffMappings.AddRangeAsync(newRows);
                }

                if (updatedRows.Any())
                {
                    _context.EcodeWiseWeekOffMappings.UpdateRange(updatedRows);
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Uploaded EcodeWiseWeekOffMapping: {NewCount} new records, {UpdatedCount} updated records", 
                    newRows.Count, updatedRows.Count);

                return new ExecuteAndReponse
                {
                    Status = true,
                    Message = $"Uploaded successfully. {newRows.Count} new record(s) added, {updatedRows.Count} record(s) updated.",
                    Code = HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading EcodeWiseWeekOffMapping");
                return new ExecuteAndReponse
                {
                    Status = false,
                    Message = $"Error uploading EcodeWiseWeekOffMapping: {ex.Message}",
                    Code = HttpStatusCode.InternalServerError
                };
            }
        }
    }
}

