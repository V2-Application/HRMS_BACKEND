using ClosedXML.Excel;
using HRMSAPI.Data;
using HRMSAPI.DTO;
using HRMSAPI.Interfaces;
using HRMSAPI.Utility;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Roomsy.DTOS.GenericsResponses;
using System.Data;

namespace HRMSAPI.Implementation
{
    public class AttendanceRegularizationService : IAttendanceRegularizationService
    {
        private readonly HRMSContext _context;
        private readonly ILogger<AttendanceRegularizationService> _logger;

        public AttendanceRegularizationService(HRMSContext context, ILogger<AttendanceRegularizationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<FetchAndResponse> GetAttendanceRegularizationAsync(string monthYear, bool asExcel = false)
        {
            try
            {
                // Validate monthYear format (MMM-YY)
                if (string.IsNullOrWhiteSpace(monthYear))
                {
                    return new FetchAndResponse
                    {
                        Status = false,
                        Message = "MonthYear parameter is required",
                        Code = System.Net.HttpStatusCode.BadRequest,
                        Data = null
                    };
                }

                // Validate format: MMM-YY (e.g., Nov-25)
                if (!System.Text.RegularExpressions.Regex.IsMatch(monthYear, @"^(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)-\d{2}$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    return new FetchAndResponse
                    {
                        Status = false,
                        Message = "MonthYear must be in format MMM-YY (e.g., Nov-25)",
                        Code = System.Net.HttpStatusCode.BadRequest,
                        Data = null
                    };
                }

                var data = await GetAttendanceRegularizationDataAsync(monthYear);

                if (asExcel)
                {
                    var excelBytes = await GenerateExcelAsync(data, monthYear);
                    return new FetchAndResponse
                    {
                        Status = true,
                        Message = "Attendance regularization data exported successfully",
                        Code = System.Net.HttpStatusCode.OK,
                        Data = excelBytes
                    };
                }

                return new FetchAndResponse
                {
                    Status = true,
                    Message = "Attendance regularization data retrieved successfully",
                    Code = System.Net.HttpStatusCode.OK,
                    Data = data
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving attendance regularization data for MonthYear: {MonthYear}", monthYear);
                return new FetchAndResponse
                {
                    Status = false,
                    Message = $"An error occurred while retrieving attendance regularization data: {ex.Message}",
                    Code = System.Net.HttpStatusCode.InternalServerError,
                    Data = null
                };
            }
        }

        private async Task<List<AttendanceRegularizationResultDto>> GetAttendanceRegularizationDataAsync(string monthYear)
        {
            var results = new List<AttendanceRegularizationResultDto>();

            try
            {
                using (var connection = _context.Database.GetDbConnection())
                {
                    await connection.OpenAsync();

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "usp_GetAttendanceRegularization";
                        command.CommandType = CommandType.StoredProcedure;

                        // Add parameter
                        var monthYearParam = new SqlParameter("@MonthYear", SqlDbType.VarChar, 10)
                        {
                            Value = monthYear
                        };
                        command.Parameters.Add(monthYearParam);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                results.Add(new AttendanceRegularizationResultDto
                                {
                                    Ecode = reader.IsDBNull(reader.GetOrdinal("Ecode")) ? null : reader.GetString("Ecode"),
                                    EmpName = reader.IsDBNull(reader.GetOrdinal("EmpName")) ? null : reader.GetString("EmpName"),
                                    STCode = reader.IsDBNull(reader.GetOrdinal("STCode")) ? null : reader.GetString("STCode"),
                                    LocationName = reader.IsDBNull(reader.GetOrdinal("LocationName")) ? null : reader.GetString("LocationName"),
                                    DepartmentName = reader.IsDBNull(reader.GetOrdinal("DepartmentName")) ? null : reader.GetString("DepartmentName"),
                                    DesignationName = reader.IsDBNull(reader.GetOrdinal("DesignationName")) ? null : reader.GetString("DesignationName"),
                                    RequestDate = reader.IsDBNull(reader.GetOrdinal("RequestDate")) ? DateTime.MinValue : reader.GetDateTime("RequestDate"),
                                    Reason = reader.IsDBNull(reader.GetOrdinal("Reason")) ? null : reader.GetString("Reason"),
                                    RM_ECODE = reader.IsDBNull(reader.GetOrdinal("RM_ECODE")) ? null : reader.GetString("RM_ECODE"),
                                    ReportManagerName = reader.IsDBNull(reader.GetOrdinal("ReportManagerName")) ? null : reader.GetString("ReportManagerName"),
                                    PunchIn = reader.GetNullableTimeSpan("PunchIn"),
                                    PunchOut = reader.GetNullableTimeSpan("PunchOut"),
                                    StatusName = reader.IsDBNull(reader.GetOrdinal("StatusName")) ? null : reader.GetString("StatusName"),
                                    FileUrl = reader.IsDBNull(reader.GetOrdinal("FileUrl")) ? null : reader.GetString("FileUrl"),
                                    PunchTypeId = reader.IsDBNull(reader.GetOrdinal("PunchTypeId")) ? null : reader.GetInt32("PunchTypeId"),
                                    RequestTypeName = reader.IsDBNull(reader.GetOrdinal("RequestTypeName")) ? null : reader.GetString("RequestTypeName"),
                                    EmployeeRemarks = reader.IsDBNull(reader.GetOrdinal("EmployeeRemarks")) ? null : reader.GetString("EmployeeRemarks"),
                                    ManagerStatus = reader.IsDBNull(reader.GetOrdinal("ManagerStatus")) ? null : reader.GetString("ManagerStatus"),
                                    ManagerApprovalOn = reader.IsDBNull(reader.GetOrdinal("ManagerApprovalOn")) ? null : (DateTime?)reader.GetDateTime("ManagerApprovalOn"),
                                    ManagerRemarks = reader.IsDBNull(reader.GetOrdinal("ManagerRemarks")) ? null : reader.GetString("ManagerRemarks"),
                                    LpApprovalStatus = reader.IsDBNull(reader.GetOrdinal("LpApprovalStatus")) ? null : reader.GetString("LpApprovalStatus"),
                                    LpApprovalOn = reader.IsDBNull(reader.GetOrdinal("LpApprovalOn")) ? null : (DateTime?)reader.GetDateTime("LpApprovalOn"),
                                    LpRemarks = reader.IsDBNull(reader.GetOrdinal("LpRemarks")) ? null : reader.GetString("LpRemarks")
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing stored procedure usp_GetAttendanceRegularization for MonthYear: {MonthYear}", monthYear);
                throw;
            }

            return results;
        }

        private async Task<byte[]> GenerateExcelAsync(List<AttendanceRegularizationResultDto> data, string monthYear)
        {
            try
            {
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("AttendanceRegularization");

                // Add headers
                var headers = new[]
                {
                    "Ecode", "Employee Name", "ST Code", "Location Name", "Department Name", "Designation Name",
                    "Request Date", "Reason", "RM Ecode", "Report Manager Name",
                    "Punch In", "Punch Out", "Status", "File Url", "Punch Type Id", "Request Type Name",
                    "Employee Remarks", "Manager Status", "Manager Approval On", "Manager Remarks",
                    "LP Approval Status", "LP Approval On", "LP Remarks"
                };

                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(1, i + 1).Value = headers[i];
                    worksheet.Cell(1, i + 1).Style.Font.Bold = true;
                    worksheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
                }

                // Add data
                for (int i = 0; i < data.Count; i++)
                {
                    var item = data[i];
                    int row = i + 2;

                    worksheet.Cell(row, 1).Value = item.Ecode ?? "";
                    worksheet.Cell(row, 2).Value = item.EmpName ?? "";
                    worksheet.Cell(row, 3).Value = item.STCode ?? "";
                    worksheet.Cell(row, 4).Value = item.LocationName ?? "";
                    worksheet.Cell(row, 5).Value = item.DepartmentName ?? "";
                    worksheet.Cell(row, 6).Value = item.DesignationName ?? "";
                    worksheet.Cell(row, 7).Value = item.RequestDate != DateTime.MinValue ? item.RequestDate.ToString("yyyy-MM-dd") : "";
                    worksheet.Cell(row, 8).Value = item.Reason ?? "";
                    worksheet.Cell(row, 9).Value = item.RM_ECODE ?? "";
                    worksheet.Cell(row, 10).Value = item.ReportManagerName ?? "";
                    worksheet.Cell(row, 11).Value = item.PunchIn.HasValue ? item.PunchIn.Value.ToString(@"hh\:mm\:ss") : "";
                    worksheet.Cell(row, 12).Value = item.PunchOut.HasValue ? item.PunchOut.Value.ToString(@"hh\:mm\:ss") : "";
                    worksheet.Cell(row, 13).Value = item.StatusName ?? "";
                    worksheet.Cell(row, 14).Value = item.FileUrl ?? "";
                    worksheet.Cell(row, 15).Value = item.PunchTypeId?.ToString() ?? "";
                    worksheet.Cell(row, 16).Value = item.RequestTypeName ?? "";
                    worksheet.Cell(row, 17).Value = item.EmployeeRemarks ?? "";
                    worksheet.Cell(row, 18).Value = item.ManagerStatus ?? "";
                    worksheet.Cell(row, 19).Value = item.ManagerApprovalOn.HasValue ? item.ManagerApprovalOn.Value.ToString("yyyy-MM-dd HH:mm:ss") : "";
                    worksheet.Cell(row, 20).Value = item.ManagerRemarks ?? "";
                    worksheet.Cell(row, 21).Value = item.LpApprovalStatus ?? "";
                    worksheet.Cell(row, 22).Value = item.LpApprovalOn.HasValue ? item.LpApprovalOn.Value.ToString("yyyy-MM-dd HH:mm:ss") : "";
                    worksheet.Cell(row, 23).Value = item.LpRemarks ?? "";
                }

                // Auto-fit columns for better readability
                worksheet.ColumnsUsed().AdjustToContents();

                // Save workbook to memory stream
                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                stream.Position = 0;

                return stream.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating Excel file for MonthYear: {MonthYear}", monthYear);
                throw;
            }
        }
    }
}

