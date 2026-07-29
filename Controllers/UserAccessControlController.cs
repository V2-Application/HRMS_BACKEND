using HRMSAPI.Extension;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace HRMSAPI.Controllers
{
    /// <summary>
    /// User Access Control — per-employee (Ecode) custom access grants.
    ///
    /// Plain RBAC only grants access at the ROLE level. This admin page adds
    /// GRANULAR, PER-EMPLOYEE grants that RBAC cannot express:
    ///   * Modules/SubModules an ecode may open  -> tblUserModuleAccess
    ///   * Store Codes (STCode) an ecode may see  -> tblUserStoreAccess
    ///   * Other ecodes' data an ecode may access -> tblUserEcodeAccess
    ///
    /// Self-contained ADO.NET (no DbContext/EF changes). IT Superadmin only.
    /// Save is REPLACE-semantics per selected ecode (the page is the source of
    /// truth for that user); when multiple ecodes are selected the same grants
    /// are applied to each. DEV ONLY.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UserAccessControlController : ControllerBase
    {
        private readonly IConfiguration _config;

        public UserAccessControlController(IConfiguration config)
        {
            _config = config;
        }

        private SqlConnection Open()
        {
            var c = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
            c.Open();
            return c;
        }

        // Restricted to IT Superadmin (tolerant of spacing/casing).
        private bool IsItSuperAdmin()
        {
            var identity = HttpContext.User.Identity as ClaimsIdentity;
            var userClaims = AuthenticUserDetails.GetCurrentUserDetails(identity);
            var role = (userClaims?.role ?? string.Empty).Replace(" ", string.Empty);
            return string.Equals(role, "ITSuperadmin", StringComparison.OrdinalIgnoreCase);
        }

        private string CurrentUser()
        {
            var identity = HttpContext.User.Identity as ClaimsIdentity;
            var userClaims = AuthenticUserDetails.GetCurrentUserDetails(identity);
            return string.IsNullOrWhiteSpace(userClaims?.EmployeeId) ? "System" : userClaims.EmployeeId;
        }

        private IActionResult Forbidden() =>
            StatusCode(StatusCodes.Status403Forbidden, new { status = false, message = "Only IT Superadmin can manage user access." });

        // ---------- Catalog: all active modules + their submodules ----------
        [HttpGet("Modules")]
        public async Task<IActionResult> Modules()
        {
            if (!IsItSuperAdmin()) return Forbidden();

            const string sql = @"
SELECT m.Id AS ModuleId, m.ModuleName, s.Id AS SubModuleId, s.SubModuleName
FROM dbo.ModuleMaster m
JOIN dbo.SubModuleMaster s ON s.ModuleId = m.Id AND ISNULL(s.IsDeleted,0)=0 AND ISNULL(s.IsActive,1)=1
WHERE ISNULL(m.IsDeleted,0)=0 AND ISNULL(m.IsActive,1)=1
ORDER BY m.ModuleName, s.SubModuleName;";

            var modules = new List<Dictionary<string, object>>();
            var byModule = new Dictionary<int, Dictionary<string, object>>();

            await using var conn = Open();
            await using var cmd = new SqlCommand(sql, conn);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                int moduleId = Convert.ToInt32(r["ModuleId"]);
                if (!byModule.TryGetValue(moduleId, out var mod))
                {
                    mod = new Dictionary<string, object>
                    {
                        ["moduleId"] = moduleId,
                        ["moduleName"] = r["ModuleName"]?.ToString(),
                        ["subModules"] = new List<Dictionary<string, object>>()
                    };
                    byModule[moduleId] = mod;
                    modules.Add(mod);
                }
                ((List<Dictionary<string, object>>)mod["subModules"]).Add(new Dictionary<string, object>
                {
                    ["subModuleId"] = Convert.ToInt32(r["SubModuleId"]),
                    ["subModuleName"] = r["SubModuleName"]?.ToString()
                });
            }
            return Ok(new { status = true, data = modules });
        }

        // ---------- Store codes (STCode) ----------
        [HttpGet("Stores")]
        public async Task<IActionResult> Stores()
        {
            if (!IsItSuperAdmin()) return Forbidden();

            const string sql = @"
SELECT STCode, MAX(LocationName) AS LocationName
FROM dbo.tblLocation
WHERE ISNULL(IsDeleted,0)=0 AND STCode IS NOT NULL AND LTRIM(RTRIM(STCode)) <> ''
GROUP BY STCode
ORDER BY STCode;";

            var rows = new List<Dictionary<string, object>>();
            await using var conn = Open();
            await using var cmd = new SqlCommand(sql, conn);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                rows.Add(new Dictionary<string, object>
                {
                    ["storeCode"] = r["STCode"]?.ToString(),
                    ["locationName"] = r["LocationName"] is DBNull ? null : r["LocationName"]?.ToString()
                });
            return Ok(new { status = true, data = rows });
        }

        // ---------- Employee search (by ecode or name) ----------
        [HttpGet("Employees")]
        public async Task<IActionResult> Employees([FromQuery] string? search = null)
        {
            if (!IsItSuperAdmin()) return Forbidden();
            search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

            const string sql = @"
SELECT TOP 50 e.Ecode,
       LTRIM(RTRIM(ISNULL(e.FirstName,'') + ' ' + ISNULL(e.LastName,''))) AS EmpName
FROM dbo.tblEmployee e
WHERE e.Ecode IS NOT NULL AND LTRIM(RTRIM(e.Ecode)) <> ''
  AND (@search IS NULL
       OR e.Ecode LIKE '%' + @search + '%'
       OR e.FirstName LIKE '%' + @search + '%'
       OR e.LastName LIKE '%' + @search + '%')
ORDER BY e.Ecode;";

            var rows = new List<Dictionary<string, object>>();
            await using var conn = Open();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add(new SqlParameter("@search", (object?)search ?? DBNull.Value));
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var name = r["EmpName"]?.ToString();
                rows.Add(new Dictionary<string, object>
                {
                    ["ecode"] = r["Ecode"]?.ToString(),
                    ["name"] = string.IsNullOrWhiteSpace(name) ? null : name
                });
            }
            return Ok(new { status = true, data = rows });
        }

        // ---------- Current grants for ONE ecode (used to prefill the form) ----------
        [HttpGet("Access")]
        public async Task<IActionResult> Access([FromQuery] string ecode)
        {
            if (!IsItSuperAdmin()) return Forbidden();
            if (string.IsNullOrWhiteSpace(ecode))
                return BadRequest(new { status = false, message = "ecode is required." });
            ecode = ecode.Trim();

            var subModuleIds = new List<int>();
            var storeCodes = new List<string>();
            var allowedEcodes = new List<string>();

            await using var conn = Open();

            await using (var cmd = new SqlCommand(
                "SELECT SubModuleId FROM dbo.tblUserModuleAccess WHERE Ecode=@e AND IsChecked=1", conn))
            {
                cmd.Parameters.AddWithValue("@e", ecode);
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync()) subModuleIds.Add(Convert.ToInt32(r[0]));
            }
            await using (var cmd = new SqlCommand(
                "SELECT StoreCode FROM dbo.tblUserStoreAccess WHERE Ecode=@e", conn))
            {
                cmd.Parameters.AddWithValue("@e", ecode);
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync()) storeCodes.Add(r[0]?.ToString());
            }
            await using (var cmd = new SqlCommand(
                "SELECT AllowedEcode FROM dbo.tblUserEcodeAccess WHERE Ecode=@e", conn))
            {
                cmd.Parameters.AddWithValue("@e", ecode);
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync()) allowedEcodes.Add(r[0]?.ToString());
            }

            return Ok(new { status = true, data = new { ecode, subModuleIds, storeCodes, allowedEcodes } });
        }

        public class SaveDto
        {
            public List<string> Ecodes { get; set; } = new();
            public List<int> SubModuleIds { get; set; } = new();
            public List<string> StoreCodes { get; set; } = new();
            public List<string> AllowedEcodes { get; set; } = new();
        }

        // ---------- Save (replace grants for each selected ecode) ----------
        [HttpPost("Save")]
        public async Task<IActionResult> Save([FromBody] SaveDto dto)
        {
            if (!IsItSuperAdmin()) return Forbidden();
            if (dto == null || dto.Ecodes == null || dto.Ecodes.Count == 0)
                return BadRequest(new { status = false, message = "Select at least one employee (ecode)." });

            var ecodes = dto.Ecodes.Where(e => !string.IsNullOrWhiteSpace(e)).Select(e => e.Trim()).Distinct().ToList();
            var subIds = (dto.SubModuleIds ?? new()).Distinct().ToList();
            var stores = (dto.StoreCodes ?? new()).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).Distinct().ToList();
            var allowed = (dto.AllowedEcodes ?? new()).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).Distinct().ToList();
            var user = CurrentUser();

            await using var conn = Open();

            // module -> submodule map for the chosen submodules (to store ModuleId alongside)
            var subToModule = new Dictionary<int, int>();
            if (subIds.Count > 0)
            {
                await using var mc = new SqlCommand(
                    $"SELECT Id, ModuleId FROM dbo.SubModuleMaster WHERE Id IN ({string.Join(",", subIds)})", conn);
                await using var mr = await mc.ExecuteReaderAsync();
                while (await mr.ReadAsync())
                    subToModule[Convert.ToInt32(mr["Id"])] = mr["ModuleId"] is DBNull ? 0 : Convert.ToInt32(mr["ModuleId"]);
            }

            await using var tx = (SqlTransaction)await conn.BeginTransactionAsync();
            try
            {
                foreach (var ecode in ecodes)
                {
                    // wipe existing grants for this ecode
                    foreach (var t in new[] { "tblUserModuleAccess", "tblUserStoreAccess", "tblUserEcodeAccess" })
                    {
                        await using var del = new SqlCommand($"DELETE FROM dbo.{t} WHERE Ecode=@e", conn, tx);
                        del.Parameters.AddWithValue("@e", ecode);
                        await del.ExecuteNonQueryAsync();
                    }

                    // modules
                    foreach (var sub in subIds)
                    {
                        await using var ins = new SqlCommand(
                            @"INSERT INTO dbo.tblUserModuleAccess (Ecode, ModuleId, SubModuleId, IsChecked, CreatedBy, CreatedOn)
                              VALUES (@e, @m, @s, 1, @by, GETDATE())", conn, tx);
                        ins.Parameters.AddWithValue("@e", ecode);
                        ins.Parameters.AddWithValue("@m", subToModule.TryGetValue(sub, out var mid) && mid > 0 ? mid : (object)DBNull.Value);
                        ins.Parameters.AddWithValue("@s", sub);
                        ins.Parameters.AddWithValue("@by", user);
                        await ins.ExecuteNonQueryAsync();
                    }

                    // stores
                    foreach (var st in stores)
                    {
                        await using var ins = new SqlCommand(
                            @"INSERT INTO dbo.tblUserStoreAccess (Ecode, StoreCode, CreatedBy, CreatedOn)
                              VALUES (@e, @s, @by, GETDATE())", conn, tx);
                        ins.Parameters.AddWithValue("@e", ecode);
                        ins.Parameters.AddWithValue("@s", st);
                        ins.Parameters.AddWithValue("@by", user);
                        await ins.ExecuteNonQueryAsync();
                    }

                    // allowed ecodes (skip self)
                    foreach (var ae in allowed.Where(a => !string.Equals(a, ecode, StringComparison.OrdinalIgnoreCase)))
                    {
                        await using var ins = new SqlCommand(
                            @"INSERT INTO dbo.tblUserEcodeAccess (Ecode, AllowedEcode, CreatedBy, CreatedOn)
                              VALUES (@e, @a, @by, GETDATE())", conn, tx);
                        ins.Parameters.AddWithValue("@e", ecode);
                        ins.Parameters.AddWithValue("@a", ae);
                        ins.Parameters.AddWithValue("@by", user);
                        await ins.ExecuteNonQueryAsync();
                    }
                }

                await tx.CommitAsync();
                return Ok(new
                {
                    status = true,
                    message = $"Access saved for {ecodes.Count} employee(s): {subIds.Count} module(s), {stores.Count} store(s), {allowed.Count} ecode grant(s)."
                });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return StatusCode(500, new { status = false, message = "Failed to save access: " + ex.Message });
            }
        }

        // ---------- Effective RBAC access for ONE ecode (via their role) ----------
        // Shows what the employee CURRENTLY gets from their role(s) in RBAC, so the
        // admin can see the baseline and adjust (per-user overrides are saved separately).
        [HttpGet("EffectiveAccess")]
        public async Task<IActionResult> EffectiveAccess([FromQuery] string ecode)
        {
            if (!IsItSuperAdmin()) return Forbidden();
            if (string.IsNullOrWhiteSpace(ecode))
                return BadRequest(new { status = false, message = "ecode is required." });
            ecode = ecode.Trim();

            var roleNames = new List<string>();
            var roleSubModuleIds = new List<int>();
            var overrideSubModuleIds = new List<int>();

            await using var conn = Open();

            await using (var cmd = new SqlCommand(@"
SELECT DISTINCT r.RoleName
FROM dbo.tblEmployee e
JOIN dbo.tblEmployeeRole er ON er.EmployeeId = e.EmployeeId
JOIN dbo.tblRole r ON r.RoleId = er.RoleId
WHERE e.Ecode = @e", conn))
            {
                cmd.Parameters.AddWithValue("@e", ecode);
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync()) roleNames.Add(r[0]?.ToString());
            }

            await using (var cmd = new SqlCommand(@"
SELECT DISTINCT n.RefId
FROM dbo.tblEmployee e
JOIN dbo.tblEmployeeRole er ON er.EmployeeId = e.EmployeeId
JOIN dbo.RBACNode n ON n.RoleId = er.RoleId AND n.NodeType = 'SubModule' AND n.IsChecked = 1
WHERE e.Ecode = @e", conn))
            {
                cmd.Parameters.AddWithValue("@e", ecode);
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync()) roleSubModuleIds.Add(Convert.ToInt32(r[0]));
            }

            await using (var cmd = new SqlCommand(
                "SELECT SubModuleId FROM dbo.tblUserModuleAccess WHERE Ecode=@e AND IsChecked=1", conn))
            {
                cmd.Parameters.AddWithValue("@e", ecode);
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync()) overrideSubModuleIds.Add(Convert.ToInt32(r[0]));
            }

            // Baseline to pre-check the tree with: per-user override if it exists, else the role's grants.
            var effective = overrideSubModuleIds.Count > 0 ? overrideSubModuleIds : roleSubModuleIds;

            return Ok(new
            {
                status = true,
                data = new { ecode, roleNames, roleSubModuleIds, overrideSubModuleIds, hasOverride = overrideSubModuleIds.Count > 0, effectiveSubModuleIds = effective }
            });
        }

        // ---------- Feature catalog (SubModules + Actions) with global-stop status ----------
        // Actions (e.g. the "Regularize" button on View Attendance) are RBAC nodes too
        // (NodeType='Action'), so they're listed alongside SubModules and can be stopped.
        [HttpGet("Features")]
        public async Task<IActionResult> Features()
        {
            if (!IsItSuperAdmin()) return Forbidden();

            const string sql = @"
SELECT x.NodeType, x.RefId, x.ModuleName, x.ParentName, x.Name,
       (SELECT COUNT(*) FROM dbo.RBACNode n WHERE n.NodeType=x.NodeType AND n.RefId=x.RefId AND n.IsChecked=1) AS ActiveRoleCount,
       ISNULL(l.IsStopped,0) AS IsStopped,
       l.PreviousRoleIds
FROM (
    SELECT 'SubModule' AS NodeType, s.Id AS RefId, m.ModuleName,
           CAST(NULL AS nvarchar(200)) AS ParentName, s.SubModuleName AS Name
    FROM dbo.ModuleMaster m
    JOIN dbo.SubModuleMaster s ON s.ModuleId=m.Id AND ISNULL(s.IsDeleted,0)=0 AND ISNULL(s.IsActive,1)=1
    WHERE ISNULL(m.IsDeleted,0)=0 AND ISNULL(m.IsActive,1)=1
    UNION ALL
    SELECT 'Action' AS NodeType, a.Id AS RefId, m.ModuleName,
           s.SubModuleName AS ParentName, a.ActionName AS Name
    FROM dbo.ActionMaster a
    JOIN dbo.SubModuleMaster s ON s.Id=a.SubModuleId AND ISNULL(s.IsDeleted,0)=0
    JOIN dbo.ModuleMaster m ON m.Id=a.ModuleId AND ISNULL(m.IsDeleted,0)=0
    WHERE ISNULL(a.IsDeleted,0)=0 AND ISNULL(a.IsActive,1)=1
) x
LEFT JOIN dbo.tblRbacNodeAccessLock l ON l.NodeType=x.NodeType AND l.RefId=x.RefId
ORDER BY x.ModuleName, x.ParentName, x.Name;";

            var rows = new List<Dictionary<string, object>>();
            await using var conn = Open();
            await using var cmd = new SqlCommand(sql, conn);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var stopped = !(r["IsStopped"] is DBNull) && Convert.ToBoolean(r["IsStopped"]);
                var prev = r["PreviousRoleIds"] is DBNull ? null : r["PreviousRoleIds"]?.ToString();
                int prevCount = string.IsNullOrWhiteSpace(prev) ? 0 : prev.Split(',', StringSplitOptions.RemoveEmptyEntries).Length;
                rows.Add(new Dictionary<string, object>
                {
                    ["nodeType"] = r["NodeType"]?.ToString(),
                    ["refId"] = Convert.ToInt32(r["RefId"]),
                    ["moduleName"] = r["ModuleName"]?.ToString(),
                    ["parentName"] = r["ParentName"] is DBNull ? null : r["ParentName"]?.ToString(),
                    ["name"] = r["Name"]?.ToString(),
                    ["activeRoleCount"] = Convert.ToInt32(r["ActiveRoleCount"]),
                    ["isStopped"] = stopped,
                    ["stoppedRoleCount"] = prevCount
                });
            }
            return Ok(new { status = true, data = rows });
        }

        public class FeatureDto
        {
            public string NodeType { get; set; } = "SubModule"; // 'SubModule' | 'Action' | 'FurtherPart'
            public int RefId { get; set; }
            public int SubModuleId { get; set; } // back-compat: treated as RefId if RefId not set
        }

        private static string NormNodeType(string t)
        {
            t = (t ?? "").Trim();
            if (string.Equals(t, "Action", StringComparison.OrdinalIgnoreCase)) return "Action";
            if (string.Equals(t, "FurtherPart", StringComparison.OrdinalIgnoreCase)) return "FurtherPart";
            return "SubModule";
        }

        // ---------- Stop a feature (SubModule/Action) for ALL roles (snapshot who had it) ----------
        [HttpPost("StopFeature")]
        public async Task<IActionResult> StopFeature([FromBody] FeatureDto dto)
        {
            if (!IsItSuperAdmin()) return Forbidden();
            if (dto == null) return BadRequest(new { status = false, message = "Invalid request." });
            int refId = dto.RefId > 0 ? dto.RefId : dto.SubModuleId;
            var nodeType = NormNodeType(dto.NodeType);
            if (refId <= 0) return BadRequest(new { status = false, message = "RefId is required." });
            var user = CurrentUser();

            await using var conn = Open();

            bool alreadyStopped = false;
            await using (var q = new SqlCommand("SELECT IsStopped FROM dbo.tblRbacNodeAccessLock WHERE NodeType=@t AND RefId=@s", conn))
            {
                q.Parameters.AddWithValue("@t", nodeType);
                q.Parameters.AddWithValue("@s", refId);
                var v = await q.ExecuteScalarAsync();
                alreadyStopped = v != null && v != DBNull.Value && Convert.ToBoolean(v);
            }
            if (alreadyStopped)
                return Ok(new { status = false, message = "This feature is already stopped for all roles." });

            var roleIds = new List<int>();
            await using (var q = new SqlCommand(
                "SELECT RoleId FROM dbo.RBACNode WHERE NodeType=@t AND RefId=@s AND IsChecked=1", conn))
            {
                q.Parameters.AddWithValue("@t", nodeType);
                q.Parameters.AddWithValue("@s", refId);
                await using var r = await q.ExecuteReaderAsync();
                while (await r.ReadAsync()) roleIds.Add(Convert.ToInt32(r[0]));
            }
            var csv = string.Join(",", roleIds);

            await using var tx = (SqlTransaction)await conn.BeginTransactionAsync();
            try
            {
                await using (var up = new SqlCommand(@"
IF EXISTS (SELECT 1 FROM dbo.tblRbacNodeAccessLock WHERE NodeType=@t AND RefId=@s)
    UPDATE dbo.tblRbacNodeAccessLock SET IsStopped=1, PreviousRoleIds=@prev, UpdatedBy=@by, UpdatedOn=GETDATE() WHERE NodeType=@t AND RefId=@s;
ELSE
    INSERT INTO dbo.tblRbacNodeAccessLock (NodeType, RefId, IsStopped, PreviousRoleIds, UpdatedBy, UpdatedOn)
    VALUES (@t, @s, 1, @prev, @by, GETDATE());", conn, tx))
                {
                    up.Parameters.AddWithValue("@t", nodeType);
                    up.Parameters.AddWithValue("@s", refId);
                    up.Parameters.AddWithValue("@prev", (object)csv ?? DBNull.Value);
                    up.Parameters.AddWithValue("@by", user);
                    await up.ExecuteNonQueryAsync();
                }

                int affected;
                await using (var z = new SqlCommand(
                    "UPDATE dbo.RBACNode SET IsChecked=0, UpdatedBy=@by, UpdatedOn=GETDATE() WHERE NodeType=@t AND RefId=@s AND IsChecked=1", conn, tx))
                {
                    z.Parameters.AddWithValue("@t", nodeType);
                    z.Parameters.AddWithValue("@s", refId);
                    z.Parameters.AddWithValue("@by", user);
                    affected = await z.ExecuteNonQueryAsync();
                }

                await tx.CommitAsync();
                return Ok(new { status = true, message = $"Feature stopped for {affected} role(s). They are remembered and can be restored." });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return StatusCode(500, new { status = false, message = "Failed to stop feature: " + ex.Message });
            }
        }

        // ---------- Restore a feature to exactly the roles that had it ----------
        [HttpPost("RestoreFeature")]
        public async Task<IActionResult> RestoreFeature([FromBody] FeatureDto dto)
        {
            if (!IsItSuperAdmin()) return Forbidden();
            if (dto == null) return BadRequest(new { status = false, message = "Invalid request." });
            int refId = dto.RefId > 0 ? dto.RefId : dto.SubModuleId;
            var nodeType = NormNodeType(dto.NodeType);
            if (refId <= 0) return BadRequest(new { status = false, message = "RefId is required." });
            var user = CurrentUser();

            await using var conn = Open();

            string prev = null;
            bool isStopped = false;
            await using (var q = new SqlCommand("SELECT IsStopped, PreviousRoleIds FROM dbo.tblRbacNodeAccessLock WHERE NodeType=@t AND RefId=@s", conn))
            {
                q.Parameters.AddWithValue("@t", nodeType);
                q.Parameters.AddWithValue("@s", refId);
                await using var r = await q.ExecuteReaderAsync();
                if (await r.ReadAsync())
                {
                    isStopped = !(r["IsStopped"] is DBNull) && Convert.ToBoolean(r["IsStopped"]);
                    prev = r["PreviousRoleIds"] is DBNull ? null : r["PreviousRoleIds"]?.ToString();
                }
            }
            if (!isStopped)
                return Ok(new { status = false, message = "This feature is not stopped." });

            var roleIds = (prev ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => int.TryParse(x.Trim(), out var n) ? n : 0)
                .Where(n => n > 0).Distinct().ToList();

            await using var tx = (SqlTransaction)await conn.BeginTransactionAsync();
            try
            {
                int affected = 0;
                if (roleIds.Count > 0)
                {
                    await using var z = new SqlCommand(
                        $"UPDATE dbo.RBACNode SET IsChecked=1, UpdatedBy=@by, UpdatedOn=GETDATE() WHERE NodeType=@t AND RefId=@s AND RoleId IN ({string.Join(",", roleIds)})", conn, tx);
                    z.Parameters.AddWithValue("@t", nodeType);
                    z.Parameters.AddWithValue("@s", refId);
                    z.Parameters.AddWithValue("@by", user);
                    affected = await z.ExecuteNonQueryAsync();
                }

                await using (var up = new SqlCommand(
                    "UPDATE dbo.tblRbacNodeAccessLock SET IsStopped=0, UpdatedBy=@by, UpdatedOn=GETDATE() WHERE NodeType=@t AND RefId=@s", conn, tx))
                {
                    up.Parameters.AddWithValue("@t", nodeType);
                    up.Parameters.AddWithValue("@s", refId);
                    up.Parameters.AddWithValue("@by", user);
                    await up.ExecuteNonQueryAsync();
                }

                await tx.CommitAsync();
                return Ok(new { status = true, message = $"Feature restored to {affected} role(s)." });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return StatusCode(500, new { status = false, message = "Failed to restore feature: " + ex.Message });
            }
        }
    }
}
