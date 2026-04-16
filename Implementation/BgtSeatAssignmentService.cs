using ClosedXML.Excel;
using HRMSAPI.Data;
using HRMSAPI.Interfaces;
using Roomsy.DTOS.GenericsResponses;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace HRMSAPI.Implementation
{
    public class BgtSeatAssignmentService : BaseService, IBgtSeatAssignmentService
    {
        private readonly HRMSContext _context;
        private readonly ILogger<BgtSeatAssignmentService> _logger;

        public BgtSeatAssignmentService(HRMSContext context, ILogger<BgtSeatAssignmentService> logger) : base(context)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<FetchAndResponse> UploadBgtSeatAssignmentExcelAsync(IFormFile file)
        {
            var expectedHeaders = new[] { "LOC CODE", "SEAT MASTER NO.", "E-CODE" };
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

            var rows = worksheet.RowsUsed().Skip(1).ToList();

            // Prepare keys for lookup
            var locECodePairs = rows
                .Select(r => new
                {
                    LOC_CODE = r.Cell(1).GetValue<string>()?.Trim(),
                    E_CODE = r.Cell(3).GetValue<string>()?.Trim()
                })
                .Where(x => !string.IsNullOrEmpty(x.LOC_CODE) && !string.IsNullOrEmpty(x.E_CODE))
                .ToList();

            var locCodes = locECodePairs.Select(x => x.LOC_CODE).ToList();
            var eCodes = locECodePairs.Select(x => x.E_CODE).ToList();

            var existingAssignments = await _context.BGTSEATAssignments
                .Where(x => locCodes.Contains(x.LOC_CODE) && eCodes.Contains(x.E_CODE))
                .ToListAsync();

            var existingDict = existingAssignments
                .ToDictionary(
                    x => $"{x.LOC_CODE?.Trim().ToUpperInvariant()}|{x.E_CODE?.Trim().ToUpperInvariant()}",
                    x => x
                );

            var newRows = new List<BGTSEATAssignment>();
            var updatedRows = new List<BGTSEATAssignment>();

            foreach (var row in rows)
            {
                var locCode = row.Cell(1).GetValue<string>()?.Trim();
                var seatMas = row.Cell(2).GetValue<string>()?.Trim();
                var eCode = row.Cell(3).GetValue<string>()?.Trim();

                if (string.IsNullOrEmpty(locCode) || string.IsNullOrEmpty(eCode))
                    continue;

                var key = $"{locCode?.Trim().ToUpperInvariant()}|{eCode?.Trim().ToUpperInvariant()}";

                if (existingDict.TryGetValue(key, out var existing))
                {
                    // Update existing
                    existing.SEAT_MAS = seatMas;
                    updatedRows.Add(existing);
                }
                else
                {
                    // Insert new
                    newRows.Add(new BGTSEATAssignment
                    {
                        LOC_CODE = locCode,
                        SEAT_MAS = seatMas,
                        E_CODE = eCode
                    });
                }
            }

            try
            {
                if (newRows.Any())
                    await _context.BGTSEATAssignments.AddRangeAsync(newRows);
                if (updatedRows.Any())
                    _context.BGTSEATAssignments.UpdateRange(updatedRows);

                await _context.SaveChangesAsync();
                return BuildFetchSuccessResponse("BGTSEATAssignment uploaded successfully", null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading BGTSEATAssignment");
                return BuildFetchErrorResponse($"Error uploading BGTSEATAssignment: {ex.Message}", HttpStatusCode.BadRequest);
            }
        }

        public async Task<FetchAndResponse> GetAllBgtSeatAssignmentAsync()
        {
            try
            {
                var data = await _context.BGTSEATAssignments.ToListAsync();
                if (data == null || data.Count < 1)
                {
                    return BuildFetchErrorResponse("No Data Found", HttpStatusCode.NotFound);
                }
                return BuildFetchSuccessResponse("Fetched all BGTSEATAssignment records successfully", data);
            }
            catch (Exception ex)
            {
                return BuildFetchErrorResponse(ex.Message, HttpStatusCode.BadRequest);
            }
        }
    }
} 