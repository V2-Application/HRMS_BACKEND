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
    public class GroupWiseStoreCodeMappingService : IGroupWiseStoreCodeMappingService
    {
        private readonly HRMSContext _context;
        private readonly ILogger<GroupWiseStoreCodeMappingService> _logger;

        public GroupWiseStoreCodeMappingService(HRMSContext context, ILogger<GroupWiseStoreCodeMappingService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ExecuteAndReponse> UpsertGroupWiseStoreCodeMappingAsync(GroupWiseStoreCodeMappingUpsertDto mappingDto)
        {
            if (mappingDto == null)
            {
                return new ExecuteAndReponse
                {
                    Status = false,
                    Message = "Mapping data is required.",
                    Code = HttpStatusCode.BadRequest
                };
            }

            try
            {
                // Validate required fields
                if (!mappingDto.GroupId.HasValue || string.IsNullOrWhiteSpace(mappingDto.ST_CD))
                {
                    return new ExecuteAndReponse
                    {
                        Status = false,
                        Message = "GroupId and ST_CD are required.",
                        Code = HttpStatusCode.BadRequest
                    };
                }

                // Ensure group exists and is not deleted
                var groupExists = await _context.GroupMasters.AsNoTracking().AsQueryable()
                    .AnyAsync(g => g.Id == mappingDto.GroupId && (!g.IsDeleted.HasValue || g.IsDeleted == false));
                if (!groupExists)
                {
                    return new ExecuteAndReponse
                    {
                        Status = false,
                        Message = $"Group with ID {mappingDto.GroupId} not found.",
                        Code = HttpStatusCode.BadRequest
                    };
                }

                // Upsert by ST_CD: if exists, update GroupId; else create new
                var existing = await _context.GroupWiseStoreCodeMappings
                    .FirstOrDefaultAsync(m => m.ST_CD == mappingDto.ST_CD);

                if (existing != null)
                {
                    existing.GroupId = mappingDto.GroupId;
                    existing.IsDeleted = false;
                    existing.IsActive = true;
                    existing.UpdatedBy = "System";
                    existing.UpdatedOn = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    return new ExecuteAndReponse
                    {
                        Status = true,
                        Message = "Mapping updated successfully",
                        Code = HttpStatusCode.OK
                    };
                }
                else
                {
                    var mapping = new GroupWiseStoreCodeMapping
                    {
                        GroupId = mappingDto.GroupId,
                        ST_CD = mappingDto.ST_CD,
                        IsActive = true,
                        IsDeleted = false,
                        CreatedBy = "System",
                        CreatedOn = DateTime.UtcNow,
                        UpdatedBy = "System",
                        UpdatedOn = DateTime.UtcNow
                    };
                    await _context.GroupWiseStoreCodeMappings.AddAsync(mapping);
                    await _context.SaveChangesAsync();

                    return new ExecuteAndReponse
                    {
                        Status = true,
                        Message = "Mapping created successfully",
                        Code = HttpStatusCode.OK
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while upserting mapping");
                return new ExecuteAndReponse
                {
                    Status = false,
                    Message = ex.Message,
                    Code = HttpStatusCode.BadRequest
                };
            }
        }

        public async Task<ExecuteAndReponse> DeleteGroupWiseStoreCodeMappingAsync(int id)
        {
            if (id <= 0)
            {
                return new ExecuteAndReponse
                {
                    Status = false,
                    Message = "Invalid mapping ID.",
                    Code = HttpStatusCode.BadRequest
                };
            }

            try
            {
                var mapping = await _context.GroupWiseStoreCodeMappings
                    .FirstOrDefaultAsync(row => row.Id == id);
                
                if (mapping == null)
                {
                    _logger.LogWarning("Mapping with ID {Id} not found for deletion", id);
                    return new ExecuteAndReponse
                    {
                        Status = false,
                        Message = "Mapping not found for deletion",
                        Code = HttpStatusCode.BadRequest
                    };
                }

                // Soft delete
                mapping.IsDeleted = true;
                mapping.IsActive = false;
                mapping.UpdatedBy = "System";
                mapping.UpdatedOn = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                _logger.LogInformation("Mapping with ID {Id} deleted successfully", id);

                return new ExecuteAndReponse
                {
                    Status = true,
                    Message = "Mapping deleted successfully",
                    Code = HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while deleting mapping with ID: {Id}", id);
                return new ExecuteAndReponse
                {
                    Status = false,
                    Message = ex.Message,
                    Code = HttpStatusCode.BadRequest
                };
            }
        }

        public async Task<FetchAndResponse> GetAllGroupWiseStoreCodeMappingsAsync()
        {
            try
            {
                var mappings = await _context.GroupWiseStoreCodeMappings
    .AsNoTracking()
    .Where(g => !g.IsDeleted.Value)
    .GroupJoin(
        _context.GroupMasters.AsNoTracking(),
        mapping => mapping.GroupId,
        group => group.Id,
        (mapping, groups) => new { mapping, groups }
    )
    .SelectMany(
        x => x.groups.DefaultIfEmpty(),   // this makes it LEFT JOIN
        (x, group) => new GroupWiseStoreCodeMappingResponseDto
        {
            Id = x.mapping.Id,
            GroupId = x.mapping.GroupId,
            GroupName = group != null ? group.GroupName : null, // safe null handling
            ST_CD = x.mapping.ST_CD,
            IsActive = x.mapping.IsActive,
            IsDeleted = x.mapping.IsDeleted,
            CreatedOn = x.mapping.CreatedOn,
            CreatedBy = x.mapping.CreatedBy,
            UpdatedBy = x.mapping.UpdatedBy,
            UpdatedOn = x.mapping.UpdatedOn
        }
    )
    .OrderBy(g => g.GroupName)
    .ThenBy(g => g.ST_CD)
    .ToListAsync();


                if (mappings == null || !mappings.Any())
                {
                    return new FetchAndResponse
                    {
                        Status = false,
                        Message = "No data found",
                        Code = HttpStatusCode.BadRequest,
                        Data = null
                    };
                }

                _logger.LogInformation("Retrieved {Count} mappings", mappings.Count);
                return new FetchAndResponse
                {
                    Status = true,
                    Message = "Mappings fetched successfully",
                    Data = mappings,
                    Code = HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving all mappings");
                return new FetchAndResponse
                {
                    Status = false,
                    Message = ex.Message,
                    Code = HttpStatusCode.BadRequest,
                    Data = null
                };
            }
        }
        public async Task<FetchAndResponse> GetAllGroupCodeMappingsAsync(int id)
        {
            try
            {
                var mappings = await _context.GroupWiseStoreCodeMappings
    .AsNoTracking()
    .Where(g => !g.IsDeleted.Value && g.GroupId==id)
    .GroupJoin(
        _context.GroupMasters.AsNoTracking(),
        mapping => mapping.GroupId,
        group => group.Id,
        (mapping, groups) => new { mapping, groups }
    )
    .SelectMany(
        x => x.groups.DefaultIfEmpty(),   // this makes it LEFT JOIN
        (x, group) => new GroupWiseStoreCodeMappingResponseDto
        {
            Id = x.mapping.Id,
            GroupId = x.mapping.GroupId,
            GroupName = group != null ? group.GroupName : null, // safe null handling
            ST_CD = x.mapping.ST_CD,
            IsActive = x.mapping.IsActive,
            IsDeleted = x.mapping.IsDeleted,
            CreatedOn = x.mapping.CreatedOn,
            CreatedBy = x.mapping.CreatedBy,
            UpdatedBy = x.mapping.UpdatedBy,
            UpdatedOn = x.mapping.UpdatedOn
        }
    )
    .OrderBy(g => g.GroupName)
    .ThenBy(g => g.ST_CD)
    .ToListAsync();


                if (mappings == null || !mappings.Any())
                {
                    return new FetchAndResponse
                    {
                        Status = false,
                        Message = "No data found",
                        Code = HttpStatusCode.BadRequest,
                        Data = null
                    };
                }

                _logger.LogInformation("Retrieved {Count} mappings", mappings.Count);
                return new FetchAndResponse
                {
                    Status = true,
                    Message = "Mappings fetched successfully",
                    Data = mappings,
                    Code = HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving all mappings");
                return new FetchAndResponse
                {
                    Status = false,
                    Message = ex.Message,
                    Code = HttpStatusCode.BadRequest,
                    Data = null
                };
            }
        }

        public async Task<FetchAndResponse> UploadGroupWiseStoreCodeMappingAsync(IFormFile file)
        {
            var expectedHeaders = new[] { "GroupName", "ST_CD" };
            
            if (file == null || file.Length == 0)
                return new FetchAndResponse
                {
                    Status = false,
                    Message = "No file uploaded",
                    Code = HttpStatusCode.BadRequest,
                    Data = null
                };

            try
            {
                using var stream = file.OpenReadStream();
                using var workbook = new XLWorkbook(stream);
                var worksheet = workbook.Worksheet(1);

                // Validate headers
                for (int i = 0; i < expectedHeaders.Length; i++)
                {
                    var cellValue = worksheet.Cell(1, i + 1).GetValue<string>()?.Trim();
                    if (!string.Equals(cellValue, expectedHeaders[i], StringComparison.OrdinalIgnoreCase))
                        return new FetchAndResponse
                        {
                            Status = false,
                            Message = $"Header mismatch at column {i + 1}: Expected '{expectedHeaders[i]}', found '{cellValue}'",
                            Code = HttpStatusCode.BadRequest,
                            Data = null
                        };
                }

                if (worksheet.Row(1).CellsUsed().Count() != expectedHeaders.Length)
                    return new FetchAndResponse
                    {
                        Status = false,
                        Message = "Header count mismatch",
                        Code = HttpStatusCode.BadRequest,
                        Data = null
                    };

                var rows = worksheet.RowsUsed().Skip(1).ToList();
                var uploadData = new List<GroupWiseStoreCodeMappingUploadDto>();

                // Parse Excel data
                foreach (var row in rows)
                {
                    var groupName = row.Cell(1).GetValue<string>()?.Trim();
                    var stCd = row.Cell(2).GetValue<string>()?.Trim();

                    if (!string.IsNullOrEmpty(groupName) && !string.IsNullOrEmpty(stCd))
                    {
                        uploadData.Add(new GroupWiseStoreCodeMappingUploadDto
                        {
                            GroupName = groupName,
                            ST_CD = stCd
                        });
                    }
                }

                if (!uploadData.Any())
                    return new FetchAndResponse
                    {
                        Status = false,
                        Message = "No valid data found in Excel",
                        Code = HttpStatusCode.BadRequest,
                        Data = null
                    };

                // Get all valid group names from GroupMaster
                var validGroups = await _context.GroupMasters.AsNoTracking().AsQueryable()
                    .Where(g => !g.IsDeleted.Value)
                    .ToDictionaryAsync(g => g.GroupName.ToLower(), g => g.Id);

                // Validate group names
                var invalidGroups = uploadData.Where(d => !validGroups.ContainsKey(d.GroupName.ToLower()))
                    .Select(d => d.GroupName)
                    .Distinct()
                    .ToList();

                if (invalidGroups.Any())
                {
                    var validGroupNames = string.Join(", ", validGroups.Keys);
                    return new FetchAndResponse
                    {
                        Status = false,
                        Message = $"Invalid group names found: {string.Join(", ", invalidGroups)}. Valid groups are: {validGroupNames}",
                        Code = HttpStatusCode.BadRequest,
                        Data = null
                    };
                }

                // Check for duplicate ST_CD in upload data
                var duplicateStCds = uploadData.GroupBy(d => d.ST_CD.ToLower())
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();

                if (duplicateStCds.Any())
                    return new FetchAndResponse
                    {
                        Status = false,
                        Message = $"Duplicate ST_CD found in upload: {string.Join(", ", duplicateStCds)}",
                        Code = HttpStatusCode.BadRequest,
                        Data = null
                    };


                // Process upload data with upsert behavior by ST_CD
                var stCds = uploadData.Select(d => d.ST_CD).ToList();
                var existingMappings = await _context.GroupWiseStoreCodeMappings
                    .Where(m => stCds.Contains(m.ST_CD))
                    .ToListAsync();

                int updated = 0, inserted = 0;
                foreach (var data in uploadData)
                {
                    var groupId = validGroups[data.GroupName.ToLower()];
                    var existing = existingMappings.FirstOrDefault(m => string.Equals(m.ST_CD, data.ST_CD, StringComparison.OrdinalIgnoreCase));
                    if (existing != null)
                    {
                        existing.GroupId = groupId;
                        existing.IsDeleted = false;
                        existing.IsActive = true;
                        existing.UpdatedBy = "System";
                        existing.UpdatedOn = DateTime.UtcNow;
                        updated++;
                    }
                    else
                    {
                        var mapping = new GroupWiseStoreCodeMapping
                        {
                            GroupId = groupId,
                            ST_CD = data.ST_CD,
                            IsActive = true,
                            IsDeleted = false,
                            CreatedBy = "System",
                            CreatedOn = DateTime.UtcNow,
                            UpdatedBy = "System",
                            UpdatedOn = DateTime.UtcNow
                        };
                        await _context.GroupWiseStoreCodeMappings.AddAsync(mapping);
                        inserted++;
                    }
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully processed mappings. Updated: {Updated}, Inserted: {Inserted}", updated, inserted);

                return new FetchAndResponse
                {
                    Status = true,
                    Message = $"Processed mappings. Updated: {updated}, Inserted: {inserted}",
                    Data = new { Updated = updated, Inserted = inserted },
                    Code = HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while uploading mappings");
                return new FetchAndResponse
                {
                    Status = false,
                    Message = ex.Message,
                    Code = HttpStatusCode.BadRequest,
                    Data = null
                };
            }
        }
    }
}
