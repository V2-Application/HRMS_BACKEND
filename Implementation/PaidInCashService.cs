using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HRMSAPI.Data;
using HRMSAPI.Interfaces;
using HRMSAPI.DTOs;

public class PaidInCashService : IPaidInCashService
{
    private readonly HRMSContext _context;
    private readonly ILogger<PaidInCashService> _logger;

    public PaidInCashService(HRMSContext context, ILogger<PaidInCashService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<(List<PaidInCashDTO> Records, int TotalRecords)> GetPaidInCashRecordsAsync(
        string? searchTerm = null,
        string? ecode = null,
        string? month = null,
        string? location = null,
        int page = 1,
        int pageSize = 10)
    {
        if (page < 1 || pageSize < 1)
        {
            _logger.LogWarning("Invalid pagination parameters: page={Page}, pageSize={PageSize}", page, pageSize);
            throw new ArgumentException("Page and pageSize must be greater than 0.");
        }

        try
        {
            var query = _context.tblPaidInCashes
                .AsNoTracking()
                .Select(pic => new PaidInCashDTO
                {
                    PaidInCashId = pic.PaidInCashId,
                    ECode = pic.E_CODE,
                    Amount = pic.AMOUNT,
                    Month = pic.MONTH,
                    Location = pic.LOCATION
                });

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                searchTerm = searchTerm.Trim().ToLower();
                query = query.Where(pic =>
                    (pic.ECode != null && pic.ECode.ToLower().Contains(searchTerm)) ||
                    (pic.Amount != null && pic.Amount.ToString().Contains(searchTerm)) ||
                    (pic.Month != null && pic.Month.ToLower().Contains(searchTerm)) ||
                    (pic.Location != null && pic.Location.ToLower().Contains(searchTerm)) ||
                    pic.PaidInCashId.ToString().Contains(searchTerm));
            }

            if (!string.IsNullOrWhiteSpace(ecode))
            {
                query = query.Where(pic => pic.ECode != null && pic.ECode.Contains(ecode, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(month))
            {
                query = query.Where(pic => pic.Month != null && pic.Month.Contains(month, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(location))
            {
                query = query.Where(pic => pic.Location != null && pic.Location.Contains(location, StringComparison.OrdinalIgnoreCase));
            }

            var totalRecords = await query.CountAsync();
            var records = await query
                .OrderByDescending(pic => pic.PaidInCashId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (records, totalRecords);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paid in cash records for searchTerm: {SearchTerm}, ecode: {ECode}, month: {Month}, location: {Location}, page: {Page}, pageSize: {PageSize}",
                searchTerm, ecode, month, location, page, pageSize);
            throw;
        }
    }

    public async Task<(bool Success, string Message)> UploadPaidInCashDataAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            _logger.LogWarning("No file uploaded for PaidInCash data upload.");
            return (false, "No file uploaded.");
        }

        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Invalid file format uploaded: {FileName}", file.FileName);
            return (false, "Only .xlsx files are supported.");
        }

        try
        {
            string createdBy = "systembulkuploader"; // Replace with actual user context if available
            using var transaction = await _context.Database.BeginTransactionAsync();
            var (recordsToAdd, validationErrors) = await ProcessExcelFile(file, createdBy);

            if (validationErrors.Any())
            {
                _logger.LogWarning("Validation errors in file {FileName}: {Errors}", file.FileName, string.Join("; ", validationErrors));
                await transaction.RollbackAsync();
                return (false, $"Invalid data in file: {string.Join("; ", validationErrors)}");
            }

            if (recordsToAdd.Any())
            {
                _context.tblPaidInCashes.AddRange(recordsToAdd);
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return (true, "Paid in cash data uploaded successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing paid in cash data for file: {FileName}", file.FileName);
            return (false, $"Error uploading paid in cash data: {ex.Message}");
        }
    }

    private async Task<(List<tblPaidInCash> RecordsToAdd, List<string> ValidationErrors)>
        ProcessExcelFile(IFormFile file, string createdBy)
    {
        var recordsToAdd = new List<tblPaidInCash>();
        var validationErrors = new List<string>();

        using var stream = file.OpenReadStream();
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheet(1);
        var rows = worksheet.RowsUsed().Skip(1);
        int rowNumber = 2;

        // Get all valid STCodes from tblLocation for validation
        var validSTCodes = await _context.tblLocations
            .AsNoTracking()
            .Where(l => l.IsActive == true && !string.IsNullOrEmpty(l.STCode))
            .Select(l => l.STCode.ToUpper())
            .ToListAsync();

        foreach (var row in rows)
        {
            var (record, error) = ParseRow(row, rowNumber, createdBy);
            if (error != null)
            {
                validationErrors.Add(error);
                rowNumber++;
                continue;
            }

            if (record.E_CODE == "NA")
            {
                validationErrors.Add($"Row {rowNumber}: E_CODE is required.");
                rowNumber++;
                continue;
            }

            // Validate location against STCode in tblLocation
            if (!string.IsNullOrWhiteSpace(record.LOCATION))
            {
                var locationUpper = record.LOCATION.ToUpper();
                if (!validSTCodes.Contains(locationUpper))
                {
                    validationErrors.Add($"Row {rowNumber}: Location '{record.LOCATION}' not found. STCode does not exist in location master.");
                    rowNumber++;
                    continue;
                }
            }

            // Allow duplicate E_CODEs - just add all valid records
            recordsToAdd.Add(record);
            rowNumber++;
        }

        return (recordsToAdd, validationErrors);
    }

    private static (tblPaidInCash? Record, string? Error) ParseRow(IXLRow row, int rowNumber, string createdBy)
    {
        try
        {
            var ecode = GetCellValue(row.Cell(1));
            var amountValue = GetCellValue(row.Cell(2));
            var month = GetCellValue(row.Cell(3));
            var location = GetCellValue(row.Cell(4));

            if (ecode == "NA")
                return (null, $"Row {rowNumber}: E_CODE is required.");

            decimal? amount = null;
            if (!string.IsNullOrWhiteSpace(amountValue) && amountValue != "NA")
            {
                if (!decimal.TryParse(amountValue, out var parsedAmount))
                    return (null, $"Row {rowNumber}: Invalid Amount format.");
                amount = parsedAmount;
            }

            return (new tblPaidInCash
            {
                E_CODE = ecode,
                AMOUNT = amount,
                MONTH = month == "NA" ? null : month,
                LOCATION = location == "NA" ? null : location
            }, null);
        }
        catch (Exception ex)
        {
            return (null, $"Row {rowNumber}: Error parsing row - {ex.Message}");
        }
    }

    private static string GetCellValue(IXLCell cell) =>
        string.IsNullOrWhiteSpace(cell.GetValue<string>()) ? "NA" : cell.GetValue<string>().Trim();
}