using HRMSAPI.Data;
using HRMSAPI.DTO;
using HRMSAPI.Interfaces;
using Microsoft.EntityFrameworkCore;
using Roomsy.DTOS.GenericsResponses;
using System.Net;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using System.Data;

namespace HRMSAPI.Implementation
{
    public class EmployeeStoreVisibilityMappingService : BaseService, IEmployeeStoreVisibilityMappingService
    {
        public EmployeeStoreVisibilityMappingService(HRMSContext context) : base(context)
        {
        }

        public async Task<FetchAndResponse> GetAllMappingsAsync()
        {
            try
            {
                // Full list of stores assigned to each employee, grouped by ECode with comma-separated
                // (distinct, ordered) StCodes. Done in SQL via STRING_AGG (the table has ~4.8M mapping
                // rows; EF's string.Join-in-GroupBy cannot translate and grouping in memory is wasteful).
                // CAST to NVARCHAR(MAX) so the aggregate isn't capped at 8000 bytes for broad-visibility users.
                var mappings = new List<EmployeeStoreMappingResponseDto>();

                var connection = _context.Database.GetDbConnection();
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
SELECT d.ECode,
       STRING_AGG(CAST(d.StCode AS NVARCHAR(MAX)), ',') WITHIN GROUP (ORDER BY d.StCode) AS StCodes
FROM (
    SELECT DISTINCT ECode, StCode
    FROM dbo.tblEmployeeStroreVisibilityMapping WITH (NOLOCK)
    WHERE ISNULL(IsDeleted, 0) <> 1
      AND ECode IS NOT NULL
      AND StCode IS NOT NULL
) d
GROUP BY d.ECode
ORDER BY d.ECode;";
                    command.CommandType = CommandType.Text;
                    command.CommandTimeout = 180;

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            mappings.Add(new EmployeeStoreMappingResponseDto
                            {
                                ECode = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                                StCodes = reader.IsDBNull(1) ? string.Empty : reader.GetString(1)
                            });
                        }
                    }
                }

