using HRMSAPI.DTO;
using HRMSAPI.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Roomsy.DTOS.GenericsResponses;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using ClosedXML.Excel;
using HRMSAPI.Extension;
using System.Security.Claims;
using System;
using System.Globalization;

namespace HRMSAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    [RequirePageAccess("/salary_recal")]
    public class SalaryRecalculateController : ControllerBase
    {
        private readonly ISalaryRecalculate _salaryRecalculateService;
        private readonly IWebHostEnvironment _env;

        public SalaryRecalculateController(ISalaryRecalculate salaryRecalculateService, IWebHostEnvironment env)
        {
            _salaryRecalculateService = salaryRecalculateService;
            _env = env;
        }

        [HttpPost("recalculate")]
        public async Task<IActionResult> Recalculate([FromBody] SalaryRecalculateDto obj)
        {
            // ✅ Validate ECodes
            if (string.IsNullOrWhiteSpace(obj.ECodes))
            {
                return BadRequest(new ExecuteAndReponse
                {
                    Status = false,
                    Message = "ECodes are required."
                });
            }

            // ✅ Validate Month format (MMM-YY)
            if (string.IsNullOrWhiteSpace(obj.Month) ||
                !Regex.IsMatch(obj.Month, @"^[A-Z][a-z]{2}-\d{2}$", RegexOptions.IgnoreCase))
            {
                return BadRequest(new ExecuteAndReponse
                {
                    Status = false,
                    Message = "Month format must be 'MMM-YY' (e.g., Jul-25)."
                });
            }

            // ✅ Call the service
            var result = await _salaryRecalculateService.SalaryRecalculate(obj);

            if (result.Status)
                return Ok(result);

            return StatusCode(500, result);
        }

        [HttpPost("recalculate-by-month")]
        public async Task<IActionResult> RecalculateByMonth([FromBody] SalaryRecalculateByMonthDto obj)
        {
            // ✅ Validate Month format (MMM-YY)
            if (string.IsNullOrWhiteSpace(obj.Month) ||
                !Regex.IsMatch(obj.Month, @"^[A-Z][a-z]{2}-\d{2}$",RegexOptions.IgnoreCase))
            {
                return BadRequest(new ExecuteAndReponse
                {
                    Status = false,
                    Message = "Month format must be 'MMM-YY' (e.g., Jul-25)."
                });
            }

            // ✅ Call the service
            var result = await _salaryRecalculateService.SalaryRecalculateByMonth(obj);

            if (result.Status)
                return Ok(result);

            return StatusCode(500, result);
        }

        [HttpPost("recalculate-new"),Authorize]
        public async Task<IActionResult> RecalculateNew([FromBody] SalaryRecalculateDto obj)
        {
            // ✅ Validate ECodes
            if (string.IsNullOrWhiteSpace(obj.ECodes))
            {
                return BadRequest(new ExecuteAndReponse
                {
                    Status = false,
                    Message = "ECodes are required."
                });
            }

            // ✅ Validate Month format (MMM-YY)
            if (string.IsNullOrWhiteSpace(obj.Month) ||
                !Regex.IsMatch(obj.Month, @"^[A-Z][a-z]{2}-\d{2}$", RegexOptions.IgnoreCase))
            {
                return BadRequest(new ExecuteAndReponse
                {
                    Status = false,
                    Message = "Month format must be 'MMM-YY' (e.g., Jul-25)."
                });
            }

            // ✅ Call the service
            var result = await _salaryRecalculateService.SalaryRecalculateNew(obj);

            if (result.Status)
                return Ok(result);

            return StatusCode(500, result);
        }

        [HttpPost("recalculate-by-month-new"),Authorize]
        public async Task<IActionResult> RecalculateByMonthNew([FromBody] SalaryRecalculateByMonthDto obj)
        {
            // ✅ Validate Month format (MMM-YY)
            if (string.IsNullOrWhiteSpace(obj.Month) ||
                !Regex.IsMatch(obj.Month, @"^[A-Z][a-z]{2}-\d{2}$", RegexOptions.IgnoreCase))
            {
                return BadRequest(new ExecuteAndReponse
                {
                    Status = false,
                    Message = "Month format must be 'MMM-YY' (e.g., Jul-25)."
                });
            }

            // ✅ Call the service
            var result = await _salaryRecalculateService.SalaryRecalculateByMonthNew(obj);

            if (result.Status)
                return Ok(result);

            return StatusCode(500, result);
        }

        [HttpPost("upload-recalculate-new"),Authorize]
        public async Task<IActionResult> UploadRecalculateNew([FromForm] FileDTO filedto)
        {
            var file = filedto.File;
            if (file == null || file.Length == 0)
            {
                return BadRequest(new ExecuteAndReponse
                {
                    Status = false,
                    Message = "File is required."
                });
            }

            // Validate file size (e.g., max 10MB)
            const long maxFileSize = 10 * 1024 * 1024; // 10MB
            if (file.Length > maxFileSize)
            {
                return BadRequest(new ExecuteAndReponse
                {
                    Status = false,
                    Message = $"File size exceeds maximum allowed size of {maxFileSize / (1024 * 1024)}MB."
                });
            }

            // Validate file extension
            var allowedExtensions = new[] { ".xlsx", ".xls" };
            var fileExtension = Path.GetExtension(file.FileName)?.ToLowerInvariant();
            if (string.IsNullOrEmpty(fileExtension) || !allowedExtensions.Contains(fileExtension))
            {
                return BadRequest(new ExecuteAndReponse
                {
                    Status = false,
                    Message = "Only Excel files (.xlsx, .xls) are allowed."
                });
            }

            var monthToEcodes = new Dictionary<string, HashSet<string>>();
            try
            {
                // Get current authenticated user's EmployeeId
                var identity = HttpContext.User?.Identity as ClaimsIdentity;
                var user = AuthenticUserDetails.GetCurrentUserDetails(identity);
                var employeeId = user?.EmployeeId ?? "Unknown";

                using (var stream = new MemoryStream())
                {
                    await file.CopyToAsync(stream);
                    stream.Position = 0;

                    using (var workbook = new XLWorkbook(stream))
                    {
                        var ws = workbook.Worksheets.FirstOrDefault();
                        if (ws == null)
                        {
                            return BadRequest(new ExecuteAndReponse { Status = false, Message = "No worksheet found in file." });
                        }

                        var firstRowUsed = ws.FirstRowUsed();
                        if (firstRowUsed == null)
                        {
                            return BadRequest(new ExecuteAndReponse { Status = false, Message = "Worksheet is empty." });
                        }

                        var headerRow = firstRowUsed.RowUsed();
                        var headerCells = headerRow.Cells().ToDictionary(c => c.GetString().Trim(), c => c.Address.ColumnNumber);

                        // Validate headers - must have exactly "ecode", "month" (MMM) and "year" (YY) columns
                        var expectedHeaders = new[] { "ecode", "month", "year" };
                        var foundHeaders = headerCells.Keys.Select(h => h.ToLowerInvariant().Trim()).ToList();
                        
                        var missingHeaders = expectedHeaders.Where(h => !foundHeaders.Contains(h)).ToList();
                        var extraHeaders = foundHeaders.Where(h => !expectedHeaders.Contains(h)).ToList();

                        if (missingHeaders.Any())
                        {
                            return BadRequest(new ExecuteAndReponse 
                            { 
                                Status = false, 
                                Message = $"Missing required columns: {string.Join(", ", missingHeaders)}. Found columns: {string.Join(", ", foundHeaders)}" 
                            });
                        }

                        if (extraHeaders.Any())
                        {
                            return BadRequest(new ExecuteAndReponse 
                            { 
                                Status = false, 
                                Message = $"Unexpected columns found: {string.Join(", ", extraHeaders)}. Only 'ecode', 'month' and 'year' columns are allowed."
                            });
                        }

                        // Get column positions
                        int? ecodeCol = headerCells.FirstOrDefault(kv => kv.Key.Equals("ecode", System.StringComparison.OrdinalIgnoreCase)).Value;
                        int? monthCol = headerCells.FirstOrDefault(kv => kv.Key.Equals("month", System.StringComparison.OrdinalIgnoreCase)).Value;
                        int? yearCol = headerCells.FirstOrDefault(kv => kv.Key.Equals("year", System.StringComparison.OrdinalIgnoreCase)).Value;

                        var currentRow = headerRow.RowBelow();
                        var rowCount = 0;
                        while (!currentRow.IsEmpty())
                        {
                            rowCount++;
                            var ecode = currentRow.Cell(ecodeCol.Value).GetString().Trim();
                            var monthPart = currentRow.Cell(monthCol.Value).GetString().Trim();
                            var yearPart = currentRow.Cell(yearCol.Value).GetString().Trim();

                            if (!string.IsNullOrWhiteSpace(ecode) && !string.IsNullOrWhiteSpace(monthPart) && !string.IsNullOrWhiteSpace(yearPart))
                            {
                                // Validate monthPart is a valid 3-letter month name
                                if (!Regex.IsMatch(monthPart, @"^[A-Za-z]{3}$",RegexOptions.IgnoreCase))
                                {
                                    return BadRequest(new ExecuteAndReponse { Status = false, Message = $"Invalid Month value '{monthPart}' at row {currentRow.RowNumber()}. Expected MMM (e.g., Jul)." });
                                }

                                // Normalize MMM to proper case using CultureInfo
                                string normalizedMMM;
                                try
                                {
                                    var parsedTemp = DateTime.ParseExact(monthPart, "MMM", CultureInfo.InvariantCulture, DateTimeStyles.None);
                                    normalizedMMM = parsedTemp.ToString("MMM", CultureInfo.InvariantCulture);
                                }
                                catch
                                {
                                    return BadRequest(new ExecuteAndReponse { Status = false, Message = $"Invalid Month value '{monthPart}' at row {currentRow.RowNumber()}." });
                                }

                                // Validate yearPart is YY (two digits)
                                if (!Regex.IsMatch(yearPart, @"^\d{2}$",RegexOptions.IgnoreCase))
                                {
                                    return BadRequest(new ExecuteAndReponse { Status = false, Message = $"Invalid Year value '{yearPart}' at row {currentRow.RowNumber()}. Expected YY (e.g., 25)." });
                                }

                                var combinedMonth = $"{normalizedMMM}-{yearPart}"; // MMM-YY

                                if (!monthToEcodes.TryGetValue(combinedMonth, out var set))
                                {
                                    set = new HashSet<string>();
                                    monthToEcodes[combinedMonth] = set;
                                }
                                set.Add(ecode);
                            }

                            currentRow = currentRow.RowBelow();
                        }

                        // Validate minimum data rows
                        if (rowCount == 0)
                        {
                            return BadRequest(new ExecuteAndReponse { Status = false, Message = "No data rows found. Excel must contain at least one row with ecode, month (MMM) and year (YY)." });
                        }
                    }
                }

                if (monthToEcodes.Count == 0)
                {
                    return BadRequest(new ExecuteAndReponse { Status = false, Message = "No valid rows found. Ensure columns 'ecode', 'month' (MMM) and 'year' (YY)." });
                }

                if (monthToEcodes.Count > 1)
                {
                    var months = string.Join(", ", monthToEcodes.Keys.OrderBy(x => x));
                    return BadRequest(new ExecuteAndReponse { Status = false, Message = $"Only one Month-Year is allowed per upload. Found: {months}" });
                }

                // Save the uploaded file as proof: wwwroot/Uploader/SalaryRecalculate/YYYY/MMM/DD/EmployeeId/ExcelFileName_{timestamp}.ext
                var uniqueMonth = monthToEcodes.Keys.First();
                var parsedMonth = DateTime.ParseExact(uniqueMonth, "MMM-yy", CultureInfo.InvariantCulture);
                var yearFolder = parsedMonth.ToString("yyyy");
                var monthFolder = parsedMonth.ToString("MMM", CultureInfo.InvariantCulture);
                var dayFolder = DateTime.Now.ToString("dd");
                var basePath = Path.Combine(_env.WebRootPath ?? "wwwroot", "Uploader", "SalaryRecalculate", yearFolder, monthFolder, dayFolder, employeeId);
                Directory.CreateDirectory(basePath);

                var ext = Path.GetExtension(file.FileName);
                if (string.IsNullOrWhiteSpace(ext)) ext = ".xlsx";
                var proofFileName = $"ExcelFileName_{DateTime.Now:ddMMyyyyHHmmssfff}{ext}";
                var savePath = Path.Combine(basePath, proofFileName);
                

                var results = new List<string>();
                foreach (var kvp in monthToEcodes)
                {
                    var dto = new SalaryRecalculateDto
                    {
                        Month = kvp.Key,
                        ECodes = string.Join(",", kvp.Value)
                    };

                    var exec = await _salaryRecalculateService.SalaryRecalculateNew(dto);
                    if (!exec.Status) {
                        return BadRequest(new ExecuteAndReponse
                        {
                            Status = true,
                            Message = string.Join(" | ", exec.Message)
                        });
                    }
                    results.Add($"{kvp.Key}: {(exec.Status ? "OK" : "FAIL")} - {exec.Message}");
                }
                //saving file 
                using (var fs = new FileStream(savePath, FileMode.Create, FileAccess.Write))
                {
                    await file.CopyToAsync(fs);
                }
                return Ok(new ExecuteAndReponse
                {
                    Status = true,
                    Message = string.Join(" | ", results)
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ExecuteAndReponse
                {
                    Status = false,
                    Message = ex.Message
                });
            }
        }
    }
}
