using System.Data;
using System.Data.Common;
using HRMSAPI.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMSAPI.Controllers
{
    // Public, anonymous, read-only employee master.
    // Reads live from existing tables (tblEmployee + master joins) — no
    // snapshot table, no new schema. "Daily-updated" is satisfied implicitly:
    // every call returns the current state of tblEmployee.
    // No writes anywhere; no auth required.
    [ApiController]
    [AllowAnonymous]
    [Route("api/[controller]")]
    public class EmployeeMasterPublicController : ControllerBase
    {
        private readonly HRMSContext _db;

        public EmployeeMasterPublicController(HRMSContext db)
        {
            _db = db;
        }

        // GET api/EmployeeMasterPublic
        [HttpGet]
        public async Task<IActionResult> Get(CancellationToken ct)
        {
            const string sql = @"
SELECT
    e.Ecode                                                                            AS ecode,
    LTRIM(RTRIM(CONCAT_WS(' ', e.FirstName, e.MiddleName, e.LastName)))                AS name,
    d.DepartmentName                                                                   AS department,
    g.DesignationName                                                                  AS designation,
    NULLIF(LTRIM(RTRIM(COALESCE(NULLIF(LTRIM(RTRIM(e.[PRESENT ADDRESS])), ''),
                                e.[PERMANENT ADDRESS])), ''), '')                      AS address,
    CASE WHEN e.IsActive = 1 THEN 'Active' ELSE 'Inactive' END                         AS status,
    l.STCode                                                                           AS storeCode,
    CONVERT(varchar(10), e.DOB, 23)                                                    AS dob,
    CONVERT(varchar(10), e.DOJ, 23)                                                    AS doj,
    e.GENDER                                                                           AS sex,
    e.MOBILE                                                                           AS mobileNumber,
    e.[EMAIL ADDRESS]                                                                  AS emailId,
    e.ReportHeadEcode                                                                  AS reportingHeadEcode,
    LTRIM(RTRIM(CONCAT_WS(' ', rh.FirstName, rh.MiddleName, rh.LastName)))             AS reportingHeadName
FROM dbo.tblEmployee e
LEFT JOIN dbo.tblDepartment  d  ON d.DepartmentId  = e.DepartmentId
LEFT JOIN dbo.tblDesignation g  ON g.DesignationId = e.DesignationId
LEFT JOIN dbo.tblLocation    l  ON l.LocationId    = e.LocationId
LEFT JOIN dbo.tblEmployee    rh ON rh.Ecode        = e.ReportHeadEcode
WHERE e.IsActive = 1
ORDER BY e.Ecode;
";

            var rows = new List<Dictionary<string, object?>>();

            var conn = _db.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open) await conn.OpenAsync(ct);
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.CommandType = CommandType.Text;
                cmd.CommandTimeout = 120;
                using var rdr = await cmd.ExecuteReaderAsync(ct);
                while (await rdr.ReadAsync(ct))
                {
                    var row = new Dictionary<string, object?>(rdr.FieldCount);
                    for (int i = 0; i < rdr.FieldCount; i++)
                    {
                        var name = rdr.GetName(i);
                        row[name] = rdr.IsDBNull(i) ? null : rdr.GetValue(i);
                    }
                    rows.Add(row);
                }
            }

            return Ok(new
            {
                status = true,
                generatedAt = DateTime.UtcNow,
                count = rows.Count,
                data = rows
            });
        }
    }
}
