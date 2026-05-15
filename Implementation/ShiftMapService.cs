using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using HRMSAPI.Data;
using HRMSAPI.Interfaces;
using HRMSAPI.DTO;

public class ShiftMapService : IShiftMapService
{
    private readonly HRMSContext _context;
    private readonly ILogger<ShiftMapService> _logger;

    public ShiftMapService(HRMSContext context, ILogger<ShiftMapService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<(List<ShiftMapDTO> Records, int TotalRecords)> GetShiftMapRecordsAsync(
        string? searchTerm = null,
        string? ecode = null,
        int page = 1,
        int pageSize = 10)
    {
        if (page < 1 || pageSize < 1)
        {
            _logger.LogWarning("Invalid pagination parameters: page={Page}, pageSize={PageSize}", page, pageSize);
            throw new ArgumentException("Page and pageSize must be greater than 0.");
        }

        try
        {
            var records = new List<ShiftMapDTO>();
            int totalRecords = 0;

            using var connection = _context.Database.GetDbConnection();
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = "sp_GetShiftMapRecords";
            command.CommandType = CommandType.StoredProcedure;

            // Add parameters
            command.Parameters.Add(new SqlParameter("@SearchTerm", (object)searchTerm ?? DBNull.Value));
            command.Parameters.Add(new SqlParameter("@Ecode", (object)ecode ?? DBNull.Value));
            command.Parameters.Add(new SqlParameter("@Page", page));
            command.Parameters.Add(new SqlParameter("@PageSize", pageSize));

            using var reader = await command.ExecuteReaderAsync();

            // First result set: TotalRecords
            if (await reader.ReadAsync())
            {
                totalRecords = reader.GetInt32("TotalRecords");
            }

            // Move to the next result set: Records
            if (await reader.NextResultAsync())
            {
                while (await reader.ReadAsync())
                {
                    records.Add(new ShiftMapDTO
                    {
                        ShiftMapID = reader.GetInt32("ShiftMapID"),
                        Ecode = reader.GetString("Ecode"),
                        ShiftID = reader.GetInt32("ShiftID"),
                        ShiftName = reader.GetString("ShiftName"),
                        CreatedBy = reader.GetString("CreatedBy"),
                        CreatedOn = reader.GetDateTime("CreatedOn"),
                        LastUpdatedBy = reader.IsDBNull("LastUpdatedBy") ? null : reader.GetString("LastUpdatedBy"),
                        LastUpdatedOn = reader.IsDBNull("LastUpdatedOn") ? null : reader.GetDateTime("LastUpdatedOn")
                    });
                }
            }

            await connection.CloseAsync();
            return (records, totalRecords);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving shift map records for searchTerm: {SearchTerm}, ecode: {Ecode}, page: {Page}, pageSize: {PageSize}", searchTerm, ecode, page, pageSize);
            throw;
        }
    }

    public async Task<(bool Success, string Message)> UploadShiftMapDataAsync(IFormFile file, string createdBy = "System")
    {
        if (file == null || file.Length == 0)
        {
            _logger.LogWarning("No file uploaded for ShiftMap data upload.");
            return (false, "No file uploaded.");
        }

        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Invalid file format uploaded: {FileName}", file.FileName);
            return (false, "Only .xlsx files are supported.");
        }

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var (records, validationErrors) = await ProcessExcelFile(file, createdBy);

            if (validationErrors.Any())
            {
                _logger.LogWarning("Validation errors in file {FileName}: {Errors}", file.FileName, string.Join("; ", validationErrors));
                await transaction.RollbackAsync();
                return (false, $"Invalid data in file: {string.Join("; ", validationErrors)}");
            }

            foreach (var record in records)
            {
                var cmdText = "EXEC sp_UpsertShiftMap @Ecode, @ShiftName, @CreatedBy, @CreatedOn, @LastUpdatedBy, @LastUpdatedOn";
                var parameters = new[]
                {
                new SqlParameter("@Ecode", record.Ecode),
                new SqlParameter("@ShiftName", record.ShiftName),
                new SqlParameter("@CreatedBy", createdBy),
                new SqlParameter("@CreatedOn", DateTime.UtcNow),
                new SqlParameter("@LastUpdatedBy", createdBy),
                new SqlParameter("@LastUpdatedOn", DateTime.UtcNow),
            };

                try
                {
                    await _context.Database.ExecuteSqlRawAsync(cmdText, parameters);
                }
                catch (SqlException ex) when (ex.Number == 50000)
                {
                    _logger.LogWarning("Invalid ShiftName '{ShiftName}' for Ecode '{Ecode}'", record.ShiftName, record.Ecode);
                    await transaction.RollbackAsync();
                    return (false, $"Invalid ShiftName '{record.ShiftName}' for Ecode '{record.Ecode}'.");
                }
            }

            await transaction.CommitAsync();
            return (true, "Shift map data uploaded successfully.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error processing shift map data for file: {FileName}", file.FileName);
            return (false, $"Error uploading shift map data: {ex.Message}");
        }
    }

    // Assumes: using ClosedXML.Excel; using Microsoft.EntityFrameworkCore; using Microsoft.Data.SqlClient;

    private async Task<(List<ShiftMapRecord> Records, List<string> ValidationErrors)> ProcessExcelFile(IFormFile file, string createdBy)
    {
        var records = new List<ShiftMapRecord>();
        var validationErrors = new List<string>();
        var seenKeys = new HashSet<(string, string)>(); // Track duplicates in Excel file (Ecode, ShiftName)
        var allowedShiftNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "General Shift",
        "Morning Shift",
        "Morning 2 Shift",
        "Afternoon Shift",
        "Evening Shift",
        "Night Shift",
        "Night Shift 2"
    };

        try
        {
            using var stream = file.OpenReadStream();
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheet(1);
            var rows = worksheet.RowsUsed().Skip(1); // Skip header
            int rowNumber = 2;

            // Load valid Ecodes from database
            var validEcodes = await _context.tblEmployees
                .AsNoTracking()
                .Select(e => e.Ecode)
                .ToListAsync();

            foreach (var row in rows)
            {
                var (record, error) = ParseRow(row, rowNumber, createdBy, allowedShiftNames);
                if (error != null)
                {
                    validationErrors.Add(error);
                    rowNumber++;
                    continue;
                }

                if (record.Ecode == "NA")
                {
                    validationErrors.Add($"Row {rowNumber}: Ecode is required.");
                    rowNumber++;
                    continue;
                }

                if (!validEcodes.Contains(record.Ecode, StringComparer.OrdinalIgnoreCase))
                {
                    validationErrors.Add($"Row {rowNumber}: Invalid Ecode '{record.Ecode}'.");
                    rowNumber++;
                    continue;
                }

                // Check for duplicates in Excel file
                if (!seenKeys.Add((record.Ecode, record.ShiftName)))
                {
                    validationErrors.Add($"Row {rowNumber}: Duplicate Ecode '{record.Ecode}' and ShiftName '{record.ShiftName}' in file.");
                    rowNumber++;
                    continue;
                }

                records.Add(record);
                rowNumber++;
            }

            return (records, validationErrors);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Excel file: {FileName}", file.FileName);
            validationErrors.Add($"Error processing Excel file: {ex.Message}");
            return (records, validationErrors);
        }
    }

    private static (ShiftMapRecord? Record, string? Error) ParseRow(IXLRow row, int rowNumber, string createdBy, HashSet<string> allowedShiftNames)
    {
        try
        {
            var ecode = GetCellValue(row.Cell(1));
            var shiftName = GetCellValue(row.Cell(2));

            if (ecode == "NA")
                return (null, $"Row {rowNumber}: Ecode is required.");
            if (string.IsNullOrWhiteSpace(shiftName) || shiftName == "NA")
                return (null, $"Row {rowNumber}: ShiftName is required.");

            if (!allowedShiftNames.Contains(shiftName))
                return (null, $"Row {rowNumber}: Invalid ShiftName '{shiftName}'. Must be one of: {string.Join(", ", allowedShiftNames)}.");

            return (new ShiftMapRecord
            {
                Ecode = ecode,
                ShiftName = shiftName
            }, null);
        }
        catch (Exception ex)
        {
            return (null, $"Row {rowNumber}: Error parsing row - {ex.Message}");
        }
    }

    private static string GetCellValue(IXLCell cell) =>
        string.IsNullOrWhiteSpace(cell.GetValue<string>()) ? "NA" : cell.GetValue<string>().Trim();

    public async Task<EmployeeShiftAndHistoryResponse> GetEmployeeShiftAndHistoryAsync(int? employeeId = null, string? ecode = null)
    {
        try
        {
            if (employeeId == null && (string.IsNullOrWhiteSpace(ecode)))
            {
                throw new ArgumentException("Either employeeId or ecode must be provided.");
            }

            using var connection = _context.Database.GetDbConnection();
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = "dbo.usp_GetEmployeeShiftAndHistory";
            command.CommandType = CommandType.StoredProcedure;

            // Add parameters
            command.Parameters.Add(new SqlParameter("@EmployeeId", (object)employeeId ?? DBNull.Value));
            command.Parameters.Add(new SqlParameter("@Ecode", (object)ecode ?? DBNull.Value));

            using var reader = await command.ExecuteReaderAsync();

            var response = new EmployeeShiftAndHistoryResponse
            {
                EmployeeInfo = null,
                ShiftHistory = new List<ShiftHistoryItem>()
            };

            // First result set: Employee + Reporting Head + Current Shift
            if (await reader.ReadAsync())
            {
                response.EmployeeInfo = new EmployeeShiftInfo
                {
                    EmployeeId = reader.GetInt64("EmployeeId"),
                    Ecode = SafeGetString(reader, "Ecode"),
                    FirstName = SafeGetString(reader, "FirstName"),
                    LastName = SafeGetString(reader, "LastName"),
                    FullName = SafeGetString(reader, "FullName"),
                    ReportHeadEcode = SafeGetString(reader, "ReportHeadEcode"),
                    ReportHeadEmployeeId = SafeGetInt32Nullable(reader, "ReportHeadEmployeeId"),
                    ReportHeadFullName = SafeGetString(reader, "ReportHeadFullName"),
                    CurrentShiftId = SafeGetInt32Nullable(reader, "CurrentShiftId"),
                    CurrentShift = ReadShiftMasterFromReader(reader)
                };
            }

            // Second result set: Shift History + Shift Details
            if (await reader.NextResultAsync())
            {
                while (await reader.ReadAsync())
                {
                    var historyItem = new ShiftHistoryItem
                    {
                        HistoryId = reader.GetInt64("HistoryId"),
                        EmployeeId = reader.GetInt64("EmployeeId"),
                        ShiftId = SafeGetInt32Nullable(reader, "ShiftId"),
                        EffectiveFrom = SafeGetDateTimeNullable(reader, "EffectiveFrom"),
                        EffectiveTo = SafeGetDateTimeNullable(reader, "EffectiveTo"),
                        AssignedOn = SafeGetDateTimeNullable(reader, "AssignedOn"),
                        AssignedBy = SafeGetString(reader, "AssignedBy"),
                        Remarks = SafeGetString(reader, "Remarks"),
                        AppliedOn = SafeGetDateTimeNullable(reader, "AppliedOn"),
                        ShiftStatus = SafeGetString(reader, "ShiftStatus"),
                        ShiftDetails = ReadShiftMasterFromReader(reader)
                    };
                    response.ShiftHistory.Add(historyItem);
                }
            }

            await connection.CloseAsync();
            return response;
        }
        catch (SqlException ex) when (ex.Number >= 50000 && ex.Number <= 50099)
        {
            // User-defined error from stored procedure
            _logger.LogWarning("Stored procedure error: {Message}", ex.Message);
            throw new InvalidOperationException(ex.Message, ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving employee shift and history for employeeId: {EmployeeId}, ecode: {Ecode}", employeeId, ecode);
            throw;
        }
    }

    public async Task<(bool Success, string Message)> AssignEmployeeShiftAsync(AssignEmployeeShiftRequest request)
    {
        try
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            using var connection = _context.Database.GetDbConnection();
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = "dbo.usp_AssignEmployeeShift";
            command.CommandType = CommandType.StoredProcedure;

            // Add parameters
            command.Parameters.Add(new SqlParameter("@EmployeeId", request.EmployeeId));
            command.Parameters.Add(new SqlParameter("@ShiftId", request.ShiftId));
            command.Parameters.Add(new SqlParameter("@EffectiveFrom", SqlDbType.Date) { Value = request.EffectiveFrom.Date });
            command.Parameters.Add(new SqlParameter("@AssignedBy", (object)request.AssignedBy ?? DBNull.Value));
            command.Parameters.Add(new SqlParameter("@Remarks", (object)request.Remarks ?? DBNull.Value));

            await command.ExecuteNonQueryAsync();
            await connection.CloseAsync();

            return (true, "Employee shift assigned successfully.");
        }
        catch (SqlException ex) when (ex.Number >= 50000 && ex.Number <= 50099)
        {
            // User-defined error from stored procedure
            _logger.LogWarning("Stored procedure error: {Message}", ex.Message);
            return (false, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning employee shift for EmployeeId: {EmployeeId}, ShiftId: {ShiftId}", request?.EmployeeId, request?.ShiftId);
            return (false, $"An error occurred while assigning employee shift: {ex.Message}");
        }
    }

    public async Task<BulkAssignShiftResult> BulkAssignEmployeeShiftAsync(BulkAssignShiftRequest request)
    {
        var result = new BulkAssignShiftResult();

        if (request == null) throw new ArgumentNullException(nameof(request));
        if (request.ShiftId <= 0) throw new ArgumentException("ShiftId is required.");
        if (request.EffectiveFrom == default) throw new ArgumentException("EffectiveFrom is required.");

        var ecodes = ParseEcodes(request.EcodesCsv, request.EcodeExcel);
        if (ecodes.Count == 0)
            throw new ArgumentException("No ecodes provided (CSV and Excel were both empty).");

        result.TotalSubmitted = ecodes.Count;

        // Resolve ecodes -> (EmployeeId, current ShiftID)
        var found = await _context.tblEmployees
            .AsNoTracking()
            .Where(e => ecodes.Contains(e.Ecode))
            .Select(e => new { e.EmployeeId, e.Ecode, e.ShiftID })
            .ToListAsync();

        var foundEcodes = found.Select(f => f.Ecode).ToHashSet(StringComparer.OrdinalIgnoreCase);
        result.NotFoundEcodes = ecodes.Where(e => !foundEcodes.Contains(e)).ToList();

        using var connection = (SqlConnection)_context.Database.GetDbConnection();
        await connection.OpenAsync();

        foreach (var emp in found)
        {
            if (emp.ShiftID == request.ShiftId)
            {
                result.AlreadyOnShift++;
                continue;
            }

            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = "dbo.usp_AssignEmployeeShift";
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.Add(new SqlParameter("@EmployeeId", emp.EmployeeId));
                command.Parameters.Add(new SqlParameter("@ShiftId", request.ShiftId));
                command.Parameters.Add(new SqlParameter("@EffectiveFrom", SqlDbType.Date) { Value = request.EffectiveFrom.Date });
                command.Parameters.Add(new SqlParameter("@AssignedBy", (object?)request.AssignedBy ?? DBNull.Value));
                command.Parameters.Add(new SqlParameter("@Remarks", (object?)request.Remarks ?? DBNull.Value));

                await command.ExecuteNonQueryAsync();
                result.Processed++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Bulk shift assign failed for {Ecode}", emp.Ecode);
                result.Errors.Add(new BulkAssignShiftError { Ecode = emp.Ecode, Message = ex.Message });
            }
        }

        return result;
    }

    private static List<string> ParseEcodes(string? csv, IFormFile? excel)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(csv))
        {
            foreach (var token in csv.Split(new[] { ',', '\n', '\r', ';', '\t', ' ' },
                                            StringSplitOptions.RemoveEmptyEntries))
            {
                var t = token.Trim();
                if (t.Length > 0) set.Add(t);
            }
        }

        if (excel != null && excel.Length > 0)
        {
            using var stream = excel.OpenReadStream();
            using var wb = new XLWorkbook(stream);
            var ws = wb.Worksheets.First();

            // Try to find an "Ecode" header in row 1; fall back to first non-empty column.
            int ecodeCol = 1;
            var lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 1;
            for (int c = 1; c <= lastCol; c++)
            {
                var header = ws.Cell(1, c).GetString().Trim();
                if (string.Equals(header, "Ecode", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(header, "EmpCode", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(header, "Employee Code", StringComparison.OrdinalIgnoreCase))
                {
                    ecodeCol = c;
                    break;
                }
            }

            var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
            for (int r = 2; r <= lastRow; r++)
            {
                var val = ws.Cell(r, ecodeCol).GetString().Trim();
                if (val.Length > 0) set.Add(val);
            }
        }

        return set.ToList();
    }

    public async Task<(bool Success, string Message)> ApplyScheduledShiftsAsync()
    {
        try
        {
            using var connection = _context.Database.GetDbConnection();
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = "dbo.usp_ApplyScheduledShifts";
            command.CommandType = CommandType.StoredProcedure;

            await command.ExecuteNonQueryAsync();
            await connection.CloseAsync();

            _logger.LogInformation("Scheduled shifts applied successfully at {TimeUtc}", DateTime.UtcNow);
            return (true, "Scheduled shifts applied successfully.");
        }
        catch (SqlException ex) when (ex.Number >= 50000 && ex.Number <= 50099)
        {
            // User-defined error from stored procedure
            _logger.LogError(ex, "Stored procedure error while applying scheduled shifts: {Message}", ex.Message);
            return (false, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying scheduled shifts");
            return (false, $"An error occurred while applying scheduled shifts: {ex.Message}");
        }
    }

    private ShiftMasterDTO ReadShiftMasterFromReader(DbDataReader reader)
    {
        try
        {
            // Check if ShiftID column exists (indicates shift data is present)
            if (!HasColumn(reader, "ShiftID"))
            {
                return null;
            }

            var shiftIdOrdinal = reader.GetOrdinal("ShiftID");
            if (reader.IsDBNull(shiftIdOrdinal))
            {
                return null;
            }

            return new ShiftMasterDTO
            {
                ShiftID = reader.GetInt32("ShiftID"),
                ShiftName = SafeGetString(reader, "ShiftName"),
                StartTime = SafeGetTimeSpanNullable(reader, "StartTime"),
                EndTime = SafeGetTimeSpanNullable(reader, "EndTime"),
                IsActive = SafeGetBooleanNullable(reader, "IsActive"),
                CreatedBy = SafeGetString(reader, "CreatedBy"),
                CreatedOn = SafeGetDateTimeNullable(reader, "CreatedOn"),
                LastUpdatedOn = SafeGetDateTimeNullable(reader, "LastUpdatedOn"),
                LastUpdatedBy = SafeGetString(reader, "LastUpdatedBy")
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error reading shift master data from reader");
            return null;
        }
    }

    private static bool HasColumn(DbDataReader reader, string columnName)
    {
        try
        {
            reader.GetOrdinal(columnName);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string SafeGetString(DbDataReader reader, string columnName)
    {
        try
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
        }
        catch
        {
            return null;
        }
    }

    private static int? SafeGetInt32Nullable(DbDataReader reader, string columnName)
    {
        try
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
        }
        catch
        {
            return null;
        }
    }

    private static DateTime? SafeGetDateTimeNullable(DbDataReader reader, string columnName)
    {
        try
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
        }
        catch
        {
            return null;
        }
    }

    private static TimeSpan? SafeGetTimeSpanNullable(DbDataReader reader, string columnName)
    {
        try
        {
            var ordinal = reader.GetOrdinal(columnName);
            if (reader.IsDBNull(ordinal))
                return null;
            
            var value = reader.GetValue(ordinal);
            if (value is TimeSpan timeSpan)
                return timeSpan;
            
            // Try parsing if it's a string
            if (value is string strValue && TimeSpan.TryParse(strValue, out var parsed))
                return parsed;
            
            return null;
        }
        catch
        {
            return null;
        }
    }

    private static bool? SafeGetBooleanNullable(DbDataReader reader, string columnName)
    {
        try
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : reader.GetBoolean(ordinal);
        }
        catch
        {
            return null;
        }
    }
}
  