                return BuildFetchSuccessResponse("Employee store visibility mappings retrieved successfully", mappings);
            }
            catch (Exception ex)
            {
                return BuildFetchErrorResponse($"Error retrieving mappings: {ex.Message}", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<FetchAndResponse> GetActiveLocationsAsync()
        {
            try
            {
                var locations = await _context.tblLocations
                    .AsNoTracking()
                    .Where(l => l.IsActive == true)
                    .OrderBy(l => l.STCode)
                    .Select(l => new
                    {
                        l.LocationId,
                        l.STCode,
                        l.LocationName
                    })
                    .ToListAsync();

                return BuildFetchSuccessResponse("Active locations fetched successfully", locations);
            }
            catch (Exception ex)
            {
                return BuildFetchErrorResponse($"Error fetching active locations: {ex.Message}", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<FetchAndResponse> GetStoreStateAsync(string eCode)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(eCode))
                {
                    return BuildFetchErrorResponse("ECode is required", HttpStatusCode.BadRequest);
                }

                // Use raw SQL to call the function
                var sql = "SELECT StCode, IsChecked, IsIndeterminate, State FROM dbo.ufn_StoreState(@ECode)";
                var result = await _context.Database
                    .SqlQueryRaw<StoreStateDto>(sql, new SqlParameter("@ECode", eCode))
                    .ToListAsync();

                return BuildFetchSuccessResponse($"Store states fetched successfully for ECode: {eCode}", result);
            }
            catch (Exception ex)
            {
                return BuildFetchErrorResponse($"Error fetching store states: {ex.Message}", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<FetchAndResponse> GetDeptStateAsync(string eCode, string stCode)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(eCode))
                {
                    return BuildFetchErrorResponse("ECode is required", HttpStatusCode.BadRequest);
                }

                if (string.IsNullOrWhiteSpace(stCode))
                {
                    return BuildFetchErrorResponse("StCode is required", HttpStatusCode.BadRequest);
                }

                // Use raw SQL to call the function
                var sql = "SELECT DepartmentId, DepartmentName, IsChecked, IsIndeterminate, State FROM dbo.ufn_DeptState(@ECode, @StCode)";
                var result = await _context.Database
                    .SqlQueryRaw<DeptStateDto>(sql, 
                        new SqlParameter("@ECode", eCode),
                        new SqlParameter("@StCode", stCode))
                    .ToListAsync();

                return BuildFetchSuccessResponse($"Department states fetched successfully for ECode: {eCode}, StCode: {stCode}", result);
            }
            catch (Exception ex)
            {
                return BuildFetchErrorResponse($"Error fetching department states: {ex.Message}", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ExecuteAndReponse> SetDeptExceptionsForStoreAsync(SetDeptExceptionsForStoreDto request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.ECode))
                {
                    return BuildExecuteErrorResponse("ECode is required", HttpStatusCode.BadRequest);
                }

                if (string.IsNullOrWhiteSpace(request.StCode))
                {
                    return BuildExecuteErrorResponse("StCode is required", HttpStatusCode.BadRequest);
                }

                // Create DataTable for table-valued parameter
                var deselectedDeptIdsTable = new DataTable();
                deselectedDeptIdsTable.Columns.Add("ID", typeof(long));

                foreach (var deptId in request.DeselectedDeptIds)
                {
                    deselectedDeptIdsTable.Rows.Add(deptId);
                }

                // Create parameters for the stored procedure
                var eCodeParam = new SqlParameter("@ECode", request.ECode);
                var stCodeParam = new SqlParameter("@StCode", request.StCode);
                var deselectedDeptIdsParam = new SqlParameter("@DeselectedDeptIds", SqlDbType.Structured)
                {
                    TypeName = "dbo.StringListType",
                    Value = deselectedDeptIdsTable
                };
                var actorParam = new SqlParameter("@Actor", request.Actor ?? (object)DBNull.Value);

                // Execute the stored procedure
                await _context.Database.ExecuteSqlRawAsync(
                    "EXEC dbo.SetDeptExceptionsForStore @ECode, @StCode, @DeselectedDeptIds, @Actor",
                    eCodeParam, stCodeParam, deselectedDeptIdsParam, actorParam);

                return BuildExecuteSuccessResponse($"Department exceptions set successfully for ECode: {request.ECode}, StCode: {request.StCode}");
            }
            catch (Exception ex)
            {
                return BuildExecuteErrorResponse($"Error setting department exceptions: {ex.Message}", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<FetchAndResponse> GetDesigStateAsync(string eCode, string stCode, string deptId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(eCode))
                {
                    return BuildFetchErrorResponse("ECode is required", HttpStatusCode.BadRequest);
                }

                if (string.IsNullOrWhiteSpace(stCode))
                {
                    return BuildFetchErrorResponse("StCode is required", HttpStatusCode.BadRequest);
                }

                if (string.IsNullOrWhiteSpace(deptId))
                {
                    return BuildFetchErrorResponse("DeptId is required", HttpStatusCode.BadRequest);
                }

                // Use raw SQL to call the function
                var sql = "SELECT DesignationId, DesignationName, IsChecked, State FROM dbo.ufn_DesigState(@ECode, @StCode, @DeptId)";
                var result = await _context.Database
                    .SqlQueryRaw<DesigStateDto>(sql, 
                        new SqlParameter("@ECode", eCode),
                        new SqlParameter("@StCode", stCode),
                        new SqlParameter("@DeptId", deptId))
                    .ToListAsync();

                return BuildFetchSuccessResponse($"Designation states fetched successfully for ECode: {eCode}, StCode: {stCode}, DeptId: {deptId}", result);
            }
            catch (Exception ex)
            {
                return BuildFetchErrorResponse($"Error fetching designation states: {ex.Message}", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ExecuteAndReponse> SetDesigExceptionsForStoreDeptAsync(SetDesigExceptionsForStoreDeptDto request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.ECode))
                {
                    return BuildExecuteErrorResponse("ECode is required", HttpStatusCode.BadRequest);
                }

                if (string.IsNullOrWhiteSpace(request.StCode))
                {
                    return BuildExecuteErrorResponse("StCode is required", HttpStatusCode.BadRequest);
                }

                if (string.IsNullOrWhiteSpace(request.DeptId))
                {
                    return BuildExecuteErrorResponse("DeptId is required", HttpStatusCode.BadRequest);
                }

                // Create DataTable for table-valued parameter
                var deselectedDesigIdsTable = new DataTable();
                deselectedDesigIdsTable.Columns.Add("ID", typeof(long));

                foreach (var desigId in request.DeselectedDesigIds)
                {
                    deselectedDesigIdsTable.Rows.Add(desigId);
                }

                // Create parameters for the stored procedure
                var eCodeParam = new SqlParameter("@ECode", request.ECode);
                var stCodeParam = new SqlParameter("@StCode", request.StCode);
                var deptIdParam = new SqlParameter("@DeptId", request.DeptId);
                var deselectedDesigIdsParam = new SqlParameter("@DeselectedDesigIds", SqlDbType.Structured)
                {
                    TypeName = "dbo.DesigStringListType",
                    Value = deselectedDesigIdsTable
                };
                var actorParam = new SqlParameter("@Actor", request.Actor ?? (object)DBNull.Value);

                // Execute the stored procedure
                await _context.Database.ExecuteSqlRawAsync(
                    "EXEC dbo.SetDesigExceptionsForStoreDept @ECode, @StCode, @DeptId, @DeselectedDesigIds, @Actor",
                    eCodeParam, stCodeParam, deptIdParam, deselectedDesigIdsParam, actorParam);

                return BuildExecuteSuccessResponse($"Designation exceptions set successfully for ECode: {request.ECode}, StCode: {request.StCode}, DeptId: {request.DeptId}");
            }
            catch (Exception ex)
            {
                return BuildExecuteErrorResponse($"Error setting designation exceptions: {ex.Message}", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<FetchAndResponse> GetPermissionIndexForECodeAsync(string eCode, string? stCode = null, string? deptId = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(eCode))
                {
                    return BuildFetchErrorResponse("ECode is required", HttpStatusCode.BadRequest);
                }

                // Create parameters for the stored procedure
                var eCodeParam = new SqlParameter("@ECode", eCode);
                var stCodeParam = new SqlParameter("@StCode", stCode ?? (object)DBNull.Value);
                var deptIdParam = new SqlParameter("@DeptId", deptId ?? (object)DBNull.Value);

                // Execute the stored procedure and get multiple result sets
                using var command = _context.Database.GetDbConnection().CreateCommand();
                command.CommandText = "EXEC dbo.GetPermissionIndexForECode @ECode, @StCode, @DeptId";
                command.Parameters.Add(eCodeParam);
                command.Parameters.Add(stCodeParam);
                command.Parameters.Add(deptIdParam);

                await _context.Database.OpenConnectionAsync();
                using var reader = await command.ExecuteReaderAsync();

                var result = new PermissionIndexDto();

                // Read first result set - Allowed Stores
                while (await reader.ReadAsync())
                {
                    result.AllowedStores.Add(new AllowedStoreDto
                    {
                        StCode = reader.GetString("StCode")
                    });
                }

                // Move to second result set - Department Exceptions
                if (await reader.NextResultAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        result.DeptExceptions.Add(new DeptExceptionDto
                        {
                            StCode = reader.GetString("StCode"),
                            DeptId = reader.GetString("DeptId")
                        });
                    }
                }

                // Move to third result set - Designation Exceptions
                if (await reader.NextResultAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        result.DesigExceptions.Add(new DesigExceptionDto
                        {
                            StCode = reader.GetString("StCode"),
                            DeptId = reader.GetString("DeptId"),
                            DesigId = reader.GetString("DesigId")
                        });
                    }
                }

                return BuildFetchSuccessResponse($"Permission index fetched successfully for ECode: {eCode}", result);
            }
            catch (Exception ex)
            {
                return BuildFetchErrorResponse($"Error fetching permission index: {ex.Message}", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ExecuteAndReponse> UpsertMappingsAsync(EmployeeStoreMappingUpsertDto upsertDto)
        {
            try
            {
                using var transaction = await _context.Database.BeginTransactionAsync();

                // Get all existing mappings for the provided ECode (including deleted ones)
                var existingMappings = await _context.tblEmployeeStroreVisibilityMappings
                    .Where(x => upsertDto.Mappings.Select(m => m.ECode).Contains(x.ECode))
                    .ToListAsync();

                var processedEcodes = new HashSet<string>();

                foreach (var mapping in upsertDto.Mappings)
                {
                    if (processedEcodes.Contains(mapping.ECode))
                        continue;

                    processedEcodes.Add(mapping.ECode);

                    // Parse StCodes from comma-separated string
                    //var newStCodes = mapping.StCodes.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    //    .Select(s => s.Trim())
                    //    .Where(s => !string.IsNullOrEmpty(s))
                    //    .ToList();

                    //Change by Gautam on 21-06-2024 to handle empty stcodes for an ecode
                    var hasStCodes = !string.IsNullOrWhiteSpace(mapping.StCodes);

                    var newStCodes = hasStCodes
                        ? mapping.StCodes.Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(s => s.Trim())
                            .Where(s => !string.IsNullOrEmpty(s))
                            .ToList()
                        : new List<string>();

                    // Get existing mappings for this ECode
                    var existingForEcode = existingMappings.Where(x => x.ECode == mapping.ECode).ToList();

                    // Clear any active dept/designation DENY exceptions for this ECode so that
                    // saving a store assignment grants FULL visibility of the assigned stores'
                    // employees. This prevents the stale "Upload Excel With Dept" blanket-deny rows
                    // (DeptId/DesigId set, IsActive=0) from hiding assigned-store employees — the
                    // recurring ClusterManager "stores assigned but no employees visible" issue.
                    foreach (var deny in existingForEcode.Where(x => x.IsDeleted != true
                                && (x.DeptId != null || x.DesigId != null)))
                    {
                        deny.IsActive = false;
                        deny.IsDeleted = true;
                        deny.UpdatedOn = DateTime.Now;
                        deny.UpdatedBy = upsertDto.UpdatedBy;
                    }

                    if (!hasStCodes)
                    {
                        foreach (var existing in existingForEcode)
                        {
                            if (existing.IsActive == true && existing.IsDeleted != true)
                            {
                                existing.IsActive = false;
                                existing.IsDeleted = true;
                                existing.UpdatedOn = DateTime.Now;
                                existing.UpdatedBy = upsertDto.UpdatedBy;
                            }
                        }

                        // Skip add/reactivate logic for this ECode
                        continue;
                    }


                    // Soft-delete only currently active mappings that are not in the new list
                    foreach (var existing in existingForEcode)
                    {
                        if (existing.IsDeleted != true && existing.IsActive == true && !newStCodes.Contains(existing.StCode))
                        {
                            existing.IsDeleted = true;
                            existing.IsActive = false;
                            existing.UpdatedOn = DateTime.Now;
                            existing.UpdatedBy = upsertDto.UpdatedBy;
                        }
                    }

                    // Process new StCodes - check if they exist (including deleted ones) or need to be created
                    foreach (var stCode in newStCodes)
                    {
                        // Find existing record for this ECode+StCode (including deleted)
                        var record = existingForEcode.FirstOrDefault(x => x.StCode == stCode);

                        if (record == null)
                        {
                            // Maybe exists in DB but not in the in-memory list due to previous filter; fetch explicitly
                            record = await _context.tblEmployeeStroreVisibilityMappings
                                .FirstOrDefaultAsync(x => x.ECode == mapping.ECode && x.StCode == stCode);
                        }

                        if (record == null)
                        {
                            // Create new mapping
                            var newMapping = new tblEmployeeStroreVisibilityMapping
                            {
                                ECode = mapping.ECode,
                                StCode = stCode,
                                IsActive = true,
                                IsDeleted = false,
                                CreatedOn = DateTime.Now,
                                CreatedBy = upsertDto.UpdatedBy
                            };
                            await _context.tblEmployeeStroreVisibilityMappings.AddAsync(newMapping);
                        }
                        else
                        {
                            // Reactivate if soft-deleted
                            if (record.IsDeleted == true || record.IsActive != true)
                            {
                                record.IsDeleted = false;
                                record.IsActive = true;
                                record.UpdatedOn = DateTime.Now;
                                record.UpdatedBy = upsertDto.UpdatedBy;
                            }
                            // else already active; no action
                        }
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return BuildExecuteSuccessResponse("Employee store visibility mappings updated successfully");
            }
            catch (Exception ex)
            {
                return BuildExecuteErrorResponse($"Error updating mappings: {ex.Message}", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ExecuteAndReponse> UploaderMappingAsync(EmployeeStoreMappingUploaderDto uploaderDto)
        {
            try
            {
                // Check if mapping already exists
                var existingMapping = await _context.tblEmployeeStroreVisibilityMappings
                    .FirstOrDefaultAsync(x => x.ECode == uploaderDto.ECode && 
                                            x.StCode == uploaderDto.StCode && 
                                            x.IsDeleted != true);

                if (existingMapping != null)
                {
                    return BuildExecuteErrorResponse($"Mapping for ECode '{uploaderDto.ECode}' and StCode '{uploaderDto.StCode}' already exists", HttpStatusCode.Conflict);
                }

                // Check for duplicate in the same request (if multiple items with same ECode and StCode)
                var duplicateInRequest = await _context.tblEmployeeStroreVisibilityMappings
                    .AnyAsync(x => x.ECode == uploaderDto.ECode && 
                                 x.StCode == uploaderDto.StCode);

                if (duplicateInRequest)
                {
                    return BuildExecuteErrorResponse($"Duplicate mapping detected for ECode '{uploaderDto.ECode}' and StCode '{uploaderDto.StCode}'", HttpStatusCode.Conflict);
                }

                var newMapping = new tblEmployeeStroreVisibilityMapping
                {
                    ECode = uploaderDto.ECode,
                    StCode = uploaderDto.StCode,
                    IsActive = true,
                    IsDeleted = false,
                    CreatedOn = DateTime.Now,
                    CreatedBy = uploaderDto.CreatedBy
                };

                await _context.tblEmployeeStroreVisibilityMappings.AddAsync(newMapping);
                await _context.SaveChangesAsync();

                return BuildExecuteSuccessResponse($"Mapping for ECode '{uploaderDto.ECode}' and StCode '{uploaderDto.StCode}' created successfully");
            }
            catch (Exception ex)
            {
                return BuildExecuteErrorResponse($"Error creating mapping: {ex.Message}", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ExecuteAndReponse> UploadExcelAsync(IFormFile file, string? createdBy = null)
        {
            try
            {
                // Validate file
                if (file == null || file.Length == 0)
                    return BuildExecuteErrorResponse("File is mandatory to serve.", HttpStatusCode.BadRequest);

                // Validate file extension
                var allowedExtensions = new[] { ".xlsx", ".xls" };
                var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(fileExtension))
                    return BuildExecuteErrorResponse("Only Excel files (.xlsx, .xls) are allowed.", HttpStatusCode.BadRequest);

                var expectedHeaders = new[] { "ECODE", "STCODE" };
                var response = new ExcelUploadResponseDto();

                using var stream = file.OpenReadStream();
                using var workbook = new XLWorkbook(stream);
                var worksheet = workbook.Worksheet(1);

                // Validate headers
                var headerRow = worksheet.Row(1);
                var headerCount = headerRow.CellsUsed().Count();
                
                if (headerCount != expectedHeaders.Length)
                {
                    return BuildExecuteErrorResponse($"Header count mismatch. Expected {expectedHeaders.Length} columns, found {headerCount}.", HttpStatusCode.BadRequest);
                }

                for (int i = 0; i < expectedHeaders.Length; i++)
                {
                    var cellValue = worksheet.Cell(1, i + 1).GetValue<string>()?.Trim() ?? string.Empty;
                    if (!string.Equals(cellValue, expectedHeaders[i], StringComparison.OrdinalIgnoreCase))
                    {
                        return BuildExecuteErrorResponse($"Header mismatch at column {i + 1}: Expected '{expectedHeaders[i]}', found '{cellValue}'.", HttpStatusCode.BadRequest);
                    }
                }

                // Process rows
                var rows = worksheet.RowsUsed().Skip(1).ToList();
                response.TotalRows = rows.Count;

                if (response.TotalRows == 0)
                {
                    return BuildExecuteErrorResponse("No data rows found in Excel file.", HttpStatusCode.BadRequest);
                }

                var validMappings = new List<(string ECode, string StCode)>();
                var duplicateInExcel = new HashSet<string>();
                var validationErrors = new List<ExcelRowValidationDto>();

                // First pass: Validate all rows and check for duplicates within Excel
                for (int i = 0; i < rows.Count; i++)
                {
                    var row = rows[i];
                    var rowNumber = i + 2; // +2 because we skip header row and Excel is 1-indexed
                    
                    var ecode = row.Cell(1).GetValue<string>()?.Trim() ?? string.Empty;
                    var stcode = row.Cell(2).GetValue<string>()?.Trim() ?? string.Empty;
                    
                    var validation = new ExcelRowValidationDto
                    {
                        RowNumber = rowNumber,
                        ECode = ecode,
                        StCode = stcode,
                        IsValid = true
                    };

                    // Validate ECode
                    if (string.IsNullOrWhiteSpace(ecode))
                    {
                        validation.IsValid = false;
                        validation.ValidationErrors.Add("ECode is required");
                    }

                    // Validate StCode
                    if (string.IsNullOrWhiteSpace(stcode))
                    {
                        validation.IsValid = false;
                        validation.ValidationErrors.Add("StCode is required");
                    }

                    // Check for duplicates within Excel
                    var key = $"{ecode.ToUpperInvariant()}|{stcode.ToUpperInvariant()}";
                    if (!duplicateInExcel.Add(key))
                    {
                        validation.IsValid = false;
                        validation.ValidationErrors.Add("Duplicate ECode-StCode combination found in Excel");
                    }

                    validationErrors.Add(validation);

                    if (validation.IsValid)
                    {
                        validMappings.Add((ecode, stcode));
                    }
                }

                response.InvalidRows = validationErrors.Count(v => !v.IsValid);
                response.ValidRows = validMappings.Count;

                // If there are validation errors, return them
                if (response.InvalidRows > 0)
                {
                    var errorMessages = validationErrors
                        .Where(v => !v.IsValid)
                        .Select(v => $"Row {v.RowNumber}: {string.Join(", ", v.ValidationErrors)}")
                        .ToList();
                    
                    return BuildExecuteErrorResponse($"Validation errors found:\n{string.Join("\n", errorMessages)}", HttpStatusCode.BadRequest);
                }

                // Check for duplicates in database
                var ecodeStcodePairs = validMappings.Select(m => new { m.ECode, m.StCode }).ToList();
                var existingMappings = await _context.tblEmployeeStroreVisibilityMappings
                    .Where(x => ecodeStcodePairs.Any(p => p.ECode == x.ECode && p.StCode == x.StCode) && x.IsDeleted != true)
                    .Select(x => new { x.ECode, x.StCode })
                    .ToListAsync();

                var duplicateInDb = validMappings
                    .Where(m => existingMappings.Any(e => e.ECode == m.ECode && e.StCode == m.StCode))
                    .ToList();

                if (duplicateInDb.Any())
                {
                    var duplicateMessages = duplicateInDb
                        .Select(d => $"ECode: {d.ECode}, StCode: {d.StCode}")
                        .ToList();
                    
                    return BuildExecuteErrorResponse($"Duplicate mappings found in database:\n{string.Join("\n", duplicateMessages)}", HttpStatusCode.Conflict);
                }

                // Insert valid mappings
                using var transaction = await _context.Database.BeginTransactionAsync();
                
                var newMappings = validMappings.Select(m => new tblEmployeeStroreVisibilityMapping
                {
                    ECode = m.ECode,
                    StCode = m.StCode,
                    IsActive = true,
                    IsDeleted = false,
                    CreatedOn = DateTime.Now,
                    CreatedBy = createdBy
                }).ToList();

                await _context.tblEmployeeStroreVisibilityMappings.AddRangeAsync(newMappings);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                response.Success = true;
                response.Message = $"Successfully uploaded {response.ValidRows} mappings.";
                response.DuplicateRows = 0;

                return BuildExecuteSuccessResponse(response.Message);
            }
            catch (Exception ex)
            {
                return BuildExecuteErrorResponse($"Error processing Excel file: {ex.Message}", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ExecuteAndReponse> UploadExcelWithDeptAsync(IFormFile file, string? createdBy = null)
        {
            try
            {
                // Validate file
                if (file == null || file.Length == 0)
                    return BuildExecuteErrorResponse("File is mandatory to serve.", HttpStatusCode.BadRequest);

                // Validate file extension
                var allowedExtensions = new[] { ".xlsx", ".xls" };
                var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(fileExtension))
                    return BuildExecuteErrorResponse("Only Excel files (.xlsx, .xls) are allowed.", HttpStatusCode.BadRequest);

                var expectedHeaders = new[] { "ECODE", "STCODE", "DEPTNAME" };
                var response = new ExcelUploadResponseDto();

                using var stream = file.OpenReadStream();
                using var workbook = new XLWorkbook(stream);
                var worksheet = workbook.Worksheet(1);

                // Validate headers
                var headerRow = worksheet.Row(1);
                var headerCount = headerRow.CellsUsed().Count();
                
                if (headerCount != expectedHeaders.Length)
                {
                    return BuildExecuteErrorResponse($"Header count mismatch. Expected {expectedHeaders.Length} columns, found {headerCount}.", HttpStatusCode.BadRequest);
                }

                for (int i = 0; i < expectedHeaders.Length; i++)
                {
                    var cellValue = worksheet.Cell(1, i + 1).GetValue<string>()?.Trim() ?? string.Empty;
                    if (!string.Equals(cellValue, expectedHeaders[i], StringComparison.OrdinalIgnoreCase))
                    {
                        return BuildExecuteErrorResponse($"Header mismatch at column {i + 1}: Expected '{expectedHeaders[i]}', found '{cellValue}'.", HttpStatusCode.BadRequest);
                    }
                }

                // Process rows
                var rows = worksheet.RowsUsed().Skip(1).ToList();
                response.TotalRows = rows.Count;

                if (response.TotalRows == 0)
                {
                    return BuildExecuteErrorResponse("No data rows found in Excel file.", HttpStatusCode.BadRequest);
                }

                var validMappings = new List<(string ECode, string StCode, string DeptName)>();
                var duplicateInExcel = new HashSet<string>();
                var validationErrors = new List<ExcelRowValidationDto>();

                // First pass: Validate all rows and check for duplicates within Excel
                for (int i = 0; i < rows.Count; i++)
                {
                    var row = rows[i];
                    var rowNumber = i + 2; // +2 because we skip header row and Excel is 1-indexed
                    
                    var ecode = row.Cell(1).GetValue<string>()?.Trim() ?? string.Empty;
                    var stcode = row.Cell(2).GetValue<string>()?.Trim() ?? string.Empty;
                    var deptName = row.Cell(3).GetValue<string>()?.Trim() ?? string.Empty;
                    
                    var validation = new ExcelRowValidationDto
                    {
                        RowNumber = rowNumber,
                        ECode = ecode,
                        StCode = stcode,
                        IsValid = true
                    };

                    // Validate ECode
                    if (string.IsNullOrWhiteSpace(ecode))
                    {
                        validation.IsValid = false;
                        validation.ValidationErrors.Add("ECode is required");
                    }

                    // Validate StCode
                    if (string.IsNullOrWhiteSpace(stcode))
                    {
                        validation.IsValid = false;
                        validation.ValidationErrors.Add("StCode is required");
                    }

                    // Validate DeptName
                    if (string.IsNullOrWhiteSpace(deptName))
                    {
                        validation.IsValid = false;
                        validation.ValidationErrors.Add("DeptName is required");
                    }

                    // Check for duplicates within Excel (ECode + StCode + DeptName combination)
                    var key = $"{ecode.ToUpperInvariant()}|{stcode.ToUpperInvariant()}|{deptName.ToUpperInvariant()}";
                    if (!duplicateInExcel.Add(key))
                    {
                        validation.IsValid = false;
                        validation.ValidationErrors.Add("Duplicate ECode-StCode-DeptName combination found in Excel");
                    }

                    validationErrors.Add(validation);

                    if (validation.IsValid)
                    {
                        validMappings.Add((ecode, stcode, deptName));
                    }
                }

                response.InvalidRows = validationErrors.Count(v => !v.IsValid);
                response.ValidRows = validMappings.Count;

                // If there are validation errors, return them
                if (response.InvalidRows > 0)
                {
                    var errorMessages = validationErrors
                        .Where(v => !v.IsValid)
                        .Select(v => $"Row {v.RowNumber}: {string.Join(", ", v.ValidationErrors)}")
                        .ToList();
                    
                    return BuildExecuteErrorResponse($"Validation errors found:\n{string.Join("\n", errorMessages)}", HttpStatusCode.BadRequest);
                }

                // Get all departments for mapping DeptName to DeptId
                var departments = await _context.tblDepartments
                    .Select(d => new { d.DepartmentId, d.DepartmentName })
                    .ToListAsync();

                var deptNameToIdMap = departments.ToDictionary(d => d.DepartmentName.ToUpperInvariant(), d => d.DepartmentId);

                // Validate all department names exist
                var invalidDeptNames = validMappings
                    .Where(m => !deptNameToIdMap.ContainsKey(m.DeptName.ToUpperInvariant()))
                    .Select(m => m.DeptName)
                    .Distinct()
                    .ToList();

                if (invalidDeptNames.Any())
                {
                    return BuildExecuteErrorResponse($"Invalid department names found: {string.Join(", ", invalidDeptNames)}", HttpStatusCode.BadRequest);
                }

                // ── Additive, allow-only insert (NEVER truncate; never modify existing data) ──────
                // Per the data-safety requirement: this uploader only ADDS store-access (base-allow)
                // rows for ECode+Store combos that aren't already present. It does NOT truncate, does
                // NOT touch/soft-delete/update existing rows, and does NOT create any dept/designation
                // DENY exception rows (those blanket denies previously hid assigned-store employees).
                var uploadedEcodes = validMappings.Select(m => m.ECode).Distinct().ToList();

                // Existing active store-access combos for the uploaded ECodes → skip to avoid duplicates
                var existingAllowList = await _context.tblEmployeeStroreVisibilityMappings
                    .Where(x => uploadedEcodes.Contains(x.ECode)
                                && x.DeptId == null && x.DesigId == null
                                && x.IsActive == true && x.IsDeleted != true)
                    .Select(x => new { x.ECode, x.StCode })
                    .ToListAsync();
                var existingAllowSet = new HashSet<string>(
                    existingAllowList.Select(x => (x.ECode ?? "") + "|" + (x.StCode ?? "")));

                var storeAccessGroups = validMappings
                    .GroupBy(m => new { m.ECode, m.StCode })
                    .ToList();

                var newRecords = new List<tblEmployeeStroreVisibilityMapping>();

                foreach (var group in storeAccessGroups)
                {
                    var eCode = group.Key.ECode;
                    var stCode = group.Key.StCode;

                    // Skip combos that already have an active store-access row — no duplicates and
                    // no modification of existing data.
                    if (existingAllowSet.Contains((eCode ?? "") + "|" + (stCode ?? "")))
                        continue;

                    // Store-access (base-allow) row only — no deny/exception rows.
                    newRecords.Add(new tblEmployeeStroreVisibilityMapping
                    {
                        ECode = eCode, StCode = stCode,
                        DeptId = null, DesigId = null,
                        IsActive = true, IsDeleted = false,
                        CreatedOn = DateTime.Now, CreatedBy = createdBy
                    });
                }

                // Insert in batches of 5000 to avoid memory/timeout issues
                const int batchSize = 5000;
                for (int i = 0; i < newRecords.Count; i += batchSize)
                {
                    var batch = newRecords.Skip(i).Take(batchSize).ToList();
                    await _context.tblEmployeeStroreVisibilityMappings.AddRangeAsync(batch);
                    await _context.SaveChangesAsync();
                    _context.ChangeTracker.Clear();
                }

                response.Success = true;
                response.Message = $"Store access added (allow-only). {newRecords.Count} new store-access row(s) inserted for {response.ValidRows} mapping(s); existing rows left untouched.";
                response.DuplicateRows = 0;

                return BuildExecuteSuccessResponse(response.Message);
            }
            catch (Exception ex)
            {
                return BuildExecuteErrorResponse($"Error processing Excel file: {ex.Message}", HttpStatusCode.InternalServerError);
            }
        }
    }
}
