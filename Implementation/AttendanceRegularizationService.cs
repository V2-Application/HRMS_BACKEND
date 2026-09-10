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

        /// <summary>
        /// Column names present in the current result set. The HR-layer columns only
        /// exist once Add_RegularizeHrApprovalLayer_20260910.sql has been applied, so
        /// the export reads them defensively instead of throwing on a database where
        /// the procs have not been updated yet.
        /// </summary>
        private static HashSet<string> GetColumnNames(System.Data.Common.DbDataReader reader)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < reader.FieldCount; i++)
            {
                names.Add(reader.GetName(i));
            }
            return names;
        }

        private static string? ReadString(System.Data.Common.DbDataReader reader, HashSet<string> cols, string name)
        {
            if (!cols.Contains(name)) return null;
            var ord = reader.GetOrdinal(name);
            return reader.IsDBNull(ord) ? null : reader.GetString(ord);
        }

        private static DateTime? ReadDateTime(System.Data.Common.DbDataReader reader, HashSet<string> cols, string name)
        {
            if (!cols.Contains(name)) return null;
            var ord = reader.GetOrdinal(name);
            return reader.IsDBNull(ord) ? null : reader.GetDateTime(ord);
        }

        /// <summary>
        /// One export row. Shared by the month export and the date-range export so
        /// the two reports can never drift apart.
        /// </summary>
        private static AttendanceRegularizationResultDto MapRegularizationRow(
            System.Data.Common.DbDataReader reader, HashSet<string> cols)
        {
            return new AttendanceRegularizationResultDto
            {
                Ecode = ReadString(reader, cols, "Ecode"),
                EmpName = ReadString(reader, cols, "EmpName"),
                STCode = ReadString(reader, cols, "STCode"),
                LocationName = ReadString(reader, cols, "LocationName"),
                DepartmentName = ReadString(reader, cols, "DepartmentName"),
                DesignationName = ReadString(reader, cols, "DesignationName"),
                RequestDate = ReadDateTime(reader, cols, "RequestDate") ?? DateTime.MinValue,
                Reason = ReadString(reader, cols, "Reason"),
                RM_ECODE = ReadString(reader, cols, "RM_ECODE"),
                ReportManagerName = ReadString(reader, cols, "ReportManagerName"),
                PunchIn = reader.GetNullableTimeSpan("PunchIn"),
                PunchOut = reader.GetNullableTimeSpan("PunchOut"),
                StatusName = ReadString(reader, cols, "StatusName"),
                FileUrl = ReadString(reader, cols, "FileUrl"),
                PunchTypeId = cols.Contains("PunchTypeId") && !reader.IsDBNull(reader.GetOrdinal("PunchTypeId"))
                    ? reader.GetInt32(reader.GetOrdinal("PunchTypeId"))
                    : null,
                RequestTypeName = ReadString(reader, cols, "RequestTypeName"),
                EmployeeRemarks = ReadString(reader, cols, "EmployeeRemarks"),

                ManagerStatus = ReadString(reader, cols, "ManagerStatus"),
                ManagerApprovalOn = ReadDateTime(reader, cols, "ManagerApprovalOn"),
                ManagerRemarks = ReadString(reader, cols, "ManagerRemarks"),
                ManagerApproverEcode = ReadString(reader, cols, "ManagerApproverEcode"),
                ManagerApproverName = ReadString(reader, cols, "ManagerApproverName"),

                LpApprovalStatus = ReadString(reader, cols, "LpApprovalStatus"),
                LpApprovalOn = ReadDateTime(reader, cols, "LpApprovalOn"),
                LpRemarks = ReadString(reader, cols, "LpRemarks"),
                LpApproverEcode = ReadString(reader, cols, "LpApproverEcode"),
                LpApproverName = ReadString(reader, cols, "LpApproverName"),

                HrApprovalStatus = ReadString(reader, cols, "HrApprovalStatus"),
                HrApprovalOn = ReadDateTime(reader, cols, "HrApprovalOn"),
                HrRemarks = ReadString(reader, cols, "HrRemarks"),
                HrApproverEcode = ReadString(reader, cols, "HrApproverEcode"),
                HrApproverName = ReadString(reader, cols, "HrApproverName")
            };
        }

        public async Task<FetchAndResponse> ExportAttendanceRegularizationByRangeAsync(
            DateTime startDate,
            DateTime endDate,
            string? status,
            string? managerStatus,
            string? lpStatus,
            string? hrStatus = null)
        {
            try
            {
                if (startDate == DateTime.MinValue || endDate == DateTime.MinValue)
                {
                    return new FetchAndResponse
                    {
                        Status = false,
                        Message = "StartDate and EndDate are required.",
                        Code = System.Net.HttpStatusCode.BadRequest,
                        Data = null
                    };
                }

                if (endDate < startDate)
                {
                    return new FetchAndResponse
                    {
                        Status = false,
                        Message = "EndDate must be greater than or equal to StartDate.",
                        Code = System.Net.HttpStatusCode.BadRequest,
                        Data = null
                    };
                }

                var data = await GetAttendanceRegularizationByRangeDataAsync(startDate, endDate, status, managerStatus, lpStatus, hrStatus);
                var label = $"{startDate:yyyyMMdd}_{endDate:yyyyMMdd}";
                var excelBytes = await GenerateExcelAsync(data, label);

                return new FetchAndResponse
                {
                    Status = true,
                    Message = "Attendance regularization data exported successfully",
                    Code = System.Net.HttpStatusCode.OK,
                    Data = excelBytes
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting attendance regularization data for range {StartDate} - {EndDate}", startDate, endDate);
                return new FetchAndResponse
                {
                    Status = false,
                    Message = $"An error occurred while exporting attendance regularization data: {ex.Message}",
                    Code = System.Net.HttpStatusCode.InternalServerError,
                    Data = null
                };
            }
        }

        private async Task<List<AttendanceRegularizationResultDto>> GetAttendanceRegularizationByRangeDataAsync(
            DateTime startDate,
            DateTime endDate,
            string? status,
            string? managerStatus,
            string? lpStatus,
            string? hrStatus)
        {
            var results = new List<AttendanceRegularizationResultDto>();

            using var connection = _context.Database.GetDbConnection();
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = "usp_GetAttendanceRegularizationByRange";
            command.CommandType = CommandType.StoredProcedure;

            command.Parameters.Add(new SqlParameter("@StartDate", SqlDbType.Date) { Value = startDate.Date });
            command.Parameters.Add(new SqlParameter("@EndDate", SqlDbType.Date) { Value = endDate.Date });
            command.Parameters.Add(new SqlParameter("@Status", SqlDbType.VarChar, 50)
            {
                Value = string.IsNullOrWhiteSpace(status) ? (object)DBNull.Value : status
            });
            command.Parameters.Add(new SqlParameter("@ManagerStatus", SqlDbType.VarChar, 50)
            {
                Value = string.IsNullOrWhiteSpace(managerStatus) ? (object)DBNull.Value : managerStatus
            });
            command.Parameters.Add(new SqlParameter("@LpStatus", SqlDbType.VarChar, 50)
            {
                Value = string.IsNullOrWhiteSpace(lpStatus) ? (object)DBNull.Value : lpStatus
            });
            command.Parameters.Add(new SqlParameter("@HrStatus", SqlDbType.VarChar, 50)
            {
                Value = string.IsNullOrWhiteSpace(hrStatus) ? (object)DBNull.Value : hrStatus
            });

            using var reader = await command.ExecuteReaderAsync();
            var cols = GetColumnNames(reader);
            while (await reader.ReadAsync())
            {
                results.Add(MapRegularizationRow(reader, cols));
            }

            return results;
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
                            var cols = GetColumnNames(reader);
                            while (await reader.ReadAsync())
                            {
                                results.Add(MapRegularizationRow(reader, cols));
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
                // "Status" is the FINAL status, which now only turns Approved once the
                // HR layer has approved. The three per-layer blocks below show how a
                // request got there, and who signed off at each layer.
                var headers = new[]
                {
                    "Ecode", "Employee Name", "ST Code", "Location Name", "Department Name", "Designation Name",
                    "Request Date", "Reason", "RM Ecode", "Report Manager Name",
                    "Punch In", "Punch Out", "Final Status", "File Url", "Punch Type Id", "Request Type Name",
                    "Employee Remarks",
                    "Manager Status", "Manager Approval On", "Manager Remarks", "Manager Approver Ecode", "Manager Approver Name",
                    "LP Approval Status", "LP Approval On", "LP Remarks", "LP Approver Ecode", "LP Approver Name",
                    "HR Approval Status", "HR Approval On", "HR Remarks", "HR Approver Ecode", "HR Approver Name"
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
                    worksheet.Cell(row, 21).Value = item.ManagerApproverEcode ?? "";
                    worksheet.Cell(row, 22).Value = item.ManagerApproverName ?? "";
                    worksheet.Cell(row, 23).Value = item.LpApprovalStatus ?? "";
                    worksheet.Cell(row, 24).Value = item.LpApprovalOn.HasValue ? item.LpApprovalOn.Value.ToString("yyyy-MM-dd HH:mm:ss") : "";
                    worksheet.Cell(row, 25).Value = item.LpRemarks ?? "";
                    worksheet.Cell(row, 26).Value = item.LpApproverEcode ?? "";
                    worksheet.Cell(row, 27).Value = item.LpApproverName ?? "";
                    // A blank HR status means HR has not acted on the request yet.
                    worksheet.Cell(row, 28).Value = item.HrApprovalStatus ?? "Pending";
                    worksheet.Cell(row, 29).Value = item.HrApprovalOn.HasValue ? item.HrApprovalOn.Value.ToString("yyyy-MM-dd HH:mm:ss") : "";
                    worksheet.Cell(row, 30).Value = item.HrRemarks ?? "";
                    worksheet.Cell(row, 31).Value = item.HrApproverEcode ?? "";
                    worksheet.Cell(row, 32).Value = item.HrApproverName ?? "";
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

