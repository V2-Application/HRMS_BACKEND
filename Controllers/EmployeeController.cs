using ClosedXML.Excel;
using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.Wordprocessing;
using HRMSAPI.Data;
using HRMSAPI.DTO;
using HRMSAPI.Extension;
using HRMSAPI.Implementation;
using HRMSAPI.Interfaces;
using MailKit.Search;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Roomsy.DTOS.GenericsResponses;
using System.Data;
using System.Security.Claims;

namespace HRMSAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    // NOTE: controller-level RequirePageAccess removed — endpoints like
    // GetEmployee/{id} and SearchEmployee are called by MyProfile, dropdowns,
    // and many other non-/employees/list pages. Apply per-method gates to
    // admin-only actions (list/bulk/upload) when needed.
    public class EmployeeController : ControllerBase
    {
        public readonly HRMSContext _context;
        private readonly string savePath = Path.Combine("wwwroot");
        private readonly IEmployeeService _uow;
        private readonly ILogger<EmployeeController> _logger;


        public EmployeeController(HRMSContext context, IEmployeeService uow, ILogger<EmployeeController> logger)
        {
            _uow = uow;
            _context = context;
            _logger = logger;
        }

        [HttpGet("GetEmployee"), Authorize]
        public async Task<IActionResult> Get(int pageNumber = 1, int pageSize = 10, string searchTerm = "")
        {
            var identity = HttpContext.User.Identity as ClaimsIdentity;
            if (identity == null)
                return BadRequest("Authentication Fails");
            else
            {
                var (employees, totalCount, currentPageNumber) = await _uow.EmployeeList(pageNumber, pageSize, searchTerm);
                return Ok(new
                {
                    Employees = employees,
                    TotalCount = totalCount,
                    CurrentPageNumber = currentPageNumber
                });
            }
        }
        [HttpGet("SearchEmployee")]
        public async Task<IActionResult> SearchEmployee([FromQuery] string searchTerm = "", [FromQuery] string? email = null, [FromQuery(Name = "designation")] string? designationName = null)
        {
            //var identity = HttpContext.User.Identity as ClaimsIdentity;
            //if (identity == null)
            //    return BadRequest("Authentication Fails");

            //else
            //{
                // Call the service to get the search results without pagination
                var employees = await _uow.EmployeeSearchList(searchTerm, email, designationName);
                return Ok(new { Employees = employees.Employees });
            //}
        }
        // Read-only enrichment: pulls each employee's latest approved-resignation
        // row (LastUpdatedBy = the approver's ecode at the time of action), the
        // approver's full name via a self-join on tblEmployee, and LastUpdatedOn
        // (the timestamp the approve action ran). No writes.
        private async Task<Dictionary<string, (string? Date, string? Ecode, string? Name, string? ApprovedOn)>> GetResignationApproverMap(System.Data.Common.DbConnection conn)
        {
            var dict = new Dictionary<string, (string?, string?, string?, string?)>(StringComparer.OrdinalIgnoreCase);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
WITH latest AS (
    SELECT s.EmployeeId, s.ResignationDate, s.LastUpdatedBy, s.LastUpdatedOn,
           ROW_NUMBER() OVER (PARTITION BY s.EmployeeId ORDER BY s.EmployeeSeprationId DESC) AS rn
    FROM tblEmployeeSepration s
    WHERE (s.IsApprovedByManager = 1 OR s.IsApprovedByHR = 1)
)
SELECT e.Ecode,
       CONVERT(varchar(10), l.ResignationDate, 23)                                              AS ResignationDate,
       l.LastUpdatedBy                                                                          AS ApproverEcode,
       LTRIM(RTRIM(ISNULL(a.FirstName, '') + ' ' + ISNULL(a.MiddleName, '') + ' ' + ISNULL(a.LastName, ''))) AS ApproverName,
       CONVERT(varchar(19), l.LastUpdatedOn, 120)                                                AS ApprovedOn
FROM latest l
JOIN tblEmployee e ON e.EmployeeId = l.EmployeeId
LEFT JOIN tblEmployee a ON a.Ecode = l.LastUpdatedBy
WHERE l.rn = 1";

            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                var ec = rdr.IsDBNull(0) ? null : rdr.GetString(0);
                if (string.IsNullOrEmpty(ec)) continue;
                var dt = rdr.IsDBNull(1) ? null : rdr.GetString(1);
                var apEc = rdr.IsDBNull(2) ? null : rdr.GetString(2);
                var apNm = rdr.IsDBNull(3) ? null : rdr.GetString(3);
                var apOn = rdr.IsDBNull(4) ? null : rdr.GetString(4);
                if (!string.IsNullOrWhiteSpace(apNm) && apNm.Replace(" ", "").Length == 0) apNm = null;
                dict[ec] = (dt, apEc, apNm, apOn);
            }
            return dict;
        }

        [HttpGet("download-excel")]
        public async Task<IActionResult> DownloadEmployeeExcel(bool isActive = true, bool allEmployee = false, int companyId = 0)
        {
            try
            {
                using (var connection = _context.Database.GetDbConnection())
                {
                    await connection.OpenAsync();
                    using (var command = connection.CreateCommand())
                    {
                        //command.CommandText = "GetEmployeeDetailsforexcel";
                        command.CommandText = "GetEmployeeDetailsforexcel_Ishu";
                        command.CommandType = CommandType.StoredProcedure;
                        command.CommandTimeout = 600; // 10 min — full-tenant pulls can take minutes

                        // Add parameters
                        command.Parameters.Add(new SqlParameter("@IsActive", SqlDbType.Bit) { Value = isActive });
                        command.Parameters.Add(new SqlParameter("@AllEmployee", SqlDbType.Bit) { Value = allEmployee });
                        command.Parameters.Add(new SqlParameter("@CompanyId", SqlDbType.Int) { Value = companyId });

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            // Create a DataTable to hold the results
                            var dataTable = new DataTable();
                            dataTable.Load(reader);

                            // Enrich with resignation approver columns (read-only side query).
                            // Adds: "Resignation Approved By Ecode" + "Resignation Approved By Name".
                            // Existing "Resignation Date" column (returned by the SP) is left untouched.
                            reader.Close();
                            var approverMap = await GetResignationApproverMap(connection);
                            var ecodeColumn = dataTable.Columns.Contains("Employee Code") ? "Employee Code"
                                              : dataTable.Columns.Contains("EmployeeCode") ? "EmployeeCode"
                                              : dataTable.Columns.Contains("Ecode") ? "Ecode" : null;
                            if (ecodeColumn != null)
                            {
                                if (!dataTable.Columns.Contains("Resignation Approved By Ecode"))
                                    dataTable.Columns.Add("Resignation Approved By Ecode", typeof(string));
                                if (!dataTable.Columns.Contains("Resignation Approved By Name"))
                                    dataTable.Columns.Add("Resignation Approved By Name", typeof(string));
                                if (!dataTable.Columns.Contains("Resignation Approved On"))
                                    dataTable.Columns.Add("Resignation Approved On", typeof(string));
                                foreach (DataRow row in dataTable.Rows)
                                {
                                    var ec = row[ecodeColumn]?.ToString();
                                    if (!string.IsNullOrEmpty(ec) && approverMap.TryGetValue(ec, out var info))
                                    {
                                        row["Resignation Approved By Ecode"] = info.Ecode ?? (object)DBNull.Value;
                                        row["Resignation Approved By Name"] = info.Name ?? (object)DBNull.Value;
                                        row["Resignation Approved On"] = info.ApprovedOn ?? (object)DBNull.Value;
                                    }
                                }
                            }

                            // Create Excel workbook and worksheet
                            using (var workbook = new XLWorkbook())
                            {
                                var worksheet = workbook.Worksheets.Add("EmployeeDetails");
                                // Set the entire column F to short date format
                                worksheet.Column(6).Style.DateFormat.Format = "dd-mm-yyyy"; // F is column 6
                                                                                            // Add headers with formatting
                                for (int i = 0; i < dataTable.Columns.Count; i++)
                                {
                                    worksheet.Cell(1, i + 1).Value = dataTable.Columns[i].ColumnName;
                                    worksheet.Cell(1, i + 1).Style.Font.Bold = true;
                                    worksheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
                                    worksheet.Cell(1, i + 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                                }

                                // Add data
                                for (int i = 0; i < dataTable.Rows.Count; i++)
                                {
                                    for (int j = 0; j < dataTable.Columns.Count; j++)
                                    {
                                        var cellValue = dataTable.Rows[i][j]?.ToString();
                                        worksheet.Cell(i + 2, j + 1).Value = cellValue;

                                        // Apply specific formatting for date columns
                                        if (dataTable.Columns[j].ColumnName is "DateOfBirth" or "DOJ" or "FamilyMemberDOB"
                                            or "PreviousCompanyFrom" or "PreviousCompanyTo" or "DateOfResignation" or "DateOfLeft")
                                        {
                                            if (DateTime.TryParse(cellValue, out _))
                                            {
                                                worksheet.Cell(i + 2, j + 1).Style.DateFormat.Format = "dd-mm-yyyy";
                                            }
                                        }
                                        // Apply number formatting for salary columns
                                        else if (dataTable.Columns[j].ColumnName is "InHandSalary" or "LastCTCAnnual")
                                        {
                                            if (decimal.TryParse(cellValue, out _))
                                            {
                                                worksheet.Cell(i + 2, j + 1).Style.NumberFormat.Format = "#,##0.00";
                                            }
                                        }
                                    }
                                }

                                // Auto-fit columns and freeze header row
                                worksheet.Columns().AdjustToContents();
                                worksheet.SheetView.FreezeRows(1);

                                // Save to memory stream
                                using (var stream = new MemoryStream())
                                {
                                    workbook.SaveAs(stream);
                                    var content = stream.ToArray();
                                    return File(
                                        content,
                                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                                        $"EmployeeDetails_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
                                    );
                                }
                            }
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                return StatusCode(500, $"Database error: {ex.Message}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }
        [HttpGet("{id}")]
        public async Task<IActionResult> GetEmployeeById(long id)
        {
            var (success, employee, message) = await _uow.GetEmployeeByIdAsync(id);
            if (success)
            {
                return Ok(new { success = true, data = employee, message });
            }
            return NotFound(new { success = false, message });
        }

        [HttpPost("upsert")]
        public async Task<IActionResult> UpsertEmployee([FromForm] DCEmployeeDto employee, [FromForm] EmployeeDocs files)
        {
            var (success, message) = await _uow.UpsertEmployeeAsync(employee, files);
            return success ? Ok(new { success = true, message }) : BadRequest(new { success = false, message });
        }

        [HttpPost("statusofemployee")]
        public async Task<IActionResult> EmployeeStatus(long id, [FromQuery] string LastUpdatedBy, bool isactive = false)
        {
            var emp = await _context.tblEmployees.FindAsync(id);
            if (emp == null)
                return NotFound();

            emp.IsActive = isactive;
            emp.IsDeleted = !isactive;
            emp.LastUpdatedBy = string.IsNullOrWhiteSpace(LastUpdatedBy) ? "System" : LastUpdatedBy;
            emp.DeletedOn = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        [HttpGet("GetSalaryDetailsByEcode_Web")]
        public async Task<IActionResult> GetSalaryDetailsByEcode(string ecode, string month)
        {
            try
            {

                var result = await _uow.GetSalaryDetailsByEcode(ecode, month);

                if (result == null)
                    return NotFound(new { message = $"Data not found." });

                return Ok(result);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Database error occurred.", error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An unexpected error occurred.", error = ex.Message });
            }
        }

        [HttpGet("GetAllSalarySlipsDetail")]
        public async Task<IActionResult> GetAllSalarySlips([FromQuery] string month, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10, [FromQuery] string? searchTerm = "")
        {
            if (string.IsNullOrWhiteSpace(month))
            {
                return BadRequest("Month is required.");
            }

            try
            {
                var result = await _uow.GetAllSalarySlipsDetails(month, pageNumber, pageSize, searchTerm);
                return Ok(result);
            }
            catch (Exception ex)
            {

                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("GetEmployee_HoldList")]
        public async Task<IActionResult> GetEmployee_HoldList(int pageNumber = 1, int pageSize = 10, string searchTerm = "")
        {
            _logger.LogInformation("Fetching employee list with pageNumber: {PageNumber}, pageSize: {PageSize}, searchTerm: {SearchTerm}", pageNumber, pageSize, searchTerm);
            try
            {
                var identity = HttpContext.User.Identity as ClaimsIdentity;
                if (identity == null)
                {
                    _logger.LogWarning("Authentication failed: No identity provided");
                    return BadRequest("Authentication Fails");
                }

                var (employees, totalCount, currentPageNumber) = await _uow.GetEmployee_HoldList(pageNumber, pageSize, searchTerm);
                _logger.LogInformation("Employee list fetched successfully. TotalCount: {TotalCount}, CurrentPageNumber: {CurrentPageNumber}", totalCount, currentPageNumber);

                return Ok(new
                {
                    Employees = employees,
                    TotalCount = totalCount,
                    CurrentPageNumber = currentPageNumber
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching employee list");
                return StatusCode(500, new { Status = false, Message = ex.Message });
            }
        }

        [HttpPost("SendOfferLetters")]
        public async Task<IActionResult> SendOfferLetters([FromQuery] string employeeIds)
        {
            try
            {
                await _uow.SendOfferLetters(employeeIds);
                return Ok("Offer letter sending process completed.");
            }
            catch (Exception ex)
            {

                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
           
        }




        [HttpGet("employee-data")]
        public async Task<IActionResult> GetEmployeeData(bool isActive = true, bool allEmployee = false, int companyId = 0)
        {
            try
            {
                using (var connection = _context.Database.GetDbConnection())
                {
                    await connection.OpenAsync();
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "GetEmployeeDetailsforexcel_Ishu";
                        command.CommandType = CommandType.StoredProcedure;
                        command.CommandTimeout = 600; // 10 min — full-tenant pulls can take minutes

                        // Add parameters
                        command.Parameters.Add(new SqlParameter("@IsActive", SqlDbType.Bit) { Value = isActive });
                        command.Parameters.Add(new SqlParameter("@AllEmployee", SqlDbType.Bit) { Value = allEmployee });
                        command.Parameters.Add(new SqlParameter("@CompanyId", SqlDbType.Int) { Value = companyId });

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            var employees = new List<Dictionary<string, object>>();

                            while (await reader.ReadAsync())
                            {
                                var employee = new Dictionary<string, object>();

                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    var columnName = reader.GetName(i);
                                    var value = reader.GetValue(i);

                                    // Handle null values
                                    if (value == DBNull.Value)
                                    {
                                        employee[columnName] = null;
                                    }
                                    // Format date columns
                                    else if (columnName is "DateOfBirth" or "DOJ" or "FamilyMemberDOB"
                                            or "PreviousCompanyFrom" or "PreviousCompanyTo"
                                            or "DateOfResignation" or "DateOfLeft")
                                    {
                                        if (value is DateTime dateValue)
                                        {
                                            employee[columnName] = dateValue.ToString("dd-MM-yyyy");
                                        }
                                        else
                                        {
                                            employee[columnName] = value.ToString();
                                        }
                                    }
                                    // Format salary columns
                                    else if (columnName is "InHandSalary" or "LastCTCAnnual")
                                    {
                                        if (value is decimal decimalValue)
                                        {
                                            employee[columnName] = decimalValue;
                                        }
                                        else if (decimal.TryParse(value.ToString(), out decimal parsedDecimal))
                                        {
                                            employee[columnName] = parsedDecimal;
                                        }
                                        else
                                        {
                                            employee[columnName] = value;
                                        }
                                    }
                                    else
                                    {
                                        employee[columnName] = value;
                                    }
                                }

                                employees.Add(employee);
                            }

                            // Enrich with resignation approver fields. Read-only side
                            // query against tblEmployeeSepration + tblEmployee. Adds
                            // resignationApprovedByEcode + resignationApprovedByName per row.
                            reader.Close();
                            var approverMap = await GetResignationApproverMap(connection);
                            foreach (var emp in employees)
                            {
                                string ec = null;
                                if (emp.TryGetValue("Employee Code", out var v1) && v1 != null) ec = v1.ToString();
                                else if (emp.TryGetValue("EmployeeCode", out var v2) && v2 != null) ec = v2.ToString();
                                else if (emp.TryGetValue("Ecode", out var v3) && v3 != null) ec = v3.ToString();

                                if (!string.IsNullOrEmpty(ec) && approverMap.TryGetValue(ec, out var info))
                                {
                                    emp["resignationApprovedByEcode"] = (object)info.Ecode ?? null;
                                    emp["resignationApprovedByName"] = (object)info.Name ?? null;
                                    emp["resignationApprovedOn"] = (object)info.ApprovedOn ?? null;
                                }
                                else
                                {
                                    emp["resignationApprovedByEcode"] = null;
                                    emp["resignationApprovedByName"] = null;
                                    emp["resignationApprovedOn"] = null;
                                }
                            }

                            // Return the data with additional metadata
                            var response = new
                            {
                                Success = true,
                                Data = employees,
                                Count = employees.Count,
                                Parameters = new
                                {
                                    IsActive = isActive,
                                    AllEmployee = allEmployee,
                                    CompanyId = companyId
                                },
                                GeneratedAt = DateTime.Now
                            };

                            return Ok(response);
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "Database error occurred",
                    Error = ex.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "An error occurred while fetching employee data",
                    Error = ex.Message
                });
            }
        }

        //[HttpPost("upsertMarketingEmpChecklist")]
        //public async Task<IActionResult> upsertMarketingEmpChecklist([FromBody] MarketingEmpChecklistDto EmpDto)
        //{
        //    try
        //    {
        //        if (!ModelState.IsValid)
        //        {
        //            return BadRequest(ModelState);
        //        }

        //        var result = await _uow.upsertMarketingEmpChecklistAsync(EmpDto);

        //        return StatusCode((int)result.Code, new
        //        {
        //            Status = result.Status,
        //            Message = result.Message
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        // Console log instead of _logger (since logger not injected)
        //        Console.WriteLine($"Error in UpsertPFApprovel: {ex.Message}");

        //        return StatusCode(StatusCodes.Status500InternalServerError, new
        //        {
        //            Status = false,
        //            Message = "An error occurred while processing PF approval request"
        //        });
        //    }
        //}

        //[Authorize]
        //[HttpGet("GetEmployeeResignationChecklistMaster")]
        //public async Task<IActionResult> GetEmployeeResignationChecklistMaster()
        //{
        //    try
        //    {
        //        var employees = await _uow.GetEmployeeResignationChecklistMasterAsync();
        //        return Ok(new {Data = employees });
        //    }
        //    catch(Exception ex)
        //    {
        //        return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        //    }
        //}

        //[Authorize]
        [HttpGet("GetEmployeeResignationChecklist")]
        public async Task<IActionResult> GetEmployeeResignationChecklist(string ECode)
        {
            try
            {
                var employees = await _uow.GetEmployeeResignationChecklistByECodeAsync(ECode);
                //return Ok(new { Data = employees });
                return Ok(new
                {
                    Status = true,
                    Message = "Resignation list successfully retrieved",
                    Data = employees
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching Company");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    Status = false,
                    Message = "An error occurred while fetching departments",
                    Error = ex.Message
                });
            }
        }

        [HttpPost("SaveChecklistResponse")]
        public async Task<IActionResult> SaveChecklistResponse([FromForm] ResignationChecklistResponseListDto dto)
        {
            try
            {
                var identity = HttpContext.User.Identity as ClaimsIdentity;
                var userClaims = AuthenticUserDetails.GetCurrentUserDetails(identity);
                

                if (userClaims == null || string.IsNullOrEmpty(userClaims.EmployeeId))
                {
                    return BadRequest(new
                    {
                        Status = false,
                        Message = "Invalid user credentials."
                    });
                }
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                // Parse JSON items
                List<ResignationChecklistItemDto> items;
                try
                {
                    items = System.Text.Json.JsonSerializer.Deserialize<List<ResignationChecklistItemDto>>(dto.ItemsJson) 
                        ?? new List<ResignationChecklistItemDto>();
                }
                catch
                {
                    return BadRequest(new
                    {
                        Status = false,
                        Message = "Invalid ItemsJson format."
                    });
                }

                // Get files from form - they can be named as "Attachment", "Attachment[0]", "Attachment[1]", etc.
                // Extract index from name if present, otherwise use order
                var attachmentFiles = Request.Form.Files
                    .Where(f => f.Name.StartsWith("Attachment", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(f => 
                    {
                        // Try to extract index from name like "Attachment[0]" or "Attachment0"
                        var name = f.Name;
                        if (name.Contains('[') && name.Contains(']'))
                        {
                            var start = name.IndexOf('[') + 1;
                            var end = name.IndexOf(']');
                            if (int.TryParse(name.Substring(start, end - start), out int index))
                                return index;
                        }
                        else if (name.Length > "Attachment".Length)
                        {
                            var suffix = name.Substring("Attachment".Length);
                            if (int.TryParse(suffix, out int index))
                                return index;
                        }
                        return int.MaxValue; // Put files without index at the end
                    })
                    .ToList();

                var result = await _uow.SaveChecklistListAsync(items, attachmentFiles, userClaims.EmployeeId);

                return Ok(new
                {
                    Status = true,
                    Message = "Resignation checklist responses successfully saved",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving checklist responses");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    Status = false,
                    Message = "An error occurred while saving checklist responses",
                    Error = ex.Message
                });
            }
        }

        [HttpPost("CheckEcodeExists")]
        public async Task<IActionResult> CheckEcodeExists([FromForm] string ecode)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ecode))
                {
                    return BadRequest(new { message = "Ecode is required." });
                }

                using (var connection = _context.Database.GetDbConnection())
                {
                    await connection.OpenAsync();
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "SELECT Ecode FROM tblEmployee WHERE Ecode = @Ecode";
                        command.Parameters.Add(new SqlParameter("@Ecode", SqlDbType.NVarChar) { Value = ecode });

                        var result = await command.ExecuteScalarAsync();

                        if (result != null)
                        {
                            return Ok(new { exists = true, message = "Ecode already exists" });
                        }
                        else
                        {
                            return Ok(new { exists = false, message = "Ecode does not exist" });
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Database error occurred.", error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An unexpected error occurred.", error = ex.Message });
            }
        }
    }
}
