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
using System.Threading.Tasks;

namespace HRMSAPI.Controllers
{
    /// <summary>
    /// Candidate Registration Form (V2 Retail Graduate Academy).
    ///
    /// Public, pre-login form reachable from the login page (like /appform,
    /// /candidate-form, /interview-form). On submit it saves one row to
    /// dbo.tblCandidateRegistration and stores the 4 uploaded documents under
    /// wwwroot/CandidateRegistration/{id}/.
    ///
    /// Self-contained ADO.NET (no DbContext / EF changes). Register is
    /// [AllowAnonymous]; GetAll is [Authorize] for internal viewing.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class CandidateRegistrationController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;

        public CandidateRegistrationController(IConfiguration config, IWebHostEnvironment env)
        {
            _config = config;
            _env = env;
        }

        private SqlConnection Open()
        {
            var c = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
            c.Open();
            return c;
        }

        // Files coming from the multipart form. All optional.
        public class RegistrationFiles
        {
            public IFormFile? Photo { get; set; }
            public IFormFile? Resume { get; set; }
            public IFormFile? Aadhaar { get; set; }
            public IFormFile? Marksheet { get; set; }
        }

        // ---------- Public submit ----------
        [AllowAnonymous]
        [HttpPost("Register")]
        [RequestSizeLimit(50_000_000)] // 50 MB total for the 4 docs
        public async Task<IActionResult> Register(
            [FromForm] RegistrationFiles files,
            [FromForm] string? ProgramApplyingFor,
            [FromForm] string? ModeOfTraining,
            [FromForm] string? FullName,
            [FromForm] string? MobileNumber,
            [FromForm] string? WhatsAppNumber,
            [FromForm] string? Email,
            [FromForm] string? DateOfBirth,
            [FromForm] string? Gender,
            [FromForm] string? HighestQualification,
            [FromForm] string? Specialization,
            [FromForm] string? CollegeUniversity,
            [FromForm] string? PassingYear,
            [FromForm] string? PreferredLearningMode,
            [FromForm] bool AgreedToTerms = false)
        {
            if (string.IsNullOrWhiteSpace(FullName))
                return BadRequest(new { status = false, message = "Full Name is required." });
            if (!AgreedToTerms)
                return BadRequest(new { status = false, message = "You must agree to the Terms & Conditions." });

            DateTime? dob = null;
            if (!string.IsNullOrWhiteSpace(DateOfBirth) &&
                DateTime.TryParse(DateOfBirth, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                dob = d.Date;

            var ip = HttpContext?.Connection?.RemoteIpAddress?.ToString();

            try
            {
                await using var conn = Open();

                // 1) Insert the row first to get the identity Id (used as the file folder)
                int newId;
                const string insertSql = @"
INSERT INTO dbo.tblCandidateRegistration
    (ProgramApplyingFor, ModeOfTraining, FullName, MobileNumber, WhatsAppNumber, Email,
     DateOfBirth, Gender, HighestQualification, Specialization, CollegeUniversity, PassingYear,
     PreferredLearningMode, AgreedToTerms, CreatedOn, CreatedIp)
OUTPUT INSERTED.Id
VALUES
    (@ProgramApplyingFor, @ModeOfTraining, @FullName, @MobileNumber, @WhatsAppNumber, @Email,
     @DateOfBirth, @Gender, @HighestQualification, @Specialization, @CollegeUniversity, @PassingYear,
     @PreferredLearningMode, @AgreedToTerms, GETDATE(), @CreatedIp);";
                await using (var cmd = new SqlCommand(insertSql, conn))
                {
                    void P(string n, object? v) => cmd.Parameters.Add(new SqlParameter(n, v ?? DBNull.Value));
                    P("@ProgramApplyingFor", NullIfBlank(ProgramApplyingFor));
                    P("@ModeOfTraining", NullIfBlank(ModeOfTraining));
                    P("@FullName", FullName.Trim());
                    P("@MobileNumber", NullIfBlank(MobileNumber));
                    P("@WhatsAppNumber", NullIfBlank(WhatsAppNumber));
                    P("@Email", NullIfBlank(Email));
                    P("@DateOfBirth", (object?)dob);
                    P("@Gender", NullIfBlank(Gender));
                    P("@HighestQualification", NullIfBlank(HighestQualification));
                    P("@Specialization", NullIfBlank(Specialization));
                    P("@CollegeUniversity", NullIfBlank(CollegeUniversity));
                    P("@PassingYear", NullIfBlank(PassingYear));
                    P("@PreferredLearningMode", NullIfBlank(PreferredLearningMode));
                    cmd.Parameters.Add(new SqlParameter("@AgreedToTerms", SqlDbType.Bit) { Value = AgreedToTerms });
                    P("@CreatedIp", NullIfBlank(ip));
                    newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }

                // 2) Save files under wwwroot/CandidateRegistration/{id}/ and update the row with their paths
                var photoPath = await SaveFileAsync(files.Photo, newId, "Photo");
                var resumePath = await SaveFileAsync(files.Resume, newId, "Resume");
                var aadhaarPath = await SaveFileAsync(files.Aadhaar, newId, "Aadhaar");
                var marksheetPath = await SaveFileAsync(files.Marksheet, newId, "Marksheet");

                if (photoPath != null || resumePath != null || aadhaarPath != null || marksheetPath != null)
                {
                    const string updSql = @"
UPDATE dbo.tblCandidateRegistration
SET PhotoPath = @PhotoPath, ResumePath = @ResumePath, AadhaarPath = @AadhaarPath, MarksheetPath = @MarksheetPath
WHERE Id = @Id;";
                    await using var upd = new SqlCommand(updSql, conn);
                    upd.Parameters.Add(new SqlParameter("@PhotoPath", (object?)photoPath ?? DBNull.Value));
                    upd.Parameters.Add(new SqlParameter("@ResumePath", (object?)resumePath ?? DBNull.Value));
                    upd.Parameters.Add(new SqlParameter("@AadhaarPath", (object?)aadhaarPath ?? DBNull.Value));
                    upd.Parameters.Add(new SqlParameter("@MarksheetPath", (object?)marksheetPath ?? DBNull.Value));
                    upd.Parameters.Add(new SqlParameter("@Id", newId));
                    await upd.ExecuteNonQueryAsync();
                }

                return Ok(new { status = true, message = "Registration submitted successfully.", id = newId });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { status = false, message = "Failed to submit registration: " + ex.Message });
            }
        }

        // Common SELECT used by the listing + export (ordered newest first).
        // Optional filters: search text and a CreatedOn date range (inclusive).
        private const string ListSql = @"
SELECT Id, ProgramApplyingFor, ModeOfTraining, FullName, MobileNumber, WhatsAppNumber, Email,
       DateOfBirth, Gender, HighestQualification, Specialization, CollegeUniversity, PassingYear,
       PreferredLearningMode, PhotoPath, ResumePath, AadhaarPath, MarksheetPath, AgreedToTerms, CreatedOn
FROM dbo.tblCandidateRegistration
WHERE (@search IS NULL OR FullName LIKE '%' + @search + '%' OR Email LIKE '%' + @search + '%'
       OR MobileNumber LIKE '%' + @search + '%' OR ProgramApplyingFor LIKE '%' + @search + '%')
  AND (@from IS NULL OR CreatedOn >= @from)
  AND (@to   IS NULL OR CreatedOn <  @to)
ORDER BY Id DESC;";

        // Parses yyyy-MM-dd (or any parseable date). @to is made exclusive by adding a day
        // so the whole "to" day is included regardless of time component.
        private static (object from, object to) ParseRange(string? fromDate, string? toDate)
        {
            object from = DBNull.Value, to = DBNull.Value;
            if (!string.IsNullOrWhiteSpace(fromDate) &&
                DateTime.TryParse(fromDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var f))
                from = f.Date;
            if (!string.IsNullOrWhiteSpace(toDate) &&
                DateTime.TryParse(toDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t))
                to = t.Date.AddDays(1); // exclusive upper bound
            return (from, to);
        }

        // ---------- Internal listing (IT Superadmin) ----------
        [Authorize]
        [RequirePageAccess("/v2-pathshala/registrations")]
        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? search = null,
            [FromQuery] string? fromDate = null,
            [FromQuery] string? toDate = null)
        {
            search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
            var (from, to) = ParseRange(fromDate, toDate);

            var rows = new List<Dictionary<string, object?>>();
            await using var conn = Open();
            await using var cmd = new SqlCommand(ListSql, conn);
            cmd.Parameters.Add(new SqlParameter("@search", (object?)search ?? DBNull.Value));
            cmd.Parameters.Add(new SqlParameter("@from", from));
            cmd.Parameters.Add(new SqlParameter("@to", to));
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < r.FieldCount; i++)
                    row[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
                rows.Add(row);
            }
            return Ok(new { status = true, data = rows });
        }

        // ---------- Excel export (IT Superadmin) ----------
        // All details + the form-filled Date (dd-MMM-yy) and Time. Honors the same
        // search + date-range filters as the listing.
        [Authorize]
        [RequirePageAccess("/v2-pathshala/registrations")]
        [HttpGet("Export")]
        public async Task<IActionResult> Export(
            [FromQuery] string? search = null,
            [FromQuery] string? fromDate = null,
            [FromQuery] string? toDate = null)
        {
            search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
            var (from, to) = ParseRange(fromDate, toDate);

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("V2 Pathshala Registrations");

            string[] headers =
            {
                "S.No", "Reg. ID", "Full Name", "Mobile Number", "WhatsApp Number", "Email",
                "Date of Birth", "Gender", "Program Applying For", "Mode of Training",
                "Highest Qualification", "Specialization", "College/University", "Passing Year",
                "Preferred Learning Mode", "Agreed To Terms",
                "Photo", "Resume", "Aadhaar/ID", "Marksheet",
                "Form Filled Date", "Form Filled Time"
            };
            for (int c = 0; c < headers.Length; c++)
            {
                var cell = ws.Cell(1, c + 1);
                cell.Value = headers[c];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1D4ED8");
                cell.Style.Font.FontColor = XLColor.White;
            }

            await using var conn = Open();
            await using var cmd = new SqlCommand(ListSql, conn);
            cmd.Parameters.Add(new SqlParameter("@search", (object?)search ?? DBNull.Value));
            cmd.Parameters.Add(new SqlParameter("@from", from));
            cmd.Parameters.Add(new SqlParameter("@to", to));

            int row = 2; int sno = 1;
            await using (var r = await cmd.ExecuteReaderAsync())
            {
                while (await r.ReadAsync())
                {
                    string? Str(string col) => r[col] is DBNull ? null : r[col].ToString();
                    DateTime? Dt(string col) => r[col] is DBNull ? (DateTime?)null : Convert.ToDateTime(r[col]);

                    var created = Dt("CreatedOn");
                    var dob = Dt("DateOfBirth");

                    ws.Cell(row, 1).Value = sno++;
                    ws.Cell(row, 2).Value = Str("Id");
                    ws.Cell(row, 3).Value = Str("FullName");
                    ws.Cell(row, 4).Value = Str("MobileNumber");
                    ws.Cell(row, 5).Value = Str("WhatsAppNumber");
                    ws.Cell(row, 6).Value = Str("Email");
                    ws.Cell(row, 7).Value = dob.HasValue ? dob.Value.ToString("dd-MMM-yy", CultureInfo.InvariantCulture) : "";
                    ws.Cell(row, 8).Value = Str("Gender");
                    ws.Cell(row, 9).Value = Str("ProgramApplyingFor");
                    ws.Cell(row, 10).Value = Str("ModeOfTraining");
                    ws.Cell(row, 11).Value = Str("HighestQualification");
                    ws.Cell(row, 12).Value = Str("Specialization");
                    ws.Cell(row, 13).Value = Str("CollegeUniversity");
                    ws.Cell(row, 14).Value = Str("PassingYear");
                    ws.Cell(row, 15).Value = Str("PreferredLearningMode");
                    ws.Cell(row, 16).Value = (r["AgreedToTerms"] is DBNull) ? "" : (Convert.ToBoolean(r["AgreedToTerms"]) ? "Yes" : "No");
                    ws.Cell(row, 17).Value = string.IsNullOrWhiteSpace(Str("PhotoPath")) ? "" : "Yes";
                    ws.Cell(row, 18).Value = string.IsNullOrWhiteSpace(Str("ResumePath")) ? "" : "Yes";
                    ws.Cell(row, 19).Value = string.IsNullOrWhiteSpace(Str("AadhaarPath")) ? "" : "Yes";
                    ws.Cell(row, 20).Value = string.IsNullOrWhiteSpace(Str("MarksheetPath")) ? "" : "Yes";
                    // Form filled: date in dd-MMM-yy + separate time column
                    ws.Cell(row, 21).Value = created.HasValue ? created.Value.ToString("dd-MMM-yy", CultureInfo.InvariantCulture) : "";
                    ws.Cell(row, 22).Value = created.HasValue ? created.Value.ToString("HH:mm:ss", CultureInfo.InvariantCulture) : "";
                    row++;
                }
            }

            ws.Columns().AdjustToContents();
            ws.SheetView.FreezeRows(1);

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            var bytes = ms.ToArray();
            var fileName = $"V2Pathshala_Registrations_{DateTime.Now:dd-MMM-yy}.xlsx";
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        private static string? NullIfBlank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        // Saves one uploaded file under wwwroot/CandidateRegistration/{id}/{folder}/ and
        // returns the relative path (forward slashes) to store in the DB, or null if no file.
        private async Task<string?> SaveFileAsync(IFormFile? file, int id, string folder)
        {
            if (file == null || file.Length == 0) return null;

            var webRoot = _env.WebRootPath;
            if (string.IsNullOrWhiteSpace(webRoot))
                webRoot = Path.Combine(_env.ContentRootPath, "wwwroot");

            var dir = Path.Combine(webRoot, "CandidateRegistration", id.ToString(), folder);
            Directory.CreateDirectory(dir);

            var safeName = Path.GetFileName(file.FileName);
            var fileName = $"{DateTime.Now:ddMMyyyyHHmmssffff}_{safeName}";
            var fullPath = Path.Combine(dir, fileName);

            await using (var fs = new FileStream(fullPath, FileMode.Create))
                await file.CopyToAsync(fs);

            // relative path served by app.UseStaticFiles() (wwwroot root)
            return $"CandidateRegistration/{id}/{folder}/{fileName}";
        }
    }
}
