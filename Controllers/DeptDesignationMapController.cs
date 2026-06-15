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
    /// Department + Sub-Department (levels 1/2/3) -> Designation mapping.
    /// Drives the designation dropdown and is managed via a UI page + Excel uploader.
    /// Pure ADO.NET (no DbContext changes). All inserts are additive; delete is a soft delete.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DeptDesignationMapController : ControllerBase
    {
        private readonly IConfiguration _config;
        public DeptDesignationMapController(IConfiguration config) { _config = config; }

        private SqlConnection Open()
        {
            var c = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
            c.Open();
            return c;
        }

        // ---------- Grid: all active mappings with names ----------
        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll()
        {
            const string sql = @"
SELECT m.Id, m.DepartmentId, d.DepartmentName,
       m.SubDepartmentId1, s1.SubDepartmentName AS SubDepartment1,
       m.SubDepartmentId2, s2.SubDepartmentName AS SubDepartment2,
       m.SubDepartmentId3, s3.SubDepartmentName AS SubDepartment3,
       m.DesignationId, dg.DesignationName, m.IsActive, m.CreatedBy, m.CreatedOn
FROM dbo.tblDeptSubDeptDesignationMap m
LEFT JOIN dbo.tblDepartment   d  ON d.DepartmentId   = m.DepartmentId
LEFT JOIN dbo.tblSubDepartment s1 ON s1.SubDepartmentId = m.SubDepartmentId1
LEFT JOIN dbo.tblSubDepartment s2 ON s2.SubDepartmentId = m.SubDepartmentId2
LEFT JOIN dbo.tblSubDepartment s3 ON s3.SubDepartmentId = m.SubDepartmentId3
LEFT JOIN dbo.tblDesignation  dg ON dg.DesignationId  = m.DesignationId
WHERE m.IsDeleted = 0
ORDER BY d.DepartmentName, s1.SubDepartmentName, dg.DesignationName;";
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

        // ---------- Designation dropdown driven by the mapping ----------
        [HttpGet("GetDesignations")]
        public async Task<IActionResult> GetDesignations(
            [FromQuery] int departmentId,
            [FromQuery] int? subDepartmentId1 = null,
            [FromQuery] int? subDepartmentId2 = null,
            [FromQuery] int? subDepartmentId3 = null)
        {
            const string sql = @"
SELECT DISTINCT dg.DesignationId, dg.DesignationName
FROM dbo.tblDeptSubDeptDesignationMap m
JOIN dbo.tblDesignation dg ON dg.DesignationId = m.DesignationId
WHERE m.IsDeleted = 0 AND m.IsActive = 1
  AND m.DepartmentId = @dept
  AND ((@s1 IS NULL) OR (m.SubDepartmentId1 = @s1) OR (m.SubDepartmentId1 IS NULL))
  AND ((@s2 IS NULL) OR (m.SubDepartmentId2 = @s2) OR (m.SubDepartmentId2 IS NULL))
  AND ((@s3 IS NULL) OR (m.SubDepartmentId3 = @s3) OR (m.SubDepartmentId3 IS NULL))
ORDER BY dg.DesignationName;";
            var data = new List<object>();
            using (var c = Open())
            using (var cmd = new SqlCommand(sql, c) { CommandTimeout = 60 })
            {
                cmd.Parameters.AddWithValue("@dept", departmentId);
                cmd.Parameters.AddWithValue("@s1", (object?)subDepartmentId1 ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@s2", (object?)subDepartmentId2 ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@s3", (object?)subDepartmentId3 ?? DBNull.Value);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    data.Add(new { designationId = r.GetInt32(0), designationName = r.GetString(1) });
            }
            return Ok(new { status = true, message = "Designations retrieved successfully", data });
        }

        // ---------- Add a single mapping (by ids) ----------
        public class MapDto
        {
            public int DepartmentId { get; set; }
            public int? SubDepartmentId1 { get; set; }
            public int? SubDepartmentId2 { get; set; }
            public int? SubDepartmentId3 { get; set; }
            public int DesignationId { get; set; }
            public string CreatedBy { get; set; }
        }

        [HttpPost("Add")]
        public async Task<IActionResult> Add([FromBody] MapDto dto)
        {
            if (dto == null || dto.DepartmentId <= 0 || dto.DesignationId <= 0)
                return BadRequest(new { status = false, message = "DepartmentId and DesignationId are required." });
            const string sql = @"
IF NOT EXISTS (SELECT 1 FROM dbo.tblDeptSubDeptDesignationMap
   WHERE IsDeleted=0 AND DepartmentId=@d AND DesignationId=@g
     AND ISNULL(SubDepartmentId1,0)=ISNULL(@s1,0) AND ISNULL(SubDepartmentId2,0)=ISNULL(@s2,0) AND ISNULL(SubDepartmentId3,0)=ISNULL(@s3,0))
INSERT INTO dbo.tblDeptSubDeptDesignationMap (DepartmentId, SubDepartmentId1, SubDepartmentId2, SubDepartmentId3, DesignationId, CreatedBy)
VALUES (@d,@s1,@s2,@s3,@g,@by);
SELECT @@ROWCOUNT;";
            using var c = Open();
            using var cmd = new SqlCommand(sql, c);
            cmd.Parameters.AddWithValue("@d", dto.DepartmentId);
            cmd.Parameters.AddWithValue("@s1", (object?)dto.SubDepartmentId1 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@s2", (object?)dto.SubDepartmentId2 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@s3", (object?)dto.SubDepartmentId3 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@g", dto.DesignationId);
            cmd.Parameters.AddWithValue("@by", (object?)dto.CreatedBy ?? DBNull.Value);
            var n = (int)await cmd.ExecuteScalarAsync();
            return Ok(new { status = true, message = n > 0 ? "Mapping added." : "Mapping already exists." });
        }

        // ---------- Soft delete a mapping ----------
        [HttpPost("Delete")]
        public async Task<IActionResult> Delete([FromQuery] int id)
        {
            using var c = Open();
            using var cmd = new SqlCommand("UPDATE dbo.tblDeptSubDeptDesignationMap SET IsDeleted=1, UpdatedOn=GETDATE() WHERE Id=@id AND IsDeleted=0", c);
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
            string[] headers = { "Department Name", "Sub-Department 1", "Sub-Department 2", "Sub-Department 3", "Designation Name" };
            for (int i = 0; i < headers.Length; i++) { var cell = ws.Cell(1, i + 1); cell.Value = headers[i]; cell.Style.Font.Bold = true; }
            ws.Cell(2, 1).Value = "RETAIL OPERATIONS";
            ws.Cell(2, 2).Value = "HUB OPS";
            ws.Cell(2, 5).Value = "DRIVER";
            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "DeptDesignationMap_Template.xlsx");
        }

        // ---------- Export current mappings to Excel (name columns; re-uploadable) ----------
        [HttpGet("Export")]
        public async Task<IActionResult> Export()
        {
            const string sql = @"
SELECT d.DepartmentName, s1.SubDepartmentName AS Sub1, s2.SubDepartmentName AS Sub2,
       s3.SubDepartmentName AS Sub3, dg.DesignationName,
       CASE WHEN m.IsActive=1 THEN 'Active' ELSE 'Inactive' END AS Status
FROM dbo.tblDeptSubDeptDesignationMap m
LEFT JOIN dbo.tblDepartment   d  ON d.DepartmentId   = m.DepartmentId
LEFT JOIN dbo.tblSubDepartment s1 ON s1.SubDepartmentId = m.SubDepartmentId1
LEFT JOIN dbo.tblSubDepartment s2 ON s2.SubDepartmentId = m.SubDepartmentId2
LEFT JOIN dbo.tblSubDepartment s3 ON s3.SubDepartmentId = m.SubDepartmentId3
LEFT JOIN dbo.tblDesignation  dg ON dg.DesignationId  = m.DesignationId
WHERE m.IsDeleted = 0
ORDER BY d.DepartmentName, s1.SubDepartmentName, dg.DesignationName;";

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Mapping");
            string[] headers = { "Department Name", "Sub-Department 1", "Sub-Department 2", "Sub-Department 3", "Designation Name", "Status" };
            for (int i = 0; i < headers.Length; i++) { var cell = ws.Cell(1, i + 1); cell.Value = headers[i]; cell.Style.Font.Bold = true; }

            int r = 2;
            using (var c = Open())
            using (var cmd = new SqlCommand(sql, c) { CommandTimeout = 120 })
            using (var rd = await cmd.ExecuteReaderAsync())
            {
                while (await rd.ReadAsync())
                {
                    for (int i = 0; i < 6; i++)
                        ws.Cell(r, i + 1).Value = rd.IsDBNull(i) ? "" : rd.GetValue(i)?.ToString();
                    r++;
                }
            }
            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            var stamp = DateTime.Today.ToString("yyyy-MM-dd");
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"DeptDesignationMap_{stamp}.xlsx");
        }

        // ---------- Upload Excel (names -> ids) ----------
        [HttpPost("Upload")]
        public async Task<IActionResult> Upload(IFormFile file, [FromQuery] string uploadedBy = null)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { status = false, message = "No file uploaded." });

            var inserted = 0; var skipped = 0; var createdDesignations = 0; var errors = new List<string>();
            using var c = Open();

            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            stream.Position = 0;
            using var wb = new XLWorkbook(stream);
            var ws = wb.Worksheet(1);
            var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;

            for (int r = 2; r <= lastRow; r++)
            {
                string deptName = ws.Cell(r, 1).GetString().Trim();
                string sub1 = ws.Cell(r, 2).GetString().Trim();
                string sub2 = ws.Cell(r, 3).GetString().Trim();
                string sub3 = ws.Cell(r, 4).GetString().Trim();
                string desigName = ws.Cell(r, 5).GetString().Trim();
                if (deptName == "" && desigName == "" && sub1 == "" && sub2 == "" && sub3 == "") continue;

                if (deptName == "" || desigName == "") { errors.Add($"Row {r}: Department and Designation are required."); continue; }

                int? deptId = await ScalarIntAsync(c, "SELECT TOP 1 DepartmentId FROM dbo.tblDepartment WHERE LTRIM(RTRIM(DepartmentName))=@n AND ISNULL(isDeleted,0)=0", ("@n", deptName));
                if (deptId == null) { errors.Add($"Row {r}: Department '{deptName}' not found."); continue; }

                int? s1 = null, s2 = null, s3 = null;
                if (sub1 != "")
                {
                    s1 = await ScalarIntAsync(c, "SELECT TOP 1 SubDepartmentId FROM dbo.tblSubDepartment WHERE LTRIM(RTRIM(SubDepartmentName))=@n AND DepartmentId=@d AND DepthLevel=1 AND ISNULL(isDeleted,0)=0", ("@n", sub1), ("@d", deptId.Value));
                    if (s1 == null) { errors.Add($"Row {r}: Sub-Department 1 '{sub1}' not found under '{deptName}'."); continue; }
                }
                if (sub2 != "")
                {
                    s2 = await ScalarIntAsync(c, "SELECT TOP 1 SubDepartmentId FROM dbo.tblSubDepartment WHERE LTRIM(RTRIM(SubDepartmentName))=@n AND ParentSubDepartmentId=@p AND DepthLevel=2 AND ISNULL(isDeleted,0)=0", ("@n", sub2), ("@p", (object?)s1 ?? DBNull.Value));
                    if (s2 == null) { errors.Add($"Row {r}: Sub-Department 2 '{sub2}' not found under '{sub1}'."); continue; }
                }
                if (sub3 != "")
                {
                    s3 = await ScalarIntAsync(c, "SELECT TOP 1 SubDepartmentId FROM dbo.tblSubDepartment WHERE LTRIM(RTRIM(SubDepartmentName))=@n AND ParentSubDepartmentId=@p AND DepthLevel=3 AND ISNULL(isDeleted,0)=0", ("@n", sub3), ("@p", (object?)s2 ?? DBNull.Value));
                    if (s3 == null) { errors.Add($"Row {r}: Sub-Department 3 '{sub3}' not found under '{sub2}'."); continue; }
                }

                int? desigId = await ScalarIntAsync(c, "SELECT TOP 1 DesignationId FROM dbo.tblDesignation WHERE LTRIM(RTRIM(DesignationName))=@n AND ISNULL(isDeleted,0)=0", ("@n", desigName));
                if (desigId == null)
                {
                    // Designation not found -> create a new ACTIVE designation and use it.
                    // Set DesignationCode = the new DesignationId so it is never blank (blank codes break
                    // downstream seat-number generation in BGT Seat Master, which uses the designation code).
                    desigId = await ScalarIntAsync(c,
                        @"INSERT INTO dbo.tblDesignation (DesignationName, isActive, isDeleted, CreatedOn) VALUES (@n, 1, 0, GETDATE());
                          DECLARE @newId INT = CAST(SCOPE_IDENTITY() AS INT);
                          UPDATE dbo.tblDesignation SET DesignationCode = CAST(@newId AS varchar(20)) WHERE DesignationId = @newId;
                          SELECT @newId;",
                        ("@n", desigName));
                    if (desigId == null) { errors.Add($"Row {r}: could not create designation '{desigName}'."); continue; }
                    createdDesignations++;
                }

                using var ins = new SqlCommand(@"
IF NOT EXISTS (SELECT 1 FROM dbo.tblDeptSubDeptDesignationMap
   WHERE IsDeleted=0 AND DepartmentId=@d AND DesignationId=@g
     AND ISNULL(SubDepartmentId1,0)=ISNULL(@s1,0) AND ISNULL(SubDepartmentId2,0)=ISNULL(@s2,0) AND ISNULL(SubDepartmentId3,0)=ISNULL(@s3,0))
BEGIN
   INSERT INTO dbo.tblDeptSubDeptDesignationMap (DepartmentId, SubDepartmentId1, SubDepartmentId2, SubDepartmentId3, DesignationId, CreatedBy)
   VALUES (@d,@s1,@s2,@s3,@g,@by);
   SELECT 1;
END
ELSE SELECT 0;", c);
                ins.Parameters.AddWithValue("@d", deptId.Value);
                ins.Parameters.AddWithValue("@s1", (object?)s1 ?? DBNull.Value);
                ins.Parameters.AddWithValue("@s2", (object?)s2 ?? DBNull.Value);
                ins.Parameters.AddWithValue("@s3", (object?)s3 ?? DBNull.Value);
                ins.Parameters.AddWithValue("@g", desigId.Value);
                ins.Parameters.AddWithValue("@by", (object?)uploadedBy ?? DBNull.Value);
                var added = (int)await ins.ExecuteScalarAsync();
                if (added == 1) inserted++; else skipped++;
            }

            return Ok(new { status = true, message = $"Upload complete. Inserted {inserted}, skipped {skipped} (duplicates), created {createdDesignations} new designation(s).", inserted, skipped, createdDesignations, errors });
        }

        private static async Task<int?> ScalarIntAsync(SqlConnection c, string sql, params (string, object)[] ps)
        {
            using var cmd = new SqlCommand(sql, c);
            foreach (var (k, v) in ps) cmd.Parameters.AddWithValue(k, v);
            var o = await cmd.ExecuteScalarAsync();
            return (o == null || o == DBNull.Value) ? (int?)null : Convert.ToInt32(o);
        }
    }
}
