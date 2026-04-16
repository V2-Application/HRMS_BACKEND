using ClosedXML.Excel;
using HRMSAPI.Data;
using HRMSAPI.DTO;
using HRMSAPI.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace HRMSAPI.Implementation
{
    public class BankTransferService : IBankTransferService
    {
        private readonly HRMSContext _context;
        private readonly ILogger<BankTransferService> _logger;

        public BankTransferService(HRMSContext context, ILogger<BankTransferService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<(List<BankTransferDTO> Records, int TotalRecords)> GetBankTransferList(
    string? searchTerm = null,
    string? ecode = null,
    int page = 1,
    int pageSize = 10)
        {
            if (page < 1 || pageSize < 1)
            {
                _logger.LogWarning("Invalid pagination: page={Page}, pageSize={PageSize}", page, pageSize);
                throw new ArgumentException("Page and pageSize must be greater than 0.");
            }

            try
            {
                var query = _context.tblBankTransfers
                    .AsNoTracking()
                    .Select(bt => new BankTransferDTO
                    {
                        BankTransferId = (int)bt.BankTransferId,
                        Ecode = bt.Ecode,
                        AC = bt.A_C,
                        BankTransfer = bt.BankTransfer,
                        CreatedBy = bt.CreatedBy,
                        CreatedOn = bt.CreatedOn,
                        LastUpdatedBy = bt.LastUpdatedBy,
                        LastUpdatedOn = bt.LastUpdatedOn,
                        Date = bt.Date
                    });

                // Apply search across all columns
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    searchTerm = searchTerm.Trim().ToLower();
                    query = query.Where(bt =>
                        (bt.Ecode != null && bt.Ecode.ToLower().Contains(searchTerm)) ||
                        (bt.AC != null && bt.AC.ToLower().Contains(searchTerm)) ||
                        (bt.BankTransfer != null && bt.BankTransfer.ToString().Contains(searchTerm)) ||
                        (bt.CreatedBy != null && bt.CreatedBy.ToLower().Contains(searchTerm)) ||
                        (bt.CreatedOn != null && bt.CreatedOn.ToString().Contains(searchTerm)) ||
                        (bt.LastUpdatedBy != null && bt.LastUpdatedBy.ToLower().Contains(searchTerm)) ||
                        (bt.LastUpdatedOn != null && bt.LastUpdatedOn.ToString().Contains(searchTerm)) ||
                        (bt.Date != null && bt.Date.ToString().Contains(searchTerm)) ||
                        bt.BankTransferId.ToString().Contains(searchTerm));
                }

                // Apply specific ecode filter
                if (!string.IsNullOrWhiteSpace(ecode))
                {
                    query = query.Where(bt => bt.Ecode != null && bt.Ecode.Contains(ecode, StringComparison.OrdinalIgnoreCase));
                }

                var totalRecords = await query.CountAsync();
                var records = await query
                    .OrderByDescending(bt => bt.CreatedOn)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                return (records, totalRecords);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving bank transfer records for searchTerm: {SearchTerm}, ecode: {Ecode}, page: {Page}, pageSize: {PageSize}", searchTerm, ecode, page, pageSize);
                throw;
            }
        }

        public async Task<(bool Success, string Message)> UploadBankTransferDataAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return (false, "No file uploaded.");

            if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                return (false, "Only .xlsx files are supported.");

            try
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                var (recordsToAdd, recordsToUpdate, errors) = await ProcessExcelFile(file);

                if (errors.Any())
                {
                    await transaction.RollbackAsync();
                    return (false, $"Invalid data: {string.Join("; ", errors)}");
                }

                if (recordsToAdd.Any())
                    _context.tblBankTransfers.AddRange(recordsToAdd);

                if (recordsToUpdate.Any())
                    _context.tblBankTransfers.UpdateRange(recordsToUpdate);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return (true, "Bank transfer data uploaded successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading bank transfer data for file: {FileName}", file.FileName);
                return (false, $"Error uploading data: {ex.Message}");
            }
        }

        private async Task<(List<tblBankTransfer> RecordsToAdd, List<tblBankTransfer> RecordsToUpdate, List<string> Errors)>
            ProcessExcelFile(IFormFile file)
        {
            var recordsToAdd = new List<tblBankTransfer>();
            var recordsToUpdate = new List<tblBankTransfer>();
            var errors = new List<string>();

            using var stream = file.OpenReadStream();
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheet(1);
            var rows = worksheet.RowsUsed().Skip(1); // Skip header
            int rowNumber = 2;

            // Load existing records into a list
            var existingRecords = await _context.tblBankTransfers
                .AsNoTracking()
                .ToListAsync();

            foreach (var row in rows)
            {
                var (record, error) = ParseRow(row, rowNumber);
                if (error != null)
                {
                    errors.Add(error);
                    rowNumber++;
                    continue;
                }

                if (record.Ecode == "NA")
                {
                    errors.Add($"Row {rowNumber}: Ecode is required.");
                    rowNumber++;
                    continue;
                }

                // Check for existing record by Ecode and Date
                var existingRecord = existingRecords.FirstOrDefault(r =>
                    r.Ecode.Equals(record.Ecode, StringComparison.OrdinalIgnoreCase) &&
                    r.Date == record.Date);

                if (existingRecord != null)
                {
                    UpdateExistingRecord(existingRecord, record);
                    recordsToUpdate.Add(existingRecord);
                }
                else
                {
                    recordsToAdd.Add(record);
                }
                rowNumber++;
            }

            return (recordsToAdd, recordsToUpdate, errors);
        }

        private static (tblBankTransfer? Record, string? Error) ParseRow(IXLRow row, int rowNumber)
        {
            try
            {
                var ecode = GetCellValue(row.Cell(1));
                var ac = GetCellValue(row.Cell(2));
                var bankTransfer = GetCellValue(row.Cell(3));
                var dateValue = GetCellValue(row.Cell(4));

                if (ecode == "NA")
                    return (null, $"Row {rowNumber}: Ecode is required.");
                if (string.IsNullOrWhiteSpace(dateValue) || dateValue == "NA")
                    return (null, $"Row {rowNumber}: Date is required.");

                var date = ParseDate(dateValue);
                if (!date.HasValue)
                    return (null, $"Row {rowNumber}: Invalid Date format.");

                return (new tblBankTransfer
                {
                    Ecode = ecode,
                    A_C = ac,
                    BankTransfer = bankTransfer,
                    CreatedBy = "ADMIN",
                    CreatedOn = DateTime.UtcNow,
                    LastUpdatedBy = "admin",
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

        private static void UpdateExistingRecord(tblBankTransfer existing, tblBankTransfer newRecord)
        {
            existing.A_C = newRecord.A_C;
            existing.BankTransfer = newRecord.BankTransfer;
            existing.CreatedBy = "ADMIN";
            existing.CreatedOn = DateTime.UtcNow;
            existing.LastUpdatedBy = "admin";
            existing.LastUpdatedOn = DateTime.UtcNow;
            existing.Date = newRecord.Date;
        }
    }
}
