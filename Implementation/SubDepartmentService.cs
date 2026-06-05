using System.Data;
using System.Net;
using ClosedXML.Excel;
using HRMSAPI.Data;
using HRMSAPI.DTO;
using HRMSAPI.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Roomsy.DTOS.GenericsResponses;

namespace HRMSAPI.Implementation
{
    // Manages a 3-level sub-department hierarchy under a department.
    // Mirrors DepartmentService (raw SQL via BaseService helpers). Only writes to
    // the NEW dbo.tblSubDepartment table; tblDepartment is read-only here.
    public class SubDepartmentService : BaseService, ISubDepartmentService
    {
        private readonly IConfiguration _configuration;
        private readonly HRMSContext _context;
        private readonly ILogger<SubDepartmentService> _logger;

        public SubDepartmentService(HRMSContext context, IConfiguration configuration, ILogger<SubDepartmentService> logger) : base(context)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<FetchAndResponse> GetAllAsync(int departmentId, int? parentSubDepartmentId, int depthLevel, bool onlyInactive = false, string? searchTerm = null)
        {
            try
            {
                if (departmentId <= 0)
                    return BuildFetchErrorResponse("departmentId is required.", HttpStatusCode.BadRequest);
                if (depthLevel < 1 || depthLevel > 3)
                    return BuildFetchErrorResponse("depthLevel must be 1, 2, or 3.", HttpStatusCode.BadRequest);

                var connStr = _configuration.GetConnectionString("DefaultConnection");
                var rows = new List<SubDepartmentResponseDto>();

                var sql = @"
                    SELECT SubDepartmentId, SubDepartmentName, SubDepartmentCode, DepartmentId,
                           ParentSubDepartmentId, DepthLevel,
                           ISNULL(isActive, 1)  AS IsActive,
                           ISNULL(isDeleted, 0) AS IsDeleted,
                           CreatedOn, CreatedBy, UpdatedOn, UpdatedBy
                    FROM dbo.tblSubDepartment
                    WHERE ISNULL(isDeleted, 0) = 0
                      AND DepartmentId = @deptId
                      AND DepthLevel   = @level
                      AND (
                            (@parent IS NULL AND ParentSubDepartmentId IS NULL)
                         OR (ParentSubDepartmentId = @parent)
                          )
                      AND (
                            (@onlyInactive = 0 AND ISNULL(isActive, 1) = 1)
                         OR (@onlyInactive = 1 AND ISNULL(isActive, 1) = 0)
                          )
                      AND (@search IS NULL OR SubDepartmentName LIKE @search OR SubDepartmentCode LIKE @search)
                    ORDER BY SubDepartmentName;";

                await using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@deptId", departmentId);
                cmd.Parameters.AddWithValue("@level", depthLevel);
                cmd.Parameters.AddWithValue("@parent", (object?)parentSubDepartmentId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@onlyInactive", onlyInactive ? 1 : 0);
                cmd.Parameters.AddWithValue("@search", string.IsNullOrWhiteSpace(searchTerm) ? (object)DBNull.Value : "%" + searchTerm.Trim() + "%");

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    rows.Add(new SubDepartmentResponseDto
                    {
                        SubDepartmentId       = reader.GetInt32(reader.GetOrdinal("SubDepartmentId")),
                        SubDepartmentName     = reader["SubDepartmentName"] as string,
                        SubDepartmentCode     = reader["SubDepartmentCode"] as string,
                        DepartmentId          = reader.GetInt32(reader.GetOrdinal("DepartmentId")),
                        ParentSubDepartmentId = reader.IsDBNull(reader.GetOrdinal("ParentSubDepartmentId")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("ParentSubDepartmentId")),
                        DepthLevel            = Convert.ToInt32(reader["DepthLevel"]),
                        IsActive              = !reader.IsDBNull(reader.GetOrdinal("IsActive"))  && reader.GetBoolean(reader.GetOrdinal("IsActive")),
                        IsDeleted             = !reader.IsDBNull(reader.GetOrdinal("IsDeleted")) && reader.GetBoolean(reader.GetOrdinal("IsDeleted")),
                        CreatedOn             = reader.IsDBNull(reader.GetOrdinal("CreatedOn")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("CreatedOn")),
                        CreatedBy             = reader.IsDBNull(reader.GetOrdinal("CreatedBy")) ? (long?)null     : reader.GetInt64(reader.GetOrdinal("CreatedBy")),
                        UpdatedOn             = reader.IsDBNull(reader.GetOrdinal("UpdatedOn")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("UpdatedOn")),
                        UpdatedBy             = reader.IsDBNull(reader.GetOrdinal("UpdatedBy")) ? (long?)null     : reader.GetInt64(reader.GetOrdinal("UpdatedBy")),
                    });
                }

                return BuildFetchSuccessResponse("Sub-departments fetched", rows);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SubDepartmentService.GetAllAsync error");
                return BuildFetchErrorResponse(ex.Message, HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ExecuteAndReponse> UpsertAsync(SubDepartmentUpsertDto dto, long currentEmployeeId)
        {
            try
            {
                if (dto == null || string.IsNullOrWhiteSpace(dto.SubDepartmentName))
                    return BuildExecuteErrorResponse("Sub-department name is required.", HttpStatusCode.BadRequest);
                if (dto.DepartmentId <= 0)
                    return BuildExecuteErrorResponse("DepartmentId is required.", HttpStatusCode.BadRequest);
                if (dto.DepthLevel < 1 || dto.DepthLevel > 3)
                    return BuildExecuteErrorResponse("Sub-departments are limited to 3 levels.", HttpStatusCode.BadRequest);

                var name = dto.SubDepartmentName.Trim();
                var code = string.IsNullOrWhiteSpace(dto.SubDepartmentCode) ? null : dto.SubDepartmentCode.Trim();
                var connStr = _configuration.GetConnectionString("DefaultConnection");

                await using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();

                int departmentId = dto.DepartmentId;
                int? parentId = dto.ParentSubDepartmentId;

                if (dto.DepthLevel == 1)
                {
                    parentId = null; // level 1 hangs directly off the department
                }
                else
                {
                    if (parentId is null or 0)
                        return BuildExecuteErrorResponse("A parent sub-department is required for level 2 and 3.", HttpStatusCode.BadRequest);

                    // Validate parent: exists, not deleted, and exactly one level above.
                    await using var pcmd = new SqlCommand(
                        @"SELECT TOP 1 DepartmentId, DepthLevel FROM dbo.tblSubDepartment
                          WHERE SubDepartmentId = @pid AND ISNULL(isDeleted, 0) = 0;", conn);
                    pcmd.Parameters.AddWithValue("@pid", parentId.Value);
                    await using var prdr = await pcmd.ExecuteReaderAsync();
                    if (!await prdr.ReadAsync())
                        return BuildExecuteErrorResponse("Parent sub-department not found.", HttpStatusCode.BadRequest);
                    var parentDept = prdr.GetInt32(0);
                    var parentLevel = Convert.ToInt32(prdr.GetValue(1));
                    await prdr.CloseAsync();

                    if (parentLevel + 1 != dto.DepthLevel)
                        return BuildExecuteErrorResponse("Level does not match the chosen parent.", HttpStatusCode.BadRequest);
                    departmentId = parentDept; // carry the root department from the parent
                }

                // Uniqueness: case-insensitive name within the same (department, parent) scope.
                await using (var dup = new SqlCommand(
                    @"SELECT TOP 1 SubDepartmentId FROM dbo.tblSubDepartment
                      WHERE ISNULL(isDeleted, 0) = 0
                        AND DepartmentId = @dept
                        AND ((@parent IS NULL AND ParentSubDepartmentId IS NULL) OR ParentSubDepartmentId = @parent)
                        AND SubDepartmentName = @name COLLATE Latin1_General_CI_AS
                        AND (@id IS NULL OR SubDepartmentId <> @id);", conn))
                {
                    dup.Parameters.AddWithValue("@dept", departmentId);
                    dup.Parameters.AddWithValue("@parent", (object?)parentId ?? DBNull.Value);
                    dup.Parameters.AddWithValue("@name", name);
                    dup.Parameters.AddWithValue("@id", (object?)dto.SubDepartmentId ?? DBNull.Value);
                    var hit = await dup.ExecuteScalarAsync();
                    if (hit != null && hit != DBNull.Value)
                        return BuildExecuteErrorResponse($"A sub-department named '{name}' already exists under this parent.", HttpStatusCode.Conflict);
                }

                if (dto.SubDepartmentId is null or 0)
                {
                    await using var ins = new SqlCommand(
                        @"INSERT INTO dbo.tblSubDepartment
                          (SubDepartmentName, SubDepartmentCode, DepartmentId, ParentSubDepartmentId, DepthLevel, CreatedOn, CreatedBy, isActive, isDeleted)
                          VALUES (@name, @code, @dept, @parent, @level, SYSUTCDATETIME(), @by, 1, 0);
                          SELECT CAST(SCOPE_IDENTITY() AS INT);", conn);
                    ins.Parameters.AddWithValue("@name", name);
                    ins.Parameters.AddWithValue("@code", (object?)code ?? DBNull.Value);
                    ins.Parameters.AddWithValue("@dept", departmentId);
                    ins.Parameters.AddWithValue("@parent", (object?)parentId ?? DBNull.Value);
                    ins.Parameters.AddWithValue("@level", dto.DepthLevel);
                    ins.Parameters.AddWithValue("@by", currentEmployeeId);
                    var newId = (int?)await ins.ExecuteScalarAsync();
                    return BuildExecuteSuccessResponse($"Sub-department '{name}' created (id {newId}).");
                }
                else
                {
                    await using var upd = new SqlCommand(
                        @"UPDATE dbo.tblSubDepartment
                          SET SubDepartmentName = @name,
                              SubDepartmentCode = @code,
                              UpdatedOn = SYSUTCDATETIME(),
                              UpdatedBy = @by
                          WHERE SubDepartmentId = @id AND ISNULL(isDeleted, 0) = 0;", conn);
                    upd.Parameters.AddWithValue("@name", name);
                    upd.Parameters.AddWithValue("@code", (object?)code ?? DBNull.Value);
                    upd.Parameters.AddWithValue("@by", currentEmployeeId);
                    upd.Parameters.AddWithValue("@id", dto.SubDepartmentId.Value);
                    var rows = await upd.ExecuteNonQueryAsync();
                    if (rows == 0)
                        return BuildExecuteErrorResponse("Sub-department not found or already deleted.", HttpStatusCode.NotFound);
                    return BuildExecuteSuccessResponse($"Sub-department '{name}' updated.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SubDepartmentService.UpsertAsync error");
                return BuildExecuteErrorResponse(ex.Message, HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ExecuteAndReponse> ToggleActiveAsync(int id, bool isActive, long currentEmployeeId)
        {
            try
            {
                if (id <= 0)
                    return BuildExecuteErrorResponse("Invalid sub-department id.", HttpStatusCode.BadRequest);

                var connStr = _configuration.GetConnectionString("DefaultConnection");
                await using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(
                    @"UPDATE dbo.tblSubDepartment
                      SET isActive = @active, UpdatedOn = SYSUTCDATETIME(), UpdatedBy = @by
                      WHERE SubDepartmentId = @id AND ISNULL(isDeleted, 0) = 0;", conn);
                cmd.Parameters.AddWithValue("@active", isActive ? 1 : 0);
                cmd.Parameters.AddWithValue("@by", currentEmployeeId);
                cmd.Parameters.AddWithValue("@id", id);
                var rows = await cmd.ExecuteNonQueryAsync();
                if (rows == 0)
                    return BuildExecuteErrorResponse("Sub-department not found.", HttpStatusCode.NotFound);
                return BuildExecuteSuccessResponse(isActive ? "Sub-department activated." : "Sub-department deactivated.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SubDepartmentService.ToggleActiveAsync error");
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

                string H(int c) => ws.Cell(1, c).GetValue<string>().Trim();
                if (!string.Equals(H(1), "DEPARTMENT NAME", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(H(2), "SUB DEPT 1", StringComparison.OrdinalIgnoreCase))
                    return BuildFetchErrorResponse(
                        "Header mismatch: expected columns 'DEPARTMENT NAME', 'SUB DEPT 1', 'SUB DEPT 2', 'SUB DEPT 3'.",
                        HttpStatusCode.BadRequest);

                var rows = ws.RowsUsed().Skip(1).ToList();
                if (rows.Count == 0)
                    return BuildFetchErrorResponse("No data rows.", HttpStatusCode.BadRequest);

                var connStr = _configuration.GetConnectionString("DefaultConnection");
                await using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();

                int created = 0, skipped = 0;
                var errors = new List<string>();

                for (int r = 0; r < rows.Count; r++)
                {
                    var rowNum = r + 2;
                    var deptName = rows[r].Cell(1).GetValue<string>()?.Trim();
                    var s1 = rows[r].Cell(2).GetValue<string>()?.Trim();
                    var s2 = rows[r].Cell(3).GetValue<string>()?.Trim();
                    var s3 = rows[r].Cell(4).GetValue<string>()?.Trim();

                    if (string.IsNullOrWhiteSpace(deptName) || string.IsNullOrWhiteSpace(s1))
                    {
                        skipped++;
                        errors.Add($"Row {rowNum}: Department Name and Sub Dept 1 are required; skipped.");
                        continue;
                    }

                    // Resolve the (existing) department by name — read-only.
                    var deptId = await ResolveDepartmentIdAsync(conn, deptName);
                    if (deptId is null)
                    {
                        skipped++;
                        errors.Add($"Row {rowNum}: department '{deptName}' not found; skipped.");
                        continue;
                    }

                    try
                    {
                        var (l1Id, l1New) = await GetOrCreateAsync(conn, deptId.Value, null, 1, s1, currentEmployeeId);
                        if (l1New) created++;

                        if (!string.IsNullOrWhiteSpace(s2))
                        {
                            var (l2Id, l2New) = await GetOrCreateAsync(conn, deptId.Value, l1Id, 2, s2, currentEmployeeId);
                            if (l2New) created++;

                            if (!string.IsNullOrWhiteSpace(s3))
                            {
                                var (_, l3New) = await GetOrCreateAsync(conn, deptId.Value, l2Id, 3, s3, currentEmployeeId);
                                if (l3New) created++;
                            }
                        }
                        else if (!string.IsNullOrWhiteSpace(s3))
                        {
                            errors.Add($"Row {rowNum}: Sub Dept 3 given without Sub Dept 2; level 3 ignored.");
                        }
                    }
                    catch (Exception exRow)
                    {
                        errors.Add($"Row {rowNum}: {exRow.Message}");
                    }
                }

                return BuildFetchSuccessResponse(
                    $"Sub-departments upload: {created} created, {skipped} row(s) skipped.",
                    new { created, skipped, errors });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SubDepartmentService.BulkUploadAsync error");
                return BuildFetchErrorResponse(ex.Message, HttpStatusCode.InternalServerError);
            }
        }

        // --- helpers ---
        private static async Task<int?> ResolveDepartmentIdAsync(SqlConnection conn, string deptName)
        {
            await using var cmd = new SqlCommand(
                @"SELECT TOP 1 DepartmentId FROM dbo.tblDepartment
                  WHERE ISNULL(isDeleted, 0) = 0
                    AND DepartmentName = @name COLLATE Latin1_General_CI_AS;", conn);
            cmd.Parameters.AddWithValue("@name", deptName);
            return await cmd.ExecuteScalarAsync() as int?;
        }

        // Returns (id, wasCreated) for a sub-dept node identified by (department, parent, level, name).
        private static async Task<(int id, bool created)> GetOrCreateAsync(
            SqlConnection conn, int departmentId, int? parentId, int level, string name, long by)
        {
            name = name.Trim();
            await using (var find = new SqlCommand(
                @"SELECT TOP 1 SubDepartmentId FROM dbo.tblSubDepartment
                  WHERE ISNULL(isDeleted, 0) = 0
                    AND DepartmentId = @dept
                    AND ((@parent IS NULL AND ParentSubDepartmentId IS NULL) OR ParentSubDepartmentId = @parent)
                    AND SubDepartmentName = @name COLLATE Latin1_General_CI_AS;", conn))
            {
                find.Parameters.AddWithValue("@dept", departmentId);
                find.Parameters.AddWithValue("@parent", (object?)parentId ?? DBNull.Value);
                find.Parameters.AddWithValue("@name", name);
                var existing = await find.ExecuteScalarAsync() as int?;
                if (existing.HasValue) return (existing.Value, false);
            }

            await using var ins = new SqlCommand(
                @"INSERT INTO dbo.tblSubDepartment
                  (SubDepartmentName, DepartmentId, ParentSubDepartmentId, DepthLevel, CreatedOn, CreatedBy, isActive, isDeleted)
                  VALUES (@name, @dept, @parent, @level, SYSUTCDATETIME(), @by, 1, 0);
                  SELECT CAST(SCOPE_IDENTITY() AS INT);", conn);
            ins.Parameters.AddWithValue("@name", name);
            ins.Parameters.AddWithValue("@dept", departmentId);
            ins.Parameters.AddWithValue("@parent", (object?)parentId ?? DBNull.Value);
            ins.Parameters.AddWithValue("@level", level);
            ins.Parameters.AddWithValue("@by", by);
            var newId = (int)await ins.ExecuteScalarAsync();
            return (newId, true);
        }
    }
}
