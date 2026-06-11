using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading.Tasks;

namespace HRMSAPI.Controllers
{
    /// <summary>
    /// Gap Reports - exports the absconding gap reports (read-only) as Excel.
    /// Each report is backed by a stored procedure; the proc only SELECTs (marks/changes nothing).
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class GapReportsController : ControllerBase
    {
        private readonly IConfiguration _config;
        public GapReportsController(IConfiguration config) { _config = config; }

        // report key -> (stored proc, file prefix, whether the proc takes @BatchSize)
        private static readonly Dictionary<string, (string Proc, string FilePrefix, bool HasBatch)> Reports =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["location"] = ("dbo.usp_LocationWiseAbscondingReport", "LOC_Absconding_Report_", true),
                ["employee"] = ("dbo.usp_EmployeeWiseAbscondingReport", "EMP_Absconding_Report_", false),
                ["mispunch"] = ("dbo.usp_MispunchGapReport", "MISPUNCH_Gap_Report_", false),
                ["mispunch-loc"] = ("dbo.usp_LocationWiseMispunchReport", "LOC_Mispunch_Gap_Report_", false),
            };

        /// <summary>Dropdown options for the Gap Reports page.</summary>
        [HttpGet("List")]
        public IActionResult List()
        {
            var list = new[]
            {
                new { key = "location", name = "Location-wise Absconding Report" },
                new { key = "employee", name = "Employee-wise Absconding Report" },
                new { key = "mispunch", name = "TD/MTD Mis-Punch Gap Report (Employee-wise)" },
                new { key = "mispunch-loc", name = "TD/MTD Mis-Punch Gap Report (Location-wise)" },
            };
            return Ok(list);
        }

        /// <summary>Runs the selected report's stored proc and streams it back as an .xlsx file.</summary>
        [HttpGet("Export")]
        public async Task<IActionResult> Export([FromQuery] string report, [FromQuery] DateTime? asOfDate = null)
        {
            if (string.IsNullOrWhiteSpace(report) || !Reports.TryGetValue(report, out var def))
                return BadRequest("Unknown report. Use report=location or report=employee.");

            var connectionString = _config.GetConnectionString("DefaultConnection");
            var data = new DataTable();

            using (var conn = new SqlConnection(connectionString))
            {
                await conn.OpenAsync();
                using var cmd = new SqlCommand(def.Proc, conn)
                {
                    CommandType = CommandType.StoredProcedure,
                    CommandTimeout = 900,
                };
                cmd.Parameters.Add(new SqlParameter("@AsOfDate", (object?)asOfDate?.Date ?? DBNull.Value));
                if (def.HasBatch)
                    cmd.Parameters.Add(new SqlParameter("@BatchSize", 8000));

                using var reader = await cmd.ExecuteReaderAsync();
                data.Load(reader);
            }

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add(report.Equals("location", StringComparison.OrdinalIgnoreCase) ? "LOC._WISE" : "EMP._WISE");

            // header row
            for (int c = 0; c < data.Columns.Count; c++)
            {
                var cell = ws.Cell(1, c + 1);
                cell.Value = data.Columns[c].ColumnName;
                cell.Style.Font.Bold = true;
            }
            // data rows
            for (int r = 0; r < data.Rows.Count; r++)
            {
                for (int c = 0; c < data.Columns.Count; c++)
                {
                    var val = data.Rows[r][c];
                    ws.Cell(r + 2, c + 1).Value = XLCellValue.FromObject(val == DBNull.Value ? null : val);
                }
            }
            ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);

            var stamp = (asOfDate?.Date ?? DateTime.Today).ToString("yyyy-MM-dd");
            var fileName = $"{def.FilePrefix}{stamp}.xlsx";
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
    }
}
