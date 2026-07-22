using ClosedXML.Excel;
using HRMSAPI.Extension;
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
    /// Store (STCode) -> State mapping manager (dbo.tblLocation.StateId).
    /// Two dropdowns on the UI: STCode + State (names from dbo.tblState); selecting a
    /// state for a store UPDATES tblLocation.StateId (the StateId is resolved from
    /// tblState). Plus Excel upload (bulk STCode->State) and export.
    ///
    /// Self-contained ADO.NET (no DbContext changes). UPDATE-only on existing stores —
    /// NEVER inserts/deletes stores, never truncates. tblLocation is system-versioned,
    /// so every StateId change is captured in history automatically.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [RequirePageAccess("/master/store-state-mapping")]
    public class LocationStateMapController : ControllerBase
    {
        private readonly IConfiguration _config;
        public LocationStateMapController(IConfiguration config) { _config = config; }

        private SqlConnection Open()
        {
            var c = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
            c.Open();
            return c;
        }

        public class UpdateStateDto
        {
            public string? STCode { get; set; }
            public int? StateId { get; set; }
        }

        // ---------- Grid: STCode + Store Name + current State ----------
        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll([FromQuery] string? search = null)
        {
            search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
            const string sql = @"
SELECT l.LocationId, LTRIM(RTRIM(l.STCode)) AS STCode, l.LocationName, l.StateId, s.StateName
FROM dbo.tblLocation l
LEFT JOIN dbo.tblState s ON s.StateId = l.StateId
WHERE (@search IS NULL
       OR l.STCode LIKE '%' + @search + '%'
       OR l.LocationName LIKE '%' + @search + '%'
       OR s.StateName LIKE '%' + @search + '%')
ORDER BY LTRIM(RTRIM(l.STCode));";

            var rows = new List<Dictionary<string, object>>();
            using var c = Open();
            using var cmd = new SqlCommand(sql, c) { CommandTimeout = 120 };
            cmd.Parameters.AddWithValue("@search", (object?)search ?? DBNull.Value);
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var row = new Dictionary<string, object>();
                for (int i = 0; i < r.FieldCount; i++)
                    row[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
                rows.Add(row);
            }
            return Ok(new { status = true, data = rows, total = rows.Count });
        }

        // ---------- STCode dropdown source ----------
        [HttpGet("GetStores")]
        public async Task<IActionResult> GetStores()
        {
            const string sql = @"
SELECT LTRIM(RTRIM(STCode)) AS STCode, LocationName, StateId
FROM dbo.tblLocation
WHERE STCode IS NOT NULL AND LTRIM(RTRIM(STCode)) <> ''
ORDER BY LTRIM(RTRIM(STCode));";
            var rows = new List<Dictionary<string, object>>();
            using var c = Open();
            using var cmd = new SqlCommand(sql, c) { CommandTimeout = 120 };
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var row = new Dictionary<string, object>();
                for (int i = 0; i < r.FieldCount; i++)
                    row[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
                rows.Add(row);
            }
            return Ok(new { status = true, data = rows });
        }

        // ---------- State dropdown source (names + ids from tblState) ----------
        [HttpGet("GetStates")]
        public async Task<IActionResult> GetStates()
        {
            const string sql = @"SELECT StateId, StateName FROM dbo.tblState
                                 WHERE StateName IS NOT NULL AND LTRIM(RTRIM(StateName)) <> ''
                                 ORDER BY StateName;";
            var rows = new List<Dictionary<string, object>>();
            using var c = Open();
            using var cmd = new SqlCommand(sql, c) { CommandTimeout = 120 };
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                rows.Add(new Dictionary<string, object>
                {
                    ["StateId"] = r.GetInt32(0),
                    ["StateName"] = r.IsDBNull(1) ? null : r.GetString(1)
                });
            return Ok(new { status = true, data = rows });
        }

        // ---------- Update one store's State ----------
        [HttpPost("UpdateState")]
        public async Task<IActionResult> UpdateState([FromBody] UpdateStateDto dto)
        {
            var stcode = dto?.STCode?.Trim();
            if (string.IsNullOrEmpty(stcode))
                return BadRequest(new { status = false, message = "STCode is required." });
            if (dto?.StateId == null)
                return BadRequest(new { status = false, message = "State is required." });

            using var c = Open();

            // StateId must exist in tblState.
            using (var chk = new SqlCommand("SELECT COUNT(1) FROM dbo.tblState WHERE StateId=@sid;", c))
            {
                chk.Parameters.AddWithValue("@sid", dto.StateId.Value);
                if (Convert.ToInt32(await chk.ExecuteScalarAsync()) == 0)
                    return BadRequest(new { status = false, message = "Selected state does not exist." });
            }

            using var up = new SqlCommand(
                "UPDATE dbo.tblLocation SET StateId=@sid WHERE LTRIM(RTRIM(STCode))=@st;", c)
            { CommandTimeout = 120 };
            up.Parameters.AddWithValue("@sid", dto.StateId.Value);
            up.Parameters.AddWithValue("@st", stcode);
            var n = await up.ExecuteNonQueryAsync();
            if (n == 0)
                return BadRequest(new { status = false, message = $"STCode '{stcode}' not found." });

            return Ok(new { status = true, message = $"State updated for {stcode}.", updated = n });
        }

        // ---------- Excel template (STCode, StateName) ----------
        [HttpGet("DownloadTemplate")]
        public IActionResult DownloadTemplate()
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("StoreState");
            string[] headers = { "STCode", "StateName" };
            for (int i = 0; i < headers.Length; i++) { var cell = ws.Cell(1, i + 1); cell.Value = headers[i]; cell.Style.Font.Bold = true; }
            ws.Cell(2, 1).Value = "RH01"; ws.Cell(2, 2).Value = "ARA";
            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "StoreState_Template.xlsx");
        }

        // ---------- Export current mapping ----------
        [HttpGet("Export")]
        public async Task<IActionResult> Export([FromQuery] string? search = null)
        {
            search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
            const string sql = @"
SELECT LTRIM(RTRIM(l.STCode)) AS STCode, l.LocationName, s.StateName
FROM dbo.tblLocation l
LEFT JOIN dbo.tblState s ON s.StateId = l.StateId
WHERE (@search IS NULL OR l.STCode LIKE '%'+@search+'%' OR l.LocationName LIKE '%'+@search+'%' OR s.StateName LIKE '%'+@search+'%')
ORDER BY LTRIM(RTRIM(l.STCode));";

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("StoreState");
            string[] headers = { "STCode", "Store Name", "StateName" };
            for (int i = 0; i < headers.Length; i++) { var cell = ws.Cell(1, i + 1); cell.Value = headers[i]; cell.Style.Font.Bold = true; }
            int rr = 2;
            using (var c = Open())
            using (var cmd = new SqlCommand(sql, c) { CommandTimeout = 300 })
            {
                cmd.Parameters.AddWithValue("@search", (object?)search ?? DBNull.Value);
                using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    ws.Cell(rr, 1).Value = rd.IsDBNull(0) ? "" : rd.GetValue(0)?.ToString();
                    ws.Cell(rr, 2).Value = rd.IsDBNull(1) ? "" : rd.GetValue(1)?.ToString();
                    ws.Cell(rr, 3).Value = rd.IsDBNull(2) ? "" : rd.GetValue(2)?.ToString();
                    rr++;
                }
            }
            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            var stamp = DateTime.Today.ToString("yyyy-MM-dd");
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"StoreState_{stamp}.xlsx");
        }

        // ---------- Upload Excel -> update StateId by STCode (StateName resolved via tblState) ----------
        [HttpPost("Upload")]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { status = false, message = "No file uploaded." });

            var updated = 0; var skipped = 0; var errors = new List<string>();

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
                if (headerRow == null) return BadRequest(new { status = false, message = "The sheet is empty." });
                int headerRowNum = headerRow.RowNumber();
                int lastCol = headerRow.LastCellUsed()?.Address.ColumnNumber ?? 1;

                int stCol = 0, stateCol = 0;
                string Norm(string s) => new string((s ?? "").ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
                for (int col = 1; col <= lastCol; col++)
                {
                    var n = Norm(ws.Cell(headerRowNum, col).GetString());
                    if (stCol == 0 && (n == "stcode" || n == "storecode")) stCol = col;
                    else if (stateCol == 0 && (n == "statename" || n == "state")) stateCol = col;
                }
                if (stCol == 0 || stateCol == 0)
                    return BadRequest(new { status = false, message = "Could not find required columns 'STCode' and 'StateName'." });

                using var c = Open();

                // Build a case-insensitive StateName -> StateId lookup.
                var stateMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                using (var sc = new SqlCommand("SELECT StateId, StateName FROM dbo.tblState WHERE StateName IS NOT NULL;", c))
                using (var sr = await sc.ExecuteReaderAsync())
                    while (await sr.ReadAsync())
                    {
                        var nm = sr.IsDBNull(1) ? "" : sr.GetString(1).Trim();
                        if (nm != "" && !stateMap.ContainsKey(nm)) stateMap[nm] = sr.GetInt32(0);
                    }

                var lastRow = ws.LastRowUsed()?.RowNumber() ?? headerRowNum;
                for (int rw = headerRowNum + 1; rw <= lastRow; rw++)
                {
                    var stcode = ws.Cell(rw, stCol).GetString().Trim();
                    var stateName = ws.Cell(rw, stateCol).GetString().Trim();
                    if (stcode == "" && stateName == "") continue;
                    if (stcode == "" || stateName == "") { errors.Add($"Row {rw}: STCode and StateName are both required."); skipped++; continue; }
                    if (!stateMap.TryGetValue(stateName, out var sid)) { errors.Add($"Row {rw}: state '{stateName}' not found in tblState."); skipped++; continue; }

                    using var up = new SqlCommand("UPDATE dbo.tblLocation SET StateId=@sid WHERE LTRIM(RTRIM(STCode))=@st;", c);
                    up.Parameters.AddWithValue("@sid", sid);
                    up.Parameters.AddWithValue("@st", stcode);
                    var n = await up.ExecuteNonQueryAsync();
                    if (n > 0) updated += n; else { errors.Add($"Row {rw}: STCode '{stcode}' not found."); skipped++; }
                }
            }

            return Ok(new { status = true, message = $"Upload complete. Updated {updated}, skipped {skipped}.", updated, skipped, errors });
        }
    }
}
