using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HRMSAPI.Controllers
{
    /// <summary>
    /// Punch location (which biometric device/store each punch was recorded at) + its mapped ST Code.
    /// DEV-ONLY feature. Punch locations come from the SmartOffice biometric raw log
    /// [192.168.151.25].[SmartOfficedb].[dbo].ATTLOG (linked server exists only on PROD), so the location
    /// lookup is READ-ONLY against prod (connection "PunchLocationProd"), or from the saved local table
    /// dbo.tblAttendancePunchLocation. The ST Code is ALWAYS resolved live from the local (dev) Biomax
    /// mapping table dbo.tblBiomaxAttendanceLocationMap, so mappings added/edited on the Biomax page
    /// reflect immediately. No writes to prod; no delete/truncate anywhere.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PunchLocationController : ControllerBase
    {
        private readonly IConfiguration _config;
        public PunchLocationController(IConfiguration config) { _config = config; }

        private class PunchRow
        {
            public string PunchNo { get; set; }
            public string PunchTime { get; set; }
            public string PunchLocation { get; set; }
            public string PunchStCode { get; set; }
        }

        // Per-punch location + ST Code for one employee + date. The frontend "i" popover reads this.
        [HttpGet("ByEcodeDate")]
        public async Task<IActionResult> ByEcodeDate([FromQuery] string ecode, [FromQuery] DateTime date)
        {
            if (string.IsNullOrWhiteSpace(ecode))
                return BadRequest(new { status = false, message = "ecode is required." });

            ecode = ecode.Trim();

            // 1) Get the punch locations — saved local table first (fast), else live prod query.
            var punches = await ReadFromSavedTableAsync(ecode, date.Date);
            var source = "saved";
            if (punches.Count == 0)
            {
                punches = await ReadFromLiveProdAsync(ecode, date.Date);
                source = "live";
            }

            // 2) Resolve ST Code from the CURRENT local (dev) Biomax mapping — so edits reflect at once.
            await ApplyStCodesAsync(punches);

            var data = punches.Select(p => new
            {
                punchNo = p.PunchNo,
                punchTime = p.PunchTime,
                punchLocation = p.PunchLocation,
                punchStCode = p.PunchStCode,
            });
            return Ok(new { status = true, source, data });
        }

        // Punch locations from the saved local table (DefaultConnection DB).
        private async Task<List<PunchRow>> ReadFromSavedTableAsync(string ecode, DateTime date)
        {
            var rows = new List<PunchRow>();
            const string sql = @"
IF OBJECT_ID('dbo.tblAttendancePunchLocation') IS NOT NULL
    SELECT PunchNo, PunchTime, PunchLocation
    FROM dbo.tblAttendancePunchLocation
    WHERE ECode = @Ecode AND AttendanceDate = @Date
    ORDER BY PunchTime;";
            try
            {
                using var conn = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
                await conn.OpenAsync();
                using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
                cmd.Parameters.AddWithValue("@Ecode", ecode);
                cmd.Parameters.AddWithValue("@Date", date);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    rows.Add(new PunchRow
                    {
                        PunchNo = r["PunchNo"] as string,
                        PunchTime = r["PunchTime"] as string,
                        PunchLocation = r["PunchLocation"] == DBNull.Value ? null : r["PunchLocation"] as string,
                    });
            }
            catch { /* fall through to live */ }
            return rows;
        }

        // Live punch locations via prod (linked server exists only there). Location only; ST Code resolved later.
        private async Task<List<PunchRow>> ReadFromLiveProdAsync(string ecode, DateTime date)
        {
            var rows = new List<PunchRow>();
            const string sql = @"
SELECT u.PunchNo, u.PunchTime, a.location AS PunchLocation
FROM dbo.tbl_fn_GetMonthlyPunchesRange_productionnewnick_test AS p
CROSS APPLY (VALUES
    ('Punch1',  p.Punch1),('Punch2',  p.Punch2),('Punch3',  p.Punch3),('Punch4',  p.Punch4),
    ('Punch5',  p.Punch5),('Punch6',  p.Punch6),('Punch7',  p.Punch7),('Punch8',  p.Punch8),
    ('Punch9',  p.Punch9),('Punch10', p.Punch10),('Punch11', p.Punch11),('Punch12', p.Punch12)
) AS u(PunchNo, PunchTime)
LEFT JOIN [192.168.151.25].[SmartOfficedb].[dbo].ATTLOG AS a
    ON a.Employeecode COLLATE DATABASE_DEFAULT = p.ECode COLLATE DATABASE_DEFAULT
   AND a.Logdatetime = CONVERT(datetime,
                               CONVERT(varchar(10), p.AttendanceDate, 120) + ' ' + u.PunchTime)
WHERE p.ECode = @Ecode
  AND p.AttendanceDate = @Date
  AND u.PunchTime IS NOT NULL AND u.PunchTime <> '' AND u.PunchTime <> '00:00:00'
ORDER BY u.PunchTime;";
            var cs = _config.GetConnectionString("PunchLocationProd")
                     ?? _config.GetConnectionString("DefaultConnection");
            try
            {
                using var conn = new SqlConnection(cs);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 120 };
                cmd.Parameters.AddWithValue("@Ecode", ecode);
                cmd.Parameters.AddWithValue("@Date", date);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    rows.Add(new PunchRow
                    {
                        PunchNo = r["PunchNo"] as string,
                        PunchTime = r["PunchTime"] as string,
                        PunchLocation = r["PunchLocation"] == DBNull.Value ? null : r["PunchLocation"] as string,
                    });
            }
            catch { /* leave rows as-is */ }
            return rows;
        }

        // Resolve ST Code for each punch's location from the CURRENT local (dev) Biomax mapping.
        private async Task ApplyStCodesAsync(List<PunchRow> punches)
        {
            var locations = punches
                .Select(p => p.PunchLocation)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => l.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (locations.Count == 0) return;

            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var conn = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
                await conn.OpenAsync();
                if (await ObjectMissingAsync(conn, "dbo.tblBiomaxAttendanceLocationMap")) return;

                // Parameterized IN list.
                var pnames = locations.Select((_, i) => "@l" + i).ToList();
                var sql = $@"SELECT DeviceLocation, STCode
                             FROM dbo.tblBiomaxAttendanceLocationMap
                             WHERE IsDeleted = 0 AND DeviceLocation IN ({string.Join(",", pnames)});";
                using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
                for (int i = 0; i < locations.Count; i++)
                    cmd.Parameters.AddWithValue(pnames[i], locations[i]);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    var dev = (r["DeviceLocation"] as string)?.Trim();
                    if (!string.IsNullOrEmpty(dev))
                        map[dev] = r["STCode"] == DBNull.Value ? null : r["STCode"] as string;
                }
            }
            catch { return; }

            foreach (var p in punches)
            {
                if (!string.IsNullOrWhiteSpace(p.PunchLocation) && map.TryGetValue(p.PunchLocation.Trim(), out var st))
                    p.PunchStCode = st;
            }
        }

        private static async Task<bool> ObjectMissingAsync(SqlConnection conn, string obj)
        {
            using var cmd = new SqlCommand("SELECT CASE WHEN OBJECT_ID(@o) IS NULL THEN 1 ELSE 0 END", conn);
            cmd.Parameters.AddWithValue("@o", obj);
            return Convert.ToInt32(await cmd.ExecuteScalarAsync()) == 1;
        }
    }
}
