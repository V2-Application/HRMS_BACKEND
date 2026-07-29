using HRMSAPI.Extension;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace HRMSAPI.Controllers
{
    // Shared logic for the "Regularize Access" and "Geofence Access" admin pages.
    // Each opens a window (per ecode / STCode / global, over a date range and/or custom
    // dates) so those requests surface in the Manager & LP approval queues (OpenApprovals).
    // Self-contained ADO.NET, IT Superadmin only. DEV ONLY.
    [ApiController]
    [Authorize]
    public abstract class AccessWindowControllerBase : ControllerBase
    {
        protected readonly IConfiguration _config;
        protected AccessWindowControllerBase(IConfiguration config) { _config = config; }

        // Whitelisted table name for the concrete page (safe to interpolate).
        protected abstract string Table { get; }

        protected SqlConnection Open()
        {
            var c = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
            c.Open();
            return c;
        }

        protected bool IsItSuperAdmin()
        {
            var identity = HttpContext.User.Identity as ClaimsIdentity;
            var uc = AuthenticUserDetails.GetCurrentUserDetails(identity);
            var role = (uc?.role ?? string.Empty).Replace(" ", string.Empty);
            return string.Equals(role, "ITSuperadmin", StringComparison.OrdinalIgnoreCase);
        }

        protected string CurrentUser()
        {
            var identity = HttpContext.User.Identity as ClaimsIdentity;
            var uc = AuthenticUserDetails.GetCurrentUserDetails(identity);
            return string.IsNullOrWhiteSpace(uc?.EmployeeId) ? "System" : uc.EmployeeId;
        }

        protected IActionResult Forbidden() =>
            StatusCode(StatusCodes.Status403Forbidden, new { status = false, message = "Only IT Superadmin can manage access windows." });

        // ---- Employee search (ecode / name) ----
        [HttpGet("Employees")]
        public async Task<IActionResult> Employees([FromQuery] string? search = null)
        {
            if (!IsItSuperAdmin()) return Forbidden();
            search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
            const string sql = @"
SELECT TOP 50 e.Ecode, LTRIM(RTRIM(ISNULL(e.FirstName,'') + ' ' + ISNULL(e.LastName,''))) AS EmpName
FROM dbo.tblEmployee e
WHERE e.Ecode IS NOT NULL AND LTRIM(RTRIM(e.Ecode)) <> ''
  AND (@search IS NULL OR e.Ecode LIKE '%'+@search+'%' OR e.FirstName LIKE '%'+@search+'%' OR e.LastName LIKE '%'+@search+'%')
ORDER BY e.Ecode;";
            var rows = new List<Dictionary<string, object>>();
            await using var conn = Open();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add(new SqlParameter("@search", (object?)search ?? DBNull.Value));
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var name = r["EmpName"]?.ToString();
                rows.Add(new Dictionary<string, object> { ["ecode"] = r["Ecode"]?.ToString(), ["name"] = string.IsNullOrWhiteSpace(name) ? null : name });
            }
            return Ok(new { status = true, data = rows });
        }

        // ---- Store codes ----
        [HttpGet("Stores")]
        public async Task<IActionResult> Stores()
        {
            if (!IsItSuperAdmin()) return Forbidden();
            const string sql = @"
SELECT STCode, MAX(LocationName) AS LocationName
FROM dbo.tblLocation
WHERE ISNULL(IsDeleted,0)=0 AND STCode IS NOT NULL AND LTRIM(RTRIM(STCode))<>''
GROUP BY STCode ORDER BY STCode;";
            var rows = new List<Dictionary<string, object>>();
            await using var conn = Open();
            await using var cmd = new SqlCommand(sql, conn);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                rows.Add(new Dictionary<string, object> { ["storeCode"] = r["STCode"]?.ToString(), ["locationName"] = r["LocationName"] is DBNull ? null : r["LocationName"]?.ToString() });
            return Ok(new { status = true, data = rows });
        }

        // ---- My open dates (ANY authenticated employee) ----
        // Returns the access dates opened for the logged-in employee (their ecode / their
        // store STCode / a global row). The attendance view uses this to decide whether to
        // show the Regularize button and to restrict the date picker.
        [HttpGet("MyOpenDates")]
        public async Task<IActionResult> MyOpenDates()
        {
            var identity = HttpContext.User.Identity as ClaimsIdentity;
            var uc = AuthenticUserDetails.GetCurrentUserDetails(identity);
            if (uc == null || !long.TryParse(uc.EmployeeId, out var empId))
                return Unauthorized(new { status = false, message = "Invalid authentication." });

            string ecode = null, stcode = null;
            var dates = new List<string>();
            await using var conn = Open();

            await using (var c = new SqlCommand(
                "SELECT e.Ecode, l.STCode FROM dbo.tblEmployee e LEFT JOIN dbo.tblLocation l ON l.LocationId = e.LocationId WHERE e.EmployeeId = @id", conn))
            {
                c.Parameters.AddWithValue("@id", empId);
                await using var r = await c.ExecuteReaderAsync();
                if (await r.ReadAsync())
                {
                    ecode = r["Ecode"] is DBNull ? null : r["Ecode"]?.ToString();
                    stcode = r["STCode"] is DBNull ? null : r["STCode"]?.ToString();
                }
            }

            var sql = $@"
SELECT DISTINCT CONVERT(varchar(10), AccessDate, 23) AS d
FROM dbo.{Table}
WHERE IsActive = 1
  AND ( (Ecode = @e) OR (STCode = @s) OR (Ecode IS NULL AND STCode IS NULL) )
ORDER BY d;";
            await using (var c = new SqlCommand(sql, conn))
            {
                c.Parameters.AddWithValue("@e", (object)ecode ?? DBNull.Value);
                c.Parameters.AddWithValue("@s", (object)stcode ?? DBNull.Value);
                await using var r = await c.ExecuteReaderAsync();
                while (await r.ReadAsync()) dates.Add(r.GetString(0));
            }

            return Ok(new { status = true, ecode, data = dates });
        }

        // ---- List current active windows ----
        [HttpGet("List")]
        public async Task<IActionResult> List()
        {
            if (!IsItSuperAdmin()) return Forbidden();
            var sql = $@"
SELECT Id, Ecode, STCode, CAST(AccessDate AS date) AS AccessDate, OpenApprovals, CreatedBy, CreatedOn
FROM dbo.{Table}
WHERE IsActive = 1
ORDER BY AccessDate DESC, Id DESC;";
            var rows = new List<Dictionary<string, object?>>();
            await using var conn = Open();
            await using var cmd = new SqlCommand(sql, conn);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                rows.Add(new Dictionary<string, object?>
                {
                    ["id"] = Convert.ToInt32(r["Id"]),
                    ["ecode"] = r["Ecode"] is DBNull ? null : r["Ecode"]?.ToString(),
                    ["stCode"] = r["STCode"] is DBNull ? null : r["STCode"]?.ToString(),
                    ["target"] = r["Ecode"] is not DBNull ? $"Ecode: {r["Ecode"]}" : (r["STCode"] is not DBNull ? $"Store: {r["STCode"]}" : "ALL"),
                    ["accessDate"] = Convert.ToDateTime(r["AccessDate"]).ToString("yyyy-MM-dd"),
                    ["openApprovals"] = !(r["OpenApprovals"] is DBNull) && Convert.ToBoolean(r["OpenApprovals"]),
                    ["createdBy"] = r["CreatedBy"] is DBNull ? null : r["CreatedBy"]?.ToString(),
                    ["createdOn"] = r["CreatedOn"] is DBNull ? null : Convert.ToDateTime(r["CreatedOn"]).ToString("yyyy-MM-dd HH:mm")
                });
            }
            return Ok(new { status = true, data = rows });
        }

        public class SaveDto
        {
            public List<string> Ecodes { get; set; } = new();
            public List<string> StCodes { get; set; } = new();
            public bool ApplyAll { get; set; }
            public string? FromDate { get; set; }
            public string? ToDate { get; set; }
            public List<string> CustomDates { get; set; } = new();
            public bool OpenApprovals { get; set; }
        }

        private static DateTime? ParseDate(string? s) =>
            !string.IsNullOrWhiteSpace(s) && DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d.Date : (DateTime?)null;

        // ---- Save: expand targets x dates, upsert one row each ----
        [HttpPost("Save")]
        public async Task<IActionResult> Save([FromBody] SaveDto dto)
        {
            if (!IsItSuperAdmin()) return Forbidden();
            if (dto == null) return BadRequest(new { status = false, message = "Invalid request." });

            // resolve dates (range + custom), distinct
            var dates = new HashSet<DateTime>();
            var from = ParseDate(dto.FromDate); var to = ParseDate(dto.ToDate);
            if (from.HasValue && to.HasValue)
            {
                if (to.Value < from.Value) return BadRequest(new { status = false, message = "To date is before From date." });
                if ((to.Value - from.Value).TotalDays > 400) return BadRequest(new { status = false, message = "Date range too large (max ~400 days)." });
                for (var d = from.Value; d <= to.Value; d = d.AddDays(1)) dates.Add(d);
            }
            else if (from.HasValue) dates.Add(from.Value);
            foreach (var cd in dto.CustomDates ?? new()) { var p = ParseDate(cd); if (p.HasValue) dates.Add(p.Value); }

            if (dates.Count == 0) return BadRequest(new { status = false, message = "Pick a date range and/or custom dates." });

            // resolve targets: (Ecode, STCode) tuples; null/null = global
            var targets = new List<(string? Ecode, string? StCode)>();
            if (dto.ApplyAll) targets.Add((null, null));
            else
            {
                foreach (var e in (dto.Ecodes ?? new()).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct()) targets.Add((e, null));
                foreach (var s in (dto.StCodes ?? new()).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct()) targets.Add((null, s));
            }
            if (targets.Count == 0) return BadRequest(new { status = false, message = "Select employee(s), store(s), or 'Apply to all'." });

            var user = CurrentUser();
            int affected = 0;
            await using var conn = Open();
            await using var tx = (SqlTransaction)await conn.BeginTransactionAsync();
            try
            {
                foreach (var (ec, st) in targets)
                foreach (var day in dates)
                {
                    var upsert = $@"
IF EXISTS (SELECT 1 FROM dbo.{Table}
           WHERE AccessDate=@d
             AND ((@e IS NULL AND Ecode IS NULL) OR Ecode=@e)
             AND ((@s IS NULL AND STCode IS NULL) OR STCode=@s))
    UPDATE dbo.{Table} SET OpenApprovals=@oa, IsActive=1, UpdatedBy=@by, UpdatedOn=GETDATE()
     WHERE AccessDate=@d
       AND ((@e IS NULL AND Ecode IS NULL) OR Ecode=@e)
       AND ((@s IS NULL AND STCode IS NULL) OR STCode=@s);
ELSE
    INSERT INTO dbo.{Table} (Ecode, STCode, AccessDate, OpenApprovals, IsActive, CreatedBy, CreatedOn)
    VALUES (@e, @s, @d, @oa, 1, @by, GETDATE());";
                    await using var cmd = new SqlCommand(upsert, conn, tx);
                    cmd.Parameters.Add(new SqlParameter("@e", (object?)ec ?? DBNull.Value));
                    cmd.Parameters.Add(new SqlParameter("@s", (object?)st ?? DBNull.Value));
                    cmd.Parameters.Add(new SqlParameter("@d", day));
                    cmd.Parameters.Add(new SqlParameter("@oa", dto.OpenApprovals));
                    cmd.Parameters.Add(new SqlParameter("@by", user));
                    affected += await cmd.ExecuteNonQueryAsync();
                }
                await tx.CommitAsync();
                return Ok(new { status = true, message = $"Saved access window for {targets.Count} target(s) x {dates.Count} date(s). Approvals {(dto.OpenApprovals ? "OPENED" : "not opened")}." });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return StatusCode(500, new { status = false, message = "Failed to save: " + ex.Message });
            }
        }

        public class RemoveDto { public List<int> Ids { get; set; } = new(); }

        // ---- Remove (soft delete: IsActive=0) ----
        [HttpPost("Remove")]
        public async Task<IActionResult> Remove([FromBody] RemoveDto dto)
        {
            if (!IsItSuperAdmin()) return Forbidden();
            var ids = (dto?.Ids ?? new()).Where(i => i > 0).Distinct().ToList();
            if (ids.Count == 0) return BadRequest(new { status = false, message = "No rows selected." });
            var user = CurrentUser();
            var sql = $"UPDATE dbo.{Table} SET IsActive=0, UpdatedBy=@by, UpdatedOn=GETDATE() WHERE Id IN ({string.Join(",", ids)});";
            await using var conn = Open();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add(new SqlParameter("@by", user));
            var n = await cmd.ExecuteNonQueryAsync();
            return Ok(new { status = true, message = $"Removed {n} window row(s)." });
        }
    }

    [Route("api/[controller]")]
    public class RegularizeAccessController : AccessWindowControllerBase
    {
        public RegularizeAccessController(IConfiguration config) : base(config) { }
        protected override string Table => "tblRegularizeAccessWindow";
    }

    [Route("api/[controller]")]
    public class GeofenceAccessController : AccessWindowControllerBase
    {
        public GeofenceAccessController(IConfiguration config) : base(config) { }
        protected override string Table => "tblGeofenceAccessWindow";
    }
}
