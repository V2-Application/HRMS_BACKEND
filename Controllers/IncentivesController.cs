using ClosedXML.Excel;
using HRMSAPI.DTO;
using HRMSAPI.Extension;
using HRMSAPI.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Security.Claims;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[RequirePageAccess("/incentive/requests")]
public class IncentivesController : ControllerBase
{
    private readonly IIncentiveService _repo;
    private readonly IWebHostEnvironment _env;

    public IncentivesController(IIncentiveService repo, IWebHostEnvironment env)
    {
        _repo = repo;
        _env = env;
    }

    // 1) UPSERT with [FromForm] (attachments via multipart/form-data)
    [HttpPost("upsert")]
    [RequestSizeLimit(60_000_000)] // ~60MB total, adjust as needed
    public async Task<IActionResult> Upsert([FromForm] IncentiveUpsertForm form)
    {
        var identity = HttpContext.User.Identity as ClaimsIdentity;
        if (identity == null) return BadRequest("Authentication Fails");

        // folder to save files (wwwroot/uploads)
        var webRoot = _env.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot)) webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var uploadsRoot = Path.Combine(webRoot, "uploads");

        var dto = await _repo.UpsertAsync(form, identity, uploadsRoot, HttpContext.RequestAborted);
        return Ok(dto);
    }

    // 2) GET BY ID
    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id)
    {
        var identity = HttpContext.User.Identity as ClaimsIdentity;
        if (identity == null) return BadRequest("Authentication Fails");

        var dto = await _repo.GetByIdAsync(id, HttpContext.RequestAborted);
        if (dto == null) return NotFound();
        return Ok(dto);
    }

    // 3) LIST (paged)
    [HttpGet("list")]
    public async Task<IActionResult> List(int pageNumber = 1, int pageSize = 10, string? searchTerm = "")
    {
        var identity = HttpContext.User.Identity as ClaimsIdentity;
        if (identity == null) return BadRequest("Authentication Fails");

        var (items, total, current) = await _repo.ListAsync(pageNumber, pageSize, searchTerm, HttpContext.RequestAborted);

        return Ok(new
        {
            Incentives = items,
            TotalCount = total,
            CurrentPageNumber = current
        });
    }

