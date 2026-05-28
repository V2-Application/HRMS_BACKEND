using ClosedXML.Excel;
using DocumentFormat.OpenXml.Bibliography;
using HRMSAPI.Data;
using HRMSAPI.DTO;
using HRMSAPI.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Roomsy.DTOS.GenericsResponses;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace HRMSAPI.Implementation
{
    public class UploaderService : BaseService, IUploaderService
    {
        private readonly HRMSContext _context;
        private readonly ILogger<UploaderService> _logger;

        public UploaderService(HRMSContext context, ILogger<UploaderService> logger) : base(context)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<ExecuteAndReponse> UploadEcodeZoneRegionClusterMappingAsync(IFormFile file, string updatedBy)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return BuildExecuteErrorResponse("No file uploaded", HttpStatusCode.BadRequest);

                var allowedExtensions = new[] { ".xlsx", ".xls" };
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(extension))
                    return BuildExecuteErrorResponse("Only Excel files (.xlsx, .xls) are allowed.", HttpStatusCode.BadRequest);

                var expectedHeaders = new[] { "ECODE", "ZONE", "CLUSTER", "REGION" };

                // Save file to path: wwwroot/EcodeZoneRegionClusterMapping/Year/Month/Date/UpdatedBy/ExcelFile.xlsx
                var now = DateTime.Now;
                var year = now.Year.ToString();
                var month = now.ToString("MM");
                var day = now.ToString("dd");
                var uploader = string.IsNullOrWhiteSpace(updatedBy) ? "Unknown" : updatedBy;
                var folderPath = Path.Combine("wwwroot", "EcodeZoneRegionClusterMapping", year, month, day, uploader);
                Directory.CreateDirectory(folderPath);
                var fileName = Path.GetFileName(file.FileName);
                var savePath = Path.Combine(folderPath, fileName);
                using (var fileStream = new FileStream(savePath, FileMode.Create))
                {
                    await file.CopyToAsync(fileStream);
                }

                using var stream = file.OpenReadStream();
                using var workbook = new XLWorkbook(stream);
                var worksheet = workbook.Worksheet(1);

                // Header validation (names and count)
                var headerRow = worksheet.Row(1);
                var headerCount = headerRow.CellsUsed().Count();
                if (headerCount != expectedHeaders.Length)
                {
                    return BuildExecuteErrorResponse($"Header count mismatch. Expected {expectedHeaders.Length}, found {headerCount}.", HttpStatusCode.BadRequest);
                }
                for (int i = 0; i < expectedHeaders.Length; i++)
                {
                    var cellValue = worksheet.Cell(1, i + 1).GetValue<string>().Trim();
                    if (!string.Equals(cellValue, expectedHeaders[i], StringComparison.OrdinalIgnoreCase))
                        return BuildExecuteErrorResponse($"Header mismatch at column {i + 1}: Expected '{expectedHeaders[i]}', found '{cellValue}'", HttpStatusCode.BadRequest);
                }

                var rows = worksheet.RowsUsed().Skip(1).ToList();
                if (rows.Count == 0)
                    return BuildExecuteErrorResponse("No data rows found in Excel.", HttpStatusCode.BadRequest);

                // Validate no duplicate ECODEs within Excel
                var seenEcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var row in rows)
                {
                    var ecode = row.Cell(1).GetValue<string>()?.Trim();
                    if (string.IsNullOrWhiteSpace(ecode))
                        return BuildExecuteErrorResponse($"ECode cannot be blank at row {row.RowNumber()}.", HttpStatusCode.BadRequest);
                    if (!seenEcodes.Add(ecode))
                        return BuildExecuteErrorResponse($"Duplicate ECode '{ecode}' found in Excel (row {row.RowNumber()}).", HttpStatusCode.BadRequest);
                }

                // Validate all rows before making any DB changes
                var toInsert = new List<EcodeZoneRegionClusterMapping>();
                foreach (var row in rows)
                {
                    var ecode = row.Cell(1).GetValue<string>()?.Trim();
                    var zone = row.Cell(2).GetValue<string>()?.Trim();
                    var cluster = row.Cell(3).GetValue<string>()?.Trim();
                    var region = row.Cell(4).GetValue<string>()?.Trim();

                    if (ecode.Length > 50)
                        return BuildExecuteErrorResponse($"ECode length exceeds 50 at row {row.RowNumber()}.", HttpStatusCode.BadRequest);
                    if (!string.IsNullOrEmpty(zone) && zone.Length > 100)
                        return BuildExecuteErrorResponse($"Zone length exceeds 100 at row {row.RowNumber()}.", HttpStatusCode.BadRequest);
                    if (!string.IsNullOrEmpty(cluster) && cluster.Length > 100)
                        return BuildExecuteErrorResponse($"Cluster length exceeds 100 at row {row.RowNumber()}.", HttpStatusCode.BadRequest);
                    if (!string.IsNullOrEmpty(region) && region.Length > 100)
                        return BuildExecuteErrorResponse($"Region length exceeds 100 at row {row.RowNumber()}.", HttpStatusCode.BadRequest);

                    toInsert.Add(new EcodeZoneRegionClusterMapping
                    {
                        Ecode = ecode,
                        Zone = zone,
                        Cluster = cluster,
                        Region = region
                    });
                }

                // Replace entire hierarchy: remove all existing records, then insert new ones
                var allExisting = await _context.EcodeZoneRegionClusterMappings.ToListAsync();
                _context.EcodeZoneRegionClusterMappings.RemoveRange(allExisting);
                await _context.EcodeZoneRegionClusterMappings.AddRangeAsync(toInsert);

                await _context.SaveChangesAsync();

                return BuildExecuteSuccessResponse($"Hierarchy replaced successfully. {toInsert.Count} records uploaded.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UploadEcodeZoneRegionClusterMappingAsync");
                return BuildExecuteErrorResponse($"Error processing upload: {ex.Message}", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<FetchAndResponse> GetAllEcodeZoneRegionClusterMappingAsync()
        {
            try
            {
                var data = await _context.EcodeZoneRegionClusterMappings
                    .AsNoTracking()
                    .OrderBy(x => x.Ecode)
                    .Select(x => new
                    {
                        x.Ecode,
                        x.Zone,
                        x.Cluster,
                        x.Region
                    })
                    .ToListAsync();
                if (data == null)
                {
                    return BuildFetchErrorResponse("Fetched Successfully", HttpStatusCode.NotFound);
                }
                return BuildFetchSuccessResponse("Fetched Successfully", data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching EcodeZoneRegionCluster mappings");
                return BuildFetchErrorResponse($"Error fetching data: {ex.Message}", HttpStatusCode.BadRequest);
            }
        }

        public async Task<(bool Success, string Message, byte[] FileBytes, string ContentType, string FileName)> GetEcodeZoneRegionClusterMappingExcelAsync()
        {
            try
            {
                var rows = await _context.EcodeZoneRegionClusterMappings
                    .AsNoTracking()
                    .OrderBy(x => x.Ecode)
                    .ToListAsync();

                using var workbook = new XLWorkbook();
                var ws = workbook.AddWorksheet("ECode-Zone-Region-Cluster");
                // Headers
                ws.Cell(1, 1).Value = "ECODE";
                ws.Cell(1, 2).Value = "ZONE";
                ws.Cell(1, 3).Value = "CLUSTER";
                ws.Cell(1, 4).Value = "REGION";

                int r = 2;
                foreach (var x in rows)
                {
                    ws.Cell(r, 1).Value = x.Ecode;
                    ws.Cell(r, 2).Value = x.Zone;
                    ws.Cell(r, 3).Value = x.Cluster;
                    ws.Cell(r, 4).Value = x.Region;
                    r++;
                }

                ws.Columns().AdjustToContents();

                using var ms = new MemoryStream();
                workbook.SaveAs(ms);
                var bytes = ms.ToArray();
                var fileName = $"EcodeZoneRegionClusterMapping_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                return (true, "OK", bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating Excel for EcodeZoneRegionCluster mappings");
                return (false, ex.Message, Array.Empty<byte>(), "", "");
            }
        }
        private bool ParseYesNo(string value)
        {
            return !string.IsNullOrEmpty(value) && value.Trim().ToLower() == "yes";
        }

        public async Task<FetchAndResponse> UploadEmpAttendanceMasterAsync(IFormFile file)
        {
            var expectedHeaders = new[] { "E.CODE", "MONTH", "MACHINE", "MANUAL", "TOTAL PRESENT", "PRESENT ON WEEKLYOFF" };
            if (file == null || file.Length == 0)
                return BuildFetchErrorResponse("No file uploaded", HttpStatusCode.BadRequest);

            // Tolerant decimal parse: numeric cells, numeric-as-text, "", "-", "N/A" all -> 0.
            decimal SafeDecimal(IXLCell cell)
            {
                if (cell == null || cell.IsEmpty()) return 0m;
                try { if (cell.TryGetValue<decimal>(out var d)) return d; } catch { /* fall through to string */ }
                var s = cell.GetValue<string>()?.Trim();
                if (string.IsNullOrEmpty(s)) return 0m;
                return decimal.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d2) ? d2 : 0m;
            }

            try
            {
                using var stream = file.OpenReadStream();
                XLWorkbook workbook;
                try { workbook = new XLWorkbook(stream); }
                catch (System.Exception ex)
                { return BuildFetchErrorResponse($"Could not read Excel file. Please re-save as .xlsx and try again. ({ex.Message})", HttpStatusCode.BadRequest); }

                using (workbook)
                {
                    var worksheet = workbook.Worksheet(1);

                    // Validate headers
                    for (int i = 0; i < expectedHeaders.Length; i++)
                    {
                        var cellValue = worksheet.Cell(1, i + 1).GetValue<string>().Trim();
                        if (!string.Equals(cellValue, expectedHeaders[i], System.StringComparison.OrdinalIgnoreCase))
                            return BuildFetchErrorResponse($"Header mismatch at column {i + 1}: Expected '{expectedHeaders[i]}', found '{cellValue}'", HttpStatusCode.BadRequest);
                    }
                    if (worksheet.Row(1).CellsUsed().Count() != expectedHeaders.Length)
                        return BuildFetchErrorResponse("Header count mismatch", HttpStatusCode.BadRequest);

                    var rows = worksheet.RowsUsed().Skip(1).ToList();

                    // Helper to format month as MMM-yy
                    string FormatMonth(string input)
                    {
                        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
                        input = input.Trim();
                        DateTime dt;
                        // Try parse as date
                        if (DateTime.TryParse(input, out dt))
                        {
                            return dt.ToString("MMM-yy").ToUpper();
                        }
                        // Try parse as MMM-yy or similar
                        if (DateTime.TryParseExact(input, new[] { "MMM-yy", "MMM-yyyy", "MM-yyyy", "MM-yy", "yyyy-MM", "yy-MM" }, null, System.Globalization.DateTimeStyles.None, out dt))
                        {
                            return dt.ToString("MMM-yy").ToUpper();
                        }
                        // If already in format, just uppercase
                        if (input.Length == 6 && input[3] == '-')
                            return input.ToUpper();
                        return input.ToUpper();
                    }

                    // Check for duplicate (E_CODE, MONTH) in Excel
                    var seenKeys = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
                    foreach (var row in rows)
                    {
                        var ecode = row.Cell(1).GetValue<string>()?.Trim();
                        var monthRaw = row.Cell(2).GetValue<string>()?.Trim();
                        var month = FormatMonth(monthRaw);
                        var key = $"{ecode}|{month}";
                        if (!seenKeys.Add(key))
                            return BuildFetchErrorResponse($"Duplicate combination of E_CODE '{ecode}' and MONTH '{month}' found in Excel.", HttpStatusCode.BadRequest);
                    }

                    // Upsert logic using (E_CODE, MONTH) as key
                    var keys = rows.Select(r => new
                    {
                        E_CODE = r.Cell(1).GetValue<string>()?.Trim(),
                        MONTH = FormatMonth(r.Cell(2).GetValue<string>()?.Trim())
                    }).ToList();

                    var ecodes = keys.Select(k => k.E_CODE).Distinct().ToList();
                    var months = keys.Select(k => k.MONTH).Distinct().ToList();

                    // Fetch all possible matches from DB
                    var existing = await _context.EmpAttendanceMasters.AsQueryable()
                        .Where(x => ecodes.Contains(x.E_CODE) && months.Contains(x.MONTH))
                        .ToListAsync();

                    // Build dictionary with composite key.
                    // Trim BOTH sides — prod has historical MONTH values stored with a
                    // trailing space (e.g. "MAY-26 ") while incoming Excel produces clean
                    // "MAY-26". SQL Server's unique-key collation treats them as equal,
                    // but C# string equality (StringComparer) does not — without trimming,
                    // the lookup misses and we try to INSERT a duplicate, hitting
                    // UQ_EmpAttendanceMaster_ECode_Month.
                    var existingDict = existing.ToDictionary(
                        x => $"{(x.E_CODE ?? string.Empty).Trim()}|{(x.MONTH ?? string.Empty).Trim()}",
                        System.StringComparer.OrdinalIgnoreCase);

                    var newRows = new List<EmpAttendanceMaster>();
                    var updatedRows = new List<EmpAttendanceMaster>();

                    foreach (var row in rows)
                    {
                        var ecode = row.Cell(1).GetValue<string>()?.Trim();
                        var monthRaw = row.Cell(2).GetValue<string>()?.Trim();
                        var month = FormatMonth(monthRaw);
                        if (string.IsNullOrEmpty(ecode) || string.IsNullOrEmpty(month)) continue;
                        var key = $"{ecode}|{month}";

                        if (existingDict.TryGetValue(key, out var existingRow))
                        {
                            // Update — only the data columns. MONTH text format is
                            // intentionally preserved (do not rewrite "MAY-26 " to "MAY-26")
                            // so we never alter the structural key of an existing row.
                            existingRow.MACHINE = row.Cell(3).GetValue<string>()?.Trim();
                            existingRow.MANUAL = row.Cell(4).GetValue<string>()?.Trim();
                            existingRow.TOTAL_PRESENT = SafeDecimal(row.Cell(5));
                            existingRow.PRESENT_ON_WEEKLYOFF = SafeDecimal(row.Cell(6));
                            updatedRows.Add(existingRow);
                        }
                        else
                        {
                            // Insert
                            newRows.Add(new EmpAttendanceMaster
                            {
                                E_CODE = ecode,
                                MONTH = month,
                                MACHINE = row.Cell(3).GetValue<string>()?.Trim(),
                                MANUAL = row.Cell(4).GetValue<string>()?.Trim(),
                                TOTAL_PRESENT = SafeDecimal(row.Cell(5)),
                                PRESENT_ON_WEEKLYOFF = SafeDecimal(row.Cell(6)),
                                GF = 0
                            });
                        }
                    }

                    if (newRows.Any())
                        await _context.EmpAttendanceMasters.AddRangeAsync(newRows);
                    if (updatedRows.Any())
                        _context.EmpAttendanceMasters.UpdateRange(updatedRows);

                    await _context.SaveChangesAsync();
                    return BuildFetchSuccessResponse(
                        $"EmpAttendanceMaster uploaded successfully. Inserted: {newRows.Count}, Updated: {updatedRows.Count}.", null);
                }
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error uploading EmpAttendanceMaster");
                // Walk the inner-exception chain so EF's generic "See the inner exception"
                // doesn't hide the actual SQL/constraint message from the caller.
                var msgs = new List<string>();
                for (var cur = ex; cur != null; cur = cur.InnerException)
                    msgs.Add($"{cur.GetType().Name}: {cur.Message}");
                return BuildFetchErrorResponse(
                    "Error uploading EmpAttendanceMaster: " + string.Join(" -> ", msgs),
                    HttpStatusCode.BadRequest);
            }
        }
        public async Task<FetchAndResponse> GetAllEmpAttendanceMasterAsync()
        {
            try
            {
                var data = await _context.EmpAttendanceMasters.AsNoTracking().ToListAsync();
                if (data == null || data.Count < 1)
                    return BuildFetchErrorResponse("No Data Found", HttpStatusCode.NotFound);
                return BuildFetchSuccessResponse("Fetched all EmpAttendanceMaster records successfully", data);
            }
            catch (System.Exception ex)
            {
                return BuildFetchErrorResponse(ex.Message, HttpStatusCode.BadRequest);
            }
        }
        public async Task<FetchAndResponse> UploadEmpTDSTableAsync(IFormFile file)
        {
            var expectedHeaders = new[] { "E.CODE", "MTH", "TDS", "PTax", "Loan", "CashShort", "DieselDeduction", "Penality", "Lwf" };
            if (file == null || file.Length == 0)
                return BuildFetchErrorResponse("No file uploaded", HttpStatusCode.BadRequest);

            using var stream = file.OpenReadStream();
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheet(1);

            // Validate headers
            for (int i = 0; i < expectedHeaders.Length; i++)
            {
                var cellValue = worksheet.Cell(1, i + 1).GetValue<string>().Trim();
                if (!string.Equals(cellValue, expectedHeaders[i], System.StringComparison.OrdinalIgnoreCase))
                    return BuildFetchErrorResponse($"Header mismatch at column {i + 1}: Expected '{expectedHeaders[i]}', found '{cellValue}'", HttpStatusCode.BadRequest);
            }
            if (worksheet.Row(1).CellsUsed().Count() != expectedHeaders.Length)
                return BuildFetchErrorResponse("Header count mismatch", HttpStatusCode.BadRequest);

            var rows = worksheet.RowsUsed().Skip(1).ToList();

            // Check for duplicate (E_CODE, MTH) in Excel
            var seenKeys = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                var ecode = row.Cell(1).GetValue<string>()?.Trim();
                var mth = row.Cell(2).GetValue<string>()?.Trim();
                var key = $"{ecode}|{mth}";
                if (!seenKeys.Add(key))
                    return BuildFetchErrorResponse($"Duplicate combination of E_CODE '{ecode}' and MTH '{mth}' found in Excel.", HttpStatusCode.BadRequest);
            }

            // Upsert logic using (E_CODE, MTH) as key
            var keys = rows.Select(r => new
            {
                E_CODE = r.Cell(1).GetValue<string>()?.Trim(),
                MTH = r.Cell(2).GetValue<string>()?.Trim()
            }).ToList();

            var ecodes = keys.Select(k => k.E_CODE).Distinct().ToList();
            var mths = keys.Select(k => k.MTH).Distinct().ToList();

            // Fetch all possible matches from DB
            var existing = await _context.EmpTDSTables.AsQueryable()
                .Where(x => ecodes.Contains(x.E_CODE) && mths.Contains(x.MTH))
                .ToListAsync();

            // Build dictionary with composite key
            var existingDict = existing.ToDictionary(x => $"{x.E_CODE}|{x.MTH}", System.StringComparer.OrdinalIgnoreCase);

            var newRows = new List<EmpTDSTable>();
            var updatedRows = new List<EmpTDSTable>();

            foreach (var row in rows)
            {
                var ecode = row.Cell(1).GetValue<string>()?.Trim();
                var mth = row.Cell(2).GetValue<string>()?.Trim();
                if (string.IsNullOrEmpty(ecode) || string.IsNullOrEmpty(mth)) continue;
                var key = $"{ecode}|{mth}";

                if (existingDict.TryGetValue(key, out var existingRow))
                {
                    // Update only if cell is not empty
                    if (!string.IsNullOrWhiteSpace(row.Cell(3).GetValue<string>()))
                        existingRow.TDS = row.Cell(3).GetValue<decimal>();
                    if (!string.IsNullOrWhiteSpace(row.Cell(4).GetValue<string>()))
                        existingRow.PTax = row.Cell(4).GetValue<decimal>();
                    if (!string.IsNullOrWhiteSpace(row.Cell(5).GetValue<string>()))
                        existingRow.Loan = row.Cell(5).GetValue<decimal>();
                    if (!string.IsNullOrWhiteSpace(row.Cell(6).GetValue<string>()))
                        existingRow.CashShort = row.Cell(6).GetValue<decimal>();
                    if (!string.IsNullOrWhiteSpace(row.Cell(7).GetValue<string>()))
                        existingRow.DieselDeduction = row.Cell(7).GetValue<decimal>();
                    if (!string.IsNullOrWhiteSpace(row.Cell(8).GetValue<string>()))
                        existingRow.Penality = row.Cell(8).GetValue<decimal>();
                    if (!string.IsNullOrWhiteSpace(row.Cell(9).GetValue<string>()))
                        existingRow.Lwf = row.Cell(9).GetValue<decimal>();
                    existingRow.MTH = mth; // Ensure format
                    updatedRows.Add(existingRow);
                }
                else
                {
                    // Insert: only set value if cell is not empty, else default
                    newRows.Add(new EmpTDSTable
                    {
                        E_CODE = ecode,
                        MTH = mth,
                        TDS = !string.IsNullOrWhiteSpace(row.Cell(3).GetValue<string>()) ? row.Cell(3).GetValue<decimal>() : default(decimal),
                        PTax = !string.IsNullOrWhiteSpace(row.Cell(4).GetValue<string>()) ? row.Cell(4).GetValue<decimal>() : default(decimal),
                        Loan = !string.IsNullOrWhiteSpace(row.Cell(5).GetValue<string>()) ? row.Cell(5).GetValue<decimal>() : default(decimal),
                        CashShort = !string.IsNullOrWhiteSpace(row.Cell(6).GetValue<string>()) ? row.Cell(6).GetValue<decimal>() : default(decimal),
                        DieselDeduction = !string.IsNullOrWhiteSpace(row.Cell(7).GetValue<string>()) ? row.Cell(7).GetValue<decimal>() : default(decimal),
                        Penality = !string.IsNullOrWhiteSpace(row.Cell(8).GetValue<string>()) ? row.Cell(8).GetValue<decimal>() : default(decimal),
                        Lwf = !string.IsNullOrWhiteSpace(row.Cell(9).GetValue<string>()) ? row.Cell(9).GetValue<decimal>() : default(decimal)
                    });
                }
            }

            try
            {
                if (newRows.Any())
                    await _context.EmpTDSTables.AddRangeAsync(newRows);
                if (updatedRows.Any())
                    _context.EmpTDSTables.UpdateRange(updatedRows);

                await _context.SaveChangesAsync();
                return BuildFetchSuccessResponse("EmpTDSTable uploaded successfully", null);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error uploading EmpTDSTable");
                return BuildFetchErrorResponse($"Error uploading EmpTDSTable: {ex.Message}", HttpStatusCode.BadRequest);
            }
        }
        public async Task<FetchAndResponse> GetAllEmpTDSTableAsync()
        {
            try
            {
                var data = await _context.EmpTDSTables.AsNoTracking().ToListAsync();
                if (data == null || data.Count < 1)
                    return BuildFetchErrorResponse("No Data Found", HttpStatusCode.NotFound);
                return BuildFetchSuccessResponse("Fetched all EmpTDSTable records successfully", data);
            }
            catch (System.Exception ex)
            {
                return BuildFetchErrorResponse(ex.Message, HttpStatusCode.BadRequest);
            }
        }
        public async Task<FetchAndResponse> UploadApplicabilityMasterAsync(IFormFile file)
        {
            var expectedHeaders = new[] { "E.CODE", "P.F. APPLICABLE", "EPS APPLICABLE", "P.TAX APPLICABLE", "ESIC APPLICABLE", "EXTRA DAY APPLICABLE" };
            if (file == null || file.Length == 0)
                return BuildFetchErrorResponse("No file uploaded", HttpStatusCode.BadRequest);

            using var stream = file.OpenReadStream();
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheet(1);

            for (int i = 0; i < expectedHeaders.Length; i++)
            {
                var cellValue = worksheet.Cell(1, i + 1).GetValue<string>().Trim();
                if (!string.Equals(cellValue, expectedHeaders[i], StringComparison.OrdinalIgnoreCase))
                    return BuildFetchErrorResponse($"Header mismatch at column {i + 1}: Expected '{expectedHeaders[i]}', found '{cellValue}'", HttpStatusCode.BadRequest);
            }
            if (worksheet.Row(1).CellsUsed().Count() != expectedHeaders.Length)
                return BuildFetchErrorResponse("Header count mismatch", HttpStatusCode.BadRequest);

            var rows = worksheet.RowsUsed().Skip(1).ToList();
            var seenEcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                var ecode = row.Cell(1).GetValue<string>()?.Trim();
                if (!seenEcodes.Add(ecode))
                    return BuildFetchErrorResponse($"Duplicate E_CODE '{ecode}' found in Excel.", HttpStatusCode.BadRequest);
            }

            var ecodes = rows.Select(r => r.Cell(1).GetValue<string>()?.Trim()).ToList();
            var existing = await _context.ApplicabilityMasters.AsQueryable()
                .Where(x => ecodes.Contains(x.E_CODE))
                .ToListAsync();
            var existingDict = existing.ToDictionary(x => x.E_CODE, StringComparer.OrdinalIgnoreCase);

            var newRows = new List<ApplicabilityMaster>();
            var updatedRows = new List<ApplicabilityMaster>();

            foreach (var row in rows)
            {
                var ecode = row.Cell(1).GetValue<string>()?.Trim();
                if (string.IsNullOrEmpty(ecode)) continue;

                if (existingDict.TryGetValue(ecode, out var existingRow))
                {
                    existingRow.PF_APPLICABLE = ParseYesNo(row.Cell(2).GetValue<string>());
                    existingRow.EPS_APPLICABLE = ParseYesNo(row.Cell(3).GetValue<string>());
                    existingRow.PTAX_APPLICABLE = ParseYesNo(row.Cell(4).GetValue<string>());
                    existingRow.ESIC_APPLICABLE = ParseYesNo(row.Cell(5).GetValue<string>());
                    existingRow.EXTRA_DAY_APPLICABLE = ParseYesNo(row.Cell(6).GetValue<string>());
                    updatedRows.Add(existingRow);
                }
                else
                {
                    newRows.Add(new ApplicabilityMaster
                    {
                        E_CODE = ecode,
                        PF_APPLICABLE = ParseYesNo(row.Cell(2).GetValue<string>()),
                        EPS_APPLICABLE = ParseYesNo(row.Cell(3).GetValue<string>()),
                        PTAX_APPLICABLE = ParseYesNo(row.Cell(4).GetValue<string>()),
                        ESIC_APPLICABLE = ParseYesNo(row.Cell(5).GetValue<string>()),
                        EXTRA_DAY_APPLICABLE = ParseYesNo(row.Cell(6).GetValue<string>())
                    });
                }
            }

            try
            {
                if (newRows.Any())
                    await _context.ApplicabilityMasters.AddRangeAsync(newRows);
                if (updatedRows.Any())
                    _context.ApplicabilityMasters.UpdateRange(updatedRows);

                await _context.SaveChangesAsync();
                return BuildFetchSuccessResponse("ApplicabilityMaster uploaded successfully", null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading ApplicabilityMaster");
                return BuildFetchErrorResponse($"Error uploading ApplicabilityMaster: {ex.Message}", HttpStatusCode.BadRequest);
            }
        }
        public async Task<FetchAndResponse> GetAllApplicabilityMasterAsync()
        {
            try
            {
                var data = await _context.ApplicabilityMasters.AsNoTracking().ToListAsync();
                if (data == null || data.Count < 1)
                    return BuildFetchErrorResponse("No Data Found", HttpStatusCode.NotFound);
                return BuildFetchSuccessResponse("Fetched all ApplicabilityMaster records successfully", data);
            }
            catch (Exception ex)
            {
                return BuildFetchErrorResponse(ex.Message, HttpStatusCode.BadRequest);
            }
        }
        public async Task<FetchAndResponse> UploadEmpSalaryStructureAsync(IFormFile file)
        {
            var expectedHeaders = new[] { "E.CODE", "BASIC RATE", "HRA RATE", "DA RATE", "CCA RATE", "SPL ALLOWANCE RATE", "REIMB RATE", "Fuel_and_Maintainence", "Books_and_Periodicals", "Professional Attire", "Driver Wages", "MOBILE BIll", "Meal Voucher" };
            if (file == null || file.Length == 0)
                return BuildFetchErrorResponse("No file uploaded", HttpStatusCode.BadRequest);

            using var stream = file.OpenReadStream();
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheet(1);

            for (int i = 0; i < expectedHeaders.Length; i++)
            {
                var cellValue = worksheet.Cell(1, i + 1).GetValue<string>().Trim();
                if (!string.Equals(cellValue, expectedHeaders[i], StringComparison.OrdinalIgnoreCase))
                    return BuildFetchErrorResponse($"Header mismatch at column {i + 1}: Expected '{expectedHeaders[i]}', found '{cellValue}'", HttpStatusCode.BadRequest);
            }
            if (worksheet.Row(1).CellsUsed().Count() != expectedHeaders.Length)
                return BuildFetchErrorResponse("Header count mismatch", HttpStatusCode.BadRequest);

            var rows = worksheet.RowsUsed().Skip(1).ToList();
            var seenEcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                var ecode = row.Cell(1).GetValue<string>()?.Trim();
                if (!seenEcodes.Add(ecode))
                    return BuildFetchErrorResponse($"Duplicate E_CODE '{ecode}' found in Excel.", HttpStatusCode.BadRequest);
            }

            var ecodes = rows.Select(r => r.Cell(1).GetValue<string>()?.Trim()).ToList();
            var existing = await _context.EmpSalaryStructures.AsQueryable()
                .Where(x => ecodes.Contains(x.E_CODE))
                .ToListAsync();
            var existingDict = existing.ToDictionary(x => x.E_CODE, StringComparer.OrdinalIgnoreCase);

            var newRows = new List<EmpSalaryStructure>();
            var updatedRows = new List<EmpSalaryStructure>();

            foreach (var row in rows)
            {
                var ecode = row.Cell(1).GetValue<string>()?.Trim();
                if (string.IsNullOrEmpty(ecode)) continue;

                if (existingDict.TryGetValue(ecode, out var existingRow))
                {
                    existingRow.BASIC_RATE = row.Cell(2).GetValue<decimal>();
                    existingRow.HRA_RATE = row.Cell(3).GetValue<decimal>();
                    existingRow.DA_RATE = row.Cell(4).GetValue<decimal>();
                    existingRow.CCA_RATE = row.Cell(5).GetValue<decimal>();
                    existingRow.SPL_ALLOWANCE_RATE = row.Cell(6).GetValue<decimal>();
                    existingRow.REIMB_RATE = row.Cell(7).GetValue<decimal>();
                    existingRow.Fuel_and_Maintainence = row.Cell(8).GetValue<decimal>();
                    existingRow.Books_and_Periodicals = row.Cell(9).GetValue<decimal>();
                    existingRow.ProfessionalAttire = row.Cell(10).GetValue<decimal>();
                    existingRow.DriverWages = row.Cell(11).GetValue<decimal>();
                    existingRow.MOBILE_BILL = row.Cell(12).GetValue<decimal>();
                    existingRow.MealVoucher = row.Cell(13).GetValue<decimal>();
                    updatedRows.Add(existingRow);
                }
                else
                {
                    newRows.Add(new EmpSalaryStructure
                    {
                        E_CODE = ecode,
                        BASIC_RATE = row.Cell(2).GetValue<decimal>(),
                        HRA_RATE = row.Cell(3).GetValue<decimal>(),
                        DA_RATE = row.Cell(4).GetValue<decimal>(),
                        CCA_RATE = row.Cell(5).GetValue<decimal>(),
                        SPL_ALLOWANCE_RATE = row.Cell(6).GetValue<decimal>(),
                        REIMB_RATE = row.Cell(7).GetValue<decimal>(),
                        Fuel_and_Maintainence = row.Cell(8).GetValue<decimal>(),
                        Books_and_Periodicals = row.Cell(9).GetValue<decimal>(),
                        ProfessionalAttire = row.Cell(10).GetValue<decimal>(),
                        DriverWages = row.Cell(11).GetValue<decimal>(),
                        MOBILE_BILL = row.Cell(12).GetValue<decimal>(),
                        MealVoucher = row.Cell(13).GetValue<decimal>()
                    });
                }
            }

            try
            {
                if (newRows.Any())
                    await _context.EmpSalaryStructures.AddRangeAsync(newRows);
                if (updatedRows.Any())
                    _context.EmpSalaryStructures.UpdateRange(updatedRows);

                await _context.SaveChangesAsync();
                return BuildFetchSuccessResponse("EmpSalaryStructure uploaded successfully", null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading EmpSalaryStructure");
                return BuildFetchErrorResponse($"Error uploading EmpSalaryStructure: {ex.Message}", HttpStatusCode.BadRequest);
            }
        }
        public async Task<FetchAndResponse> GetAllEmpSalaryStructureAsync()
        {
            try
            {
                var data = await _context.EmpSalaryStructures.AsNoTracking().ToListAsync();
                if (data == null || data.Count < 1)
                    return BuildFetchErrorResponse("No Data Found", HttpStatusCode.NotFound);
                return BuildFetchSuccessResponse("Fetched all EmpSalaryStructure records successfully", data);
            }
            catch (Exception ex)
            {
                return BuildFetchErrorResponse(ex.Message, HttpStatusCode.BadRequest);
            }
        }
        public async Task<FetchAndResponse> UploadLeaveOpeningBalTableAsync(IFormFile file)
        {
            try
            {
                var expectedHeaders = new[] { "E.CODE", "MONTH", "EL", "CL", "COMP OFF", "SL" };
                if (file == null || file.Length == 0)
                    return BuildFetchErrorResponse("No file uploaded", HttpStatusCode.BadRequest);

                using var stream = file.OpenReadStream();
                using var workbook = new XLWorkbook(stream);
                var worksheet = workbook.Worksheet(1);

                // Validate headers
                for (int i = 0; i < expectedHeaders.Length; i++)
                {
                    var cellValue = worksheet.Cell(1, i + 1).GetValue<string>().Trim();
                    if (!string.Equals(cellValue, expectedHeaders[i], StringComparison.OrdinalIgnoreCase))
                        return BuildFetchErrorResponse($"Header mismatch at column {i + 1}: Expected '{expectedHeaders[i]}', found '{cellValue}'", HttpStatusCode.BadRequest);
                }
                if (worksheet.Row(1).CellsUsed().Count() != expectedHeaders.Length)
                    return BuildFetchErrorResponse("Header count mismatch", HttpStatusCode.BadRequest);

                // Read rows, skipping header
                var rows = worksheet.RowsUsed().Skip(1).ToList();

                // Detect duplicates in Excel based on (E_CODE, MONTH)
                var seenKeys = new HashSet<(string, string)>();
                foreach (var row in rows)
                {
                    var ecode = row.Cell(1).GetValue<string>()?.Trim();
                    var month = row.Cell(2).GetValue<string>()?.Trim();
                    if (string.IsNullOrEmpty(ecode) || string.IsNullOrEmpty(month))
                        return BuildFetchErrorResponse("E_CODE and MONTH cannot be empty.", HttpStatusCode.BadRequest);

                    var key = (ecode.ToLower(), month.ToLower());
                    if (!seenKeys.Add(key))
                        return BuildFetchErrorResponse($"Duplicate (E_CODE, MONTH) '{ecode}, {month}' found in Excel.", HttpStatusCode.BadRequest);
                }

                var keyStrings = rows
    .Select(r => $"{r.Cell(1).GetValue<string>()?.Trim().ToLower()}|{r.Cell(2).GetValue<string>()?.Trim().ToLower()}")
    .ToList();

                var existing = await _context.LeaveOpeningBalTables
                    .Where(x => keyStrings.Contains(x.E_CODE.ToLower() + "|" + x.MONTH.ToLower()))
                    .ToListAsync();


                // Build dictionary with (E_CODE, MONTH) as key
                var existingDict = existing.ToDictionary(
                    x => (x.E_CODE.ToLower(), x.MONTH.ToLower())
                );

                var newRows = new List<LeaveOpeningBalTable>();
                var updatedRows = new List<LeaveOpeningBalTable>();

                // Process each Excel row
                foreach (var row in rows)
                {
                    var ecode = row.Cell(1).GetValue<string>()?.Trim();
                    var month = row.Cell(2).GetValue<string>()?.Trim();
                    if (string.IsNullOrEmpty(ecode) || string.IsNullOrEmpty(month)) continue;

                    decimal elVal, clVal, compOffVal, slVal;
                    bool hasEL = decimal.TryParse(row.Cell(3).GetValue<string>(), out elVal);
                    bool hasCL = decimal.TryParse(row.Cell(4).GetValue<string>(), out clVal);
                    bool hasCompOff = decimal.TryParse(row.Cell(5).GetValue<string>(), out compOffVal);
                    bool hasSL = decimal.TryParse(row.Cell(6).GetValue<string>(), out slVal);

                    var key = (ecode.ToLower(), month.ToLower());

                    if (existingDict.TryGetValue(key, out var existingRow))
                    {
                        if (hasEL) existingRow.EL = elVal;
                        if (hasCL) existingRow.CL = clVal;
                        if (hasCompOff) existingRow.COMP_OFF = compOffVal;
                        if (hasSL) existingRow.SL = slVal;
                        updatedRows.Add(existingRow);
                    }
                    else
                    {
                        newRows.Add(new LeaveOpeningBalTable
                        {
                            E_CODE = ecode,
                            MONTH = month,
                            EL = hasEL ? elVal : 0,
                            CL = hasCL ? clVal : 0,
                            COMP_OFF = hasCompOff ? compOffVal : 0,
                            SL = hasSL ? slVal : 0
                        });
                    }
                }

                try
                {
                    if (newRows.Any())
                        await _context.LeaveOpeningBalTables.AddRangeAsync(newRows);
                    if (updatedRows.Any())
                        _context.LeaveOpeningBalTables.UpdateRange(updatedRows);

                    await _context.SaveChangesAsync();
                    return BuildFetchSuccessResponse("LeaveOpeningBalTable uploaded successfully", null);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error uploading LeaveOpeningBalTable");
                    return BuildFetchErrorResponse($"Error uploading LeaveOpeningBalTable: {ex.Message}", HttpStatusCode.BadRequest);
                }
            }
            catch (Exception ex)
            {
                return BuildFetchErrorResponse($"Error uploading LeaveOpeningBalTable: {ex.Message}", HttpStatusCode.BadRequest);
            }
        }
        public async Task<FetchAndResponse> GetAllLeaveOpeningBalTableAsync()
        {
            try
            {
                var data = await _context.vw_LeaveOpeningBalTables.AsNoTracking().ToListAsync();
                if (data == null || data.Count < 1)
                    return BuildFetchErrorResponse("No Data Found", HttpStatusCode.NotFound);
                return BuildFetchSuccessResponse("Fetched all LeaveOpeningBalTable records successfully", data);
            }
            catch (Exception ex)
            {
                return BuildFetchErrorResponse(ex.Message, HttpStatusCode.BadRequest);
            }
        }
        public async Task<FetchAndResponse> UploadEmpPersonalDetailsAsync(IFormFile file)
        {
            var expectedHeaders = new[] { "E.CODE", "NAME", "GENDER", "FATHER NAME", "MOBILE NO", "ADDRESS DETAIL", "D.O.B" };
            if (file == null || file.Length == 0)
                return BuildFetchErrorResponse("No file uploaded", HttpStatusCode.BadRequest);

            using var stream = file.OpenReadStream();
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheet(1);

            for (int i = 0; i < expectedHeaders.Length; i++)
            {
                var cellValue = worksheet.Cell(1, i + 1).GetValue<string>().Trim();
                if (!string.Equals(cellValue, expectedHeaders[i], StringComparison.OrdinalIgnoreCase))
                    return BuildFetchErrorResponse($"Header mismatch at column {i + 1}: Expected '{expectedHeaders[i]}', found '{cellValue}'", HttpStatusCode.BadRequest);
            }
            if (worksheet.Row(1).CellsUsed().Count() != expectedHeaders.Length)
                return BuildFetchErrorResponse("Header count mismatch", HttpStatusCode.BadRequest);

            var rows = worksheet.RowsUsed().Skip(1).ToList();
            var seenEcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                var ecode = row.Cell(1).GetValue<string>()?.Trim();
                if (!seenEcodes.Add(ecode))
                    return BuildFetchErrorResponse($"Duplicate E_CODE '{ecode}' found in Excel.", HttpStatusCode.BadRequest);
            }

            var ecodes = rows.Select(r => r.Cell(1).GetValue<string>()?.Trim()).ToList();
            var existing = await _context.EmpPersonalDetails.AsQueryable()
                .Where(x => ecodes.Contains(x.E_CODE))
                .ToListAsync();
            var existingDict = existing.ToDictionary(x => x.E_CODE, StringComparer.OrdinalIgnoreCase);

            var newRows = new List<EmpPersonalDetail>();
            var updatedRows = new List<EmpPersonalDetail>();

            foreach (var row in rows)
            {
                var ecode = row.Cell(1).GetValue<string>()?.Trim();
                if (string.IsNullOrEmpty(ecode)) continue;

                if (existingDict.TryGetValue(ecode, out var existingRow))
                {
                    existingRow.NAME = row.Cell(2).GetValue<string>()?.Trim();
                    existingRow.GENDER = row.Cell(3).GetValue<string>()?.Trim();
                    existingRow.FATHER_NAME = row.Cell(4).GetValue<string>()?.Trim();
                    existingRow.MOBILE_NO = row.Cell(5).GetValue<string>()?.Trim();
                    existingRow.ADDRESS_DETAIL = row.Cell(6).GetValue<string>()?.Trim();
                    existingRow.D_O_B = row.Cell(7).GetValue<DateTime>();
                    updatedRows.Add(existingRow);
                }
                else
                {
                    newRows.Add(new EmpPersonalDetail
                    {
                        E_CODE = ecode,
                        NAME = row.Cell(2).GetValue<string>()?.Trim(),
                        GENDER = row.Cell(3).GetValue<string>()?.Trim(),
                        FATHER_NAME = row.Cell(4).GetValue<string>()?.Trim(),
                        MOBILE_NO = row.Cell(5).GetValue<string>()?.Trim(),
                        ADDRESS_DETAIL = row.Cell(6).GetValue<string>()?.Trim(),
                        D_O_B = row.Cell(7).GetValue<DateTime>()
                    });
                }
            }

            try
            {
                if (newRows.Any())
                    await _context.EmpPersonalDetails.AddRangeAsync(newRows);
                if (updatedRows.Any())
                    _context.EmpPersonalDetails.UpdateRange(updatedRows);

                await _context.SaveChangesAsync();
                return BuildFetchSuccessResponse("EmpPersonalDetails uploaded successfully", null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading EmpPersonalDetails");
                return BuildFetchErrorResponse($"Error uploading EmpPersonalDetails: {ex.Message}", HttpStatusCode.BadRequest);
            }
        }
        public async Task<FetchAndResponse> GetAllEmpPersonalDetailsAsync()
        {
            try
            {
                var data = await _context.EmpPersonalDetails.AsNoTracking().ToListAsync();
                if (data == null || data.Count < 1)
                    return BuildFetchErrorResponse("No Data Found", HttpStatusCode.NotFound);
                return BuildFetchSuccessResponse("Fetched all EmpPersonalDetails records successfully", data);
            }
            catch (Exception ex)
            {
                return BuildFetchErrorResponse(ex.Message, HttpStatusCode.BadRequest);
            }
        }
        public async Task<FetchAndResponse> UploadEmpStatutoryDetailsAsync(IFormFile file)
        {
            var expectedHeaders = new[] { "E.CODE", "Name of Bank", "IFSC Code", "A/c No.", "U.A.N NO", "P.F.NO.", "E.S.I NO", "PAN NO", "AADHAR NO" };
            if (file == null || file.Length == 0)
                return BuildFetchErrorResponse("No file uploaded", HttpStatusCode.BadRequest);

            using var stream = file.OpenReadStream();
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheet(1);

            for (int i = 0; i < expectedHeaders.Length; i++)
            {
                var cellValue = worksheet.Cell(1, i + 1).GetValue<string>().Trim();
                if (!string.Equals(cellValue, expectedHeaders[i], StringComparison.OrdinalIgnoreCase))
                    return BuildFetchErrorResponse($"Header mismatch at column {i + 1}: Expected '{expectedHeaders[i]}', found '{cellValue}'", HttpStatusCode.BadRequest);
            }
            if (worksheet.Row(1).CellsUsed().Count() != expectedHeaders.Length)
                return BuildFetchErrorResponse("Header count mismatch", HttpStatusCode.BadRequest);

            var rows = worksheet.RowsUsed().Skip(1).ToList();
            var seenEcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                var ecode = row.Cell(1).GetValue<string>()?.Trim();
                if (!seenEcodes.Add(ecode))
                    return BuildFetchErrorResponse($"Duplicate E_CODE '{ecode}' found in Excel.", HttpStatusCode.BadRequest);
            }

            var ecodes = rows.Select(r => r.Cell(1).GetValue<string>()?.Trim()).ToList();
            var existing = await _context.EmpStatutoryDetails.AsQueryable()
                .Where(x => ecodes.Contains(x.E_CODE))
                .ToListAsync();
            var existingDict = existing.ToDictionary(x => x.E_CODE, StringComparer.OrdinalIgnoreCase);

            var newRows = new List<EmpStatutoryDetail>();
            var updatedRows = new List<EmpStatutoryDetail>();

            foreach (var row in rows)
            {
                var ecode = row.Cell(1).GetValue<string>()?.Trim();
                if (string.IsNullOrEmpty(ecode)) continue;

                if (existingDict.TryGetValue(ecode, out var existingRow))
                {
                    existingRow.Name_of_Bank = row.Cell(2).GetValue<string>()?.Trim();
                    existingRow.IFSC_Code = row.Cell(3).GetValue<string>()?.Trim();
                    existingRow.AC_NO = row.Cell(4).GetValue<string>()?.Trim();
                    existingRow.UAN_NO = row.Cell(5).GetValue<string>()?.Trim();
                    existingRow.PF_NO = row.Cell(6).GetValue<string>()?.Trim();
                    existingRow.ESI_NO = row.Cell(7).GetValue<string>()?.Trim();
                    existingRow.PAN_NO = row.Cell(8).GetValue<string>()?.Trim();
                    existingRow.AADHAR_NO = row.Cell(9).GetValue<string>()?.Trim();
                    updatedRows.Add(existingRow);
                }
                else
                {
                    newRows.Add(new EmpStatutoryDetail
                    {
                        E_CODE = ecode,
                        Name_of_Bank = row.Cell(2).GetValue<string>()?.Trim(),
                        IFSC_Code = row.Cell(3).GetValue<string>()?.Trim(),
                        AC_NO = row.Cell(4).GetValue<string>()?.Trim(),
                        UAN_NO = row.Cell(5).GetValue<string>()?.Trim(),
                        PF_NO = row.Cell(6).GetValue<string>()?.Trim(),
                        ESI_NO = row.Cell(7).GetValue<string>()?.Trim(),
                        PAN_NO = row.Cell(8).GetValue<string>()?.Trim(),
                        AADHAR_NO = row.Cell(9).GetValue<string>()?.Trim()
                    });
                }
            }

            try
            {
                if (newRows.Any())
                    await _context.EmpStatutoryDetails.AddRangeAsync(newRows);
                if (updatedRows.Any())
                    _context.EmpStatutoryDetails.UpdateRange(updatedRows);

                await _context.SaveChangesAsync();
                return BuildFetchSuccessResponse("EmpStatutoryDetails uploaded successfully", null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading EmpStatutoryDetails");
                return BuildFetchErrorResponse($"Error uploading EmpStatutoryDetails: {ex.Message}", HttpStatusCode.BadRequest);
            }
        }
        public async Task<FetchAndResponse> GetAllEmpStatutoryDetailsAsync()
        {
            try
            {
                var data = await _context.EmpStatutoryDetails.AsNoTracking().ToListAsync();
                if (data == null || data.Count < 1)
                    return BuildFetchErrorResponse("No Data Found", HttpStatusCode.NotFound);
                return BuildFetchSuccessResponse("Fetched all EmpStatutoryDetails records successfully", data);
            }
            catch (Exception ex)
            {
                return BuildFetchErrorResponse(ex.Message, HttpStatusCode.BadRequest);
            }
        }
        public async Task<FetchAndResponse> UploadEmpDegreeQualificationAsync(IFormFile file)
        {
            var expectedHeaders = new[] { "E.CODE", "NAME OF THE DEGREE", "YEAR OF PASSING", "GRADE" };
            if (file == null || file.Length == 0)
                return BuildFetchErrorResponse("No file uploaded", HttpStatusCode.BadRequest);

            using var stream = file.OpenReadStream();
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheet(1);

            for (int i = 0; i < expectedHeaders.Length; i++)
            {
                var cellValue = worksheet.Cell(1, i + 1).GetValue<string>().Trim();
                if (!string.Equals(cellValue, expectedHeaders[i], StringComparison.OrdinalIgnoreCase))
                    return BuildFetchErrorResponse($"Header mismatch at column {i + 1}: Expected '{expectedHeaders[i]}', found '{cellValue}'", HttpStatusCode.BadRequest);
            }
            if (worksheet.Row(1).CellsUsed().Count() != expectedHeaders.Length)
                return BuildFetchErrorResponse("Header count mismatch", HttpStatusCode.BadRequest);

            var rows = worksheet.RowsUsed().Skip(1).ToList();
            var seenEcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                var ecode = row.Cell(1).GetValue<string>()?.Trim();
                if (!seenEcodes.Add(ecode))
                    return BuildFetchErrorResponse($"Duplicate E_CODE '{ecode}' found in Excel.", HttpStatusCode.BadRequest);
            }

            var ecodes = rows.Select(r => r.Cell(1).GetValue<string>()?.Trim()).ToList();
            var existing = await _context.EmpDegreeQualifications.AsQueryable()
                .Where(x => ecodes.Contains(x.E_CODE))
                .ToListAsync();
            var existingDict = existing.ToDictionary(x => x.E_CODE, StringComparer.OrdinalIgnoreCase);

            var newRows = new List<EmpDegreeQualification>();
            var updatedRows = new List<EmpDegreeQualification>();

            foreach (var row in rows)
            {
                var ecode = row.Cell(1).GetValue<string>()?.Trim();
                if (string.IsNullOrEmpty(ecode)) continue;

                if (existingDict.TryGetValue(ecode, out var existingRow))
                {
                    existingRow.NAME_OF_THE_DEGREE = row.Cell(2).GetValue<string>()?.Trim();
                    existingRow.YEAR_OF_PASSING = row.Cell(3).GetValue<string>();
                    existingRow.GRADE = row.Cell(4).GetValue<string>()?.Trim();
                    updatedRows.Add(existingRow);
                }
                else
                {
                    newRows.Add(new EmpDegreeQualification
                    {
                        E_CODE = ecode,
                        NAME_OF_THE_DEGREE = row.Cell(2).GetValue<string>()?.Trim(),
                        YEAR_OF_PASSING = row.Cell(3).GetValue<string>(),
                        GRADE = row.Cell(4).GetValue<string>()?.Trim()
                    });
                }
            }

            try
            {
                if (newRows.Any())
                    await _context.EmpDegreeQualifications.AddRangeAsync(newRows);
                if (updatedRows.Any())
                    _context.EmpDegreeQualifications.UpdateRange(updatedRows);

                await _context.SaveChangesAsync();
                return BuildFetchSuccessResponse("EmpDegreeQualification uploaded successfully", null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading EmpDegreeQualification");
                return BuildFetchErrorResponse($"Error uploading EmpDegreeQualification: {ex.Message}", HttpStatusCode.BadRequest);
            }
        }
        public async Task<FetchAndResponse> GetAllEmpDegreeQualificationAsync()
        {
            try
            {
                var data = await _context.EmpDegreeQualifications.AsNoTracking().ToListAsync();
                if (data == null || data.Count < 1)
                    return BuildFetchErrorResponse("No Data Found", HttpStatusCode.NotFound);
                return BuildFetchSuccessResponse("Fetched all EmpDegreeQualification records successfully", data);
            }
            catch (Exception ex)
            {
                return BuildFetchErrorResponse(ex.Message, HttpStatusCode.BadRequest);
            }
        }
        public async Task<FetchAndResponse> UploadEmpPastExperienceDetailsAsync(IFormFile file)
        {
            var expectedHeaders = new[] { "E.CODE", "COMP S.NO.", "COMP NAME", "LOCATION", "DESIGNATION", "FROM DATE", "TO DATE", "LAST CTC" };
            if (file == null || file.Length == 0)
                return BuildFetchErrorResponse("No file uploaded", HttpStatusCode.BadRequest);

            using var stream = file.OpenReadStream();
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheet(1);

            for (int i = 0; i < expectedHeaders.Length; i++)
            {
                var cellValue = worksheet.Cell(1, i + 1).GetValue<string>().Trim();
                if (!string.Equals(cellValue, expectedHeaders[i], StringComparison.OrdinalIgnoreCase))
                    return BuildFetchErrorResponse($"Header mismatch at column {i + 1}: Expected '{expectedHeaders[i]}', found '{cellValue}'", HttpStatusCode.BadRequest);
            }
            if (worksheet.Row(1).CellsUsed().Count() != expectedHeaders.Length)
                return BuildFetchErrorResponse("Header count mismatch", HttpStatusCode.BadRequest);

            var rows = worksheet.RowsUsed().Skip(1).ToList();
            var seenEcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                var ecode = row.Cell(1).GetValue<string>()?.Trim();
                if (!seenEcodes.Add(ecode))
                    return BuildFetchErrorResponse($"Duplicate E_CODE '{ecode}' found in Excel.", HttpStatusCode.BadRequest);
            }

            var ecodes = rows.Select(r => r.Cell(1).GetValue<string>()?.Trim()).ToList();
            var existing = await _context.EmpPastExperienceDetails.AsQueryable()
                .Where(x => ecodes.Contains(x.E_CODE))
                .ToListAsync();
            var existingDict = existing.ToDictionary(x => x.E_CODE, StringComparer.OrdinalIgnoreCase);

            var newRows = new List<EmpPastExperienceDetail>();
            var updatedRows = new List<EmpPastExperienceDetail>();

            foreach (var row in rows)
            {
                var ecode = row.Cell(1).GetValue<string>()?.Trim();
                if (string.IsNullOrEmpty(ecode)) continue;

                if (existingDict.TryGetValue(ecode, out var existingRow))
                {
                    existingRow.COMP_S_NO = row.Cell(2).GetValue<string>()?.Trim();
                    existingRow.COMP_NAME = row.Cell(3).GetValue<string>()?.Trim();
                    existingRow.LOCATION = row.Cell(4).GetValue<string>()?.Trim();
                    existingRow.DESIGNATION = row.Cell(5).GetValue<string>()?.Trim();
                    existingRow.FROM_DATE = row.Cell(6).GetValue<DateTime>();
                    existingRow.TO_DATE = row.Cell(7).GetValue<DateTime>();
                    existingRow.LAST_CTC = row.Cell(8).GetValue<decimal>();
                    updatedRows.Add(existingRow);
                }
                else
                {
                    newRows.Add(new EmpPastExperienceDetail
                    {
                        E_CODE = ecode,
                        COMP_S_NO = row.Cell(2).GetValue<string>()?.Trim(),
                        COMP_NAME = row.Cell(3).GetValue<string>()?.Trim(),
                        LOCATION = row.Cell(4).GetValue<string>()?.Trim(),
                        DESIGNATION = row.Cell(5).GetValue<string>()?.Trim(),
                        FROM_DATE = row.Cell(6).GetValue<DateTime>(),
                        TO_DATE = row.Cell(7).GetValue<DateTime>(),
                        LAST_CTC = row.Cell(8).GetValue<decimal>()
                    });
                }
            }

            try
            {
                if (newRows.Any())
                    await _context.EmpPastExperienceDetails.AddRangeAsync(newRows);
                if (updatedRows.Any())
                    _context.EmpPastExperienceDetails.UpdateRange(updatedRows);

                await _context.SaveChangesAsync();
                return BuildFetchSuccessResponse("EmpPastExperienceDetails uploaded successfully", null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading EmpPastExperienceDetails");
                return BuildFetchErrorResponse($"Error uploading EmpPastExperienceDetails: {ex.Message}", HttpStatusCode.BadRequest);
            }
        }
        public async Task<FetchAndResponse> GetAllEmpPastExperienceDetailsAsync()
        {
            try
            {
                var data = await _context.EmpPastExperienceDetails.AsNoTracking().ToListAsync();
                if (data == null || data.Count < 1)
                    return BuildFetchErrorResponse("No Data Found", HttpStatusCode.NotFound);
                return BuildFetchSuccessResponse("Fetched all EmpPastExperienceDetails records successfully", data);
            }
            catch (Exception ex)
            {
                return BuildFetchErrorResponse(ex.Message, HttpStatusCode.BadRequest);
            }
        }
        public async Task<FetchAndResponse> UploadEmpJoiningReleavingDetailsAsync(IFormFile file)
        {
            var expectedHeaders = new[] { "E.CODE", "JOINING DATE", "RELEAVING DATE", "JOINED LOCATION", "JOINED DEPARTMENT", "JOINED DESIGNATION", "STORE CODE" };
            if (file == null || file.Length == 0)
                return BuildFetchErrorResponse("No file uploaded", HttpStatusCode.BadRequest);

            using var stream = file.OpenReadStream();
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheet(1);

            for (int i = 0; i < expectedHeaders.Length; i++)
            {
                var cellValue = worksheet.Cell(1, i + 1).GetValue<string>().Trim();
                if (!string.Equals(cellValue, expectedHeaders[i], StringComparison.OrdinalIgnoreCase))
                    return BuildFetchErrorResponse($"Header mismatch at column {i + 1}: Expected '{expectedHeaders[i]}', found '{cellValue}'", HttpStatusCode.BadRequest);
            }
            if (worksheet.Row(1).CellsUsed().Count() != expectedHeaders.Length)
                return BuildFetchErrorResponse("Header count mismatch", HttpStatusCode.BadRequest);

            var rows = worksheet.RowsUsed().Skip(1).ToList();
            var seenEcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                var ecode = row.Cell(1).GetValue<string>()?.Trim();
                if (!seenEcodes.Add(ecode))
                    return BuildFetchErrorResponse($"Duplicate E_CODE '{ecode}' found in Excel.", HttpStatusCode.BadRequest);
            }

            var ecodes = rows.Select(r => r.Cell(1).GetValue<string>()?.Trim()).ToList();
            var existing = await _context.EmpJoiningReleavingDetails.AsQueryable()
                .Where(x => ecodes.Contains(x.E_CODE))
                .ToListAsync();
            var existingDict = existing.ToDictionary(x => x.E_CODE, StringComparer.OrdinalIgnoreCase);

            var newRows = new List<EmpJoiningReleavingDetail>();
            var updatedRows = new List<EmpJoiningReleavingDetail>();

            foreach (var row in rows)
            {
                var ecode = row.Cell(1).GetValue<string>()?.Trim();
                if (string.IsNullOrEmpty(ecode)) continue;

                if (existingDict.TryGetValue(ecode, out var existingRow))
                {
                    existingRow.JOINING_DATE = row.Cell(2).GetValue<DateTime>();
                    existingRow.RELEAVING_DATE = row.Cell(3).GetValue<DateTime>();
                    existingRow.JOINED_LOCATION = row.Cell(4).GetValue<string>()?.Trim();
                    existingRow.JOINED_DEPARTMENT = row.Cell(5).GetValue<string>()?.Trim();
                    existingRow.JOINED_DESIGNATION = row.Cell(6).GetValue<string>()?.Trim();
                    existingRow.STORE_CODE = row.Cell(7).GetValue<string>()?.Trim();
                    updatedRows.Add(existingRow);
                }
                else
                {
                    newRows.Add(new EmpJoiningReleavingDetail
                    {
                        E_CODE = ecode,
                        JOINING_DATE = row.Cell(2).GetValue<DateTime>(),
                        RELEAVING_DATE = row.Cell(3).GetValue<DateTime>(),
                        JOINED_LOCATION = row.Cell(4).GetValue<string>()?.Trim(),
                        JOINED_DEPARTMENT = row.Cell(5).GetValue<string>()?.Trim(),
                        JOINED_DESIGNATION = row.Cell(6).GetValue<string>()?.Trim(),
                        STORE_CODE = row.Cell(7).GetValue<string>()?.Trim()
                    });
                }
            }

            try
            {
                if (newRows.Any())
                    await _context.EmpJoiningReleavingDetails.AddRangeAsync(newRows);
                if (updatedRows.Any())
                    _context.EmpJoiningReleavingDetails.UpdateRange(updatedRows);

                await _context.SaveChangesAsync();
                return BuildFetchSuccessResponse("EmpJoiningReleavingDetails uploaded successfully", null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading EmpJoiningReleavingDetails");
                return BuildFetchErrorResponse($"Error uploading EmpJoiningReleavingDetails: {ex.Message}", HttpStatusCode.BadRequest);
            }
        }
        public async Task<FetchAndResponse> GetAllEmpJoiningReleavingDetailsAsync()
        {
            try
            {
                var data = await _context.EmpJoiningReleavingDetails.AsNoTracking().ToListAsync();
                if (data == null || data.Count < 1)
                    return BuildFetchErrorResponse("No Data Found", HttpStatusCode.NotFound);
                return BuildFetchSuccessResponse("Fetched all EmpJoiningReleavingDetails records successfully", data);
            }
            catch (Exception ex)
            {
                return BuildFetchErrorResponse(ex.Message, HttpStatusCode.BadRequest);
            }
        }
        public async Task<FetchAndResponse> UploadEmpRevisedDeptDesgLocDetailsAsync(IFormFile file)
        {
            var expectedHeaders = new[] { "E.CODE", "NEW DEPARTMENT", "NEW DESIGNATION", "POSTED LOCATION", "POSTED DT" };
            if (file == null || file.Length == 0)
                return BuildFetchErrorResponse("No file uploaded", HttpStatusCode.BadRequest);

            using var stream = file.OpenReadStream();
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheet(1);

            for (int i = 0; i < expectedHeaders.Length; i++)
            {
                var cellValue = worksheet.Cell(1, i + 1).GetValue<string>().Trim();
                if (!string.Equals(cellValue, expectedHeaders[i], StringComparison.OrdinalIgnoreCase))
                    return BuildFetchErrorResponse($"Header mismatch at column {i + 1}: Expected '{expectedHeaders[i]}', found '{cellValue}'", HttpStatusCode.BadRequest);
            }
            if (worksheet.Row(1).CellsUsed().Count() != expectedHeaders.Length)
                return BuildFetchErrorResponse("Header count mismatch", HttpStatusCode.BadRequest);

            var rows = worksheet.RowsUsed().Skip(1).ToList();
            var seenEcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                var ecode = row.Cell(1).GetValue<string>()?.Trim();
                if (!seenEcodes.Add(ecode))
                    return BuildFetchErrorResponse($"Duplicate E_CODE '{ecode}' found in Excel.", HttpStatusCode.BadRequest);
            }

            var ecodes = rows.Select(r => r.Cell(1).GetValue<string>()?.Trim()).ToList();
            var existing = await _context.EmpRevisedDeptDesgLocDetails.AsQueryable()
                .Where(x => ecodes.Contains(x.E_CODE))
                .ToListAsync();
            var existingDict = existing.ToDictionary(x => x.E_CODE, StringComparer.OrdinalIgnoreCase);

            var newRows = new List<EmpRevisedDeptDesgLocDetail>();
            var updatedRows = new List<EmpRevisedDeptDesgLocDetail>();

            foreach (var row in rows)
            {
                var ecode = row.Cell(1).GetValue<string>()?.Trim();
                if (string.IsNullOrEmpty(ecode)) continue;

                if (existingDict.TryGetValue(ecode, out var existingRow))
                {
                    existingRow.NEW_DEPARTMENT = row.Cell(2).GetValue<string>()?.Trim();
                    existingRow.NEW_DESIGNATION = row.Cell(3).GetValue<string>()?.Trim();
                    existingRow.POSTED_LOCATION = row.Cell(4).GetValue<string>()?.Trim();
                    existingRow.POSTED_DT = row.Cell(5).GetValue<DateTime>();
                    updatedRows.Add(existingRow);
                }
                else
                {
                    newRows.Add(new EmpRevisedDeptDesgLocDetail
                    {
                        E_CODE = ecode,
                        NEW_DEPARTMENT = row.Cell(2).GetValue<string>()?.Trim(),
                        NEW_DESIGNATION = row.Cell(3).GetValue<string>()?.Trim(),
                        POSTED_LOCATION = row.Cell(4).GetValue<string>()?.Trim(),
                        POSTED_DT = row.Cell(5).GetValue<DateTime>()
                    });
                }
            }

            try
            {
                if (newRows.Any())
                    await _context.EmpRevisedDeptDesgLocDetails.AddRangeAsync(newRows);
                if (updatedRows.Any())
                    _context.EmpRevisedDeptDesgLocDetails.UpdateRange(updatedRows);

                await _context.SaveChangesAsync();
                return BuildFetchSuccessResponse("EmpRevisedDeptDesgLocDetails uploaded successfully", null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading EmpRevisedDeptDesgLocDetails");
                return BuildFetchErrorResponse($"Error uploading EmpRevisedDeptDesgLocDetails: {ex.Message}", HttpStatusCode.BadRequest);
            }
        }
        public async Task<FetchAndResponse> GetAllEmpRevisedDeptDesgLocDetailsAsync()
        {
            try
            {
                var data = await _context.EmpRevisedDeptDesgLocDetails.AsNoTracking().ToListAsync();
                if (data == null || data.Count < 1)
                    return BuildFetchErrorResponse("No Data Found", HttpStatusCode.NotFound);
                return BuildFetchSuccessResponse("Fetched all EmpRevisedDeptDesgLocDetails records successfully", data);
            }
            catch (Exception ex)
            {
                return BuildFetchErrorResponse(ex.Message, HttpStatusCode.BadRequest);
            }
        }
        public async Task<FetchAndResponse> UploadPaymentsync(IFormFile file)
        {
            var expectedHeaders = new[] { "E.CODE", "MONTH", "INCENTIVE", "ARREAR", "OVERTIME", "FOODING ALLOWANCE", "MOBILE BILL", "BONUS" };
            if (file == null || file.Length == 0)
                return BuildFetchErrorResponse("No file uploaded", HttpStatusCode.BadRequest);

            using var stream = file.OpenReadStream();
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheet(1);

            // Validate headers
            for (int i = 0; i < expectedHeaders.Length; i++)
            {
                var cellValue = worksheet.Cell(1, i + 1).GetValue<string>().Trim();
                if (!string.Equals(cellValue, expectedHeaders[i], System.StringComparison.OrdinalIgnoreCase))
                    return BuildFetchErrorResponse($"Header mismatch at column {i + 1}: Expected '{expectedHeaders[i]}', found '{cellValue}'", HttpStatusCode.BadRequest);
            }
            if (worksheet.Row(1).CellsUsed().Count() != expectedHeaders.Length)
                return BuildFetchErrorResponse("Header count mismatch", HttpStatusCode.BadRequest);

            var rows = worksheet.RowsUsed().Skip(1).ToList();

            // Helper to format month as MMM-yy
            string FormatMonth(string input)
            {
                if (string.IsNullOrWhiteSpace(input)) return string.Empty;
                input = input.Trim();
                DateTime dt;
                // Try parse as date
                if (DateTime.TryParse(input, out dt))
                {
                    return dt.ToString("MMM-yy").ToUpper();
                }
                // Try parse as MMM-yy or similar
                if (DateTime.TryParseExact(input, new[] { "MMM-yy", "MMM-yyyy", "MM-yyyy", "MM-yy", "yyyy-MM", "yy-MM" }, null, System.Globalization.DateTimeStyles.None, out dt))
                {
                    return dt.ToString("MMM-yy").ToUpper();
                }
                // If already in format, just uppercase
                if (input.Length == 6 && input[3] == '-')
                    return input.ToUpper();
                return input.ToUpper();
            }

            // Check for duplicate (E_CODE, MONTH) in Excel
            var seenKeys = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                var ecode = row.Cell(1).GetValue<string>()?.Trim();
                var monthRaw = row.Cell(2).GetValue<string>()?.Trim();
                var month = FormatMonth(monthRaw);
                var key = $"{ecode}|{month}";
                if (!seenKeys.Add(key))
                    return BuildFetchErrorResponse($"Duplicate combination of E_CODE '{ecode}' and MONTH '{month}' found in Excel.", HttpStatusCode.BadRequest);
            }

            // Upsert logic using (E_CODE, MONTH) as key
            var keys = rows.Select(r => new
            {
                E_CODE = r.Cell(1).GetValue<string>()?.Trim(),
                MONTH = FormatMonth(r.Cell(2).GetValue<string>()?.Trim())
            }).ToList();

            var ecodes = keys.Select(k => k.E_CODE).Distinct().ToList();
            var months = keys.Select(k => k.MONTH).Distinct().ToList();

            // Fetch all possible matches from DB
            var existing = await _context.tblPayments.AsQueryable()
                .Where(x => ecodes.Contains(x.E_CODE) && months.Contains(x.MONTH))
                .ToListAsync();

            // Build dictionary with composite key
            var existingDict = existing
                .GroupBy(x => $"{(x.E_CODE ?? "").Trim()}|{(x.MONTH ?? "").Trim()}",
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var newRows = new List<tblPayment>();
            var updatedRows = new List<tblPayment>();

            foreach (var row in rows)
            {
                var ecode = row.Cell(1).GetValue<string>()?.Trim();
                var monthRaw = row.Cell(2).GetValue<string>()?.Trim();
                var month = FormatMonth(monthRaw);
                if (string.IsNullOrEmpty(ecode) || string.IsNullOrEmpty(month)) continue;
                var key = $"{ecode}|{month}";

                if (existingDict.TryGetValue(key, out var existingRow))
                {
                    // Update only if cell is not empty
                    if (!string.IsNullOrWhiteSpace(row.Cell(3).GetValue<string>()))
                        existingRow.Incentive = row.Cell(3).GetValue<decimal>();

                    if (!string.IsNullOrWhiteSpace(row.Cell(4).GetValue<string>()))
                        existingRow.ARREAR = row.Cell(4).GetValue<decimal>();

                    if (!string.IsNullOrWhiteSpace(row.Cell(5).GetValue<string>()))
                        existingRow.Overtime = row.Cell(5).GetValue<decimal>();

                    if (!string.IsNullOrWhiteSpace(row.Cell(6).GetValue<string>()))
                        existingRow.Fooding_Allowance = row.Cell(6).GetValue<decimal>();

                    if (!string.IsNullOrWhiteSpace(row.Cell(7).GetValue<string>()))
                        existingRow.Mobile_Bill = row.Cell(7).GetValue<decimal>();

                    // BONUS (new)
                    if (!string.IsNullOrWhiteSpace(row.Cell(8).GetValue<string>()))
                        existingRow.Bonus = row.Cell(8).GetValue<decimal>();

                    existingRow.MONTH = month; // Ensure format
                    updatedRows.Add(existingRow);
                }
                else
                {
                    // Insert: only set value if cell is not empty, else default
                    newRows.Add(new tblPayment
                    {
                        E_CODE = ecode,
                        MONTH = month,
                        Incentive = !string.IsNullOrWhiteSpace(row.Cell(3).GetValue<string>()) ? row.Cell(3).GetValue<decimal>() : default(decimal),
                        ARREAR = !string.IsNullOrWhiteSpace(row.Cell(4).GetValue<string>()) ? row.Cell(4).GetValue<decimal>() : default(decimal),
                        Overtime = !string.IsNullOrWhiteSpace(row.Cell(5).GetValue<string>()) ? row.Cell(5).GetValue<decimal>() : default(decimal),
                        Fooding_Allowance = !string.IsNullOrWhiteSpace(row.Cell(6).GetValue<string>()) ? row.Cell(6).GetValue<decimal>() : default(decimal),
                        Mobile_Bill = !string.IsNullOrWhiteSpace(row.Cell(7).GetValue<string>()) ? row.Cell(7).GetValue<decimal>() : default(decimal),
                        // BONUS (new)
                        Bonus = !string.IsNullOrWhiteSpace(row.Cell(8).GetValue<string>()) ? row.Cell(8).GetValue<decimal>() : default(decimal)
                    });
                }
            }

            try
            {
                if (newRows.Any())
                    await _context.tblPayments.AddRangeAsync(newRows);
                if (updatedRows.Any())
                    _context.tblPayments.UpdateRange(updatedRows);

                await _context.SaveChangesAsync();
                return BuildFetchSuccessResponse("Payments uploaded successfully", null);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error uploading Payments");
                return BuildFetchErrorResponse($"Error uploading Payments: {ex.Message}", HttpStatusCode.BadRequest);
            }
        }

        public async Task<FetchAndResponse> GetAllPaymentsAsync()
        {
            try
            {
                var data = await _context.tblPayments.AsNoTracking().ToListAsync();
                if (data == null || data.Count < 1)
                    return BuildFetchErrorResponse("No Data Found", HttpStatusCode.NotFound);
                return BuildFetchSuccessResponse("Fetched all Payments records successfully", data);
            }
            catch (System.Exception ex)
            {
                return BuildFetchErrorResponse(ex.Message, HttpStatusCode.BadRequest);
            }
        }
        public async Task<FetchAndResponse> UploadBonusAndGratutityOpeningAsync(IFormFile file)
        {
            var expectedHeaders = new[] { "Ecode", "Month", "Gratuity", "Bonus" };
            if (file == null || file.Length == 0)
                return BuildFetchErrorResponse("No file uploaded", HttpStatusCode.BadRequest);

            using var stream = file.OpenReadStream();
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheet(1);

            // Validate headers
            for (int i = 0; i < expectedHeaders.Length; i++)
            {
                var cellValue = worksheet.Cell(1, i + 1).GetValue<string>().Trim();
                if (!string.Equals(cellValue, expectedHeaders[i], System.StringComparison.OrdinalIgnoreCase))
                    return BuildFetchErrorResponse($"Header mismatch at column {i + 1}: Expected '{expectedHeaders[i]}', found '{cellValue}'", HttpStatusCode.BadRequest);
            }
            if (worksheet.Row(1).CellsUsed().Count() != expectedHeaders.Length)
                return BuildFetchErrorResponse("Header count mismatch", HttpStatusCode.BadRequest);

            var rows = worksheet.RowsUsed().Skip(1).ToList();

            // Check for duplicate (Ecode, Month) in Excel
            var seenKeys = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                var ecode = row.Cell(1).GetValue<string>()?.Trim();
                var month = row.Cell(2).GetValue<string>()?.Trim();
                var key = $"{ecode}|{month}";
                if (!seenKeys.Add(key))
                    return BuildFetchErrorResponse($"Duplicate combination of Ecode '{ecode}' and Month '{month}' found in Excel.", HttpStatusCode.BadRequest);
            }

            var keys = rows.Select(r => new
            {
                ECode = r.Cell(1).GetValue<string>()?.Trim(),
                Month = r.Cell(2).GetValue<string>()?.Trim()
            }).ToList();

            var ecodes = keys.Select(k => k.ECode).Distinct().ToList();
            var months = keys.Select(k => k.Month).Distinct().ToList();

            // Fetch all possible matches from DB
            var existing = await _context.BonusAndGratutityOpenings.AsQueryable()
                .Where(x => ecodes.Contains(x.ECode) && months.Contains(x.Month))
                .ToListAsync();

            // Build dictionary with composite key
            var existingDict = existing.ToDictionary(x => $"{x.ECode}|{x.Month}", System.StringComparer.OrdinalIgnoreCase);

            var newRows = new List<BonusAndGratutityOpening>();
            var updatedRows = new List<BonusAndGratutityOpening>();

            foreach (var row in rows)
            {
                var ecode = row.Cell(1).GetValue<string>()?.Trim();
                var month = row.Cell(2).GetValue<string>()?.Trim();
                if (string.IsNullOrEmpty(ecode) || string.IsNullOrEmpty(month)) continue;
                var key = $"{ecode}|{month}";

                if (existingDict.TryGetValue(key, out var existingRow))
                {
                    // Update only if cell is not empty
                    if (!string.IsNullOrWhiteSpace(row.Cell(3).GetValue<string>()))
                        existingRow.Gratuity = row.Cell(3).GetValue<decimal>();
                    if (!string.IsNullOrWhiteSpace(row.Cell(4).GetValue<string>()))
                        existingRow.Bonus = row.Cell(4).GetValue<decimal>();
                    updatedRows.Add(existingRow);
                }
                else
                {
                    // Insert: only set value if cell is not empty, else default
                    newRows.Add(new BonusAndGratutityOpening
                    {
                        ECode = ecode,
                        Month = month,
                        Gratuity = !string.IsNullOrWhiteSpace(row.Cell(3).GetValue<string>()) ? row.Cell(3).GetValue<decimal>() : default(decimal),
                        Bonus = !string.IsNullOrWhiteSpace(row.Cell(4).GetValue<string>()) ? row.Cell(4).GetValue<decimal>() : default(decimal)
                    });
                }
            }

            try
            {
                if (newRows.Any())
                    await _context.BonusAndGratutityOpenings.AddRangeAsync(newRows);
                if (updatedRows.Any())
                    _context.BonusAndGratutityOpenings.UpdateRange(updatedRows);

                await _context.SaveChangesAsync();
                return BuildFetchSuccessResponse("BonusAndGratutityOpening uploaded successfully", null);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error uploading BonusAndGratutityOpening");
                return BuildFetchErrorResponse($"Error uploading BonusAndGratutityOpening: {ex.Message}", HttpStatusCode.BadRequest);
            }
        }
        public async Task<FetchAndResponse> UploadEmpSalaryStatusAsync(IFormFile file)
        {
            try
            {
                var expectedHeaders = new[] { "Ecode", "Month", "Status", "ActionDate", "Remarks" };
                if (file == null || file.Length == 0)
                    return BuildFetchErrorResponse("No file uploaded", HttpStatusCode.BadRequest);

                using var stream = file.OpenReadStream();
                using var workbook = new XLWorkbook(stream);
                var worksheet = workbook.Worksheet(1);

                // Validate headers
                for (int i = 0; i < expectedHeaders.Length; i++)
                {
                    var cellValue = worksheet.Cell(1, i + 1).GetValue<string>().Trim();
                    if (!string.Equals(cellValue, expectedHeaders[i], System.StringComparison.OrdinalIgnoreCase))
                        return BuildFetchErrorResponse($"Header mismatch at column {i + 1}: Expected '{expectedHeaders[i]}', found '{cellValue}'", HttpStatusCode.BadRequest);
                }
                if (worksheet.Row(1).CellsUsed().Count() != expectedHeaders.Length)
                    return BuildFetchErrorResponse("Header count mismatch", HttpStatusCode.BadRequest);

                var rows = worksheet.RowsUsed().Skip(1).ToList();

                // Helper to format month as MMM-yy
                string FormatMonth(string input)
                {
                    if (string.IsNullOrWhiteSpace(input)) return string.Empty;
                    input = input.Trim();
                    DateTime dt;
                    // Try parse as date
                    if (DateTime.TryParse(input, out dt))
                    {
                        return dt.ToString("MMM-yy").ToUpper();
                    }
                    // Try parse as MMM-yy or similarLeave
                    if (DateTime.TryParseExact(input, new[] { "MMM-yy", "MMM-yyyy", "MM-yyyy", "MM-yy", "yyyy-MM", "yy-MM" }, null, System.Globalization.DateTimeStyles.None, out dt))
                    {
                        return dt.ToString("MMM-yy").ToUpper();
                    }
                    // If already in format, just uppercase
                    if (input.Length == 6 && input[3] == '-')
                        return input.ToUpper();
                    return input.ToUpper();
                }

                // Check for duplicate (Ecode, Month, ActionDate) in Excel
                var seenKeys = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
                foreach (var row in rows)
                {
                    var ecode = row.Cell(1).GetValue<string>()?.Trim();
                    var monthRaw = row.Cell(2).GetValue<string>()?.Trim();
                    var month = FormatMonth(monthRaw);
                    var actionDateStr = row.Cell(4).GetValue<string>()?.Trim();
                    if (string.IsNullOrEmpty(ecode) || string.IsNullOrEmpty(month) || string.IsNullOrWhiteSpace(actionDateStr))
                        return BuildFetchErrorResponse($"Ecode, Month, and ActionDate are required at row {row.RowNumber()}.", HttpStatusCode.BadRequest);
                    if (!DateTime.TryParse(actionDateStr, out var actionDate))
                        return BuildFetchErrorResponse($"Invalid ActionDate '{actionDateStr}' at row {row.RowNumber()}. Use YYYY-MM-DD format.", HttpStatusCode.BadRequest);
                    var key = $"{ecode}|{month}|{actionDate:yyyy-MM-dd}";
                    if (!seenKeys.Add(key))
                        return BuildFetchErrorResponse($"Duplicate combination of Ecode '{ecode}', Month '{month}', and ActionDate '{actionDate:yyyy-MM-dd}' found in Excel.", HttpStatusCode.BadRequest);
                }

                var keys = rows.Select(r =>
                {
                    var ecode = r.Cell(1).GetValue<string>()?.Trim();
                    var month = FormatMonth(r.Cell(2).GetValue<string>()?.Trim());
                    var actionDateStr = r.Cell(4).GetValue<string>()?.Trim();
                    DateTime? actionDate = null;
                    if (!string.IsNullOrWhiteSpace(actionDateStr) && DateTime.TryParse(actionDateStr, out var parsedDate))
                        actionDate = parsedDate;
                    return new { Ecode = ecode, Month = month, ActionDate = actionDate };
                }).ToList();

                var ecodes = keys.Select(k => k.Ecode).Distinct().ToList();
                var months = keys.Select(k => k.Month).Distinct().ToList();
                var actionDates = keys.Where(k => k.ActionDate.HasValue).Select(k => k.ActionDate.Value.Date).Distinct().ToList();

                // Fetch all possible matches from DB
                var existing = await _context.EmpSalaryStatuses
    .Where(x => ecodes.Contains(x.ECode)
             && months.Contains(x.Month)
             && x.ActionDate.HasValue
             && actionDates.Contains(x.ActionDate.Value.Date))
    .ToListAsync();


                // Build dictionary with composite key
                var existingDict = existing.ToDictionary(x => $"{x.ECode}|{x.Month}|{x.ActionDate:yyyy-MM-dd}", System.StringComparer.OrdinalIgnoreCase);

                var newRows = new List<EmpSalaryStatus>();
                var updatedRows = new List<EmpSalaryStatus>();

                foreach (var row in rows)
                {
                    var ecode = row.Cell(1).GetValue<string>()?.Trim();
                    var monthRaw = row.Cell(2).GetValue<string>()?.Trim();
                    var month = FormatMonth(monthRaw);
                    var statusValue = row.Cell(3).GetValue<string>()?.Trim();
                    var actionDateStr = row.Cell(4).GetValue<string>()?.Trim();
                    var remarks = row.Cell(5).GetValue<string>()?.Trim();
                    if (string.IsNullOrEmpty(ecode) || string.IsNullOrEmpty(month) || string.IsNullOrWhiteSpace(actionDateStr)) continue;
                    if (!DateTime.TryParse(actionDateStr, out var actionDate))
                        return BuildFetchErrorResponse($"Invalid ActionDate '{actionDateStr}' at row {row.RowNumber()}. Use YYYY-MM-DD format.", HttpStatusCode.BadRequest);
                    var key = $"{ecode}|{month}|{actionDate:yyyy-MM-dd}";

                    // Validate status value if present
                    if (!string.IsNullOrWhiteSpace(statusValue))
                    {
                        var statusLower = statusValue.Trim().ToLower();
                        if (statusLower != "hold" && statusLower != "released")
                        {
                            return BuildFetchErrorResponse($"Invalid Status value '{statusValue}' at row {row.RowNumber()}. Only 'hold' or 'released' are allowed.", HttpStatusCode.BadRequest);
                        }
                    }

                    if (existingDict.TryGetValue(key, out var existingRow))
                    {
                        // Update only if cell is not empty
                        if (!string.IsNullOrWhiteSpace(statusValue))
                            existingRow.Status = statusValue;
                        existingRow.ActionDate = actionDate;
                        // Remarks is optional, always update (can be empty)
                        existingRow.Remarks = remarks;
                        updatedRows.Add(existingRow);
                    }
                    else
                    {
                        // Insert: only set value if cell is not empty, else default
                        newRows.Add(new EmpSalaryStatus
                        {
                            ECode = ecode,
                            Month = month,
                            Status = !string.IsNullOrWhiteSpace(statusValue) ? statusValue : null,
                            ActionDate = actionDate,
                            Remarks = remarks
                        });
                    }
                }

                try
                {
                    if (newRows.Any())
                        await _context.EmpSalaryStatuses.AddRangeAsync(newRows);
                    if (updatedRows.Any())
                        _context.EmpSalaryStatuses.UpdateRange(updatedRows);

                    await _context.SaveChangesAsync();
                    return BuildFetchSuccessResponse("EmpSalaryStatus uploaded successfully", null);
                }
                catch (System.Exception ex)
                {
                    _logger.LogError(ex, "Error uploading EmpSalaryStatus");
                    return BuildFetchErrorResponse($"Error uploading EmpSalaryStatus: {ex.Message}", HttpStatusCode.BadRequest);
                }
            }
            catch (Exception ex)
            {
                return BuildFetchErrorResponse($"Error uploading EmpSalaryStatus: {ex.Message}", HttpStatusCode.BadRequest);
            }
        }
        public async Task<FetchAndResponse> GetBonusAndGratutityOpeningByEcodeAsync(string? ecode)
        {
            try
            {
                var query = _context.BonusAndGratutityOpenings.AsNoTracking().AsQueryable();
                if (!string.IsNullOrWhiteSpace(ecode))
                    query = query.Where(x => x.ECode == ecode);
                var data = await query.ToListAsync();
                if (data == null || data.Count < 1)
                    return BuildFetchErrorResponse("No Data Found", HttpStatusCode.NotFound);
                return BuildFetchSuccessResponse("Fetched BonusAndGratutityOpening records successfully", data);
            }
            catch (System.Exception ex)
            {
                return BuildFetchErrorResponse(ex.Message, HttpStatusCode.BadRequest);
            }
        }
        public async Task<FetchAndResponse> GetEmpSalaryStatusByEcodeAsync(string? ecode)
        {
            try
            {
                var query = _context.EmpSalaryStatuses.AsNoTracking().AsQueryable();
                if (!string.IsNullOrWhiteSpace(ecode))
                    query = query.Where(x => x.ECode == ecode);
                var data = await query.ToListAsync();
                if (data == null || data.Count < 1)
                    return BuildFetchErrorResponse("No Data Found", HttpStatusCode.NotFound);
                return BuildFetchSuccessResponse("Fetched EmpSalaryStatus records successfully", data);
            }
            catch (System.Exception ex)
            {
                return BuildFetchErrorResponse(ex.Message, HttpStatusCode.BadRequest);
            }
        }
        public async Task<(bool Success, string Message)> UploadCompOffDataAsync(IFormFile file, string createdBy)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    _logger.LogWarning("No file uploaded in CompOff data upload.");
                    return (false, "No file uploaded");
                }

                using (var stream = file.OpenReadStream())
                {
                    using (var workbook = new XLWorkbook(stream))
                    {
                        var worksheet = workbook.Worksheet(1); // Get first worksheet
                        var rows = worksheet.RowsUsed().Skip(1); // Skip header row

                        foreach (var row in rows)
                        {
                            // Handle string columns with "NA" default
                            var ecode = row.Cell(1).GetValue<string>()?.Trim() ?? "NA";
                            var monthYearStr = row.Cell(2).GetValue<string>()?.Trim() ?? "NA";
                            var compOffEarn = row.Cell(3).IsEmpty() ? 0.0m : row.Cell(3).GetValue<decimal>();

                            // Parse MonthYear to DateTime? (nullable)
                            DateTime? monthYear = null;
                            if (!string.IsNullOrWhiteSpace(monthYearStr) && monthYearStr != "NA" && DateTime.TryParse(monthYearStr, out var parsedMonthYear))
                            {
                                monthYear = parsedMonthYear;
                            }

                            // Check if record exists for Ecode and MonthYear
                            var existingRecord = await _context.tblCompOffs
                                .FirstOrDefaultAsync(p => p.Ecode == ecode && p.MonthYear == monthYear);

                            if (existingRecord != null)
                            {
                                // Update existing record
                                existingRecord.CompOffEarn = compOffEarn;
                                existingRecord.CreatedBy = createdBy;
                                existingRecord.CreatedOn = DateTime.UtcNow;
                                _logger.LogInformation("Updated CompOff record for Ecode: {Ecode}, MonthYear: {MonthYear}", ecode, monthYear);
                            }
                            else
                            {
                                // Create new comp off record
                                var compOffRecord = new tblCompOff
                                {
                                    Ecode = ecode,
                                    MonthYear = monthYear,
                                    CompOffEarn = compOffEarn,
                                    CreatedBy = createdBy,
                                    CreatedOn = DateTime.UtcNow
                                };
                                _context.tblCompOffs.Add(compOffRecord);
                                _logger.LogInformation("Created new CompOff record for Ecode: {Ecode}, MonthYear: {MonthYear}", ecode, monthYear);
                            }
                        }

                        await _context.SaveChangesAsync();
                        _logger.LogInformation("CompOff data uploaded successfully, processed {RowCount} rows.", rows.Count());
                        return (true, "Comp off data uploaded successfully");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading comp off data.");
                return (false, $"Error uploading comp off data: {ex.Message}");
            }
        }
        public async Task<List<CompOffDto>> GetCompOffListAsync()
        {
            try
            {
                var compOffList = await _context.tblCompOffs
                    .AsNoTracking()
                    .OrderBy(c => c.MonthYear)
                    .ThenBy(c => c.Ecode)
                    .Select(c => new CompOffDto
                    {
                        CompOffId = c.CompOffId,
                        Ecode = c.Ecode,
                        MonthYear = c.MonthYear != null ? c.MonthYear.Value.ToString("MMM-yy") : null,
                        CompOffEarn = c.CompOffEarn,
                        CreatedBy = c.CreatedBy,
                        CreatedOn = (DateTime)c.CreatedOn
                    })
                    .ToListAsync();
                return compOffList;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving CompOff list.");
                throw;
            }
        }
        public async Task<FetchAndResponse> UploadStoreStateLinkingAsync(IFormFile file)
        {
            var expectedHeaders = new[] { "ST_CD", "State" };
            if (file == null || file.Length == 0)
                return BuildFetchErrorResponse("No file uploaded", HttpStatusCode.BadRequest);

            using var stream = file.OpenReadStream();
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheet(1);

            // Validate headers
            for (int i = 0; i < expectedHeaders.Length; i++)
            {
                var cellValue = worksheet.Cell(1, i + 1).GetValue<string>().Trim();
                if (!string.Equals(cellValue, expectedHeaders[i], System.StringComparison.OrdinalIgnoreCase))
                    return BuildFetchErrorResponse($"Header mismatch at column {i + 1}: Expected '{expectedHeaders[i]}', found '{cellValue}'", HttpStatusCode.BadRequest);
            }
            if (worksheet.Row(1).CellsUsed().Count() != expectedHeaders.Length)
                return BuildFetchErrorResponse("Header count mismatch", HttpStatusCode.BadRequest);

            var rows = worksheet.RowsUsed().Skip(1).ToList();

            // Check for duplicate ST_CD in Excel
            var seenStCds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                var stCd = row.Cell(1).GetValue<string>()?.Trim();
                if (string.IsNullOrWhiteSpace(stCd))
                    return BuildFetchErrorResponse($"ST_CD cannot be blank at row {row.RowNumber()}", HttpStatusCode.BadRequest);

                if (!seenStCds.Add(stCd))
                    return BuildFetchErrorResponse($"Duplicate ST_CD '{stCd}' found in Excel at row {row.RowNumber()}", HttpStatusCode.BadRequest);
            }

            // Get all ST_CDs and States from Excel
            var stCds = rows.Select(r => r.Cell(1).GetValue<string>()?.Trim()).ToList();
            var states = rows.Select(r => r.Cell(2).GetValue<string>()?.Trim()).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();

            // Validate that all States exist in either PTPolicyMaster or LWFPolicyMaster
            var validStates = await _context.PTPolicyMasters
                .Select(p => p.State)
                .Union(_context.LWFPolicyMasters.Select(l => l.State))
                .ToListAsync();

            var invalidStates = states.Where(s => !validStates.Any(vs => string.Equals(vs, s, StringComparison.OrdinalIgnoreCase))).ToList();
            if (invalidStates.Any())
            {
                return BuildFetchErrorResponse($"The following States have no LWF or PT Tax policy defined: {string.Join(", ", invalidStates)}", HttpStatusCode.BadRequest);
            }

            // Fetch existing records from DB
            var existing = await _context.StoreStateLinkings.AsQueryable()
                .Where(x => stCds.Contains(x.ST_CD))
                .ToListAsync();
            var existingDict = existing.ToDictionary(x => x.ST_CD, System.StringComparer.OrdinalIgnoreCase);

            var newRows = new List<StoreStateLinking>();
            var updatedRows = new List<StoreStateLinking>();

            foreach (var row in rows)
            {
                var stCd = row.Cell(1).GetValue<string>()?.Trim();
                var state = row.Cell(2).GetValue<string>()?.Trim();

                if (string.IsNullOrWhiteSpace(stCd))
                    continue; // Skip if ST_CD is empty (shouldn't happen due to validation above)

                if (existingDict.TryGetValue(stCd, out var existingRow))
                {
                    // Update existing record
                    existingRow.State = state;
                    updatedRows.Add(existingRow);
                }
                else
                {
                    // Create new record
                    newRows.Add(new StoreStateLinking
                    {
                        ST_CD = stCd,
                        State = state
                    });
                }
            }

            try
            {
                if (newRows.Any())
                    await _context.StoreStateLinkings.AddRangeAsync(newRows);
                if (updatedRows.Any())
                    _context.StoreStateLinkings.UpdateRange(updatedRows);

                await _context.SaveChangesAsync();
                return BuildFetchSuccessResponse("StoreStateLinking uploaded successfully", null);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error uploading StoreStateLinking");
                return BuildFetchErrorResponse($"Error uploading StoreStateLinking: {ex.Message}", HttpStatusCode.BadRequest);
            }
        }
        public async Task<FetchAndResponse> GetAllStoreStateLinkingAsync()
        {
            try
            {
                var data = await _context.StoreStateLinkings.AsNoTracking().ToListAsync();
                if (data == null || data.Count < 1)
                    return BuildFetchErrorResponse("No Data Found", HttpStatusCode.NotFound);
                return BuildFetchSuccessResponse("Fetched all StoreStateLinking records successfully", data);
            }
            catch (System.Exception ex)
            {
                return BuildFetchErrorResponse(ex.Message, HttpStatusCode.BadRequest);
            }
        }

        public async Task<FetchAndResponse> GetStoreWhichCanAddAsync()
        {
            try
            {
                var data = await _context.vw_StoreWhichCanAdds
                    .AsNoTracking()
                    .ToListAsync();
                if (data == null || data.Count < 1)
                    return BuildFetchErrorResponse("No Data Found", HttpStatusCode.NotFound);
                return BuildFetchSuccessResponse("Fetched StoreWhichCanAdd successfully", data);
            }
            catch (System.Exception ex)
            {
                return BuildFetchErrorResponse(ex.Message, HttpStatusCode.BadRequest);
            }
        }

        public async Task<(bool Success, string Message, byte[] FileBytes, string ContentType, string FileName)> GetStoreWhichCanAddExcelAsync()
        {
            try
            {
                var data = await _context.vw_StoreWhichCanAdds
                    .AsNoTracking()
                    .ToListAsync();

                if (data == null || data.Count < 1)
                {
                    return (false, "No Data Found", null, null, null);
                }

                using var wb = new XLWorkbook();
                var ws = wb.AddWorksheet("Stores");

                // Header
                ws.Cell(1, 1).Value = "State";
                ws.Cell(1, 1).Style.Font.SetBold();

                // Rows
                var rowIndex = 2;
                foreach (var row in data)
                {
                    ws.Cell(rowIndex, 1).Value = row.State;
                    rowIndex++;
                }

                // Auto fit
                ws.Columns().AdjustToContents();

                using var ms = new MemoryStream();
                wb.SaveAs(ms);
                var bytes = ms.ToArray();
                return (true, "Excel generated", bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"StoreWhichCanAdd_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx");
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error generating StoreWhichCanAdd Excel");
                return (false, ex.Message, null, null, null);
            }
        }
        public async Task<FetchAndResponse> UploadEmpPayrolDetailsAsync(IFormFile file)
        {
            try
            {
                var expectedHeaders = new[] { "ECODE", "DATE", "ACCOUNT NUMBER", "AMOUNT", "UTR" };
                if (file == null || file.Length == 0)
                    return BuildFetchErrorResponse("No file uploaded", HttpStatusCode.BadRequest);

                using var stream = file.OpenReadStream();
                using var workbook = new XLWorkbook(stream);
                var worksheet = workbook.Worksheet(1);

                for (int i = 0; i < expectedHeaders.Length; i++)
                {
                    var cellValue = worksheet.Cell(1, i + 1).GetValue<string>().Trim();
                    if (!string.Equals(cellValue, expectedHeaders[i], StringComparison.OrdinalIgnoreCase))
                        return BuildFetchErrorResponse($"Header mismatch at column {i + 1}: Expected '{expectedHeaders[i]}', found '{cellValue}'", HttpStatusCode.BadRequest);
                }
                if (worksheet.Row(1).CellsUsed().Count() != expectedHeaders.Length)
                    return BuildFetchErrorResponse("Header count mismatch", HttpStatusCode.BadRequest);

                var rows = worksheet.RowsUsed().Skip(1).ToList();
                var seenEcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var seenAccountNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var row in rows)
                {
                    var ecode = row.Cell(1).GetValue<string>()?.Trim();
                    var accountNumber = row.Cell(3).GetValue<string>()?.Trim();
                    var utr = row.Cell(5).GetValue<string>()?.Trim();
                    var date = row.Cell(2).GetValue<string>()?.Trim();
                    DateTime date1;
                    try
                    {
                        date1 = Convert.ToDateTime(date);
                    }
                    catch (FormatException)
                    {
                        throw new Exception("Invalid date format. Please provide a valid date.");
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"An error occurred: {ex.Message}");
                    }
                    string date2 = date1.ToString("yyyy-MM-dd");

                    string[] dateFormats = { "yyyy-MM-dd" };
                    DateTime _date;
                    var month = 0;
                    var year = 0;

                    if (DateTime.TryParseExact(date2, dateFormats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _date))
                    {
                        month = _date.Month;
                        year = _date.Year;
                    }
                    else
                    {
                        return BuildFetchErrorResponse($"Invalid date format {date}, please enter yyyy-MM-dd format", HttpStatusCode.BadRequest);
                    }

                    if (!DateTime.TryParse(date, out var datestr))
                        return BuildFetchErrorResponse($"Invalid DATE at row {row.RowNumber()}", HttpStatusCode.BadRequest);

                    var isutrexist = await _context.tblBonus_Uploads.AsNoTracking().AnyAsync(row => row.UTR == utr && row.E_Code == ecode && row.Date.HasValue && row.Date.Value.Month == month && row.Date.Value.Year == year);
                    if (isutrexist)
                        return BuildFetchErrorResponse($"Same utr '{utr}' exist for ecode {ecode} for month {_date.ToString("MMM-yy")}.", HttpStatusCode.BadRequest);

                    var isecodeexist = await _context.tblEmployees.AsNoTracking().AsQueryable().AnyAsync(row => row.Ecode == ecode);
                    if (!isecodeexist)
                        return BuildFetchErrorResponse($"E_CODE '{ecode}' does not exist.", HttpStatusCode.BadRequest);

                    if (!seenEcodes.Add(ecode))
                        return BuildFetchErrorResponse($"Duplicate E_CODE '{ecode}' found in Excel.", HttpStatusCode.BadRequest);

                    if (string.IsNullOrWhiteSpace(accountNumber))
                        return BuildFetchErrorResponse($"ACCOUNT NUMBER is empty at row {row.RowNumber()}", HttpStatusCode.BadRequest);

                    if (!seenAccountNumbers.Add(accountNumber))
                        return BuildFetchErrorResponse($"Duplicate ACCOUNT NUMBER '{accountNumber}' found at row {row.RowNumber()}", HttpStatusCode.BadRequest);

                    if (!seenEcodes.Add(utr))
                        return BuildFetchErrorResponse($"Duplicate UTR '{utr}' found in Excel.", HttpStatusCode.BadRequest);
                }
                var updatedRows = new List<tblBonus_Upload>();

                foreach (var row in rows)
                {
                    var ecode = row.Cell(1).GetValue<string>()?.Trim();
                    var dateStr = row.Cell(2).GetValue<string>()?.Trim();
                    var account = row.Cell(3).GetValue<string>()?.Trim();
                    var amountStr = row.Cell(4).GetValue<string>()?.Trim();
                    var utr = row.Cell(5).GetValue<string>()?.Trim();

                    if (!DateTime.TryParse(dateStr, out var date))
                        return BuildFetchErrorResponse($"Invalid DATE at row {row.RowNumber()}", HttpStatusCode.BadRequest);

                    if (!decimal.TryParse(amountStr, out var amount))
                        return BuildFetchErrorResponse($"Invalid AMOUNT at row {row.RowNumber()}", HttpStatusCode.BadRequest);

                    var month = date.Month;
                    var year = date.Year;
                    var _utr = month + year + utr;

                    updatedRows.Add(new tblBonus_Upload
                    {
                        E_Code = ecode,
                        Date = date,
                        Acc_Number = account,
                        Amount = amount,
                        UTR = _utr
                    });
                }

                try
                {
                    if (updatedRows.Any())
                        await _context.tblBonus_Uploads.AddRangeAsync(updatedRows);

                    await _context.SaveChangesAsync();
                    return BuildFetchSuccessResponse("Uploaded successfully", null);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error uploading");
                    return BuildFetchErrorResponse($"Error uploading: {ex.Message}", HttpStatusCode.BadRequest);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading");
                return BuildFetchErrorResponse($"Error uploading: {ex.Message}", HttpStatusCode.BadRequest);
            }
        }
        public async Task<FetchAndResponse> GetEMPBonusListAsync()
        {
            try
            {
                var data = await _context.GetProcedures().GETEMPBONUSLISTAsync();
                if (data == null || data.Count < 1)
                    return BuildFetchErrorResponse("No Data Found", HttpStatusCode.NotFound);
                return BuildFetchSuccessResponse("Fetched all StoreStateLinking records successfully", data);
            }
            catch (System.Exception ex)
            {
                return BuildFetchErrorResponse(ex.Message, HttpStatusCode.BadRequest);
            }
        }

        public async Task<ExecuteAndReponse> UploadPayrollWithChallanAsync(IFormFile excelFile, IFormFile challanPdf, string monthYear, JwtLoginDetailDto createdBy)
        {
            try
            {
                if (excelFile == null || excelFile.Length == 0)
                    return BuildExecuteErrorResponse("Excel file is required", HttpStatusCode.BadRequest);

                if (challanPdf == null || challanPdf.Length == 0)
                    return BuildExecuteErrorResponse("Challan PDF is required", HttpStatusCode.BadRequest);

                if (!Path.GetExtension(challanPdf.FileName)
                        .Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                    return BuildExecuteErrorResponse("Only PDF allowed for challan", HttpStatusCode.BadRequest);

                if (!DateTime.TryParseExact(
                        monthYear,
                        "MMM-yy",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out _))
                {
                    return BuildExecuteErrorResponse(
                        "Invalid MonthYear format. Expected MMM-yy (e.g. Jun-26)",
                        HttpStatusCode.BadRequest
                    );
                }

                var now = DateTime.Now;
                var folderPath = Path.Combine(
                    "wwwroot",
                    "Payroll",
                    now.Year.ToString(),
                    now.ToString("MMM"),
                    createdBy.EmployeeId
                );

                Directory.CreateDirectory(folderPath);

                var pdfFileName = $"Challan_{now:yyyyMMddHHmmss}_{Guid.NewGuid()}.pdf";
                var pdfFullPath = Path.Combine(folderPath, pdfFileName);

                using (var fs = new FileStream(pdfFullPath, FileMode.Create))
                {
                    await challanPdf.CopyToAsync(fs);
                }

                var dbPdfPath = pdfFullPath
                    .Replace("wwwroot", "")
                    .Replace("\\", "/");

                using var workbook = new XLWorkbook(excelFile.OpenReadStream());
                var worksheet = workbook.Worksheet(1);
                var rows = worksheet.RowsUsed().Skip(1).ToList();

                if (!rows.Any())
                    return BuildExecuteErrorResponse("Excel contains no data", HttpStatusCode.BadRequest);

                var challanNumber = rows
                    .Select(r => r.Cell(12).GetValue<string>()?.Trim())
                    .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

                if (string.IsNullOrWhiteSpace(challanNumber))
                    return BuildExecuteErrorResponse("Challan number not found in Excel", HttpStatusCode.BadRequest);

                var excelMonths = rows
                    .Select(r => r.Cell(7).GetValue<string>()?.Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (!excelMonths.Any())
                    return BuildExecuteErrorResponse("Excel Month-Year not found", HttpStatusCode.BadRequest);

                if (excelMonths.Count > 1)
                    return BuildExecuteErrorResponse(
                        "Excel contains multiple Month-Year values. Only one month allowed.",
                        HttpStatusCode.BadRequest
                    );

                var excelMonthYear = excelMonths.First();

                if (!DateTime.TryParseExact(
                        excelMonthYear,
                        "MMM-yy",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out _))
                {
                    return BuildExecuteErrorResponse(
                        $"Invalid Excel Month-Year '{excelMonthYear}'",
                        HttpStatusCode.BadRequest
                    );
                }

                if (!string.Equals(excelMonthYear, monthYear, StringComparison.OrdinalIgnoreCase))
                {
                    return BuildExecuteErrorResponse(
                        $"Excel Month '{excelMonthYear}' does not match selected Month '{monthYear}'",
                        HttpStatusCode.BadRequest
                    );
                }

                using var transaction = await _context.Database.BeginTransactionAsync();

                var existingRecords = await _context.EmployeePayrollUploads
                    .Where(x => x.MonthYear == monthYear)
                    .ToListAsync();

                if (existingRecords.Any())
                {
                    var historyRecords = existingRecords.Select(x => new EmployeePayrollUploadHistory
                    {
                        OriginalId = x.Id,

                        ECode = x.ECode,
                        LocCode = x.LocCode,
                        Location = x.Location,
                        EmpName = x.EmpName,
                        Department = x.Department,
                        Designation = x.Designation,

                        ExcelMonthYear = x.ExcelMonthYear,
                        MonthYear = x.MonthYear,

                        PayableDays = x.PayableDays,
                        EmpPF = x.EmpPF,
                        EmprPF = x.EmprPF,
                        DepositedPF = x.DepositedPF,

                        ChallanNumber = x.ChallanNumber,
                        ChallanPdfPath = x.ChallanPdfPath,

                        CreatedBy = x.CreatedBy,
                        CreatedOn = x.CreatedOn,

                        ArchivedBy = createdBy.EmployeeId,
                        ArchivedOn = DateTime.UtcNow
                    }).ToList();

                    _context.EmployeePayrollUploadHistories.AddRange(historyRecords);
                    _context.EmployeePayrollUploads.RemoveRange(existingRecords);

                    await _context.SaveChangesAsync();
                }

                foreach (var row in rows)
                {
                    var ecode = row.Cell(1).GetValue<string>()?.Trim();
                    if (string.IsNullOrWhiteSpace(ecode)) continue;

                    var entity = new EmployeePayrollUpload
                    {
                        ECode = ecode,
                        LocCode = row.Cell(2).GetValue<string>(),
                        Location = row.Cell(3).GetValue<string>(),
                        EmpName = row.Cell(4).GetValue<string>(),
                        Department = row.Cell(5).GetValue<string>(),
                        Designation = row.Cell(6).GetValue<string>(),

                        ExcelMonthYear = excelMonthYear,
                        MonthYear = monthYear,

                        PayableDays = row.Cell(8).TryGetValue(out decimal days) ? days : null,
                        EmpPF = row.Cell(9).TryGetValue(out decimal empPf) ? empPf : 0,
                        EmprPF = row.Cell(10).TryGetValue(out decimal emprPf) ? emprPf : 0,
                        DepositedPF = row.Cell(11).TryGetValue(out decimal depPf) ? depPf : 0,

                        ChallanNumber = challanNumber,
                        ChallanPdfPath = dbPdfPath,

                        CreatedBy = createdBy.EmployeeId,
                        CreatedOn = DateTime.UtcNow
                    };

                    _context.EmployeePayrollUploads.Add(entity);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return BuildExecuteSuccessResponse(
                    $"Payroll uploaded successfully for {monthYear} with Challan No: {challanNumber}"
                );
            }
            catch (Exception ex)
            {
                return BuildExecuteErrorResponse(
                    $"Error uploading payroll: {ex.Message}",
                    HttpStatusCode.InternalServerError
                );
            }
        }

        public async Task<PagedResultNew<EmployeePayrollUploadDto>> GetEmployeePayrollAsync(string? monthYear, int pageNumber, int pageSize, string searchTerm = "")
        {
            var query = _context.EmployeePayrollUploads.AsNoTracking();

            // 🔹 MonthYear filter (MMM-yy)
            if (!string.IsNullOrWhiteSpace(monthYear))
            {
                query = query.Where(x => x.MonthYear == monthYear);
            }

            // 🔹 Global Search
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                searchTerm = searchTerm.Trim();

                query = query.Where(x =>
            (x.ECode != null && x.ECode.Contains(searchTerm)) ||
            (x.EmpName != null && x.EmpName.Contains(searchTerm)) ||
            (x.Department != null && x.Department.Contains(searchTerm)) ||
            (x.Designation != null && x.Designation.Contains(searchTerm)) ||
            (x.Location != null && x.Location.Contains(searchTerm)) ||
            (x.LocCode != null && x.LocCode.Contains(searchTerm)) ||
            (x.MonthYear != null && x.MonthYear.Contains(searchTerm)) ||
            (x.ExcelMonthYear != null && x.ExcelMonthYear.Contains(searchTerm)) ||
            (x.ChallanNumber != null && x.ChallanNumber.Contains(searchTerm))
                  );
            }

            // 🔹 Total count AFTER filters
            var totalCount = await query.CountAsync();

            // 🔹 Pagination
            var data = await query
                .OrderBy(x => x.ECode)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new EmployeePayrollUploadDto
                {
                    Id = x.Id,
                    ECode = x.ECode,
                    LocCode = x.LocCode,
                    Location = x.Location,
                    EmpName = x.EmpName,
                    Department = x.Department,
                    Designation = x.Designation,

                    MonthYear = x.MonthYear,
                    ExcelMonthYear = x.ExcelMonthYear,

                    PayableDays = x.PayableDays,
                    EmpPF = x.EmpPF,
                    EmprPF = x.EmprPF,
                    DepositedPF = x.DepositedPF,

                    ChallanNumber = x.ChallanNumber,
                    ChallanPdfPath = x.ChallanPdfPath
                })
                .ToListAsync();

            return new PagedResultNew<EmployeePayrollUploadDto>
            {
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize,
                Data = data
            };
        }
        public async Task<ExecuteAndReponse> UploadESICFromExcelAsync(IFormFile excelFile, JwtLoginDetailDto createdBy)
        {
            try
            {

                if (excelFile == null || excelFile.Length == 0)
                {
                    return BuildExecuteErrorResponse(
                        "Excel file is required",
                        HttpStatusCode.BadRequest
                    );
                }

                using var workbook = new XLWorkbook(excelFile.OpenReadStream());
                var worksheet = workbook.Worksheet(1);
                var rows = worksheet.RowsUsed().Skip(1).ToList();

                if (!rows.Any())
                {
                    return BuildExecuteErrorResponse(
                        "Excel contains no data",
                        HttpStatusCode.BadRequest
                    );
                }

                var excelStoreCodes = rows
                    .Select(r => r.Cell(11).GetValue<string>()?.Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (!excelStoreCodes.Any())
                {
                    return BuildExecuteErrorResponse(
                        "StoreCode is missing in Excel",
                        HttpStatusCode.BadRequest
                    );
                }

                var validStoreCodes = await _context.tblLocations
                    .Where(x => excelStoreCodes.Contains(x.STCode))
                    .Select(x => x.STCode)
                    .ToListAsync();

                var invalidStoreCodes = excelStoreCodes
                    .Except(validStoreCodes, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (invalidStoreCodes.Any())
                {
                    return BuildExecuteErrorResponse(
                        $"Invalid StoreCode(s) found in Excel: {string.Join(", ", invalidStoreCodes)}",
                        HttpStatusCode.BadRequest
                    );
                }

                var groupedData = rows.GroupBy(r => new
                {
                    StoreCode = r.Cell(11).GetValue<string>()?.Trim(),
                    MonthYear = r.Cell(7).GetValue<string>()?.Trim()
                });

                using var transaction = await _context.Database.BeginTransactionAsync();

                foreach (var group in groupedData)
                {
                    var storeCode = group.Key.StoreCode;
                    var monthYear = group.Key.MonthYear;

                    if (string.IsNullOrWhiteSpace(monthYear))
                    {
                        return BuildExecuteErrorResponse(
                            $"Month-Year missing for StoreCode {storeCode}",
                            HttpStatusCode.BadRequest
                        );
                    }

                    if (!DateTime.TryParseExact(
                            monthYear,
                            "MMM-yy",
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.None,
                            out _))
                    {
                        return BuildExecuteErrorResponse(
                            $"Invalid Month-Year '{monthYear}' for StoreCode {storeCode}. Expected MMM-yy",
                            HttpStatusCode.BadRequest
                        );
                    }

                    var challanNumbers = group
                        .Select(r => r.Cell(12).GetValue<string>()?.Trim())
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    if (challanNumbers.Count != 1)
                    {
                        return BuildExecuteErrorResponse(
                            $"Multiple Challan Numbers found for Store {storeCode}, Month {monthYear}",
                            HttpStatusCode.BadRequest
                        );
                    }

                    var challanNumber = challanNumbers.First();

                    var challanPdfPaths = group
                        .Select(r => r.Cell(14).GetValue<string>()?.Trim())
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    if (challanPdfPaths.Count != 1)
                    {
                        return BuildExecuteErrorResponse(
                            $"Multiple Challan PDF paths found for Store {storeCode}, Month {monthYear}",
                            HttpStatusCode.BadRequest
                        );
                    }

                    var challanPdfPath = challanPdfPaths.First();

                    var existingRecords = await _context.EmployeeESICUploads
                        .Where(x => x.StoreCode == storeCode && x.MonthYear == monthYear)
                        .ToListAsync();

                    if (existingRecords.Any())
                    {
                        var historyRecords = existingRecords.Select(x => new EmployeeESICUploadHistory
                        {
                            OriginalId = x.Id,

                            ECode = x.ECode,
                            LocCode = x.LocCode,
                            Location = x.Location,
                            EmpName = x.EmpName,
                            Department = x.Department,
                            Designation = x.Designation,

                            MonthYear = x.MonthYear,

                            PayableDays = x.PayableDays,
                            EmpESIC = x.EmpESIC,
                            EmprESIC = x.EmprESIC,
                            DepositedESIC = x.DepositedESIC,

                            StoreCode = x.StoreCode,
                            ChallanNumber = x.ChallanNumber,
                            ChallanPdfPath = x.ChallanPdfPath,

                            CreatedBy = x.CreatedBy,
                            CreatedOn = x.CreatedOn,

                            ArchivedBy = createdBy.EmployeeId,
                            ArchivedOn = DateTime.UtcNow
                        }).ToList();

                        _context.EmployeeESICUploadHistories.AddRange(historyRecords);
                        _context.EmployeeESICUploads.RemoveRange(existingRecords);

                        await _context.SaveChangesAsync();
                    }

                    foreach (var row in group)
                    {
                        var ecode = row.Cell(1).GetValue<string>()?.Trim();
                        if (string.IsNullOrWhiteSpace(ecode)) continue;

                        var entity = new EmployeeESICUpload
                        {
                            ECode = ecode,
                            LocCode = row.Cell(2).GetValue<string>(),
                            Location = row.Cell(3).GetValue<string>(),
                            EmpName = row.Cell(4).GetValue<string>(),
                            Department = row.Cell(5).GetValue<string>(),
                            Designation = row.Cell(6).GetValue<string>(),

                            MonthYear = monthYear,

                            PayableDays = row.Cell(8).TryGetValue(out decimal days) ? days : null,
                            EmpESIC = row.Cell(9).TryGetValue(out decimal emp) ? emp : 0,
                            EmprESIC = row.Cell(10).TryGetValue(out decimal empr) ? empr : 0,
                            DepositedESIC = row.Cell(13).TryGetValue(out decimal dep) ? dep : 0,

                            StoreCode = storeCode,
                            ChallanNumber = challanNumber,
                            ChallanPdfPath = challanPdfPath,

                            CreatedBy = createdBy.EmployeeId,
                            CreatedOn = DateTime.UtcNow
                        };

                        _context.EmployeeESICUploads.Add(entity);
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return BuildExecuteSuccessResponse(
                    "ESIC uploaded successfully (multiple stores and months processed)"
                );
            }
            catch (Exception ex)
            {
                return BuildExecuteErrorResponse(
                    $"Error uploading ESIC: {ex.Message}",
                    HttpStatusCode.InternalServerError
                );
            }
        }
        public async Task<PagedResultNew<EmployeeESICUploadDto>> GetEmployeeESICAsync(string? searchTerm, int pageNumber, int pageSize)
        {
            var query = _context.EmployeeESICUploads.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                searchTerm = searchTerm.Trim();

                query = query.Where(x =>
                    x.ECode.Contains(searchTerm) ||
                    x.LocCode.Contains(searchTerm) ||
                    x.Location.Contains(searchTerm) ||
                    x.EmpName.Contains(searchTerm) ||
                    x.Department.Contains(searchTerm) ||
                    x.Designation.Contains(searchTerm) ||
                    x.StoreCode.Contains(searchTerm) ||
                    x.ChallanNumber.Contains(searchTerm)
                );
            }

            // -----------------------------
            // TOTAL COUNT
            // -----------------------------
            var totalCount = await query.CountAsync();

            // -----------------------------
            // PAGINATION + SORTING
            // -----------------------------
            var data = await query
                .OrderByDescending(x => x.CreatedOn)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new EmployeeESICUploadDto
                {
                    Id = x.Id,

                    ECode = x.ECode,
                    LocCode = x.LocCode,
                    Location = x.Location,
                    EmpName = x.EmpName,
                    Department = x.Department,
                    Designation = x.Designation,

                    MonthYear = x.MonthYear,

                    PayableDays = x.PayableDays,
                    EmpESIC = x.EmpESIC,
                    EmprESIC = x.EmprESIC,
                    DepositedESIC = x.DepositedESIC,

                    StoreCode = x.StoreCode,
                    ChallanNumber = x.ChallanNumber,
                    ChallanPdfPath = x.ChallanPdfPath
                })
                .ToListAsync();

            return new PagedResultNew<EmployeeESICUploadDto>
            {
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize,
                Data = data
            };
        }

        public async Task<ExecuteAndReponse> UploadRetentionAsync(
            IFormFile excelFile,
            JwtLoginDetailDto createdBy)
        {
            try
            {
                if (excelFile == null || excelFile.Length == 0)
                    return BuildExecuteErrorResponse("Excel file is required", HttpStatusCode.BadRequest);

                using var workbook = new XLWorkbook(excelFile.OpenReadStream());
                var worksheet = workbook.Worksheet(1);
                var rows = worksheet.RowsUsed().Skip(1).ToList();

                if (!rows.Any())
                    return BuildExecuteErrorResponse("Excel contains no data", HttpStatusCode.BadRequest);

                var retentionList = new List<tblRetention>();
                int rowNumber = 1;

                foreach (var row in rows)
                {
                    rowNumber++;

                    string locCode = row.Cell(1).GetString().Trim();
                    string location = row.Cell(2).GetString().Trim();
                    string ecode = row.Cell(3).GetString().Trim();
                    string name = row.Cell(4).GetString().Trim();

                    DateTime? joiningDate = ParseExcelDate(row.Cell(5));
                    string empStatus = row.Cell(6).GetString().Trim();
                    string retentionApplicable = row.Cell(7).GetString().Trim();

                    if (!decimal.TryParse(row.Cell(8).GetString(), out decimal retBonus))
                        return BuildExecuteErrorResponse(
                            $"Row {rowNumber}: Invalid Ret-Bonus value",
                            HttpStatusCode.BadRequest);

                    DateTime? dateOfCompletion = ParseExcelDate(row.Cell(9));
                    DateTime? retentionStartDate = ParseExcelDate(row.Cell(10));

                    // ✅ Retention Applicable
                    if (!retentionApplicable.Equals("YES", StringComparison.OrdinalIgnoreCase))
                        return BuildExecuteErrorResponse(
                            $"Row {rowNumber}: Retention Applicable must be YES",
                            HttpStatusCode.BadRequest);

                    // ✅ Ret Bonus range
                    if (retBonus < 20 || retBonus > 70)
                        return BuildExecuteErrorResponse(
                            $"Row {rowNumber}: Ret Bonus must be > 20 and < 70",
                            HttpStatusCode.BadRequest);

                    // ✅ Location validation
                    var locationEntity = await _context.tblLocations
                        .AsNoTracking()
                        .FirstOrDefaultAsync(x =>
                            x.STCode == locCode &&
                            x.LocationName == location);

                    if (locationEntity == null)
                        return BuildExecuteErrorResponse(
                            $"Row {rowNumber}: Location code/name mismatch",
                            HttpStatusCode.BadRequest);

                    // ✅ Employee validation
                    var employee = await _context.tblEmployees
                        .AsNoTracking()
                        .FirstOrDefaultAsync(x => x.Ecode == ecode);

                    if (employee == null)
                        return BuildExecuteErrorResponse(
                            $"Row {rowNumber}: Employee Ecode not found",
                            HttpStatusCode.BadRequest);

                    // ✅ Joining date fallback
                    joiningDate ??= employee.JOINING_DATE;

                    retentionList.Add(new tblRetention
                    {
                        LocCode = locCode,
                        Location = location,
                        Ecode = ecode,
                        Name = name,
                        JoiningDate = joiningDate,
                        EmpStatus = empStatus,
                        RetentionApplicable = true,
                        RetBonus = retBonus.ToString(CultureInfo.InvariantCulture),
                        DateOfComplition = dateOfCompletion,
                        ResignationStartDate = retentionStartDate,
                        CreatedBy = createdBy.EmployeeId,
                        CreateDate = DateTime.Now,
                        IsActive = true,
                        IsDeleted = false
                    });
                }

                await _context.tblRetentions.AddRangeAsync(retentionList);
                await _context.SaveChangesAsync();

                return BuildExecuteSuccessResponse("Retention data uploaded successfully");
            }
            catch (Exception ex)
            {
                return BuildExecuteErrorResponse(
                    $"Error uploading retention data: {ex.Message}",
                    HttpStatusCode.InternalServerError);
            }
        }
        public async Task<(List<RetentionDTO>? Data,
                           int TotalCount,
                           byte[]? ExcelBytes)> GetRetentionAsync(
            int pageNumber,
            int pageSize,
            string searchTerm,
            bool isExcel)
        {
            try
            {
                IQueryable<RetentionDTO> query =
                    _context.tblRetentions
                    .Where(x => x.IsDeleted == false)
                    .Select(x => new RetentionDTO
                    {
                        RetentionId = x.RetentionId,
                        LocCode = x.LocCode,
                        Location = x.Location,
                        Ecode = x.Ecode,
                        Name = x.Name,
                        JoiningDate = x.JoiningDate,
                        EmpStatus = x.EmpStatus,
                        RetentionApplicable = x.RetentionApplicable,
                        RetBonus = Convert.ToDecimal(x.RetBonus),
                        DateOfComplition = x.DateOfComplition,
                        RetentionStartDate = x.ResignationStartDate,
                        IsActive = x.IsActive ?? false
                    });

                // 🔍 Search
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    searchTerm = searchTerm.ToLower();
                    query = query.Where(x =>
                        x.Ecode.ToLower().Contains(searchTerm) ||
                        x.Name.ToLower().Contains(searchTerm) ||
                        x.Location.ToLower().Contains(searchTerm));
                }

                int totalCount = await query.CountAsync();

                // 👉 EXCEL PATH
                if (isExcel)
                {
                    var allData = await query.ToListAsync();
                    var excelBytes = GenerateRetentionExcel(allData);

                    return (null, totalCount, excelBytes);
                }

                // 👉 NORMAL LIST
                var pagedData = await query
                    .OrderByDescending(x => x.RetentionId)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                return (pagedData, totalCount, null);
            }
            catch (Exception ex)
            {
                // 🔴 Log exception here if you have logging
                // _logger.LogError(ex, "Error fetching retention data");

                throw new ApplicationException("Error while fetching retention data", ex);
            }
        }

        private byte[] GenerateRetentionExcel(List<RetentionDTO> data)
        {
            try
            {
                using var workbook = new ClosedXML.Excel.XLWorkbook();
                var ws = workbook.Worksheets.Add("Retention");

                // Headers
                ws.Cell(1, 1).Value = "Loc Code";
                ws.Cell(1, 2).Value = "Location";
                ws.Cell(1, 3).Value = "Ecode";
                ws.Cell(1, 4).Value = "Name";
                ws.Cell(1, 5).Value = "Joining Date";
                ws.Cell(1, 6).Value = "Emp Status";
                ws.Cell(1, 7).Value = "Ret Bonus %";
                ws.Cell(1, 8).Value = "Date Of Completion";
                ws.Cell(1, 9).Value = "Retention Start Date";
                ws.Cell(1, 10).Value = "Is Active";
                ws.Cell(1, 11).Value = "Retention Applicable";

                int row = 2;
                foreach (var item in data)
                {
                    ws.Cell(row, 1).Value = item.LocCode;
                    ws.Cell(row, 2).Value = item.Location;
                    ws.Cell(row, 3).Value = item.Ecode;
                    ws.Cell(row, 4).Value = item.Name;
                    ws.Cell(row, 5).Value = item.JoiningDate?.ToString("yyyy-MMM-dd");
                    ws.Cell(row, 6).Value = item.EmpStatus;
                    ws.Cell(row, 7).Value = item.RetBonus;
                    ws.Cell(row, 8).Value = item.DateOfComplition?.ToString("yyyy-MMM-dd");
                    ws.Cell(row, 9).Value = item.RetentionStartDate?.ToString("yyyy-MMM-dd");
                    ws.Cell(row, 10).Value = item.IsActive ? "Yes" : "No";
                    ws.Cell(row, 11).Value = item.RetentionApplicable.HasValue
                        ? (item.RetentionApplicable.Value ? "Yes" : "No")
                        : "N/A";
                    row++;
                }

                ws.Range(1, 1, 1, 10).Style.Font.Bold = true;
                ws.Columns().AdjustToContents();

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                return stream.ToArray();
            }
            catch (Exception ex)
            {
                // _logger.LogError(ex, "Error generating retention excel");
                throw new ApplicationException("Error generating retention Excel file", ex);
            }
        }

        private DateTime? ParseExcelDate(IXLCell cell)
        {
            if (cell == null || cell.IsEmpty())
                return null;

            var value = cell.GetString().Trim();

            if (string.IsNullOrWhiteSpace(value))
                return null;

            if (DateTime.TryParseExact(
                value,
                "yyyy-MMM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime parsedDate))
            {
                return parsedDate;
            }

            throw new Exception($"Invalid date format: {value}. Expected yyyy-MMM-dd");
        }

    }
}