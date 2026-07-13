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
    /// Biomax attendance DEVICE LOCATION -> store ST-CODE mapping.
    /// Maps the biometric device/location label (Biomax export "Device Name") to a store ST-CODE.
    /// Managed via a UI page + Excel uploader (list / add / edit / delete).
    /// Pure ADO.NET (no DbContext changes). Inserts/updates are upserts; delete is a soft delete.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class BiomaxAttendanceLocationMapController : ControllerBase
    {
        private readonly IConfiguration _config;
        public BiomaxAttendanceLocationMapController(IConfiguration config) { _config = config; }

        private SqlConnection Open()
        {
            var c = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
            c.Open();
            return c;
        }

        public class BiomaxMapDto
        {
            public int Id { get; set; }
            public string? DeviceLocation { get; set; }
            public string? STCode { get; set; }
            public string? CreatedBy { get; set; }
        }

        // ---------- Grid: all active mappings ----------
        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll()
        {
            const string sql = @"
SELECT m.Id, m.DeviceLocation, m.STCode, m.IsActive, m.CreatedBy, m.CreatedOn, m.UpdatedBy, m.UpdatedOn
FROM dbo.tblBiomaxAttendanceLocationMap m
WHERE m.IsDeleted = 0
ORDER BY m.STCode, m.DeviceLocation;";
            var rows = new List<Dictionary<string, object>>();
            using (var c = Open())
            using (var cmd = new SqlCommand(sql, c) { CommandTimeout = 120 })
            using (var r = await cmd.ExecuteReaderAsync())
            {
                while (await r.ReadAsync())
                {
                    var row = new Dictionary<string, object>();
                    for (int i = 0; i < r.FieldCount; i++)
                        row[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
                    rows.Add(row);
                }
            }
            return Ok(new { status = true, data = rows });
        }

        // ---------- Add a single mapping (upsert by DeviceLocation) ----------
        [HttpPost("Add")]
        public async Task<IActionResult> Add([FromBody] BiomaxMapDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.DeviceLocation) || string.IsNullOrWhiteSpace(dto.STCode))
                return BadRequest(new { status = false, message = "Device Location and ST Code are required." });

            var device = dto.DeviceLocation.Trim();
            var st = dto.STCode.Trim();

            const string sql = @"
IF EXISTS (SELECT 1 FROM dbo.tblBiomaxAttendanceLocationMap WHERE IsDeleted=0 AND LTRIM(RTRIM(DeviceLocation))=@dev)
BEGIN
    UPDATE dbo.tblBiomaxAttendanceLocationMap
       SET STCode=@st, UpdatedBy=@by, UpdatedOn=GETDATE()
     WHERE IsDeleted=0 AND LTRIM(RTRIM(DeviceLocation))=@dev;
    SELECT 2;   -- updated
END
ELSE
BEGIN
    INSERT INTO dbo.tblBiomaxAttendanceLocationMap (DeviceLocation, STCode, CreatedBy)
    VALUES (@dev, @st, @by);
    SELECT 1;   -- inserted
END";
            using var c = Open();
            using var cmd = new SqlCommand(sql, c);
            cmd.Parameters.AddWithValue("@dev", device);
            cmd.Parameters.AddWithValue("@st", st);
            cmd.Parameters.AddWithValue("@by", (object?)dto.CreatedBy ?? DBNull.Value);
            var n = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return Ok(new { status = true, message = n == 2 ? "Mapping updated." : "Mapping added." });
        }

        // ---------- Edit an existing mapping ----------
        [HttpPost("Update")]
        public async Task<IActionResult> Update([FromBody] BiomaxMapDto dto)
        {
            if (dto == null || dto.Id <= 0 || string.IsNullOrWhiteSpace(dto.DeviceLocation) || string.IsNullOrWhiteSpace(dto.STCode))
                return BadRequest(new { status = false, message = "Id, Device Location and ST Code are required." });

            var device = dto.DeviceLocation.Trim();
            var st = dto.STCode.Trim();

            // Block duplicate device location on a different active row.
            const string dupSql = @"SELECT COUNT(1) FROM dbo.tblBiomaxAttendanceLocationMap
                                    WHERE IsDeleted=0 AND LTRIM(RTRIM(DeviceLocation))=@dev AND Id<>@id";
            using var c = Open();
            using (var dup = new SqlCommand(dupSql, c))
            {
                dup.Parameters.AddWithValue("@dev", device);
                dup.Parameters.AddWithValue("@id", dto.Id);
                if (Convert.ToInt32(await dup.ExecuteScalarAsync()) > 0)
                    return Ok(new { status = false, message = $"Another mapping already exists for device '{device}'." });
            }

            const string sql = @"
UPDATE dbo.tblBiomaxAttendanceLocationMap
   SET DeviceLocation=@dev, STCode=@st, UpdatedBy=@by, UpdatedOn=GETDATE()
 WHERE Id=@id AND IsDeleted=0;
SELECT @@ROWCOUNT;";
            using var cmd = new SqlCommand(sql, c);
            cmd.Parameters.AddWithValue("@dev", device);
            cmd.Parameters.AddWithValue("@st", st);
            cmd.Parameters.AddWithValue("@by", (object?)dto.CreatedBy ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@id", dto.Id);
            var n = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return Ok(new { status = n > 0, message = n > 0 ? "Mapping updated." : "Mapping not found." });
        }

        // ---------- Soft delete a mapping ----------
        [HttpPost("Delete")]
        public async Task<IActionResult> Delete([FromQuery] int id)
        {
            using var c = Open();
            using var cmd = new SqlCommand("UPDATE dbo.tblBiomaxAttendanceLocationMap SET IsDeleted=1, UpdatedOn=GETDATE() WHERE Id=@id AND IsDeleted=0", c);
            cmd.Parameters.AddWithValue("@id", id);
            var n = await cmd.ExecuteNonQueryAsync();
            return Ok(new { status = n > 0, message = n > 0 ? "Mapping removed." : "Mapping not found." });
        }

        // ---------- Download Excel template ----------
        [HttpGet("DownloadTemplate")]
        public IActionResult DownloadTemplate()
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Mapping");
            string[] headers = { "Device Location", "ST Code" };
            for (int i = 0; i < headers.Length; i++) { var cell = ws.Cell(1, i + 1); cell.Value = headers[i]; cell.Style.Font.Bold = true; }
            ws.Cell(2, 1).Value = "Hub patna";
            ws.Cell(2, 2).Value = "DB03";
            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "BiomaxAttendanceLocationMap_Template.xlsx");
        }

        // ---------- Export current mappings to Excel (re-uploadable) ----------
        [HttpGet("Export")]
        public async Task<IActionResult> Export()
        {
            const string sql = @"
SELECT m.DeviceLocation, m.STCode, CASE WHEN m.IsActive=1 THEN 'Active' ELSE 'Inactive' END AS Status
FROM dbo.tblBiomaxAttendanceLocationMap m
WHERE m.IsDeleted = 0
ORDER BY m.STCode, m.DeviceLocation;";

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Mapping");
            string[] headers = { "Device Location", "ST Code", "Status" };
            for (int i = 0; i < headers.Length; i++) { var cell = ws.Cell(1, i + 1); cell.Value = headers[i]; cell.Style.Font.Bold = true; }

            int r = 2;
            using (var c = Open())
            using (var cmd = new SqlCommand(sql, c) { CommandTimeout = 120 })
            using (var rd = await cmd.ExecuteReaderAsync())
            {
                while (await rd.ReadAsync())
                {
                    for (int i = 0; i < 3; i++)
                        ws.Cell(r, i + 1).Value = rd.IsDBNull(i) ? "" : rd.GetValue(i)?.ToString();
                    r++;
                }
            }
            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            var stamp = DateTime.Today.ToString("yyyy-MM-dd");
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"BiomaxAttendanceLocationMap_{stamp}.xlsx");
        }

        // ---------- Upload Excel (Device Location + ST Code -> upsert) ----------
        // Robustly finds the two columns by header, so the raw Biomax export
        // ("Device Name" + "ST-CODE", with extra columns) works as-is.
        [HttpPost("Upload")]
        public async Task<IActionResult> Upload(IFormFile file, [FromQuery] string uploadedBy = null)
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

                // Detect columns by header name.
                int deviceCol = 0, stCol = 0;
                var foundHeaders = new List<string>();
                for (int col = 1; col <= lastCol; col++)
                {
                    var raw = ws.Cell(headerRowNum, col).GetString().Trim();
                    if (raw == "") continue;
                    foundHeaders.Add(raw);
                    var norm = new string(raw.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
                    if (deviceCol == 0 && (norm == "devicelocation" || norm == "devicename" || norm == "device"))
                        deviceCol = col;
                    else if (stCol == 0 && (norm == "stcode" || norm == "st"))
                        stCol = col;
                }

                if (deviceCol == 0 || stCol == 0)
                    return BadRequest(new
                    {
                        status = false,
                        message = "Could not find the required columns. Expected a device column ('Device Location' or 'Device Name') and 'ST Code' / 'ST-CODE'. Found: " + string.Join(", ", foundHeaders)
                    });

                var lastRow = ws.LastRowUsed()?.RowNumber() ?? headerRowNum;
                using var c = Open();

                for (int r = headerRowNum + 1; r <= lastRow; r++)
                {
                    string device = ws.Cell(r, deviceCol).GetString().Trim();
                    string st = ws.Cell(r, stCol).GetString().Trim();
                    if (device == "" && st == "") continue;
                    if (device == "" || st == "") { errors.Add($"Row {r}: Device Location and ST Code are both required."); continue; }

                    using var up = new SqlCommand(@"
IF EXISTS (SELECT 1 FROM dbo.tblBiomaxAttendanceLocationMap WHERE IsDeleted=0 AND LTRIM(RTRIM(DeviceLocation))=@dev)
BEGIN
    UPDATE dbo.tblBiomaxAttendanceLocationMap
       SET STCode=@st, UpdatedBy=@by, UpdatedOn=GETDATE()
     WHERE IsDeleted=0 AND LTRIM(RTRIM(DeviceLocation))=@dev AND STCode<>@st;
    SELECT CASE WHEN @@ROWCOUNT>0 THEN 2 ELSE 0 END;   -- 2=updated, 0=unchanged
END
ELSE
BEGIN
    INSERT INTO dbo.tblBiomaxAttendanceLocationMap (DeviceLocation, STCode, CreatedBy)
    VALUES (@dev, @st, @by);
    SELECT 1;   -- inserted
END", c);
                    up.Parameters.AddWithValue("@dev", device);
                    up.Parameters.AddWithValue("@st", st);
                    up.Parameters.AddWithValue("@by", (object?)uploadedBy ?? DBNull.Value);
                    var res = Convert.ToInt32(await up.ExecuteScalarAsync());
                    if (res == 1) inserted++;
                    else if (res == 2) updated++;
                    else skipped++;
                }
            }

            return Ok(new
            {
                status = true,
                message = $"Upload complete. Inserted {inserted}, updated {updated}, unchanged {skipped}.",
                inserted,
                updated,
                skipped,
                errors
            });
        }
    }
}
