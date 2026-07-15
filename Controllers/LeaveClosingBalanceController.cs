using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace HRMSAPI.Controllers
{
    /// <summary>
    /// Leave CLOSING balance uploader/viewer (dbo.EmpLeaveClosingBalance).
    /// Per-employee, per-month closing balances (EL / CL / CompoOff).
    /// Managed via a UI page: server-side paged grid + Excel uploader + export.
    /// Pure ADO.NET (no DbContext changes). Upload is an UPSERT by (ECODE, MONTH) —
    /// additive only: existing rows are updated, new ones inserted. NEVER truncates
    /// or deletes any data. Page access is gated to IT Superadmin via RBAC.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class LeaveClosingBalanceController : ControllerBase
    {
        private readonly IConfiguration _config;
        public LeaveClosingBalanceController(IConfiguration config) { _config = config; }

        private SqlConnection Open()
        {
            var c = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
            c.Open();
            return c;
        }

        public class ClosingBalanceDto
        {
            public string? ECode { get; set; }
            public string? Month { get; set; }
            public decimal? ElClosing { get; set; }
            public decimal? ClClosing { get; set; }
            public decimal? CompoOffClosing { get; set; }
        }

        // ---------- Grid: server-side paged (table has ~750k rows) ----------
        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] string? search = null,
            [FromQuery] string? month = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 500) pageSize = 50;
            search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
            month = string.IsNullOrWhiteSpace(month) ? null : month.Trim();

            const string where = @"
WHERE (@search IS NULL OR ECODE LIKE '%' + @search + '%' OR [MONTH] LIKE '%' + @search + '%')
  AND (@month  IS NULL OR LTRIM(RTRIM([MONTH])) = @month)";

            var countSql = "SELECT COUNT_BIG(1) FROM dbo.EmpLeaveClosingBalance" + where + ";";
            var dataSql = @"
SELECT ECODE AS ECode, [MONTH] AS [Month],
       [EL Closing] AS ElClosing, [Cl Closing] AS ClClosing, [CompoOff Closing] AS CompoOffClosing
