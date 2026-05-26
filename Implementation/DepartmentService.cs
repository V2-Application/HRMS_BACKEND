using System.Data;
using System.Net;
using ClosedXML.Excel;
using HRMSAPI.Data;
using HRMSAPI.DTO;
using HRMSAPI.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Roomsy.DTOS.GenericsResponses;

namespace HRMSAPI.Implementation
{
    public class DepartmentService : BaseService, IDepartmentService
    {
        private readonly IConfiguration _configuration;
        private readonly HRMSContext _context;
        private readonly ILogger<DepartmentService> _logger;

        public DepartmentService(HRMSContext context, IConfiguration configuration, ILogger<DepartmentService> logger) : base(context)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<FetchAndResponse> GetAllAsync(bool onlyInactive = false, string? searchTerm = null)
        {
            try
            {
                var connStr = _configuration.GetConnectionString("DefaultConnection");
                var rows = new List<DepartmentResponseDto>();

                // onlyInactive=false → show only active; true → show only inactive.
                // Soft-deleted rows are always excluded.
                var sql = @"
                    SELECT DepartmentId, DepartmentName, DepartmentCode,
                           ISNULL(isActive, 1)  AS IsActive,
                           ISNULL(isDeleted, 0) AS IsDeleted,
                           CreatedOn, CreatedBy, UpdatedOn, UpdatedBy
                    FROM dbo.tblDepartment
                    WHERE ISNULL(isDeleted, 0) = 0
                      AND (
                            (@onlyInactive = 0 AND ISNULL(isActive, 1) = 1)
                         OR (@onlyInactive = 1 AND ISNULL(isActive, 1) = 0)
                          )
                      AND (@search IS NULL OR DepartmentName LIKE @search OR DepartmentCode LIKE @search)
                    ORDER BY DepartmentName;";

                await using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@onlyInactive", onlyInactive ? 1 : 0);
                cmd.Parameters.AddWithValue("@search", string.IsNullOrWhiteSpace(searchTerm) ? (object)DBNull.Value : "%" + searchTerm.Trim() + "%");

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    rows.Add(new DepartmentResponseDto
                    {
                        DepartmentId   = reader.GetInt32(reader.GetOrdinal("DepartmentId")),
                        DepartmentName = reader["DepartmentName"] as string,
                        DepartmentCode = reader["DepartmentCode"] as string,
                        IsActive       = !reader.IsDBNull(reader.GetOrdinal("IsActive"))  && reader.GetBoolean(reader.GetOrdinal("IsActive")),
                        IsDeleted      = !reader.IsDBNull(reader.GetOrdinal("IsDeleted")) && reader.GetBoolean(reader.GetOrdinal("IsDeleted")),
                        CreatedOn      = reader.IsDBNull(reader.GetOrdinal("CreatedOn")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("CreatedOn")),
                        CreatedBy      = reader.IsDBNull(reader.GetOrdinal("CreatedBy")) ? (long?)null     : reader.GetInt64(reader.GetOrdinal("CreatedBy")),
                        UpdatedOn      = reader.IsDBNull(reader.GetOrdinal("UpdatedOn")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("UpdatedOn")),
                        UpdatedBy      = reader.IsDBNull(reader.GetOrdinal("UpdatedBy")) ? (long?)null     : reader.GetInt64(reader.GetOrdinal("UpdatedBy")),
                    });
                }

                return BuildFetchSuccessResponse("Departments fetched", rows);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DepartmentService.GetAllAsync error");
                return BuildFetchErrorResponse(ex.Message, HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ExecuteAndReponse> UpsertAsync(DepartmentUpsertDto dto, long currentEmployeeId)
        {
            try
            {
                if (dto == null || string.IsNullOrWhiteSpace(dto.DepartmentName))
                    return BuildExecuteErrorResponse("Department name is required.", HttpStatusCode.BadRequest);

                var name = dto.DepartmentName.Trim();
                var connStr = _configuration.GetConnectionString("DefaultConnection");

                await using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();

                // Uniqueness: case-insensitive name within not-deleted set, excluding self on update
                await using (var dup = new SqlCommand(
                    @"SELECT TOP 1 DepartmentId FROM dbo.tblDepartment
                      WHERE ISNULL(isDeleted, 0) = 0
                        AND DepartmentName = @name COLLATE Latin1_General_CI_AS
                        AND (@id IS NULL OR DepartmentId <> @id);", conn))
                {
                    dup.Parameters.AddWithValue("@name", name);
                    dup.Parameters.AddWithValue("@id", (object?)dto.DepartmentId ?? DBNull.Value);
                    var hit = await dup.ExecuteScalarAsync();
                    if (hit != null && hit != DBNull.Value)
                        return BuildExecuteErrorResponse($"A department named '{name}' already exists.", HttpStatusCode.Conflict);
                }

                if (dto.DepartmentId is null or 0)
                {
                    await using var ins = new SqlCommand(
                        @"INSERT INTO dbo.tblDepartment
                          (DepartmentName, CreatedOn, CreatedBy, isActive, isDeleted)
                          VALUES (@name, SYSUTCDATETIME(), @by, 1, 0);
                          SELECT CAST(SCOPE_IDENTITY() AS INT);", conn);
                    ins.Parameters.AddWithValue("@name", name);
                    ins.Parameters.AddWithValue("@by", currentEmployeeId);
                    var newId = (int?)await ins.ExecuteScalarAsync();
                    return BuildExecuteSuccessResponse($"Department '{name}' created (id {newId}).");
                }
                else
                {
                    await using var upd = new SqlCommand(
                        @"UPDATE dbo.tblDepartment
                          SET DepartmentName = @name,
                              UpdatedOn      = SYSUTCDATETIME(),
                              UpdatedBy      = @by
                          WHERE DepartmentId = @id AND ISNULL(isDeleted, 0) = 0;", conn);
                    upd.Parameters.AddWithValue("@name", name);
                    upd.Parameters.AddWithValue("@by", currentEmployeeId);
                    upd.Parameters.AddWithValue("@id", dto.DepartmentId.Value);
                    var rows = await upd.ExecuteNonQueryAsync();
                    if (rows == 0)
                        return BuildExecuteErrorResponse("Department not found or already deleted.", HttpStatusCode.NotFound);
                    return BuildExecuteSuccessResponse($"Department '{name}' updated.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DepartmentService.UpsertAsync error");
                return BuildExecuteErrorResponse(ex.Message, HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ExecuteAndReponse> ToggleActiveAsync(int id, bool isActive, long currentEmployeeId)
        {
            try
            {
                if (id <= 0)
                    return BuildExecuteErrorResponse("Invalid department id.", HttpStatusCode.BadRequest);

                var connStr = _configuration.GetConnectionString("DefaultConnection");
                await using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(
                    @"UPDATE dbo.tblDepartment
                      SET isActive  = @active,
                          UpdatedOn = SYSUTCDATETIME(),
                          UpdatedBy = @by
                      WHERE DepartmentId = @id AND ISNULL(isDeleted, 0) = 0;", conn);
                cmd.Parameters.AddWithValue("@active", isActive ? 1 : 0);
                cmd.Parameters.AddWithValue("@by", currentEmployeeId);
                cmd.Parameters.AddWithValue("@id", id);
                var rows = await cmd.ExecuteNonQueryAsync();
                if (rows == 0)
                    return BuildExecuteErrorResponse("Department not found.", HttpStatusCode.NotFound);
                return BuildExecuteSuccessResponse(isActive ? "Department activated." : "Department deactivated.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DepartmentService.ToggleActiveAsync error");
                return BuildExecuteErrorResponse(ex.Message, HttpStatusCode.InternalServerError);
            }
        }

        public async Task<FetchAndResponse> BulkUploadAsync(IFormFile file, long currentEmployeeId)
        {
            if (file == null || file.Length == 0)
                return BuildFetchErrorResponse("No file uploaded.", HttpStatusCode.BadRequest);

            try
            {
                using var stream = file.OpenReadStream();
                using var workbook = new XLWorkbook(stream);
                var ws = workbook.Worksheet(1);

                var header = ws.Cell(1, 1).GetValue<string>().Trim();
                if (!string.Equals(header, "DEPARTMENT NAME", StringComparison.OrdinalIgnoreCase))
                    return BuildFetchErrorResponse(
                        $"Header mismatch at column 1: expected 'DEPARTMENT NAME', got '{header}'.",
                        HttpStatusCode.BadRequest);

                var rows = ws.RowsUsed().Skip(1).ToList();
                if (rows.Count == 0)
                    return BuildFetchErrorResponse("No data rows.", HttpStatusCode.BadRequest);

                var connStr = _configuration.GetConnectionString("DefaultConnection");
                await using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();

                int inserted = 0, updated = 0, skipped = 0;
                var errors = new List<string>();
                var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                for (int r = 0; r < rows.Count; r++)
                {
                    var rowNum = r + 2;
                    var name = rows[r].Cell(1).GetValue<string>()?.Trim();

                    if (string.IsNullOrWhiteSpace(name))
                    {
                        skipped++;
                        errors.Add($"Row {rowNum}: blank Department Name; skipped.");
                        continue;
                    }
                    if (!seenNames.Add(name))
                    {
                        skipped++;
                        errors.Add($"Row {rowNum}: duplicate '{name}' within file; skipped.");
                        continue;
                    }

                    await using var existing = new SqlCommand(
                        @"SELECT TOP 1 DepartmentId FROM dbo.tblDepartment
                          WHERE ISNULL(isDeleted, 0) = 0
                            AND DepartmentName = @name COLLATE Latin1_General_CI_AS;", conn);
                    existing.Parameters.AddWithValue("@name", name);
                    var existingId = await existing.ExecuteScalarAsync() as int?;

                    if (existingId.HasValue)
                    {
                        skipped++;
                        errors.Add($"Row {rowNum}: '{name}' already exists (id {existingId.Value}); skipped.");
                        continue;
                    }

                    await using var ins = new SqlCommand(
                        @"INSERT INTO dbo.tblDepartment
                          (DepartmentName, CreatedOn, CreatedBy, isActive, isDeleted)
                          VALUES (@name, SYSUTCDATETIME(), @by, 1, 0);", conn);
                    ins.Parameters.AddWithValue("@name", name);
                    ins.Parameters.AddWithValue("@by", currentEmployeeId);
                    await ins.ExecuteNonQueryAsync();
                    inserted++;
                }

                return BuildFetchSuccessResponse(
                    $"Departments upload: {inserted} inserted, {skipped} skipped.",
                    new { inserted, updated, skipped, errors });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DepartmentService.BulkUploadAsync error");
                return BuildFetchErrorResponse(ex.Message, HttpStatusCode.InternalServerError);
            }
        }
    }
}
