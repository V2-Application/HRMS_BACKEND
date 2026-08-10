using ClosedXML.Excel;
using HRMSAPI.Extension;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace HRMSAPI.Controllers
{
    /// <summary>
    /// Official Visit: an employee applies for an official visit over a date range; the request
    /// goes to their reporting manager for Approve/Reject. Separately, IT Superadmin has an admin
    /// page (list + Excel uploader + export) -- rows HR uploads there are auto-approved, no
    /// manager step. Self-contained ADO.NET (no DbContext changes), mirroring
    /// LeaveClosingBalanceController's uploader shape and AccessWindowControllers' date-range +
    /// custom-dates filter shape. Purely additive: no DELETE/TRUNCATE/DROP/UPDATE of any other
    /// table anywhere in this controller.
    ///
    /// Restricted to IT Superadmin only (2026-08-10): every action, including apply/my-requests,
    /// now carries [RequirePageAccess] on top of [Authorize] -- matching the RBAC grants, which
    /// were pulled from every other role so the whole module is IT-Superadmin-only for now.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OfficialVisitController : ControllerBase
    {
        private readonly IConfiguration _config;
        public OfficialVisitController(IConfiguration config) { _config = config; }

        private SqlConnection Open()
        {
            var c = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
            c.Open();
            return c;
        }

        private static class Statuses
        {
            public const int Approved = 1;
            public const int Rejected = 2;
            public const int Pending = 4;
        }

        private static class SourceTypes
        {
            public const int SelfApply = 1;
            public const int HrUpload = 2;
        }

        private (long employeeId, string role) CurrentUser()
        {
            var identity = HttpContext.User.Identity as ClaimsIdentity;
            var uc = AuthenticUserDetails.GetCurrentUserDetails(identity);
            long.TryParse(uc?.EmployeeId, out var employeeId);
            return (employeeId, uc?.role ?? string.Empty);
        }

        private static bool IsAdminRole(string role)
        {
            var r = (role ?? string.Empty).Replace(" ", string.Empty);
            return string.Equals(r, "ITSuperadmin", StringComparison.OrdinalIgnoreCase)
                || string.Equals(r, "SuperAdmin", StringComparison.OrdinalIgnoreCase)
                || string.Equals(r, "Master", StringComparison.OrdinalIgnoreCase)
                || string.Equals(r, "HR", StringComparison.OrdinalIgnoreCase);
        }

        // Shared row projection used by my-requests / pending-for-manager / GetAll / Export.
        // Joins Department/SubDepartment1-3/Designation/BaseLocation live from tblEmployee (never
        // stored on the request row), and VisitLocation's name live from tblLocation off the
        // stored VisitStoreCode -- same non-duplication principle used in
        // usp_GetGeoAttendanceByRange's Department/SubDept join (2026-07-29).
        private const string SelectWithJoins = @"
SELECT
    v.OfficialVisitRequestId, v.EmployeeId, v.Ecode, v.EmployeeName,
    v.FromDate, v.ToDate, v.NoOfDays, v.Purpose,
    v.VisitStoreCode, visitLoc.LocationName AS VisitLocationName,
    v.EmployeeRemarks, v.RecommendedByEcode, v.RecommendedByName,
    v.ReportingManagerId, v.ManagerApprovalStatusId, v.ManagerApproverId, v.ManagerApprovalOn, v.ManagerRemarks,
    v.SourceTypeId,
    v.CreatedBy, v.CreatedOn, v.LastUpdatedBy, v.UpdatedOn,
    d.DepartmentName, des.DesignationName,
    sd1.SubDepartmentName AS SubDepartment1, sd2.SubDepartmentName AS SubDepartment2, sd3.SubDepartmentName AS SubDepartment3,
    baseLoc.STCode AS BaseStoreCode, baseLoc.LocationName AS BaseLocationName,
    mgr.Ecode AS ManagerEcode,
    COALESCE(mgr.[FULL NAME], NULLIF(LTRIM(RTRIM(ISNULL(mgr.FirstName,N'')+N' '+ISNULL(mgr.LastName,N''))),N'')) AS ManagerName
FROM dbo.tblOfficialVisitRequest v
LEFT JOIN dbo.tblEmployee e   ON e.EmployeeId = v.EmployeeId
LEFT JOIN dbo.tblDepartment d ON d.DepartmentId = e.DepartmentId
LEFT JOIN dbo.tblDesignation des ON des.DesignationId = e.DesignationId
LEFT JOIN dbo.tblSubDepartment sd1 ON sd1.SubDepartmentId = e.SubDepartmentId1
LEFT JOIN dbo.tblSubDepartment sd2 ON sd2.SubDepartmentId = e.SubDepartmentId2
LEFT JOIN dbo.tblSubDepartment sd3 ON sd3.SubDepartmentId = e.SubDepartmentId3
LEFT JOIN dbo.tblLocation baseLoc  ON baseLoc.LocationId = e.LocationId
LEFT JOIN dbo.tblLocation visitLoc ON visitLoc.STCode = v.VisitStoreCode
LEFT JOIN dbo.tblEmployee mgr ON mgr.EmployeeId = v.ReportingManagerId
";

        private static Dictionary<string, object> ReadRow(SqlDataReader r)
        {
            var row = new Dictionary<string, object>();
            for (int i = 0; i < r.FieldCount; i++)
                row[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
            row["sourceLabel"] = Convert.ToInt32(row["SourceTypeId"]) == SourceTypes.HrUpload
                ? "Uploaded by HR" : "Requested by User";
            return row;
        }

        public class CreateOfficialVisitRequestDto
        {
            public DateTime FromDate { get; set; }
            public DateTime ToDate { get; set; }
            public string? Purpose { get; set; }
            public string? VisitStoreCode { get; set; }
            public string? EmployeeRemarks { get; set; }
            public string? RecommendedByEcode { get; set; }
        }

        public class UpdateOfficialVisitStatusDto
        {
            public int StatusId { get; set; } // Statuses.Approved / Statuses.Rejected
            public string? Remarks { get; set; }
        }

        public class OfficialVisitExportFilterDto
        {
            public List<string> Ecodes { get; set; } = new();
            public bool ApplyAll { get; set; }
            public string? FromDate { get; set; }
            public string? ToDate { get; set; }
            public List<string> CustomDates { get; set; } = new();
        }

        // ---------------------------------------------------------------
        // Self-service (restricted to IT Superadmin only, 2026-08-10)
        // ---------------------------------------------------------------

        [RequirePageAccess("/official-visit")]
        [HttpGet("stores")]
        public async Task<IActionResult> Stores()
        {
            const string sql = @"
SELECT STCode, MAX(LocationName) AS LocationName
FROM dbo.tblLocation
WHERE ISNULL(IsDeleted,0)=0 AND STCode IS NOT NULL AND LTRIM(RTRIM(STCode))<>''
GROUP BY STCode ORDER BY STCode;";
            var rows = new List<Dictionary<string, object>>();
            using var conn = Open();
            using var cmd = new SqlCommand(sql, conn);
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                rows.Add(new Dictionary<string, object>
                {
                    ["storeCode"] = r["STCode"]?.ToString(),
                    ["locationName"] = r["LocationName"] is DBNull ? null : r["LocationName"]?.ToString()
                });
            return Ok(new { status = true, data = rows });
        }

        [RequirePageAccess("/official-visit")]
        [HttpPost("apply")]
        public async Task<IActionResult> Apply([FromBody] CreateOfficialVisitRequestDto dto)
        {
            var (employeeId, _) = CurrentUser();
            if (employeeId == 0) return Unauthorized(new { status = false, message = "Invalid or missing authentication." });
            if (dto == null) return BadRequest(new { status = false, message = "Invalid request." });
            if (dto.ToDate.Date < dto.FromDate.Date) return BadRequest(new { status = false, message = "To date is before From date." });

            using var conn = Open();

            // Resolve applicant Ecode/Name + reporting manager, same two-hop lookup as
            // EmpAttendanceService's regularization-request flow (ReportHeadEcode -> manager EmployeeId).
            string? ecode = null, employeeName = null, reportHeadEcode = null;
            using (var cmd = new SqlCommand(@"
SELECT Ecode,
       COALESCE([FULL NAME], NULLIF(LTRIM(RTRIM(ISNULL(FirstName,N'')+N' '+ISNULL(LastName,N''))),N'')) AS FullName,
       ReportHeadEcode
FROM dbo.tblEmployee WHERE EmployeeId=@id", conn))
            {
                cmd.Parameters.AddWithValue("@id", employeeId);
                using var r = await cmd.ExecuteReaderAsync();
                if (!await r.ReadAsync())
                    return BadRequest(new { status = false, message = "Employee not found." });
                ecode = r["Ecode"] as string;
                employeeName = r["FullName"] as string;
                reportHeadEcode = r["ReportHeadEcode"] as string;
            }

            if (string.IsNullOrWhiteSpace(reportHeadEcode))
                return BadRequest(new { status = false, message = "Reporting head not assigned. Please update reporting head information." });

            long reportingManagerId = 0;
            using (var cmd = new SqlCommand("SELECT EmployeeId FROM dbo.tblEmployee WHERE Ecode=@e", conn))
            {
                cmd.Parameters.AddWithValue("@e", reportHeadEcode);
                var result = await cmd.ExecuteScalarAsync();
                if (result != null) reportingManagerId = Convert.ToInt64(result);
            }
            if (reportingManagerId == 0)
                return BadRequest(new { status = false, message = "Reporting head ID not found. Please update reporting head information." });

            var noOfDays = (dto.ToDate.Date - dto.FromDate.Date).Days + 1;

            // Recommended By is a free-form Ecode pick (not tied to the applicant's own record) --
            // resolve its display name once at apply time, same snapshot convention as Ecode/EmployeeName.
            string? recommendedByEcode = string.IsNullOrWhiteSpace(dto.RecommendedByEcode) ? null : dto.RecommendedByEcode.Trim();
            string? recommendedByName = null;
            if (recommendedByEcode != null)
            {
                using var lookup = new SqlCommand(@"
SELECT COALESCE([FULL NAME], NULLIF(LTRIM(RTRIM(ISNULL(FirstName,N'')+N' '+ISNULL(LastName,N''))),N''))
FROM dbo.tblEmployee WHERE Ecode=@e", conn);
                lookup.Parameters.AddWithValue("@e", recommendedByEcode);
                var result = await lookup.ExecuteScalarAsync();
                recommendedByName = result as string;
            }

            using var ins = new SqlCommand(@"
INSERT INTO dbo.tblOfficialVisitRequest
    (EmployeeId, Ecode, EmployeeName, FromDate, ToDate, NoOfDays, Purpose, VisitStoreCode, EmployeeRemarks,
     RecommendedByEcode, RecommendedByName,
     ReportingManagerId, ManagerApprovalStatusId, SourceTypeId, CreatedBy, CreatedOn)
VALUES
    (@EmployeeId, @Ecode, @EmployeeName, @FromDate, @ToDate, @NoOfDays, @Purpose, @VisitStoreCode, @EmployeeRemarks,
     @RecommendedByEcode, @RecommendedByName,
     @ReportingManagerId, @Pending, @SourceType, @CreatedBy, GETDATE());
SELECT SCOPE_IDENTITY();", conn);
            ins.Parameters.AddWithValue("@EmployeeId", employeeId);
            ins.Parameters.AddWithValue("@Ecode", (object?)ecode ?? DBNull.Value);
            ins.Parameters.AddWithValue("@EmployeeName", (object?)employeeName ?? DBNull.Value);
            ins.Parameters.AddWithValue("@FromDate", dto.FromDate.Date);
            ins.Parameters.AddWithValue("@ToDate", dto.ToDate.Date);
            ins.Parameters.AddWithValue("@NoOfDays", noOfDays);
            ins.Parameters.AddWithValue("@Purpose", (object?)dto.Purpose ?? DBNull.Value);
            ins.Parameters.AddWithValue("@VisitStoreCode", (object?)dto.VisitStoreCode ?? DBNull.Value);
            ins.Parameters.AddWithValue("@EmployeeRemarks", (object?)dto.EmployeeRemarks ?? DBNull.Value);
            ins.Parameters.AddWithValue("@RecommendedByEcode", (object?)recommendedByEcode ?? DBNull.Value);
            ins.Parameters.AddWithValue("@RecommendedByName", (object?)recommendedByName ?? DBNull.Value);
            ins.Parameters.AddWithValue("@ReportingManagerId", reportingManagerId);
            ins.Parameters.AddWithValue("@Pending", Statuses.Pending);
            ins.Parameters.AddWithValue("@SourceType", SourceTypes.SelfApply);
            ins.Parameters.AddWithValue("@CreatedBy", ecode ?? employeeId.ToString());
            var newId = await ins.ExecuteScalarAsync();

            return Ok(new { status = true, message = "Official visit request submitted.", id = Convert.ToInt64(newId) });
        }

        [RequirePageAccess("/official-visit")]
        [HttpGet("my-requests")]
        public async Task<IActionResult> MyRequests()
        {
            var (employeeId, _) = CurrentUser();
            if (employeeId == 0) return Unauthorized(new { status = false, message = "Invalid or missing authentication." });

            var sql = SelectWithJoins + " WHERE v.EmployeeId=@id ORDER BY v.CreatedOn DESC;";
            var rows = new List<Dictionary<string, object>>();
            using var conn = Open();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", employeeId);
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) rows.Add(ReadRow(r));
            return Ok(new { status = true, data = rows });
        }

        [RequirePageAccess("/official-visit-approval")]
        [HttpGet("pending-for-manager/{managerId}")]
        public async Task<IActionResult> PendingForManager(long managerId, [FromQuery] bool includeDecided = false)
        {
            var (employeeId, role) = CurrentUser();
            if (employeeId == 0) return Unauthorized(new { status = false, message = "Invalid or missing authentication." });

            // Admin/HR roles may look at any manager's queue (e.g. for support); everyone else can
            // only ever query their own reportee queue, regardless of what managerId is passed.
            var effectiveManagerId = IsAdminRole(role) ? managerId : employeeId;

            var where = includeDecided
                ? "WHERE v.ReportingManagerId=@mid"
                : "WHERE v.ReportingManagerId=@mid AND (v.ManagerApprovalStatusId=@pending OR v.ManagerApprovalStatusId IS NULL)";

            var sql = SelectWithJoins + " " + where + " ORDER BY v.CreatedOn DESC;";
            var rows = new List<Dictionary<string, object>>();
            using var conn = Open();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@mid", effectiveManagerId);
            if (!includeDecided) cmd.Parameters.AddWithValue("@pending", Statuses.Pending);
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) rows.Add(ReadRow(r));
            return Ok(new { status = true, data = rows });
        }

        [RequirePageAccess("/official-visit-approval")]
        [HttpPost("approve/{id}")]
        public async Task<IActionResult> Approve(long id, [FromBody] UpdateOfficialVisitStatusDto dto)
        {
            var (employeeId, role) = CurrentUser();
            if (employeeId == 0) return Unauthorized(new { status = false, message = "Invalid or missing authentication." });
            if (dto == null || (dto.StatusId != Statuses.Approved && dto.StatusId != Statuses.Rejected))
                return BadRequest(new { status = false, message = "StatusId must be Approved or Rejected." });

            using var conn = Open();

            long? reportingManagerId; int? currentStatus;
            using (var cmd = new SqlCommand("SELECT ReportingManagerId, ManagerApprovalStatusId FROM dbo.tblOfficialVisitRequest WHERE OfficialVisitRequestId=@id", conn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                using var r = await cmd.ExecuteReaderAsync();
                if (!await r.ReadAsync()) return NotFound(new { status = false, message = "Request not found." });
                reportingManagerId = r["ReportingManagerId"] is DBNull ? (long?)null : Convert.ToInt64(r["ReportingManagerId"]);
                currentStatus = r["ManagerApprovalStatusId"] is DBNull ? (int?)null : Convert.ToInt32(r["ManagerApprovalStatusId"]);
            }

            if (reportingManagerId == null)
                return BadRequest(new { status = false, message = "This request has no manager step (HR-uploaded)." });
            if (!IsAdminRole(role) && reportingManagerId != employeeId)
                return StatusCode(StatusCodes.Status403Forbidden, new { status = false, message = "You are not the reporting manager for this request." });
            if (currentStatus == Statuses.Approved || currentStatus == Statuses.Rejected)
                return BadRequest(new { status = false, message = "This request has already been decided." });

            using var upd = new SqlCommand(@"
UPDATE dbo.tblOfficialVisitRequest
   SET ManagerApprovalStatusId=@status, ManagerApproverId=@approver, ManagerApprovalOn=GETDATE(),
       ManagerRemarks=@remarks, LastUpdatedBy=@by, UpdatedOn=GETDATE()
 WHERE OfficialVisitRequestId=@id;", conn);
            upd.Parameters.AddWithValue("@status", dto.StatusId);
            upd.Parameters.AddWithValue("@approver", employeeId);
            upd.Parameters.AddWithValue("@remarks", (object?)dto.Remarks ?? DBNull.Value);
            upd.Parameters.AddWithValue("@by", employeeId.ToString());
            upd.Parameters.AddWithValue("@id", id);
            await upd.ExecuteNonQueryAsync();

            return Ok(new { status = true, message = dto.StatusId == Statuses.Approved ? "Request approved." : "Request rejected." });
        }

        // ---------------------------------------------------------------
        // Admin: IT Superadmin only
        // ---------------------------------------------------------------

        [HttpGet("GetAll"), RequirePageAccess("/official-visit-admin")]
        public async Task<IActionResult> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] string? search = null,
            [FromQuery] string? fromDate = null,
            [FromQuery] string? toDate = null,
            [FromQuery] int? statusId = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 500) pageSize = 50;
            search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
            DateTime? from = DateTime.TryParse(fromDate, out var f) ? f.Date : (DateTime?)null;
            DateTime? to = DateTime.TryParse(toDate, out var t) ? t.Date : (DateTime?)null;

            const string where = @"
WHERE (@search IS NULL OR v.Ecode LIKE '%'+@search+'%' OR v.EmployeeName LIKE '%'+@search+'%')
  AND (@from IS NULL OR v.ToDate >= @from)
  AND (@to IS NULL OR v.FromDate <= @to)
  AND (@statusId IS NULL OR v.ManagerApprovalStatusId = @statusId)";

            var countSql = "SELECT COUNT_BIG(1) FROM dbo.tblOfficialVisitRequest v " + where + ";";
            var dataSql = SelectWithJoins + where + " ORDER BY v.CreatedOn DESC OFFSET @offset ROWS FETCH NEXT @take ROWS ONLY;";

            long total;
            var rows = new List<Dictionary<string, object>>();
            using var conn = Open();
            using (var cc = new SqlCommand(countSql, conn))
            {
                cc.Parameters.AddWithValue("@search", (object?)search ?? DBNull.Value);
                cc.Parameters.AddWithValue("@from", (object?)from ?? DBNull.Value);
                cc.Parameters.AddWithValue("@to", (object?)to ?? DBNull.Value);
                cc.Parameters.AddWithValue("@statusId", (object?)statusId ?? DBNull.Value);
                total = Convert.ToInt64(await cc.ExecuteScalarAsync());
            }
            using (var cmd = new SqlCommand(dataSql, conn))
            {
                cmd.Parameters.AddWithValue("@search", (object?)search ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@from", (object?)from ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@to", (object?)to ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@statusId", (object?)statusId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@offset", (page - 1) * pageSize);
                cmd.Parameters.AddWithValue("@take", pageSize);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync()) rows.Add(ReadRow(r));
            }
            return Ok(new { status = true, data = rows, total, page, pageSize });
        }

        [HttpGet("DownloadTemplate"), RequirePageAccess("/official-visit-admin")]
        public IActionResult DownloadTemplate()
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("OfficialVisit");
            string[] headers = { "ECODE", "FROM DATE", "TO DATE", "PURPOSE", "VISIT LOCATION STORE CODE", "RECOMMENDED BY ECODE", "REMARKS" };
            for (int i = 0; i < headers.Length; i++) { var c = ws.Cell(1, i + 1); c.Value = headers[i]; c.Style.Font.Bold = true; }
            ws.Cell(2, 1).Value = "V33154"; ws.Cell(2, 2).Value = "01-Aug-26"; ws.Cell(2, 3).Value = "03-Aug-26";
            ws.Cell(2, 4).Value = "Client site visit"; ws.Cell(2, 5).Value = "HD26"; ws.Cell(2, 6).Value = "V12345"; ws.Cell(2, 7).Value = "";
            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "OfficialVisit_Template.xlsx");
        }

        // Upsert keyed on (Ecode, FromDate, ToDate) -- overlapping re-uploads update in place
        // rather than duplicating, per confirmed decision. Every uploaded row is auto-approved,
        // SourceTypeId=HrUpload, no ReportingManagerId/ManagerApproverId -- no manager gate.
        [HttpPost("Upload"), RequirePageAccess("/official-visit-admin")]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            var (employeeId, _) = CurrentUser();
            if (file == null || file.Length == 0)
                return BadRequest(new { status = false, message = "No file uploaded." });

            var inserted = 0; var updated = 0; var errors = new List<string>();

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

                int ecodeCol = 0, fromCol = 0, toCol = 0, purposeCol = 0, storeCol = 0, recommendedByCol = 0, remarksCol = 0;
                var foundHeaders = new List<string>();
                string Norm(string s) => new string((s ?? "").ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
                for (int col = 1; col <= lastCol; col++)
                {
                    var raw = ws.Cell(headerRowNum, col).GetString().Trim();
                    if (raw == "") continue;
                    foundHeaders.Add(raw);
                    var n = Norm(raw);
                    if (ecodeCol == 0 && (n == "ecode" || n == "empcode")) ecodeCol = col;
                    else if (fromCol == 0 && (n == "fromdate" || n == "from")) fromCol = col;
                    else if (toCol == 0 && (n == "todate" || n == "to")) toCol = col;
                    else if (purposeCol == 0 && n == "purpose") purposeCol = col;
                    else if (storeCol == 0 && (n == "visitlocationstorecode" || n == "storecode" || n == "visitstorecode")) storeCol = col;
                    else if (recommendedByCol == 0 && (n == "recommendedbyecode" || n == "recommendedby")) recommendedByCol = col;
                    else if (remarksCol == 0 && n == "remarks") remarksCol = col;
                }

                if (ecodeCol == 0 || fromCol == 0 || toCol == 0)
                    return BadRequest(new
                    {
                        status = false,
                        message = "Could not find required columns 'ECODE', 'FROM DATE', 'TO DATE'. (Optional: Purpose, Visit Location Store Code, Recommended By Ecode, Remarks.) Found: " + string.Join(", ", foundHeaders)
                    });

                var lastRow = ws.LastRowUsed()?.RowNumber() ?? headerRowNum;
                using var conn = Open();

                for (int rIdx = headerRowNum + 1; rIdx <= lastRow; rIdx++)
                {
                    string ecode = ws.Cell(rIdx, ecodeCol).GetString().Trim();
                    var fromCell = ws.Cell(rIdx, fromCol);
                    var toCell = ws.Cell(rIdx, toCol);
                    if (ecode == "" && fromCell.GetString().Trim() == "") continue;

                    if (!TryReadDate(fromCell, out var fromDate) || !TryReadDate(toCell, out var toDate))
                    {
                        errors.Add($"Row {rIdx}: could not parse FROM DATE/TO DATE.");
                        continue;
                    }
                    if (ecode == "")
                    {
                        errors.Add($"Row {rIdx}: ECODE is required.");
                        continue;
                    }

                    long empId = 0; string? empName = null;
                    using (var lookup = new SqlCommand(@"
SELECT EmployeeId, COALESCE([FULL NAME], NULLIF(LTRIM(RTRIM(ISNULL(FirstName,N'')+N' '+ISNULL(LastName,N''))),N''))
FROM dbo.tblEmployee WHERE Ecode=@e", conn))
                    {
                        lookup.Parameters.AddWithValue("@e", ecode);
                        using var lr = await lookup.ExecuteReaderAsync();
                        if (await lr.ReadAsync())
                        {
                            empId = Convert.ToInt64(lr[0]);
                            empName = lr[1] as string;
                        }
                    }
                    if (empId == 0)
                    {
                        errors.Add($"Row {rIdx}: ecode '{ecode}' not found in tblEmployee.");
                        continue;
                    }

                    var purpose = purposeCol > 0 ? ws.Cell(rIdx, purposeCol).GetString().Trim() : "";
                    var store = storeCol > 0 ? ws.Cell(rIdx, storeCol).GetString().Trim() : "";
                    var remarks = remarksCol > 0 ? ws.Cell(rIdx, remarksCol).GetString().Trim() : "";
                    var recommendedByEcode = recommendedByCol > 0 ? ws.Cell(rIdx, recommendedByCol).GetString().Trim() : "";
                    string? recommendedByName = null;
                    if (recommendedByEcode != "")
                    {
                        using var rbLookup = new SqlCommand(@"
SELECT COALESCE([FULL NAME], NULLIF(LTRIM(RTRIM(ISNULL(FirstName,N'')+N' '+ISNULL(LastName,N''))),N''))
FROM dbo.tblEmployee WHERE Ecode=@e", conn);
                        rbLookup.Parameters.AddWithValue("@e", recommendedByEcode);
                        var rbResult = await rbLookup.ExecuteScalarAsync();
                        recommendedByName = rbResult as string;
                    }
                    var noOfDays = (toDate.Date - fromDate.Date).Days + 1;

                    using var up = new SqlCommand(@"
UPDATE dbo.tblOfficialVisitRequest
   SET Purpose=@purpose, VisitStoreCode=@store, EmployeeRemarks=@remarks, NoOfDays=@days,
       RecommendedByEcode=@rbEcode, RecommendedByName=@rbName,
       ManagerApprovalStatusId=@approved, SourceTypeId=@hrUpload, ReportingManagerId=NULL, ManagerApproverId=NULL,
       LastUpdatedBy=@by, UpdatedOn=GETDATE()
 WHERE Ecode=@ecode AND FromDate=@from AND ToDate=@to;
IF @@ROWCOUNT = 0
BEGIN
    INSERT INTO dbo.tblOfficialVisitRequest
        (EmployeeId, Ecode, EmployeeName, FromDate, ToDate, NoOfDays, Purpose, VisitStoreCode, EmployeeRemarks,
         RecommendedByEcode, RecommendedByName,
         ManagerApprovalStatusId, SourceTypeId, CreatedBy, CreatedOn)
    VALUES
        (@empId, @ecode, @empName, @from, @to, @days, @purpose, @store, @remarks,
         @rbEcode, @rbName,
         @approved, @hrUpload, @by, GETDATE());
    SELECT 1;
END
ELSE SELECT 2;", conn);
                    up.Parameters.AddWithValue("@ecode", ecode);
                    up.Parameters.AddWithValue("@from", fromDate.Date);
                    up.Parameters.AddWithValue("@to", toDate.Date);
                    up.Parameters.AddWithValue("@days", noOfDays);
                    up.Parameters.AddWithValue("@purpose", (object?)purpose ?? DBNull.Value);
                    up.Parameters.AddWithValue("@store", (object?)store ?? DBNull.Value);
                    up.Parameters.AddWithValue("@remarks", (object?)remarks ?? DBNull.Value);
                    up.Parameters.AddWithValue("@rbEcode", recommendedByEcode == "" ? (object)DBNull.Value : recommendedByEcode);
                    up.Parameters.AddWithValue("@rbName", (object?)recommendedByName ?? DBNull.Value);
                    up.Parameters.AddWithValue("@approved", Statuses.Approved);
                    up.Parameters.AddWithValue("@hrUpload", SourceTypes.HrUpload);
                    up.Parameters.AddWithValue("@empId", empId);
                    up.Parameters.AddWithValue("@empName", (object?)empName ?? DBNull.Value);
                    up.Parameters.AddWithValue("@by", employeeId.ToString());
                    var res = Convert.ToInt32(await up.ExecuteScalarAsync());
                    if (res == 1) inserted++; else updated++;
                }
            }

            return Ok(new { status = true, message = $"Upload complete. Inserted {inserted}, updated {updated}.", inserted, updated, errors });
        }

        private static bool TryReadDate(IXLCell cell, out DateTime value)
        {
            if (cell.DataType == XLDataType.DateTime) { value = cell.GetDateTime(); return true; }
            var s = cell.GetString().Trim();
            return DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out value)
                || DateTime.TryParseExact(s, "dd-MMM-yy", CultureInfo.InvariantCulture, DateTimeStyles.None, out value)
                || DateTime.TryParseExact(s, "dd-MMM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
        }

        // Filter shape mirrors AccessWindowControllers.SaveDto exactly: range + custom dates +
        // ecode list + an explicit "no filters, export everything" flag.
        [HttpPost("Export"), RequirePageAccess("/official-visit-admin")]
        public async Task<IActionResult> Export([FromBody] OfficialVisitExportFilterDto dto)
        {
            dto ??= new OfficialVisitExportFilterDto();

            var dates = new HashSet<DateTime>();
            if (!dto.ApplyAll)
            {
                var from = ParseDate(dto.FromDate);
                var to = ParseDate(dto.ToDate);
                if (from.HasValue && to.HasValue)
                {
                    if (to.Value < from.Value) return BadRequest(new { status = false, message = "To date is before From date." });
                    if ((to.Value - from.Value).TotalDays > 730) return BadRequest(new { status = false, message = "Date range too large (max ~2 years)." });
                    for (var d = from.Value; d <= to.Value; d = d.AddDays(1)) dates.Add(d);
                }
                else if (from.HasValue) dates.Add(from.Value);
                foreach (var cd in dto.CustomDates ?? new()) { var p = ParseDate(cd); if (p.HasValue) dates.Add(p.Value); }
            }

            var ecodes = (dto.Ecodes ?? new()).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList();

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("OfficialVisit");
            string[] headers = {
                "Ecode","Name","Department","Sub Department 1","Sub Department 2","Sub Department 3","Designation",
                "From Date","To Date","Purpose of Visit","Visit Location Store Code","Visit Location Store Name",
                "Base Location Store Code","Base Location Store Name","Recommended By Ecode","Recommended By Name",
                "Manager Approval","Remarks","Manager Remarks","Source","Created On","Updated On"
            };
            for (int i = 0; i < headers.Length; i++) { var c = ws.Cell(1, i + 1); c.Value = headers[i]; c.Style.Font.Bold = true; }

            var sql = SelectWithJoins + " ORDER BY v.CreatedOn DESC;";
            int row = 2;
            using (var conn = Open())
            using (var cmd = new SqlCommand(sql, conn) { CommandTimeout = 300 })
            using (var r = await cmd.ExecuteReaderAsync())
            {
                while (await r.ReadAsync())
                {
                    if (!dto.ApplyAll)
                    {
                        if (ecodes.Count > 0)
                        {
                            var ecode = r["Ecode"] as string;
                            if (ecode == null || !ecodes.Contains(ecode, StringComparer.OrdinalIgnoreCase)) continue;
                        }
                        if (dates.Count > 0)
                        {
                            var f = (DateTime)r["FromDate"]; var t = (DateTime)r["ToDate"];
                            if (!dates.Any(d => d.Date >= f.Date && d.Date <= t.Date)) continue;
                        }
                        else if (ecodes.Count == 0)
                        {
                            // no dates, no ecodes, not ApplyAll -> nothing requested to export
                            continue;
                        }
                    }

                    int c = 1;
                    ws.Cell(row, c++).Value = r["Ecode"] as string ?? "";
                    ws.Cell(row, c++).Value = r["EmployeeName"] as string ?? "";
                    ws.Cell(row, c++).Value = r["DepartmentName"] as string ?? "";
                    ws.Cell(row, c++).Value = r["SubDepartment1"] as string ?? "";
                    ws.Cell(row, c++).Value = r["SubDepartment2"] as string ?? "";
                    ws.Cell(row, c++).Value = r["SubDepartment3"] as string ?? "";
                    ws.Cell(row, c++).Value = r["DesignationName"] as string ?? "";
                    ws.Cell(row, c++).Value = ((DateTime)r["FromDate"]).ToString("yyyy-MM-dd");
                    ws.Cell(row, c++).Value = ((DateTime)r["ToDate"]).ToString("yyyy-MM-dd");
                    ws.Cell(row, c++).Value = r["Purpose"] as string ?? "";
                    ws.Cell(row, c++).Value = r["VisitStoreCode"] as string ?? "";
                    ws.Cell(row, c++).Value = r["VisitLocationName"] as string ?? "";
                    ws.Cell(row, c++).Value = r["BaseStoreCode"] as string ?? "";
                    ws.Cell(row, c++).Value = r["BaseLocationName"] as string ?? "";
                    ws.Cell(row, c++).Value = r["RecommendedByEcode"] as string ?? "";
                    ws.Cell(row, c++).Value = r["RecommendedByName"] as string ?? "";
                    ws.Cell(row, c++).Value = StatusLabel(r["ManagerApprovalStatusId"]);
                    ws.Cell(row, c++).Value = r["EmployeeRemarks"] as string ?? "";
                    ws.Cell(row, c++).Value = r["ManagerRemarks"] as string ?? "";
                    ws.Cell(row, c++).Value = Convert.ToInt32(r["SourceTypeId"]) == SourceTypes.HrUpload ? "Uploaded by HR" : "Requested by User";
                    ws.Cell(row, c++).Value = ((DateTime)r["CreatedOn"]).ToString("yyyy-MM-dd HH:mm");
                    ws.Cell(row, c++).Value = r["UpdatedOn"] is DBNull ? "" : ((DateTime)r["UpdatedOn"]).ToString("yyyy-MM-dd HH:mm");
                    row++;
                }
            }
            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            var stamp = DateTime.Today.ToString("yyyy-MM-dd");
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"OfficialVisit_{stamp}.xlsx");
        }

        private static string StatusLabel(object statusIdObj)
        {
            if (statusIdObj is DBNull || statusIdObj == null) return "";
            return Convert.ToInt32(statusIdObj) switch
            {
                Statuses.Approved => "Approved",
                Statuses.Rejected => "Rejected",
                Statuses.Pending => "Pending",
                _ => ""
            };
        }

        private static DateTime? ParseDate(string? s) =>
            !string.IsNullOrWhiteSpace(s) && DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d.Date : (DateTime?)null;
    }
}