FROM dbo.EmpLeaveClosingBalance" + where + @"
ORDER BY ECODE, [MONTH]
OFFSET @offset ROWS FETCH NEXT @take ROWS ONLY;";

            long total;
            var rows = new List<Dictionary<string, object>>();
            using (var c = Open())
            {
                using (var cc = new SqlCommand(countSql, c) { CommandTimeout = 120 })
                {
                    cc.Parameters.AddWithValue("@search", (object?)search ?? DBNull.Value);
                    cc.Parameters.AddWithValue("@month", (object?)month ?? DBNull.Value);
                    total = Convert.ToInt64(await cc.ExecuteScalarAsync());
                }
                using (var cmd = new SqlCommand(dataSql, c) { CommandTimeout = 120 })
                {
                    cmd.Parameters.AddWithValue("@search", (object?)search ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@month", (object?)month ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@offset", (page - 1) * pageSize);
                    cmd.Parameters.AddWithValue("@take", pageSize);
                    using var r = await cmd.ExecuteReaderAsync();
                    while (await r.ReadAsync())
                    {
                        var row = new Dictionary<string, object>();
                        for (int i = 0; i < r.FieldCount; i++)
                            row[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
                        rows.Add(row);
                    }
                }
            }
            return Ok(new { status = true, data = rows, total, page, pageSize });
        }

        // ---------- Distinct months for the filter dropdown ----------
        [HttpGet("GetMonths")]
        public async Task<IActionResult> GetMonths()
        {
            const string sql = @"SELECT DISTINCT LTRIM(RTRIM([MONTH])) AS m
                                 FROM dbo.EmpLeaveClosingBalance
                                 WHERE [MONTH] IS NOT NULL AND LTRIM(RTRIM([MONTH])) <> ''
                                 ORDER BY m;";
            var months = new List<string>();
            using var c = Open();
            using var cmd = new SqlCommand(sql, c) { CommandTimeout = 120 };
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                if (!r.IsDBNull(0)) months.Add(r.GetString(0));
            return Ok(new { status = true, data = months });
        }

        // ---------- Download Excel template ----------
        [HttpGet("DownloadTemplate")]
        public IActionResult DownloadTemplate()
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("ClosingBalance");
            string[] headers = { "ECODE", "MONTH", "EL Closing", "Cl Closing", "CompoOff Closing" };
            for (int i = 0; i < headers.Length; i++) { var cell = ws.Cell(1, i + 1); cell.Value = headers[i]; cell.Style.Font.Bold = true; }
            ws.Cell(2, 1).Value = "V33154"; ws.Cell(2, 2).Value = "Jul-25";
            ws.Cell(2, 3).Value = 4.85; ws.Cell(2, 4).Value = 0.78; ws.Cell(2, 5).Value = 0;
            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "LeaveClosingBalance_Template.xlsx");
        }

        // ---------- Export current data to Excel (filtered same as grid) ----------
        [HttpGet("Export")]
        public async Task<IActionResult> Export([FromQuery] string? search = null, [FromQuery] string? month = null)
        {
            search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
            month = string.IsNullOrWhiteSpace(month) ? null : month.Trim();

            const string sql = @"
SELECT ECODE, [MONTH], [EL Closing], [Cl Closing], [CompoOff Closing]
FROM dbo.EmpLeaveClosingBalance
WHERE (@search IS NULL OR ECODE LIKE '%' + @search + '%' OR [MONTH] LIKE '%' + @search + '%')
  AND (@month  IS NULL OR LTRIM(RTRIM([MONTH])) = @month)
ORDER BY ECODE, [MONTH];";

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("ClosingBalance");
            string[] headers = { "ECODE", "MONTH", "EL Closing", "Cl Closing", "CompoOff Closing" };
            for (int i = 0; i < headers.Length; i++) { var cell = ws.Cell(1, i + 1); cell.Value = headers[i]; cell.Style.Font.Bold = true; }

            int rrow = 2;
            using (var c = Open())
            using (var cmd = new SqlCommand(sql, c) { CommandTimeout = 300 })
            {
                cmd.Parameters.AddWithValue("@search", (object?)search ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@month", (object?)month ?? DBNull.Value);
                using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    ws.Cell(rrow, 1).Value = rd.IsDBNull(0) ? "" : rd.GetValue(0)?.ToString();
                    ws.Cell(rrow, 2).Value = rd.IsDBNull(1) ? "" : rd.GetValue(1)?.ToString();
                    for (int i = 2; i < 5; i++)
                    {
                        if (rd.IsDBNull(i)) ws.Cell(rrow, i + 1).Value = "";
                        else ws.Cell(rrow, i + 1).Value = Convert.ToDouble(rd.GetValue(i));
                    }
                    rrow++;
                }
            }
            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            var stamp = DateTime.Today.ToString("yyyy-MM-dd");
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"LeaveClosingBalance_{stamp}.xlsx");
        }

        // ---------- Upload Excel -> upsert by (ECODE, MONTH). Additive only. ----------
        [HttpPost("Upload")]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { status = false, message = "No file uploaded." });

            var inserted = 0; var updated = 0; var skipped = 0; var errors = new List<string>();

            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            stream.Position = 0;
            XLWorkbook wb;
            try { wb = new XLWorkbook(stream); }
            catch { return BadRequest(new { status = false, message = "Could not read the Excel file." }); }

            using (wb)
            {
                var ws = wb.Worksheet(1);
                var headerRow = ws.FirstRowUsed();
                if (headerRow == null)
                    return BadRequest(new { status = false, message = "The sheet is empty." });

                int headerRowNum = headerRow.RowNumber();
                int lastCol = headerRow.LastCellUsed()?.Address.ColumnNumber ?? 1;

                // Detect columns by header name (tolerant of spacing/case/punctuation).
                int ecodeCol = 0, monthCol = 0, elCol = 0, clCol = 0, coCol = 0;
                var foundHeaders = new List<string>();
                string Norm(string s) => new string((s ?? "").ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
                for (int col = 1; col <= lastCol; col++)
                {
                    var raw = ws.Cell(headerRowNum, col).GetString().Trim();
                    if (raw == "") continue;
                    foundHeaders.Add(raw);
                    var n = Norm(raw);
                    if (ecodeCol == 0 && (n == "ecode" || n == "empcode" || n == "employeecode")) ecodeCol = col;
                    else if (monthCol == 0 && n == "month") monthCol = col;
                    else if (elCol == 0 && (n == "elclosing" || n == "el")) elCol = col;
                    else if (clCol == 0 && (n == "clclosing" || n == "cl")) clCol = col;
                    else if (coCol == 0 && (n == "compooffclosing" || n == "compooff" || n == "compoff")) coCol = col;
                }

                if (ecodeCol == 0 || monthCol == 0)
                    return BadRequest(new
                    {
                        status = false,
                        message = "Could not find the required columns 'ECODE' and 'MONTH'. Found: " + string.Join(", ", foundHeaders)
                    });

                var lastRow = ws.LastRowUsed()?.RowNumber() ?? headerRowNum;
                using var c = Open();

                decimal? Dec(int col, int r)
                {
                    if (col == 0) return null;
                    var s = ws.Cell(r, col).GetString().Trim();
                    if (s == "") return null;
                    return decimal.TryParse(s, out var d) ? d : (decimal?)null;
                }

                for (int r = headerRowNum + 1; r <= lastRow; r++)
                {
                    string ecode = ws.Cell(r, ecodeCol).GetString().Trim();
                    string month = ws.Cell(r, monthCol).GetString().Trim();
                    if (ecode == "" && month == "") continue;
                    if (ecode == "" || month == "") { errors.Add($"Row {r}: ECODE and MONTH are both required."); continue; }

                    var el = Dec(elCol, r);
                    var cl = Dec(clCol, r);
                    var co = Dec(coCol, r);

                    // Upsert by (ECODE, MONTH): update existing rows, else insert. Never deletes.
                    using var up = new SqlCommand(@"
UPDATE dbo.EmpLeaveClosingBalance
   SET [EL Closing]=@el, [Cl Closing]=@cl, [CompoOff Closing]=@co
 WHERE LTRIM(RTRIM(ECODE))=@ecode AND LTRIM(RTRIM([MONTH]))=@month;
IF @@ROWCOUNT = 0
BEGIN
    INSERT INTO dbo.EmpLeaveClosingBalance (ECODE, [MONTH], [EL Closing], [Cl Closing], [CompoOff Closing])
    VALUES (@ecode, @month, @el, @cl, @co);
    SELECT 1;   -- inserted
END
ELSE SELECT 2;  -- updated", c);
                    up.Parameters.AddWithValue("@ecode", ecode);
                    up.Parameters.AddWithValue("@month", month);
                    up.Parameters.AddWithValue("@el", (object?)el ?? DBNull.Value);
                    up.Parameters.AddWithValue("@cl", (object?)cl ?? DBNull.Value);
                    up.Parameters.AddWithValue("@co", (object?)co ?? DBNull.Value);
                    var res = Convert.ToInt32(await up.ExecuteScalarAsync());
                    if (res == 1) inserted++;
                    else if (res == 2) updated++;
                    else skipped++;
                }
            }

            return Ok(new
            {
                status = true,
                message = $"Upload complete. Inserted {inserted}, updated {updated}.",
                inserted,
                updated,
                skipped,
                errors
            });
        }
    }
}