// 4) BULK CREATE from Excel (no attachments) — inserts only NEW (Ecode, Month); returns inserted + skipped + row errors
[HttpPost("bulk-excel")]
[RequestSizeLimit(30_000_000)] // ~30MB; adjust as needed
public async Task<IActionResult> BulkCreateFromExcel(
    [FromForm] IFormFile file,
    [FromForm] string? sheetName = null,      // optional: pick a sheet by name
    [FromForm] int headerRow = 1              // header row index (1-based)
)
{
    var identity = HttpContext.User.Identity as ClaimsIdentity;
    if (identity == null) return BadRequest("Authentication Fails");

    if (file == null || file.Length == 0)
        return BadRequest("Please upload a non-empty Excel file.");

    // Parse Excel -> forms
    var forms = new List<IncentiveUpsertForm>();
    var rowErrors = new List<object>();

    try
    {
        using var stream = file.OpenReadStream();
        using var workbook = new XLWorkbook(stream);
        var ws = !string.IsNullOrWhiteSpace(sheetName)
                 ? workbook.Worksheets.FirstOrDefault(s => string.Equals(s.Name, sheetName, StringComparison.OrdinalIgnoreCase))
                 : workbook.Worksheets.FirstOrDefault();

        if (ws == null) return BadRequest("No worksheet found in the uploaded file.");

        // Build header map (case-insensitive)
        var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var lastCol = ws.Row(headerRow).LastCellUsed()?.Address.ColumnNumber ?? 0;
        for (int c = 1; c <= lastCol; c++)
        {
            var name = ws.Cell(headerRow, c).GetString().Trim();
            if (!string.IsNullOrEmpty(name) && !headers.ContainsKey(name))
                headers[name] = c;
        }

        // Helper to get column index by any of the allowed names
        int? Col(params string[] names)
        {
            foreach (var n in names)
                if (headers.TryGetValue(n, out var col)) return col;
            return null;
        }

        // Expected columns (flexible names)
        var colEcode = Col("Ecode", "EmpCode", "EmployeeCode");
        var colMonth = Col("Month", "IncentiveMonth");
        var colAmount = Col("Amount", "IncentiveAmount");
        var colRemarks = Col("Remarks", "Remark");
        var colCmdStatusId = Col("CmdStatusId", "CMD Status Id", "CMDStatus");
        var colHrStatusId = Col("HrStatusId", "HR Status Id", "HRStatus");
        var colCmdRemarks = Col("CmdRemarks", "CMD Remarks");
        var colHrRemarks = Col("HrRemarks", "HR Remarks");
        var colCreatedBy = Col("CreatedBy");

        // Basic validation
        if (colEcode == null || colMonth == null || colAmount == null)
            return BadRequest("Excel must contain at least the columns: Ecode, Month, Amount.");

        var lastRow = ws.LastRowUsed()?.RowNumber() ?? headerRow;
        for (int r = headerRow + 1; r <= lastRow; r++)
        {
            try
            {
                // Read raw values
                var ecode = ws.Cell(r, colEcode.Value).GetString()?.Trim();
                var monthV = ws.Cell(r, colMonth.Value);
                var amtV = ws.Cell(r, colAmount.Value);

                // Required validations
                if (string.IsNullOrWhiteSpace(ecode))
                    throw new ArgumentException("Ecode is required.");

                // Parse Month (date or text)
                DateTime month;
                if (monthV.DataType == XLDataType.DateTime)
                {
                    month = monthV.GetDateTime();
                }
                else
                {
                    var raw = monthV.GetString().Trim();
                    if (string.IsNullOrWhiteSpace(raw))
                        throw new ArgumentException("Month is required.");

                    // Accept common formats: yyyy-MM, yyyy-MM-dd, dd/MM/yyyy, MMM yyyy, etc.
                    var ok = DateTime.TryParse(raw, CultureInfo.CurrentCulture, DateTimeStyles.None, out month)
                          || DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out month);
                    if (!ok) throw new ArgumentException($"Invalid Month format: '{raw}'.");
                }
                // Normalize to first day of month
                month = new DateTime(month.Year, month.Month, 1);

                // Parse Amount
                decimal amount;
                if (amtV.DataType == XLDataType.Number)
                {
                    amount = Convert.ToDecimal(amtV.GetDouble());
                }
                else
                {
                    var rawAmt = amtV.GetString().Trim();
                    if (!decimal.TryParse(rawAmt, NumberStyles.Number, CultureInfo.CurrentCulture, out amount) &&
                        !decimal.TryParse(rawAmt, NumberStyles.Number, CultureInfo.InvariantCulture, out amount))
                        throw new ArgumentException($"Invalid Amount: '{rawAmt}'.");
                }

                // Optional columns
                string? remarks = colRemarks.HasValue ? ws.Cell(r, colRemarks.Value).GetString()?.Trim() : null;
                int? cmdStatusId = TryParseInt(ws, r, colCmdStatusId);
                int? hrStatusId = TryParseInt(ws, r, colHrStatusId);
                string? cmdRemarks = colCmdRemarks.HasValue ? ws.Cell(r, colCmdRemarks.Value).GetString()?.Trim() : null;
                string? hrRemarks = colHrRemarks.HasValue ? ws.Cell(r, colHrRemarks.Value).GetString()?.Trim() : null;
                string? createdBy = colCreatedBy.HasValue ? ws.Cell(r, colCreatedBy.Value).GetString()?.Trim() : null;

                forms.Add(new IncentiveUpsertForm
                {
                    Ecode = ecode,
                    Month = month,
                    Amount = amount,
                    Remarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks,
                    CreatedBy = string.IsNullOrWhiteSpace(createdBy) ? null : createdBy,
                    CmdStatusId = cmdStatusId,
                    HrStatusId = hrStatusId,
                    CmdRemarks = string.IsNullOrWhiteSpace(cmdRemarks) ? null : cmdRemarks,
                    HrRemarks = string.IsNullOrWhiteSpace(hrRemarks) ? null : hrRemarks
                });
            }
            catch (Exception exRow)
            {
                rowErrors.Add(new
                {
                    Row = r,
                    Error = exRow.Message
                });
            }
        }
    }
    catch (Exception ex)
    {
        return Problem(title: "Failed to parse Excel", detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
    }

    if (forms.Count == 0 && rowErrors.Count > 0)
    {
        // Everything failed
        return Ok(new
        {
            InsertedCount = 0,
            SkippedCount = 0,
            Errors = rowErrors
        });
    }

    // Call service BulkCreate
    var identity2 = HttpContext.User.Identity as ClaimsIdentity;
    var (inserted, skipped) = await _repo.BulkCreateAsync(forms, identity2, HttpContext.RequestAborted);

    return Ok(new
    {
        InsertedCount = inserted.Count,
        SkippedCount = skipped.Count,
        Inserted = inserted,
        Skipped = skipped.Select(s => new { s.RowNo, s.Ecode, s.Month, s.Reason }).ToList(),
        Errors = rowErrors // row-level parse/validation errors
    });
}

// helpers
static int? TryParseInt(IXLWorksheet ws, int row, int? col)
{
    if (!col.HasValue) return null;
    var cell = ws.Cell(row, col.Value);
    if (cell.DataType == XLDataType.Number)
        return Convert.ToInt32(cell.GetDouble());

    var s = cell.GetString().Trim();
    return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : (int?)null;
}

}
