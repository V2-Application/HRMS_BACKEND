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
                ["geofence-loc"] = ("dbo.usp_LocationWiseGeofenceReport", "LOC_Geofence_Gap_Report_", false),
                ["geofence-emp"] = ("dbo.usp_EmployeeWiseGeofenceReport", "EMP_Geofence_Gap_Report_", false),
                ["regularization-loc"] = ("dbo.usp_LocationWiseRegularizationReport", "LOC_Regularization_Gap_Report_", false),
                ["regularization-emp"] = ("dbo.usp_EmployeeWiseRegularizationReport", "EMP_Regularization_Gap_Report_", false),
                ["lastpunch-sep"] = ("dbo.usp_LastPunchVsSeparationGapReport", "LastPunch_vs_Separation_HighAgeing_Gap_Report_", false),
                ["lastpunch-after-sep"] = ("dbo.usp_LastPunchAfterSeparationGapReport", "LastPunch_After_Separation_Gap_Report_", false),
                ["sep-fnf-pending"] = ("dbo.usp_SeparatedFnFPendingGapReport", "Separated_But_FnF_Pending_Gap_Report_", false),
                ["sep-lastpunch-missing"] = ("dbo.usp_SeparatedLastPunchMissingGapReport", "Separated_But_LastPunch_Missing_Gap_Report_", false),
                ["sep-resignation-missing"] = ("dbo.usp_SeparatedResignationMissingGapReport", "Separated_But_Resignation_Missing_Gap_Report_", false),
                ["rm-geofence-pending"] = ("dbo.usp_RMGeofenceApprovalPendingGapReport", "LOC_RM_Geofence_Approval_Pending_Gap_Report_", false),
                ["rm-regularization-pending"] = ("dbo.usp_RMRegularizationApprovalPendingGapReport", "LOC_RM_Regularization_Approval_Pending_Gap_Report_", false),
                ["audit-regularization-pending"] = ("dbo.usp_AuditRegularizationApprovalPendingGapReport", "LOC_Audit_Regularization_Approval_Pending_Gap_Report_", false),
                ["absent-loc"] = ("dbo.usp_LocationWiseAbsentReport", "LOC_Absent_TD_MTD_Gap_Report_", false),
                ["actemp-vs-attend-loc"] = ("dbo.usp_LocationWiseActEmpVsAttendanceReport", "LOC_ActEmp_vs_ActAttend_Gap_Report_", false),
                ["bgtemp-vs-attend-loc"] = ("dbo.usp_LocationWiseBgtEmpVsAttendanceReport", "LOC_BgtEmp_vs_ActEmp_Gap_Report_", false),
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
                new { key = "geofence-loc", name = "TD/MTD Geo-Fencing Gap Report (Location-wise)" },
                new { key = "geofence-emp", name = "TD/MTD Geo-Fencing Gap Report (Employee-wise)" },
                new { key = "regularization-loc", name = "TD/MTD Regularization Gap Report (Location-wise)" },
                new { key = "regularization-emp", name = "TD/MTD Regularization Gap Report (Employee-wise)" },
                new { key = "lastpunch-sep", name = "Last Punch vs Separation High Ageing Gap Report" },
                new { key = "lastpunch-after-sep", name = "Last Punching Shows After Separation Gap Report" },
                new { key = "sep-fnf-pending", name = "Separated But F&F Pending Gap Report" },
                new { key = "sep-lastpunch-missing", name = "Separated But Last Punch Date Missing Gap Report" },
                new { key = "sep-resignation-missing", name = "Separated But Resignation Missing Gap Report" },
                new { key = "rm-geofence-pending", name = "TD/MTD RM Geo-Fencing Approval Pending Gap Report" },
                new { key = "rm-regularization-pending", name = "TD/MTD RM Regularization Approval Pending Gap Report" },
                new { key = "audit-regularization-pending", name = "TD/MTD Audit Regularization Approval Pending Gap Report" },
                new { key = "absent-loc", name = "Location-wise Absent TD/MTD Gap Report" },
                new { key = "actemp-vs-attend-loc", name = "Location-wise Act Emp vs Act Attendance Gap Report" },
                new { key = "bgtemp-vs-attend-loc", name = "Location-wise Bgt Emp vs Act Emp Gap Report" },
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
                    var cell = ws.Cell(r + 2, c + 1);
                    if (val == DBNull.Value || val == null)
                    {
                        // leave blank
                    }
                    else if (val is DateTime dt)
                    {
                        // standard display date format DD-MMM-YY (e.g. 17-Jun-26)
                        cell.Value = dt;
                        cell.Style.DateFormat.Format = "dd-mmm-yy";
                    }
                    else
                    {
                        cell.Value = XLCellValue.FromObject(val);
                    }
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
