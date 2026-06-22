using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using HRMSAPI.Data;
using System.Data;
using System.Security.Claims;

namespace HRMSAPI.Controllers
{
    /// <summary>
    /// Manage the Reporting Manager (tblEmployee.ReportHeadEcode) of STORE-ID login accounts
    /// (employees whose ECode equals a tblLocation.STCode). Read list + per-row/bulk update.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class StoreReportingManagerController : ControllerBase
    {
        private readonly HRMSContext _context;
        public StoreReportingManagerController(HRMSContext context) { _context = context; }

        public class UpdateStoreRmRequest
        {
            public List<string> Ecodes { get; set; } = new();   // store-account ECodes to update
            public string ReportHeadEcode { get; set; } = "";    // new reporting manager's ECode
        }

        /// <summary>List all store-id accounts with their current reporting manager. Optional search by store code / name.</summary>
        [HttpGet("GetStoreAccounts")]
        public async Task<IActionResult> GetStoreAccounts([FromQuery] string? search = null)
        {
            var list = new List<object>();
            var conn = _context.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open) await conn.OpenAsync();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
SELECT e.EmployeeId,
       e.ECode                         AS StoreEcode,
       l.LocationName                  AS StoreName,
       CASE WHEN l.IsActive = 1 THEN 'Active' ELSE 'UPC' END AS LocStatus,
       CASE WHEN e.IsActive = 1 THEN 'Active' ELSE 'Inactive' END AS AccountStatus,
       e.ReportHeadEcode               AS CurrentRmEcode,
       rh.[FULL NAME]                  AS CurrentRmName
FROM dbo.tblEmployee e WITH (NOLOCK)
JOIN dbo.tblLocation l WITH (NOLOCK) ON l.STCode = e.ECode
LEFT JOIN dbo.tblEmployee rh WITH (NOLOCK) ON rh.ECode = e.ReportHeadEcode
WHERE (@search IS NULL OR @search = ''
       OR e.ECode LIKE '%' + @search + '%'
       OR l.LocationName LIKE '%' + @search + '%'
       OR e.ReportHeadEcode LIKE '%' + @search + '%')
ORDER BY e.ECode;";
                var p = cmd.CreateParameter(); p.ParameterName = "@search"; p.Value = (object?)search ?? DBNull.Value; cmd.Parameters.Add(p);
                cmd.CommandTimeout = 120;
                using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                {
                    list.Add(new
                    {
                        employeeId = rdr.IsDBNull(0) ? 0 : Convert.ToInt64(rdr[0]),
                        storeEcode = rdr.IsDBNull(1) ? "" : rdr.GetString(1),
                        storeName = rdr.IsDBNull(2) ? "" : rdr.GetString(2),
                        locStatus = rdr.IsDBNull(3) ? "" : rdr.GetString(3),
                        accountStatus = rdr.IsDBNull(4) ? "" : rdr.GetString(4),
                        currentRmEcode = rdr.IsDBNull(5) ? "" : rdr.GetString(5),
                        currentRmName = rdr.IsDBNull(6) ? "" : rdr.GetString(6),
                    });
                }
            }
            return Ok(new { status = true, message = "Store accounts fetched", data = list });
        }

        /// <summary>Set the reporting manager (ReportHeadEcode) for one or more store-id accounts.</summary>
        [HttpPost("UpdateReportingManager")]
        public async Task<IActionResult> UpdateReportingManager([FromBody] UpdateStoreRmRequest body)
        {
            if (body == null || body.Ecodes == null || body.Ecodes.Count == 0)
                return BadRequest(new { status = false, message = "No store accounts provided." });
            if (string.IsNullOrWhiteSpace(body.ReportHeadEcode))
                return BadRequest(new { status = false, message = "Reporting manager ECode is required." });

            var rm = body.ReportHeadEcode.Trim();
            var updatedBy = (User.Identity as ClaimsIdentity)?.FindFirst("EmployeeId")?.Value;

            var conn = _context.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open) await conn.OpenAsync();

            // 1) Validate the new RM is an ACTIVE, non-deleted employee
            using (var chk = conn.CreateCommand())
            {
                chk.CommandText = "SELECT COUNT(*) FROM dbo.tblEmployee WITH (NOLOCK) WHERE ECode = @rm AND IsActive = 1 AND ISNULL(IsDeleted,0) = 0;";
                var pr = chk.CreateParameter(); pr.ParameterName = "@rm"; pr.Value = rm; chk.Parameters.Add(pr);
                var cnt = Convert.ToInt32(await chk.ExecuteScalarAsync());
                if (cnt == 0)
                    return BadRequest(new { status = false, message = $"Reporting manager '{rm}' is not an active employee." });
            }

            // 2) Update ReportHeadEcode for the selected STORE accounts only (ECode must be a store STCode)
            int affected;
            using (var upd = conn.CreateCommand())
            {
                upd.CommandText = @"
UPDATE e
SET e.ReportHeadEcode = @rm,
    e.UpdatedBy = @by,
    e.UpdatedOn = GETDATE()
FROM dbo.tblEmployee e
JOIN STRING_SPLIT(@ecodes, ',') s ON LTRIM(RTRIM(s.value)) = e.ECode
WHERE EXISTS (SELECT 1 FROM dbo.tblLocation l WHERE l.STCode = e.ECode);";
                var pr = upd.CreateParameter(); pr.ParameterName = "@rm"; pr.Value = rm; upd.Parameters.Add(pr);
                var pb = upd.CreateParameter(); pb.ParameterName = "@by"; pb.Value = (object?)updatedBy ?? DBNull.Value; upd.Parameters.Add(pb);
                var pe = upd.CreateParameter(); pe.ParameterName = "@ecodes"; pe.Value = string.Join(",", body.Ecodes); upd.Parameters.Add(pe);
                upd.CommandTimeout = 120;
                affected = await upd.ExecuteNonQueryAsync();
            }

            return Ok(new { status = true, message = $"Reporting manager updated for {affected} store account(s).", updated = affected });
        }
    }
}
