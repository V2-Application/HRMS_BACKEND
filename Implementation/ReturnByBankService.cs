using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HRMSAPI.Data;
using HRMSAPI.Interfaces;

public class ReturnByBankService : IReturnByBankService
{
    private readonly HRMSContext _context;
    private readonly ILogger<ReturnByBankService> _logger;

    public ReturnByBankService(HRMSContext context, ILogger<ReturnByBankService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<(List<ReturnByBankDTO> Records, int TotalRecords)> GetPaidByBankRecordsAsync(
    string? searchTerm = null,
    string? ecode = null,
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
            var query = _context.tblReturnByBanks
                .AsNoTracking()
                .Select(rbb => new ReturnByBankDTO
                {
                    ReturnByBankId = rbb.ReturnByBankId,
                    Ecode = rbb.Ecode,
                    AC = rbb.A_C,
                    ReturnByBank = rbb.ReturnByBank,
                    CreatedBy = rbb.CreatedBy,
                    CreatedOn = rbb.CreatedOn,
                    LastUpdatedBy = rbb.LastUpdatedBy,
                    LastUpdatedOn = rbb.LastUpdatedOn,
                    Date = rbb.Date
                });

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                searchTerm = searchTerm.Trim().ToLower();
                query = query.Where(rbb =>
                    (rbb.Ecode != null && rbb.Ecode.ToLower().Contains(searchTerm)) ||
                    (rbb.AC != null && rbb.AC.ToLower().Contains(searchTerm)) ||
                    (rbb.ReturnByBank != null && rbb.ReturnByBank.ToLower().Contains(searchTerm)) ||
                    (rbb.CreatedBy != null && rbb.CreatedBy.ToLower().Contains(searchTerm)) ||
                    (rbb.CreatedOn != null && rbb.CreatedOn.ToString().Contains(searchTerm)) ||
                    (rbb.LastUpdatedBy != null && rbb.LastUpdatedBy.ToLower().Contains(searchTerm)) ||
                    (rbb.LastUpdatedOn != null && rbb.LastUpdatedOn.ToString().Contains(searchTerm)) ||
                    (rbb.Date != null && rbb.Date.ToString().Contains(searchTerm)) ||
                    rbb.ReturnByBankId.ToString().Contains(searchTerm));
            }

            if (!string.IsNullOrWhiteSpace(ecode))
            {
                query = query.Where(rbb => rbb.Ecode != null && rbb.Ecode.Contains(ecode, StringComparison.OrdinalIgnoreCase));
            }

            var totalRecords = await query.CountAsync();
            var records = await query
                .OrderByDescending(rbb => rbb.ReturnByBankId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (records, totalRecords);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving return by bank records for searchTerm: {SearchTerm}, ecode: {Ecode}, page: {Page}, pageSize: {PageSize}", searchTerm, ecode, page, pageSize);
            throw;
        }
    }
    public async Task<(bool Success, string Message)> UploadReturnByBankDataAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            _logger.LogWarning("No file uploaded for ReturnByBank data upload.");
            return (false, "No file uploaded.");
        }

        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Invalid file format uploaded: {FileName}", file.FileName);
            return (false, "Only .xlsx files are supported.");
        }

        try
        {
            string createdBy = "system"; // Replace with actual user context if available
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
                _context.tblReturnByBanks.AddRange(recordsToAdd);
            }

            if (recordsToUpdate.Any())
            {
                _context.tblReturnByBanks.UpdateRange(recordsToUpdate);
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return (true, "Return by bank data uploaded successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing return by bank data for file: {FileName}", file.FileName);
            return (false, $"Error uploading return by bank data: {ex.Message}");
        }
    }

    private async Task<(List<tblReturnByBank> RecordsToAdd, List<tblReturnByBank> RecordsToUpdate, List<string> ValidationErrors)>
        ProcessExcelFile(IFormFile file, string createdBy)
    {
        var recordsToAdd = new List<tblReturnByBank>();
        var recordsToUpdate = new List<tblReturnByBank>();
        var validationErrors = new List<string>();
        var seenKeys = new HashSet<(string, DateTime?)>();

        using var stream = file.OpenReadStream();
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheet(1);
        var rows = worksheet.RowsUsed().Skip(1);
        int rowNumber = 2;

        var existingRecords = await _context.tblReturnByBanks
            .AsNoTracking()
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

            if (record.Ecode == "NA")
            {
                validationErrors.Add($"Row {rowNumber}: Ecode is required.");
                rowNumber++;
                continue;
            }

            if (!seenKeys.Add((record.Ecode, record.Date)))
            {
                validationErrors.Add($"Row {rowNumber}: Duplicate Ecode '{record.Ecode}' and Date '{record.Date}' in file.");
                rowNumber++;
                continue;
            }

            var existingRecord = existingRecords.FirstOrDefault(r =>
                r.Ecode.Equals(record.Ecode, StringComparison.OrdinalIgnoreCase) &&
                r.Date == record.Date);

            if (existingRecord != null)
            {
                UpdateExistingRecord(existingRecord, record, createdBy);
                recordsToUpdate.Add(existingRecord);
            }
            else
            {
                recordsToAdd.Add(record);
            }
            rowNumber++;
        }

        return (recordsToAdd, recordsToUpdate, validationErrors);
    }

    private static (tblReturnByBank? Record, string? Error) ParseRow(IXLRow row, int rowNumber, string createdBy)
    {
        try
        {
            var ecode = GetCellValue(row.Cell(1));
            var ac = GetCellValue(row.Cell(2));
            var returnByBank = GetCellValue(row.Cell(3));
            var dateValue = GetCellValue(row.Cell(4));

            if (ecode == "NA")
                return (null, $"Row {rowNumber}: Ecode is required.");
            if (string.IsNullOrWhiteSpace(dateValue) || dateValue == "NA")
                return (null, $"Row {rowNumber}: Date is required.");

            var date = ParseDate(dateValue);
            if (!date.HasValue)
                return (null, $"Row {rowNumber}: Invalid Date format.");

            return (new tblReturnByBank
            {
                Ecode = ecode,
                A_C = ac,
                ReturnByBank = returnByBank,
                CreatedBy = createdBy,
                CreatedOn = DateTime.UtcNow,
                LastUpdatedBy = createdBy,
                LastUpdatedOn = DateTime.UtcNow,
                Date = date.Value
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

    private static void UpdateExistingRecord(tblReturnByBank existing, tblReturnByBank newRecord, string createdBy)
    {
        existing.A_C = newRecord.A_C;
        existing.ReturnByBank = newRecord.ReturnByBank;
        existing.LastUpdatedBy = createdBy;
        existing.LastUpdatedOn = DateTime.UtcNow;
    }
}