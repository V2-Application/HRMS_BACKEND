using ClosedXML.Excel;
using HRMSAPI.Data;
using HRMSAPI.Implementation;
using Microsoft.EntityFrameworkCore;
using Roomsy.DTOS.GenericsResponses;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

public class PaidByBankService : BaseService, IPaidByBankService
{
    private readonly HRMSContext _context;
    private readonly ILogger<PaidByBankService> _logger;

    public PaidByBankService(HRMSContext context, ILogger<PaidByBankService> logger) : base(context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // GetPaidByBankRecordsAsync remains unchanged
    //public async Task<(List<PaidByBankDTO> Records, int TotalRecords)> GetPaidByBankRecordsAsync(
    // string? searchTerm = null,
    // string? ecode = null,
    // int page = 1,
    // int pageSize = 10)
    //{
    //    if (page < 1 || pageSize < 1)
    //    {
    //        _logger.LogWarning("Invalid pagination parameters: page={Page}, pageSize={PageSize}", page, pageSize);
    //        throw new ArgumentException("Page and pageSize must be greater than 0.");
    //    }

    //    try
    //    {
    //        var query = _context.tblPaidByBanks
    //            .AsNoTracking()
    //            .Select(pbb => new PaidByBankDTO
    //            {
    //                tblPaidByBankId = pbb.tblPaidByBankId,
    //                Ecode = pbb.Ecode,
    //                AC = pbb.A_C,
    //                PaidByBank = pbb.PaidByBank,
    //                CreatedBy = pbb.CreatedBy,
    //                CreatedOn = pbb.CreatedOn,
    //                LastUpdatedBy = pbb.LastUpdatedBy,
    //                LastUpdatedOn = pbb.LastUpdatedOn,
    //                Date = pbb.Date,
    //                UTR = pbb.UTR
    //            });

    //        // Apply search across all columns
    //        if (!string.IsNullOrWhiteSpace(searchTerm))
    //        {
    //            searchTerm = searchTerm.Trim().ToLower();
    //            query = query.Where(pbb =>
    //                (pbb.Ecode != null && pbb.Ecode.ToLower().Contains(searchTerm)) ||
    //                (pbb.AC != null && pbb.AC.ToLower().Contains(searchTerm)) ||
    //                (pbb.PaidByBank != null && pbb.PaidByBank.ToString().Contains(searchTerm)) ||
    //                (pbb.CreatedBy != null && pbb.CreatedBy.ToLower().Contains(searchTerm)) ||
    //                (pbb.CreatedOn != null && pbb.CreatedOn.ToString().Contains(searchTerm)) ||
    //                (pbb.LastUpdatedBy != null && pbb.LastUpdatedBy.ToLower().Contains(searchTerm)) ||
    //                (pbb.LastUpdatedOn != null && pbb.LastUpdatedOn.ToString().Contains(searchTerm)) ||
    //                (pbb.Date != null && pbb.Date.ToString().Contains(searchTerm)) ||
    //                (pbb.UTR != null && pbb.UTR.ToLower().Contains(searchTerm)) ||
    //                pbb.tblPaidByBankId.ToString().Contains(searchTerm));
    //        }

    //        // Apply specific ecode filter
    //        if (!string.IsNullOrWhiteSpace(ecode))
    //        {
    //            query = query.Where(pbb => pbb.Ecode != null && pbb.Ecode.Contains(ecode, StringComparison.OrdinalIgnoreCase));
    //        }

    //        var totalRecords = await query.CountAsync();
    //        var records = await query
    //            .OrderByDescending(pbb => pbb.tblPaidByBankId)
    //            .Skip((page - 1) * pageSize)
    //            .Take(pageSize)
    //            .ToListAsync();

    //        return (records, totalRecords);
    //    }
    //    catch (Exception ex)
    //    {
    //        _logger.LogError(ex, "Error retrieving paid by bank records for searchTerm: {SearchTerm}, ecode: {Ecode}, page: {Page}, pageSize: {PageSize}", searchTerm, ecode, page, pageSize);
    //        throw;
    //    }
    //}
    public async Task<FetchAndResponse> GetPaidByBankRecordsAsync(
    string? searchTerm = null,
    string? ecode = null,
    string? monthYear = null,
    bool asExcel = false,
    int? page = null,
    int? pageSize = null)
    {
        try
        {
            // 🔹 BASE QUERY (ENTITY)
            IQueryable<tblPaidByBank> query = _context.tblPaidByBanks.AsNoTracking();

            // 🔍 Global search (string columns only)
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();

                query = query.Where(pbb =>
                    (pbb.Ecode != null && pbb.Ecode.ToLower().Contains(term)) ||
                    (pbb.A_C != null && pbb.A_C.ToLower().Contains(term)) ||
                    (pbb.CreatedBy != null && pbb.CreatedBy.ToLower().Contains(term)) ||
                    (pbb.LastUpdatedBy != null && pbb.LastUpdatedBy.ToLower().Contains(term)) ||
                    (pbb.UTR != null && pbb.UTR.ToLower().Contains(term)) ||
                    (pbb.Remarks != null && pbb.Remarks.ToLower().Contains(term))
                );
            }

            // 🔎 Ecode filter
            if (!string.IsNullOrWhiteSpace(ecode))
            {
                var ecodeLower = ecode.Trim().ToLower();
                query = query.Where(pbb =>
                    pbb.Ecode != null &&
                    pbb.Ecode.ToLower().Contains(ecodeLower));
            }

            // 📅 Month-Year filter (JUN-25)
            if (!string.IsNullOrWhiteSpace(monthYear))
            {
                if (!DateTime.TryParseExact(
                        monthYear.Trim().ToUpperInvariant(),
                        "MMM-yy",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var parsedDate))
                {
                    return BuildFetchErrorResponse(
                        "Invalid monthYear format. Expected MMM-yy (e.g. JUN-25)",
                        System.Net.HttpStatusCode.BadRequest);
                }

                int year = parsedDate.Year;
                int month = parsedDate.Month;

                query = query.Where(pbb =>
                    pbb.Date.HasValue &&
                    pbb.Date.Value.Year == year &&
                    pbb.Date.Value.Month == month);
            }

            if (asExcel)
            {
                var data = await query
                    .OrderByDescending(x => x.tblPaidByBankId)
                    .ToListAsync();

                if (data.Count == 0)
                    return BuildFetchErrorResponse("No Data Found", System.Net.HttpStatusCode.NotFound);

                // 🔹 Convert to DataTable
                var dt = new DataTable("PaidByBank");
                dt.Columns.AddRange(new[]
                {
                new DataColumn("Ecode"),
                new DataColumn("AC"),
                new DataColumn("PaidByBank"),
                new DataColumn("Date"),
                new DataColumn("UTR"),
                new DataColumn("Remarks"),

            });

                foreach (var item in data)
                {
                    dt.Rows.Add(
                        item.Ecode ?? "",
                        item.A_C ?? "",
                        item.PaidByBank,
                        item.Date?.ToString("yyyy-MM-dd") ?? "",
                        item.UTR ?? "",
                        item.Remarks ?? ""

                    );
                }

                // 🔹 Create Excel
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add(dt, "PaidByBank");

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);

                return BuildFetchSuccessResponse(
                    "Fetched Successfully (Excel)",
                    stream.ToArray());
            }

            var totalRecords = await query.CountAsync();

            if (totalRecords == 0)
                return BuildFetchErrorResponse("No Data Found", System.Net.HttpStatusCode.NotFound);

            IQueryable<tblPaidByBank> pagedQuery = query
                .OrderByDescending(pbb => pbb.tblPaidByBankId);

            if (page.HasValue && pageSize.HasValue && page > 0 && pageSize > 0)
            {
                pagedQuery = pagedQuery
                    .Skip((page.Value - 1) * pageSize.Value)
                    .Take(pageSize.Value);
            }

            var records = await pagedQuery
                .Select(pbb => new PaidByBankDTO
                {
                    tblPaidByBankId = pbb.tblPaidByBankId,
                    Ecode = pbb.Ecode,
                    AC = pbb.A_C,
                    PaidByBank = pbb.PaidByBank,
                    Date = pbb.Date,
                    UTR = pbb.UTR,
                    Remarks = pbb.Remarks
                })
                .ToListAsync();

            var resultObj = new
            {
                Data = records,
                TotalCount = totalRecords,
                Page = page,
                PageSize = pageSize
            };

            return BuildFetchSuccessResponse("Fetched Successfully", resultObj);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetPaidByBankRecordsAsync");
            return BuildFetchErrorResponse(ex.Message, System.Net.HttpStatusCode.BadRequest);
        }
    }
    public async Task<(bool Success, string Message)> UploadPaidByBankDataAsync(IFormFile file, string createdBy)
    {
        if (file == null || file.Length == 0)
        {
            _logger.LogWarning("No file uploaded for PaidByBank data upload.");
            return (false, "No file uploaded.");
        }

        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Invalid file format uploaded: {FileName}", file.FileName);
            return (false, "Only .xlsx files are supported.");
        }

        try
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            var (recordsToAdd, recordsToUpdate, validationErrors) = await ProcessExcelFile(file, createdBy);

            if (validationErrors.Any())
            {
                _logger.LogWarning("Validation errors in file {FileName}: {Errors}", file.FileName, string.Join("; ", validationErrors));
                await transaction.RollbackAsync();
                return (false, $"Invalid data in file: {string.Join("; ", validationErrors)}");
            }

            if (recordsToAdd.Any())
            {
                _context.tblPaidByBanks.AddRange(recordsToAdd);
            }

            if (recordsToUpdate.Any())
            {
                _context.tblPaidByBanks.UpdateRange(recordsToUpdate);
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return (true, "Paid by bank data uploaded successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing paid by bank data for file: {FileName}", file.FileName);
            return (false, $"Error uploading paid by bank data: {ex.Message}");
        }
    }

    private async Task<(List<tblPaidByBank> RecordsToAdd, List<tblPaidByBank> RecordsToUpdate, List<string> ValidationErrors)>
        ProcessExcelFile(IFormFile file, string createdBy)
    {
        var recordsToAdd = new List<tblPaidByBank>();
        var recordsToUpdate = new List<tblPaidByBank>();
        var validationErrors = new List<string>();
        var seenKeys = new HashSet<(string, DateTime?, string)>(); // Track duplicates in Excel file (Ecode, Date, UTR)

        using var stream = file.OpenReadStream();
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheet(1);
        var rows = worksheet.RowsUsed().Skip(1); // Skip header
        int rowNumber = 2;

        // First pass: Collect all ECodes and validate them
        var allEcodes = new List<string>();
        var tempRecords = new List<(tblPaidByBank Record, int RowNumber)>();

        foreach (var row in rows)
        {
            var (record, error) = ParseRow(row, rowNumber, createdBy);
            if (error != null)
            {
                validationErrors.Add(error);
                rowNumber++;
                continue;
            }

            if (record.Ecode == "NA")
            {
                validationErrors.Add($"Row {rowNumber}: Ecode is required.");
                rowNumber++;
                continue;
            }
            if (record.Date.HasValue && record.Date.Value.Date > DateTime.Today)
            {
                validationErrors.Add(
                    $"Row {rowNumber}: Date '{record.Date:yyyy-MM-dd}' cannot be a future date.");
                rowNumber++;
                continue;
            }
            // Check for duplicates in Excel file (Ecode, Date, UTR combination)
            var key = (record.Ecode, record.Date, record.UTR);
            if (!seenKeys.Add(key))
            {
                validationErrors.Add($"Row {rowNumber}: Duplicate combination of Ecode '{record.Ecode}', Date '{record.Date}', and UTR '{record.UTR}' in file.");
                rowNumber++;
                continue;
            }

            allEcodes.Add(record.Ecode);
            tempRecords.Add((record, rowNumber));
            rowNumber++;
        }

        // Validate all ECodes against tblEmployee
        if (allEcodes.Any())
        {
            var validEcodes = await _context.tblEmployees
                .AsNoTracking()
                .Where(e => allEcodes.Contains(e.Ecode))
                .Select(e => e.Ecode)
                .ToListAsync();

            var invalidEcodes = allEcodes.Except(validEcodes, StringComparer.OrdinalIgnoreCase).ToList();
            if (invalidEcodes.Any())
            {
                validationErrors.Add($"Invalid ECodes (not found in tblEmployee or IsDeleted = true): {string.Join(", ", invalidEcodes)}");
                return (recordsToAdd, recordsToUpdate, validationErrors);
            }
        }

        // Load existing records into a list
        var existingRecords = await _context.tblPaidByBanks
            .AsNoTracking()
            .ToListAsync();

        // Check for existing records in database (Ecode, Date, UTR combination)
        var existingCombinations = new List<string>();
        foreach (var (record, originalRowNumber) in tempRecords)
        {
            //var existingRecord = existingRecords.FirstOrDefault(r =>
            //    r.Ecode.Equals(record.Ecode, StringComparison.OrdinalIgnoreCase) &&
            //    r.Date == record.Date &&
            //    r.UTR.Equals(record.UTR, StringComparison.OrdinalIgnoreCase));

            //Updated By Gautam
            var existingRecord = existingRecords.FirstOrDefault(r =>string.Equals(r.Ecode, record.Ecode, StringComparison.OrdinalIgnoreCase) &&
            r.Date == record.Date && string.Equals(r.UTR, record.UTR, StringComparison.OrdinalIgnoreCase));

            if (existingRecord != null)
            {
                existingCombinations.Add($"Ecode: {record.Ecode}, Date: {record.Date:yyyy-MM-dd}, UTR: {record.UTR},Remarks:{record.Remarks}");
            }
        }

        if (existingCombinations.Any())
        {
            validationErrors.Add($"Records already exist in database: {string.Join("; ", existingCombinations)}");
            return (recordsToAdd, recordsToUpdate, validationErrors);
        }

        // Second pass: Process records for add (no updates since we don't allow duplicates)
        foreach (var (record, originalRowNumber) in tempRecords)
        {
            recordsToAdd.Add(record);
        }

        return (recordsToAdd, recordsToUpdate, validationErrors);
    }

    private static (tblPaidByBank? Record, string? Error) ParseRow(IXLRow row, int rowNumber, string createdBy)
    {
        try
        {
            var ecode = GetCellValue(row.Cell(1));
            var ac = GetCellValue(row.Cell(2));
            var paidByBank = GetCellValue(row.Cell(3));
            var dateValue = GetCellValue(row.Cell(4));
            var utr = GetCellValue(row.Cell(5));
            var remarks = GetCellValue(row.Cell(6));

            if (ecode == "NA")
                return (null, $"Row {rowNumber}: Ecode is required.");
            if (string.IsNullOrWhiteSpace(dateValue) || dateValue == "NA")
                return (null, $"Row {rowNumber}: Date is required.");
            if (string.IsNullOrWhiteSpace(utr) || utr == "NA")
                return (null, $"Row {rowNumber}: UTR is required.");

            var date = ParseDate(dateValue);
            if (!date.HasValue)
                return (null, $"Row {rowNumber}: Invalid Date format.");

            return (new tblPaidByBank
            {
                Ecode = ecode,
                A_C = ac,
                PaidByBank = paidByBank,
                CreatedBy = createdBy,
                CreatedOn = DateTime.UtcNow,
                LastUpdatedBy = createdBy,
                LastUpdatedOn = DateTime.UtcNow,
                Date = date.Value,
                UTR = utr,
                Remarks = remarks
            }, null);
        }
        catch (Exception ex)
        {
            return (null, $"Row {rowNumber}: Error parsing row - {ex.Message}");
        }
    }

    private static string GetCellValue(IXLCell cell) =>
        string.IsNullOrWhiteSpace(cell.GetValue<string>()) ? "NA" : cell.GetValue<string>().Trim();

    private static DateTime? ParseDate(string? value) =>
        string.IsNullOrWhiteSpace(value) || value == "NA" ? null : DateTime.TryParse(value, out var result) ? result : null;

    private static void UpdateExistingRecord(tblPaidByBank existing, tblPaidByBank newRecord, string createdBy)
    {
        existing.A_C = newRecord.A_C;
        existing.PaidByBank = newRecord.PaidByBank;
        existing.CreatedBy = createdBy;
        existing.CreatedOn = DateTime.UtcNow;
        existing.LastUpdatedBy = createdBy;
        existing.LastUpdatedOn = DateTime.UtcNow;
        existing.Date = newRecord.Date;
        existing.UTR = newRecord.UTR;
    }
}
