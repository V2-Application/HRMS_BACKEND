using ClosedXML.Excel;
using DocumentFormat.OpenXml.Drawing.Charts;
using DocumentFormat.OpenXml.InkML;
using HRMSAPI.Data;
using HRMSAPI.DTO;
using HRMSAPI.Interfaces;
using HRMSAPI.Utility;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Data.Common;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using static HRMSAPI.Enum.Enums;

namespace HRMSAPI.Implementation
{ 
    public class EmpAttendanceService : IEmpAttendanceService
    {

        private readonly IGeoService _geo;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        public readonly HRMSContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<EmpAttendanceService> _logger;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private const int PageSize = 50000;
        
        private const string ApiUrl = "http://192.168.151.36:8001/home/Attendance_Multiple_Punches";


        public EmpAttendanceService(IHttpClientFactory httpClientFactory, IConfiguration configuration, HRMSContext context, IHttpContextAccessor httpContextAccessor, ILogger<EmpAttendanceService> logger, IWebHostEnvironment webHostEnvironment)
        {
            _httpClient = httpClientFactory.CreateClient();
            _configuration = configuration;
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            _webHostEnvironment = webHostEnvironment;
        }

        public async Task FetchAndSaveAttendanceAsync()
        {
            var url = "http://192.168.151.36:8001/home/last30days_Emp_punchinout_data";
            try
            {
                // Fetch data from API
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to fetch attendance data. Status: {StatusCode}", response.StatusCode);
                    return;
                }

                var content = await response.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<List<EmpAttendanceDto>>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (data == null || !data.Any())
                {
                    _logger.LogWarning("No attendance data received from API.");
                    return;
                }

                // Log duplicates for debugging
                var duplicates = data
                    .GroupBy(x => new { x.EmpCode, x.AttendanceDate.Date })
                    .Where(g => g.Count() > 1)
                    .Select(g => new { g.Key.EmpCode, g.Key.Date, Count = g.Count() })
                    .ToList();

                if (duplicates.Any())
                {
                    _logger.LogWarning("Found {DuplicateCount} duplicate attendance records: {Duplicates}", duplicates.Count, JsonSerializer.Serialize(duplicates));
                }

                // Prepare DataTable for SqlBulkCopy
                var dt = new System.Data.DataTable();
                dt.Columns.Add("EmpCode", typeof(string));
                dt.Columns.Add("AttendanceDate", typeof(DateTime));
                dt.Columns.Add("PunchIn", typeof(TimeSpan));
                dt.Columns.Add("PunchOut", typeof(TimeSpan));
                dt.Columns.Add("CreatedBy", typeof(string));
                dt.Columns.Add("CreatedOn", typeof(DateTime));
                dt.Columns.Add("LastUpdatedBy", typeof(string));

                foreach (var record in data)
                {
                    try
                    {
                        dt.Rows.Add(
                            record.EmpCode,
                            record.AttendanceDate.Date,
                            TimeSpan.Parse(record.PunchIn),
                            TimeSpan.Parse(record.PunchOut),
                            "System",
                            DateTime.UtcNow,
                            "System"
                        );
                    }
                    catch (FormatException ex)
                    {
                        _logger.LogError("Invalid time format for EmpCode: {EmpCode}, AttendanceDate: {Date}, PunchIn: {PunchIn}, PunchOut: {PunchOut}. Error: {Message}",
                            record.EmpCode, record.AttendanceDate, record.PunchIn, record.PunchOut, ex.Message);
                        continue;
                    }
                }

                if (dt.Rows.Count == 0)
                {
                    _logger.LogWarning("No valid records to insert after processing.");
                    return;
                }

                // Insert into staging table
                var connStr = _configuration.GetConnectionString("DefaultConnection");
                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();

                // Log contents of TempEmpAttendance for debugging
                using (var checkCmd = new SqlCommand("SELECT EmpCode, AttendanceDate, COUNT(*) AS Count FROM TempEmpAttendance GROUP BY EmpCode, AttendanceDate HAVING COUNT(*) > 1", conn))
                {
                    using var reader = await checkCmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        _logger.LogWarning("Duplicate in TempEmpAttendance: EmpCode={EmpCode}, AttendanceDate={Date}, Count={Count}",
                            reader["EmpCode"], reader["AttendanceDate"], reader["Count"]);
                    }
                }

                using (var bulkCopy = new SqlBulkCopy(conn))
                {
                    bulkCopy.DestinationTableName = "dbo.TempEmpAttendance";
                    bulkCopy.WriteToServer(dt);
                }

                // Execute MERGE statement with aggregation to handle duplicates
                using var cmd = new SqlCommand(@"
            MERGE INTO tblEmpAttendance AS target
            USING (
                SELECT 
                    EmpCode, 
                    AttendanceDate, 
                    MIN(PunchIn) AS PunchIn, 
                    MAX(PunchOut) AS PunchOut, 
                    MAX(CreatedBy) AS CreatedBy, 
                    MAX(CreatedOn) AS CreatedOn, 
                    MAX(LastUpdatedBy) AS LastUpdatedBy
                FROM TempEmpAttendance
                GROUP BY EmpCode, AttendanceDate
            ) AS source
            ON target.EmpCode = source.EmpCode AND target.AttendanceDate = source.AttendanceDate
            WHEN MATCHED AND (target.PunchOut IS NULL OR target.PunchOut <> source.PunchOut) THEN
                UPDATE SET 
                    target.PunchOut = source.PunchOut,
                    target.LastUpdatedBy = source.LastUpdatedBy
            WHEN NOT MATCHED THEN
                INSERT (EmpCode, AttendanceDate, PunchIn, PunchOut, CreatedBy, CreatedOn, LastUpdatedBy)
                VALUES (source.EmpCode, source.AttendanceDate, source.PunchIn, source.PunchOut, 
                        source.CreatedBy, source.CreatedOn, source.LastUpdatedBy);

            -- Clean up staging table
            TRUNCATE TABLE TempEmpAttendance;
        ", conn);

                await cmd.ExecuteNonQueryAsync();
            }
            catch (SqlException ex) when (ex.Number == 8672) // MERGE conflict
            {
                _logger.LogError("MERGE conflict: {Message}. Check TempEmpAttendance for duplicates.", ex.Message);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError("Error in FetchAndSaveAttendanceAsync: {Message}", ex.Message);
                throw;
            }
        }
        public async Task<List<GetAttendanceProcResult>> FetchAttendance_Ishu(int month, int year, string ecode) {
            List<tbl_fn_GetMonthlyPunchesRange_productionnewnick_test> attendances = new();
            DateTime fromDate = new(year, month, 1);
            DateTime toDate = fromDate.AddMonths(1).AddDays(-1);
            var data = await _context.GetProcedures().GetAttendanceProcAsync(ecode,fromDate,toDate);
            return data;
        }

        public async Task<int> MergeMonthlyPunchesRangeAsync(DateTime fromDate, DateTime toDate, string ecode)
        {
            try
            {
                // Validate mandatory parameters
                if (fromDate == default)
                {
                    throw new ArgumentException("FromDate is required.", nameof(fromDate));
                }

                if (toDate == default)
                {
                    throw new ArgumentException("ToDate is required.", nameof(toDate));
                }

                if (fromDate > toDate)
                {
                    throw new ArgumentException("FromDate cannot be greater than ToDate.");
                }

                _logger.LogInformation("Executing usp_MergeMonthlyPunchesRange_Optimized for Ecode: {Ecode}, FromDate: {FromDate}, ToDate: {ToDate}", 
                    ecode, fromDate, toDate);

                // Call the optimized stored procedure using SqlCommand
                var connStr = _configuration.GetConnectionString("DefaultConnection");
                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();

                using var cmd = new SqlCommand("dbo.usp_MergeMonthlyPunchesRange_Optimized", conn)
                {
                    CommandType = CommandType.StoredProcedure,
                    CommandTimeout = 0
                };

                cmd.Parameters.Add(new SqlParameter("@FromDate", SqlDbType.Date) { Value = fromDate });
                cmd.Parameters.Add(new SqlParameter("@ToDate", SqlDbType.Date) { Value = toDate });
                
                if (string.IsNullOrEmpty(ecode))
                {
                    cmd.Parameters.Add(new SqlParameter("@ECode", SqlDbType.VarChar) { Value = DBNull.Value });
                }
                else
                {
                    cmd.Parameters.Add(new SqlParameter("@ECode", SqlDbType.VarChar) { Value = ecode });
                }

                var result = await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation("Successfully executed usp_MergeMonthlyPunchesRange_Optimized. Rows affected: {RowsAffected}.", result);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing usp_MergeMonthlyPunchesRange_Optimized for Ecode: {Ecode}, FromDate: {FromDate}, ToDate: {ToDate}", 
                    ecode, fromDate, toDate);
                throw new InvalidOperationException($"Failed to execute MergeMonthlyPunchesRange: {ex.Message}", ex);
            }
        }
        public async Task<List<AttendanceFetchDto>> FetchAttendance(int month, int year, string ecode)
        {
            List<AttendanceFetchDto> attendances = new();
            //List<tbl_fn_GetMonthlyPunchesRange_productionnewnick_test> attendances = new();
            DateTime fromDate = new(year, month, 1);
            DateTime toDate = fromDate.AddMonths(1).AddDays(-1);


            await using var connection = _context.Database.GetDbConnection();
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            //command.CommandText = "select * from fn_getmonthlypunchesrange_productionnewnick_test(@fromdate, @todate, @ecode)";
            command.CommandText = "select * from fn_getmonthlypunchesrange_productionnewnick_live(@fromdate, @todate, @ecode)";
            command.CommandType = CommandType.Text;

            var fromdateparam = command.CreateParameter();
            fromdateparam.ParameterName = "@fromdate";
            fromdateparam.DbType = DbType.Date;
            fromdateparam.Value = fromDate;
            command.Parameters.Add(fromdateparam);

            var todateparam = command.CreateParameter();
            todateparam.ParameterName = "@todate";
            todateparam.DbType = DbType.Date;
            todateparam.Value = toDate;
            command.Parameters.Add(todateparam);

            var ecodeparam = command.CreateParameter();
            ecodeparam.ParameterName = "@ecode";
            ecodeparam.DbType = DbType.String;
            ecodeparam.Value = string.IsNullOrEmpty(ecode) ? DBNull.Value : ecode;
            command.Parameters.Add(ecodeparam);

            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                attendances.Add(new AttendanceFetchDto
                {
                    EmpAttendanceId = HRMSAPI.Utility.DataReaderExtensions.IsDBNull(reader, "empattendanceid")
                        ? 0
                        : Convert.ToInt32(reader["empattendanceid"]),

                    EmployeeId = HRMSAPI.Utility.DataReaderExtensions.IsDBNull(reader, "employeeid")
                        ? 0
                        : Convert.ToInt32(reader["employeeid"]),

                    EmployeeName = reader["employeename"]?.ToString() ?? "na",
                    ECode = reader["ecode"]?.ToString() ?? string.Empty,
                    AttendanceDate = reader.GetNullableDateTime("attendancedate"),
                    PunchIn = reader.GetNullableTimeSpan("punchin"),
                    PunchOut = reader.GetNullableTimeSpan("punchout"),
                    RegularizePunchIn = reader.GetNullableTimeSpan("regularizepunchin"),
                    RegularizePunchOut = reader.GetNullableTimeSpan("regularizepuncout"),
                    IsRegularize = HRMSAPI.Utility.DataReaderExtensions.IsDBNull(reader, "isregularize")
                        ? false
                        : Convert.ToBoolean(reader["isregularize"]),
                    IsOnLeave = HRMSAPI.Utility.DataReaderExtensions.IsDBNull(reader, "isonleave")
                        ? false
                        : Convert.ToBoolean(reader["isonleave"]),
                    TotalWorkingMinutes = reader["totalworkingminutes"]?.ToString() ?? "0 hours and 00 minutes",
                    Status = reader["status"]?.ToString() ?? "absent",
                    Punch1 = reader.GetNullableTimeSpan("punch1"),
                    Punch2 = reader.GetNullableTimeSpan("punch2"),
                    Punch3 = reader.GetNullableTimeSpan("punch3"),
                    Punch4 = reader.GetNullableTimeSpan("punch4"),
                    Punch5 = reader.GetNullableTimeSpan("punch5"),
                    Punch6 = reader.GetNullableTimeSpan("punch6"),
                    Punch7 = reader.GetNullableTimeSpan("punch7"),
                    Punch8 = reader.GetNullableTimeSpan("punch8"),
                    Punch9 = reader.GetNullableTimeSpan("punch9"),
                    Punch10 = reader.GetNullableTimeSpan("punch10"),
                    Punch11 = reader.GetNullableTimeSpan("punch11"),
                    Punch12 = reader.GetNullableTimeSpan("punch12"),
                    TotalWorkingDays = HRMSAPI.Utility.DataReaderExtensions.IsDBNull(reader, "totalworkingdays")
                        ? 0.0
                        : Convert.ToDouble(reader["totalworkingdays"]),
                    LateMinutes = HRMSAPI.Utility.DataReaderExtensions.IsDBNull(reader, "lateminutes")
                        ? 0
                        : Convert.ToInt32(reader["lateminutes"]),
                    EarlyMinutes = HRMSAPI.Utility.DataReaderExtensions.IsDBNull(reader, "earlyminutes")
                        ? 0
                        : Convert.ToInt32(reader["earlyminutes"]),
                    TotalMonthlyWorkingHours = reader["totalmonthlyworkinghours"]?.ToString() ?? "0 hours and 00 minutes",
                    ValidPunchCount = HRMSAPI.Utility.DataReaderExtensions.IsDBNull(reader, "validpunchcount")
                        ? 0
                        : Convert.ToInt32(reader["validpunchcount"]),
                    //Location = reader["Location"]?.ToString() ?? null
                });
            }

            return attendances;
        }


        #region Attendance Request
        public async Task<int> CreateAttendanceRequestAsync(AttendanceRegularizationRequestDto requestDto, JwtLoginDetailDto loginDetail, string? fileUrl)

        {
            // Validate input parameters
            if (requestDto == null)
            {
                throw new ArgumentNullException(nameof(requestDto), "Attendance request data is required.");
            }

            if (loginDetail == null || string.IsNullOrEmpty(loginDetail.EmployeeId))
            {
                throw new UnauthorizedAccessException("User not authenticated.");
            }

            try
            {

                long employeeId;

                // Check if requestDto.EmployeeId has valid data (non-null and > 0)
                if (requestDto.EmployeeId.HasValue && requestDto.EmployeeId > 0)
                {
                    employeeId = requestDto.EmployeeId.Value;
                }
                else
                {
                    // Try to parse loginDetail.EmployeeId
                    if (!long.TryParse(loginDetail.EmployeeId, out employeeId))
                    {
                        throw new ArgumentException("Invalid EmployeeId in loginDetail.");
                    }
                }


                var isAlreadyRequested = await _context.tblAttendanceRegularizationRequests
                     .FirstOrDefaultAsync(r => r.EmployeeId == employeeId
                     && r.RequestDate.Date == requestDto.RequestDate.Date
                   && (r.StatusId == 1 || r.StatusId == 4));

                if (isAlreadyRequested != null)
                {
                    if (isAlreadyRequested.StatusId == 1)
                    {
                        throw new InvalidOperationException("An attendance request approval for this date already exists. Please wait for manager action.");
                    }
                    else if (isAlreadyRequested.StatusId == 4)
                    {
                        throw new InvalidOperationException("An attendance request pending for this date already exists. Please wait for manager action.");
                    }
                }



                // Fetch reporting head and manager ID in a single query if possible
                var reportingHead = await _context.tblEmployees
                    .Where(e => e.EmployeeId == employeeId)
                    .Select(e => new { e.ReportHeadEcode })
                    .FirstOrDefaultAsync();

                if (reportingHead == null)
                {
                    throw new InvalidOperationException("Employee not found.");
                }

                if (string.IsNullOrEmpty(reportingHead.ReportHeadEcode))
                {
                    throw new InvalidOperationException("Reporting head not assigned. Please update reporting head information.");
                }

                var reportingManagerId = await _context.tblEmployees
                    .Where(e => e.Ecode == reportingHead.ReportHeadEcode)
                    .Select(e => e.EmployeeId)
                    .FirstOrDefaultAsync();

                if (reportingManagerId == 0)
                {
                    throw new InvalidOperationException("Reporting head ID not found. Please update reporting head information.");
                }

                // Create the attendance request
                var request = new tblAttendanceRegularizationRequest
                {
                    EmployeeId = employeeId,
                    RequestDate = requestDto.RequestDate,
                    Reason = requestDto.Reason?.Trim(),
                    EmployeeRemarks = requestDto.Remarks?.Trim(),
                    StatusId = requestDto.StatusId ?? 4,
                    ReportingManagerId = reportingManagerId,
                    PunchIn = requestDto.PunchIn,
                    PunchOut = requestDto.PunchOut,
                    CreatedOn = DateTime.UtcNow,
                    CreatedBy = loginDetail.EmployeeId,
                    LastUpdatedBy = loginDetail.EmployeeId,
                    FileUrl = fileUrl,
                    PunchTypeId = requestDto.PunchTypeId
                };

                // Validate required fields
                if (request.EmployeeId == 0 || request.RequestDate == default)
                {
                    throw new ArgumentException("Employee ID and Request Date are required.");
                }

                // Use transaction for data consistency
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    _context.tblAttendanceRegularizationRequests.Add(request);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return (int)request.AttendanceRequestId;
                }
                catch (DbUpdateException ex)
                {
                    await transaction.RollbackAsync();
                    throw new InvalidOperationException("Failed to save attendance request due to database error.", ex);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("An error occurred while creating the attendance request.", ex);
            }
        }

        //public async Task<bool> UpdateAttendanceRequestStatusAsync(int requestId, UpdateAttendanceRequestDto updateDto, string updatedBy)
        //{
        //    // Validate input parameters
        //    if (requestId <= 0)
        //    {
        //        throw new ArgumentException("Request ID must be a positive integer.", nameof(requestId));
        //    }
        //    if (updateDto == null)
        //    {
        //        throw new ArgumentNullException(nameof(updateDto), "Update data is required.");
        //    }
        //    if (string.IsNullOrEmpty(updatedBy))
        //    {
        //        throw new ArgumentException("UpdatedBy cannot be null or empty.", nameof(updatedBy));
        //    }

        //    try
        //    {

        //        var request = await _context.tblAttendanceRegularizationRequests
        //            .FirstOrDefaultAsync(r => r.AttendanceRequestId == requestId);
        //        var Ecode = await _context.tblEmployees
        //            .Where(e => e.EmployeeId == request.EmployeeId)
        //            .Select(e => e.Ecode)
        //            .FirstOrDefaultAsync();
        //        if (request == null)
        //        {
        //            _logger.LogWarning("Attendance request not found for ID: {RequestId}", requestId);
        //            return false;
        //        }

        //        // Validate StatusId
        //        if (!IsValidStatusId(updateDto.StatusId))
        //        {
        //            throw new ArgumentException($"Invalid status ID: {updateDto.StatusId}", nameof(updateDto.StatusId));
        //        }

        //        // Update request properties
        //        request.StatusId = updateDto.StatusId;
        //        request.Remarks = updateDto.Remarks?.Trim();
        //        request.LastUpdatedBy = updatedBy;
        //        request.UpdatedOn = DateTime.UtcNow;

        //        if(updateDto.StatusId == 2)
        //        {
        //            var attendances = await _context.tblEmployeeMultiPunches
        //               .FirstOrDefaultAsync(a =>
        //                   a.UserID == Ecode &&
        //                   a.PunchDate.Date == request.RequestDate.Date);
        //            if (attendances != null)
        //            {
        //                // Update existing attendance record

        //                attendances.LastUpdatedBy = updatedBy;
        //                attendances.CreatedOn = DateTime.UtcNow;
        //                attendances.IsRegularize = false;

        //                _context.tblEmployeeMultiPunches.Update(attendances);
        //            }

        //        }
        //        if (updateDto.StatusId == 1)
        //        {
        //            var attendance = await _context.tblEmployeeMultiPunches
        //                .FirstOrDefaultAsync(a =>
        //                    a.UserID == Ecode &&
        //                    a.PunchDate.Date == request.RequestDate.Date);

        //            if (attendance == null)
        //            {
        //                _logger.LogWarning(
        //                    "No attendance record found for employee {EmployeeId} on date {RequestDate}",
        //                    Ecode,
        //                    request.RequestDate.Date);

        //                // Optionally create a new attendance record instead of silently skipping
        //                attendance = new tblEmployeeMultiPunch
        //                {
        //                    UserID = Ecode,
        //                    PunchDate = request.RequestDate.Date,
        //                    RegularizePunchIn = request.PunchIn,
        //                    RegularizePuncOut = request.PunchOut,
        //                    LastUpdatedBy = updatedBy,
        //                    CreatedOn = DateTime.UtcNow,
        //                    IsRegularize = true,
        //                    CreatedBy = updatedBy
        //                };
        //                _context.tblEmployeeMultiPunches.Add(attendance);
        //            }
        //            else
        //            {
        //                // Update existing attendance record
        //                attendance.RegularizePunchIn = request.PunchIn;
        //                attendance.RegularizePuncOut = request.PunchOut;
        //                attendance.LastUpdatedBy = updatedBy;
        //                attendance.CreatedOn = DateTime.UtcNow; 
        //                attendance.IsRegularize = true;

        //                _context.tblEmployeeMultiPunches.Update(attendance);
        //            }
        //        }

        //        // Use transaction for data consistency
        //        using var transaction = await _context.Database.BeginTransactionAsync();
        //        try
        //        {
        //            await _context.SaveChangesAsync();
        //            await transaction.CommitAsync();

        //            _logger.LogInformation(
        //                "Successfully updated attendance request {RequestId} with status {StatusId}",
        //                requestId,
        //                updateDto.StatusId);

        //            return true;
        //        }
        //        catch (DbUpdateException ex)
        //        {
        //            await transaction.RollbackAsync();
        //            _logger.LogError(ex,
        //                "Database error updating attendance request {RequestId}",
        //                requestId);
        //            throw new InvalidOperationException("Failed to update attendance request due to database error.", ex);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex,
        //            "Unexpected error updating attendance request status for request {RequestId}",
        //            requestId);
        //        throw new InvalidOperationException("An error occurred while updating the attendance request.", ex);
        //    }
        //}
        //public async Task<bool> UpdateAttendanceRequestStatusAsync(int requestId, UpdateAttendanceRequestDto updateDto, string updatedBy)
        //{
        //    // Input validation
        //    if (requestId <= 0) throw new ArgumentException("Request ID must be a positive integer.", nameof(requestId));
        //    if (updateDto == null) throw new ArgumentNullException(nameof(updateDto), "Update data is required.");
        //    if (string.IsNullOrEmpty(updatedBy)) throw new ArgumentException("UpdatedBy cannot be null or empty.", nameof(updatedBy));
        //    if (!IsValidStatusId(updateDto.StatusId)) throw new ArgumentException($"Invalid status ID: {updateDto.StatusId}", nameof(updateDto.StatusId));

        //    try
        //    {
        //        // Fetch request and employee code in single query
        //        var request = await _context.tblAttendanceRegularizationRequests
        //            .Join(_context.tblEmployees,
        //                r => r.EmployeeId,
        //                e => e.EmployeeId,
        //                (r, e) => new { Request = r, Ecode = e.Ecode })
        //            .FirstOrDefaultAsync(x => x.Request.AttendanceRequestId == requestId);

        //        if (request == null)
        //        {
        //            _logger.LogWarning("Attendance request not found for ID: {RequestId}", requestId);
        //            return false;
        //        }

        //        // Update request properties
        //        request.Request.StatusId = updateDto.StatusId;
        //        request.Request.Remarks = updateDto.Remarks?.Trim();
        //        request.Request.LastUpdatedBy = updatedBy;
        //        request.Request.UpdatedOn = DateTime.UtcNow;

        //        // Handle attendance record
        //        var attendance = await _context.tblEmployeeMultiPunches
        //            .FirstOrDefaultAsync(a => a.UserID == request.Ecode && a.PunchDate.Date == request.Request.RequestDate.Date);

        //        if (updateDto.StatusId == 1)
        //        {
        //            if (attendance == null)
        //            {
        //                attendance = new tblEmployeeMultiPunch
        //                {
        //                    UserID = request.Ecode,
        //                    PunchDate = request.Request.RequestDate.Date,
        //                    RegularizePunchIn = request.Request.PunchIn,
        //                    RegularizePuncOut = request.Request.PunchOut,
        //                    LastUpdatedBy = updatedBy,
        //                    CreatedOn = DateTime.UtcNow,
        //                    IsRegularize = true,
        //                    CreatedBy = updatedBy
        //                };
        //                _context.tblEmployeeMultiPunches.Add(attendance);
        //            }
        //            else
        //            {
        //                attendance.RegularizePunchIn = request.Request.PunchIn;
        //                attendance.RegularizePuncOut = request.Request.PunchOut;
        //                attendance.LastUpdatedBy = updatedBy;
        //                attendance.CreatedOn = DateTime.UtcNow;
        //                attendance.IsRegularize = true;
        //                _context.tblEmployeeMultiPunches.Update(attendance);
        //            }
        //        }
        //        else if (updateDto.StatusId == 2 && attendance != null) // Only update if entry exists
        //        {
        //            attendance.LastUpdatedBy = updatedBy;
        //            attendance.CreatedOn = DateTime.UtcNow;
        //            attendance.IsRegularize = false;
        //            _context.tblEmployeeMultiPunches.Update(attendance);
        //        }

        //        // Save changes with transaction
        //        using var transaction = await _context.Database.BeginTransactionAsync();
        //        try
        //        {
        //            await _context.SaveChangesAsync();
        //            await transaction.CommitAsync();
        //            _logger.LogInformation("Successfully updated attendance request {RequestId} with status {StatusId}", requestId, updateDto.StatusId);
        //            return true;
        //        }
        //        catch (DbUpdateException ex)
        //        {
        //            await transaction.RollbackAsync();
        //            _logger.LogError(ex, "Database error updating attendance request {RequestId}", requestId);
        //            throw new InvalidOperationException("Failed to update attendance request due to database error.", ex);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Unexpected error updating attendance request status for request {RequestId}", requestId);
        //        throw new InvalidOperationException("An error occurred while updating the attendance request.", ex);
        //    }
        //}

        public async Task<ApiResponse<object>> ApproveRegularizationAsync(
       int requestId,
       UpdateAttendanceRequestDto dto,
       long callerEmployeeId,
       string role)
        {
            _logger.LogInformation("ApproveRegularization called: requestId={RequestId}, callerEmployeeId={CallerId}, role='{Role}', dto.StatusId={StatusId}",
                requestId, callerEmployeeId, role, dto?.StatusId);

            if (requestId <= 0)
                return new ApiResponse<object>(HttpStatusCode.BadRequest, false, "Request ID must be positive.", null);

            if (dto == null)
                return new ApiResponse<object>(HttpStatusCode.BadRequest, false, "Request body cannot be null.", null);

            if (dto.StatusId is not (AttendanceStatuses.Approved or AttendanceStatuses.Rejected or AttendanceStatuses.Pending))
                return new ApiResponse<object>(HttpStatusCode.BadRequest, false, $"StatusId must be 1 (Approved), 2 (Rejected), or 4 (Pending). Received: {dto.StatusId}", null);

            try
            {
                var wrapper = await _context.tblAttendanceRegularizationRequests
                    .Join(_context.tblEmployees,
                        r => r.EmployeeId,
                        e => e.EmployeeId,
                        (r, e) => new { Request = r, Ecode = e.Ecode })
                    .FirstOrDefaultAsync(x => x.Request.AttendanceRequestId == requestId);

                if (wrapper == null)
                {
                    _logger.LogWarning("Regularize request not found: {Id}", requestId);
                    return new ApiResponse<object>(HttpStatusCode.BadRequest, false, $"Attendance regularization request {requestId} not found.", null);
                }

                var req = wrapper.Request;
                var roleLower = role?.Trim().ToLowerInvariant();
                var isCallerSuperAdmin = roleLower == "superadmin" || roleLower == "it superadmin" || roleLower == "master";
                var isCallerLp = isCallerSuperAdmin || roleLower == "lp" || roleLower == "audit";
                var isCallerReportingManager = req.ReportingManagerId == callerEmployeeId;

                var now = DateTime.UtcNow;
                var trimmedRemarks = dto.Remarks?.Trim();

                // ===== Role-based logic =====
                if (isCallerSuperAdmin || callerEmployeeId==10)
                {
                    // SuperAdmin updates both
                    req.ManagerApprovalStatusId = dto.StatusId;
                    req.ManagerApproverId = callerEmployeeId;
                    req.ManagerApprovalOn = now;
                    req.ManagerRemarks = trimmedRemarks;

                    req.LpApprovalStatusId = dto.StatusId;
                    req.LpApproverId = callerEmployeeId;
                    req.LpApprovalOn = now;
                    req.LpRemarks = trimmedRemarks;
                }
                else if (isCallerLp)
                {
                    if (isCallerReportingManager)
                    {
                        req.ManagerApprovalStatusId = dto.StatusId;
                        req.ManagerApproverId = callerEmployeeId;
                        req.ManagerApprovalOn = now;
                        req.ManagerRemarks = trimmedRemarks;

                        req.LpApprovalStatusId = dto.StatusId;
                        req.LpApproverId = callerEmployeeId;
                        req.LpApprovalOn = now;
                        req.LpRemarks = trimmedRemarks;
                    }
                    else
                    {
                        req.LpApprovalStatusId = dto.StatusId;
                        req.LpApproverId = callerEmployeeId;
                        req.LpApprovalOn = now;
                        req.LpRemarks = trimmedRemarks;
                    }
                }
                else
                {
                    if (!isCallerReportingManager)
                    {
                        _logger.LogWarning("Approval denied: caller not reporting manager. ReqId={Req}, Caller={Caller}, RM={RM}",
                            requestId, callerEmployeeId, req.ReportingManagerId);

                        return new ApiResponse<object>(HttpStatusCode.BadRequest, false,
                            "Only the reporting manager, LP/Audit, or SuperAdmin can approve this request.", null);
                    }

                    req.ManagerApprovalStatusId = dto.StatusId;
                    req.ManagerApproverId = callerEmployeeId;
                    req.ManagerApprovalOn = now;
                    req.ManagerRemarks = trimmedRemarks;

                    req.LpApprovalStatusId = dto.StatusId;
                    req.LpApproverId = callerEmployeeId;
                    req.LpApprovalOn = now;
                    req.LpRemarks = trimmedRemarks;
                }

                // ===== Final Status =====
                int manager = req.ManagerApprovalStatusId ?? AttendanceStatuses.Pending;
                int lp = req.LpApprovalStatusId ?? AttendanceStatuses.Pending;

                int finalStatus =
                    (manager == AttendanceStatuses.Rejected || lp == AttendanceStatuses.Rejected)
                        ? AttendanceStatuses.Rejected
                        : (manager == AttendanceStatuses.Approved && lp == AttendanceStatuses.Approved)
                            ? AttendanceStatuses.Approved
                            : AttendanceStatuses.Pending;

                req.StatusId = finalStatus;
                req.Remarks = trimmedRemarks;
                req.LastUpdatedBy = callerEmployeeId.ToString();
                req.UpdatedOn = now;

                // ===== MultiPunch update =====
                var attendance = await _context.tblEmployeeMultiPunches
                    .FirstOrDefaultAsync(a => a.UserID == wrapper.Ecode && a.PunchDate.Date == req.RequestDate.Date);

                if (finalStatus == AttendanceStatuses.Approved)
                {
                    if (attendance == null)
                    {
                        attendance = new tblEmployeeMultiPunch
                        {
                            UserID = wrapper.Ecode,
                            PunchDate = req.RequestDate.Date,
                            RegularizePunchIn = req.PunchIn,
                            RegularizePuncOut = req.PunchOut,
                            LastUpdatedBy = callerEmployeeId.ToString(),
                            CreatedOn = now,
                            IsRegularize = true,
                            CreatedBy = callerEmployeeId.ToString()
                        };
                        _context.tblEmployeeMultiPunches.Add(attendance);
                    }
                    else
                    {
                        attendance.RegularizePunchIn = req.PunchIn;
                        attendance.RegularizePuncOut = req.PunchOut;
                        attendance.LastUpdatedBy = callerEmployeeId.ToString();
                        attendance.CreatedOn = now;
                        attendance.IsRegularize = true;
                        _context.tblEmployeeMultiPunches.Update(attendance);
                    }
                }
                else if (finalStatus == AttendanceStatuses.Rejected && attendance != null)
                {
                    attendance.LastUpdatedBy = callerEmployeeId.ToString();
                    attendance.CreatedOn = now;
                    attendance.IsRegularize = false;
                    _context.tblEmployeeMultiPunches.Update(attendance);
                }

                using var tx = await _context.Database.BeginTransactionAsync();
                try
                {
                    await _context.SaveChangesAsync();
                    await tx.CommitAsync();

                    return new ApiResponse<object>(HttpStatusCode.OK, true, "Approval updated successfully.",
                        new { requestId, dto.StatusId });
                }
                catch (DbUpdateException ex)
                {
                    await tx.RollbackAsync();
                    var innerMsg = ex.InnerException?.Message ?? ex.Message;
                    _logger.LogError(ex, "DB error updating regularization {RequestId}: {Error}", requestId, innerMsg);
                    return new ApiResponse<object>(HttpStatusCode.BadRequest, false, $"Database error: {innerMsg}", null);
                }
            }
            catch (Exception ex)
            {
                var innerMsg = ex.InnerException?.Message ?? ex.Message;
                _logger.LogError(ex, "Unexpected error updating regularization {RequestId}: {Error}", requestId, innerMsg);
                return new ApiResponse<object>(HttpStatusCode.BadRequest, false, $"Unexpected error: {innerMsg}", null);
            }
        }




        public async Task<PagedResult<AttendanceRegularizationRequestDto>> GetRegularizationRequestsAsync(
       long managerId,
       string role,
       long currentEmployeeId,
       int statusId = 0,
       int pageNumber = 1,
       int pageSize = 10,
       string? searchTerm = null)
        {
            if (managerId < 0) throw new ArgumentException("Manager ID cannot be negative.", nameof(managerId));
            if (string.IsNullOrWhiteSpace(role)) throw new ArgumentException("Role cannot be empty.", nameof(role));
            if (pageNumber < 1) throw new ArgumentException("Page number must be > 0.", nameof(pageNumber));
            if (pageSize < 1) throw new ArgumentException("Page size must be > 0.", nameof(pageSize));
            if (statusId < 0) throw new ArgumentException("Status ID must be non-negative.", nameof(statusId));

            try
            {
                // base join: requests -> employees
                //var query = _context.tblAttendanceRegularizationRequests.AsNoTracking()
                //    .Join(_context.tblEmployees.AsNoTracking(),
                //          r => r.EmployeeId,
                //          e => e.EmployeeId,
                //          (r, e) => new { request = r, employee = e })
                //    // LEFT JOIN employees -> location (via LocationId)
                //    .GroupJoin(_context.tblLocations.AsNoTracking(), // <-- use your actual DbSet name (tblLocation vs tblLocations)
                //               re => re.employee.LocationId,
                //               l => l.LocationId,
                //               (re, locs) => new { re.request, re.employee, location = locs.FirstOrDefault() })
                //    .GroupJoin(_context.tblEmployees.AsNoTracking(),
                //               rel => rel.request.ManagerApproverId,
                //               m => m.EmployeeId,
                //               (rel, mgrs) => new
                //               {
                //                   rel.request,
                //                   rel.employee,
                //                   rel.location,
                //                   manager = mgrs.FirstOrDefault()
                //               })
                //    .GroupJoin(_context.tblEmployees.AsNoTracking(),
                //               relm => relm.request.LpApproverId,
                //               lp => lp.EmployeeId,
                //               (relm, lps) => new
                //               {
                //                   relm.request,
                //                   relm.employee,
                //                   relm.location,
                //                   relm.manager,
                //                   lpEmp = lps.FirstOrDefault()
                //               });
                var query =
    from r in _context.tblAttendanceRegularizationRequests.AsNoTracking()
    join e in _context.tblEmployees.AsNoTracking()
        on r.EmployeeId equals e.EmployeeId

    // location LEFT JOIN
    join l in _context.tblLocations.AsNoTracking()
        on e.LocationId equals l.LocationId into locs
    from location in locs.DefaultIfEmpty()

        // manager LEFT JOIN
    join m in _context.tblEmployees.AsNoTracking()
        on r.ManagerApproverId equals m.EmployeeId into mgrs
    from manager in mgrs.DefaultIfEmpty()

        // LP LEFT JOIN
    join lp in _context.tblEmployees.AsNoTracking()
        on r.LpApproverId equals lp.EmployeeId into lps
    from lpEmp in lps.DefaultIfEmpty()

    select new
    {
        request = r,
        employee = e,
        location,
        manager,
        lpEmp
    };

                var roleNorm = role.Trim().ToLowerInvariant();
                bool isStoreHr = await _context.tblEmployeeRoles
                    .AnyAsync(r => r.EmployeeId == currentEmployeeId && r.RoleId == 8);

                int? currentEmployeeLocationId = await _context.tblEmployees
                    .Where(e => e.EmployeeId == currentEmployeeId)
                    .Select(e => e.LocationId)
                    .FirstOrDefaultAsync();

                // === Pending tab: limit to current attendance cycle (26th of prev month → 25th of current month) for all roles ===
                if (statusId == 4)
                {
                    var today = DateTime.Today;
                    var prevMonth = today.AddMonths(-1);
                    var cycleFrom = new DateTime(prevMonth.Year, prevMonth.Month, 26);
                    var cycleToExclusive = new DateTime(today.Year, today.Month, 26); // 25 inclusive == < 26
                    query = query.Where(x => x.request.RequestDate >= cycleFrom && x.request.RequestDate < cycleToExclusive);
                }

                // === Role-based filtering ===
                // Approved / Rejected tabs: only role "SuperAdmin" (strict) sees the entire organisation.
                // Everyone else (incl. IT Superadmin / Master / Manager / LP / employees) sees ONLY
                // rows where they personally acted on the request (Manager or LP approver).
                bool isSuperAdmin = roleNorm == "superadmin";
                bool isApprovedOrRejectedView = statusId == 1 || statusId == 2;

                if (isApprovedOrRejectedView)
                {
                    if (!isSuperAdmin)
                    {
                        query = query.Where(x => x.request.ManagerApproverId == currentEmployeeId
                                              || x.request.LpApproverId == currentEmployeeId);
                    }
                    // SuperAdmin: no additional filter
                }
                else if (isSuperAdmin)
                {
                    // Pending / all view for SuperAdmin (strict role): see all rows in the org (still within the date cycle when statusId==4)
                }
                else if (isStoreHr)
                {
                    // Store HR: ALL requests from same store
                    query = query.Where(x =>
                        x.employee.LocationId.HasValue &&
                        currentEmployeeLocationId.HasValue &&
                        x.employee.LocationId == currentEmployeeLocationId);
                }
                else
                {
                    // Default: team manager view (sees reportees' pending/all requests)
                    var effectiveManagerId = managerId > 0 ? managerId : currentEmployeeId;
                    query = query.Where(x => x.request.ReportingManagerId == effectiveManagerId);
                }

                // Status filter (0 = all)
                if (statusId != 0)
                    query = query.Where(x => x.request.StatusId == statusId);

                // Search (now also on STCode / LocationName)
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    var s = searchTerm.Trim().ToLower();
                    query = query.Where(x =>
                        (x.employee.FULL_NAME != null && x.employee.FULL_NAME.ToLower().Contains(s)) ||
                        (x.employee.FirstName != null && x.employee.FirstName.ToLower().Contains(s)) ||
                        (x.employee.LastName != null && x.employee.LastName.ToLower().Contains(s)) ||
                        (x.request.Reason != null && x.request.Reason.ToLower().Contains(s)) ||
                        (x.request.Remarks != null && x.request.Remarks.ToLower().Contains(s)) ||
                        (x.request.FileUrl != null && x.request.FileUrl.ToLower().Contains(s)) ||
                        (x.request.ManagerRemarks != null && x.request.ManagerRemarks.ToLower().Contains(s)) ||
                        (x.request.LpRemarks != null && x.request.LpRemarks.ToLower().Contains(s)) ||
                        x.request.AttendanceRequestId.ToString().Contains(s) ||
                        (x.request.EmployeeId.HasValue && x.request.EmployeeId.Value.ToString().Contains(s)) ||
                        (x.request.StatusId.HasValue && x.request.StatusId.Value.ToString().Contains(s)) ||
                        (x.request.PunchIn.HasValue && x.request.PunchIn.ToString()!.ToLower().Contains(s)) ||
                        (x.request.PunchOut.HasValue && x.request.PunchOut.ToString()!.ToLower().Contains(s)) ||
                        (x.employee.Ecode != null && x.employee.Ecode.ToLower().Contains(s)) ||
                        (x.location != null && (
                            (x.location.STCode != null && x.location.STCode.ToLower().Contains(s)) ||
                            (x.location.LocationName != null && x.location.LocationName.ToLower().Contains(s))
                        ))
                    );
                }

                var totalRecords = await query.CountAsync();

                var page = query
                    .OrderByDescending(x => x.request.AttendanceRequestId)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize);

                var result = await page.Select(x => new AttendanceRegularizationRequestDto
                {
                    AttendanceRequestId = x.request.AttendanceRequestId,
                    EmployeeId = x.request.EmployeeId,
                    RequestDate = x.request.RequestDate,
                    StatusId = x.request.StatusId,

                    EmployeeName = x.employee.FULL_NAME
                        ?? (x.employee.FirstName != null && x.employee.LastName != null
                            ? (x.employee.FirstName + " " + x.employee.LastName)
                            : x.employee.FirstName ?? x.employee.LastName ?? "Unknown"),

                    PunchIn = (TimeSpan)x.request.PunchIn,   // if PunchIn/PunchOut are nullable TimeSpan?, consider mapping to TimeSpan? to avoid exceptions
                    PunchOut = (TimeSpan)x.request.PunchOut,

                    Reason = x.request.Reason != null ? x.request.Reason.Trim() : null,
                    Remarks = x.request.Remarks != null ? x.request.Remarks.Trim() : null,
                    EmployeeRemarks = x.request.EmployeeRemarks != null ? x.request.EmployeeRemarks.Trim() : null,
                    Attachment = x.request.FileUrl,

                    Ecode = x.employee.Ecode ?? "Unknown",
                    ReportHeadEcode = x.employee.ReportHeadEcode ?? "Unknown",
                    ReportHeadName = _context.tblEmployees.AsNoTracking()
                                        .Where(row => row.Ecode == x.employee.ReportHeadEcode)
                                        .Select(row => row.FULL_NAME)
                                        .FirstOrDefault() ?? "Unknown",

                    // NEW: location fields with "NA" fallback when there is no location
                    STCode = x.location != null && !string.IsNullOrWhiteSpace(x.location.STCode) ? x.location.STCode : "NA",
                    LocationName = x.location != null && !string.IsNullOrWhiteSpace(x.location.LocationName) ? x.location.LocationName : "NA",

                    // approvals
                    ManagerApprovalStatusId = x.request.ManagerApprovalStatusId,
                    ManagerApproverId = x.request.ManagerApproverId,
                    ManagerApprovalOn = x.request.ManagerApprovalOn,
                    ManagerRemarks = x.request.ManagerRemarks,
                    LpApprovalStatusId = x.request.LpApprovalStatusId,
                    LpApproverId = x.request.LpApproverId,
                    LpApprovalOn = x.request.LpApprovalOn,
                    LpRemarks = x.request.LpRemarks,
                    LpEcode = x.lpEmp != null ? x.lpEmp.Ecode ?? "Unknown" : "Unknown",
                    ManagerEcode = x.manager != null ? x.manager.Ecode ?? "Unknown" : "Unknown",
                }).ToListAsync();

                return new PagedResult<AttendanceRegularizationRequestDto>(result, totalRecords);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to retrieve regularization requests.", ex);
            }
        }



        public async Task<List<AttendanceRegularizationRequestDto>> GetRegularizationRequestsSelfAsync(long EmployeeId)
        {
            // Validate input
            if (EmployeeId <= 0)
            {
                throw new ArgumentException("EmployeeId must be a positive integer.", nameof(EmployeeId));
            }

            try
            {
                // Base query with Join
                var query = _context.tblAttendanceRegularizationRequests
                    .AsNoTracking()
                    .Where(r => r.EmployeeId == EmployeeId) // Filter by EmployeeId
                    .Join(
                        _context.tblEmployees,
                        request => request.EmployeeId,
                        employee => employee.EmployeeId,
                        (request, employee) => new { request, employee }
                    );

                // Project to DTO
                var result = await query
                    .Select(x => new AttendanceRegularizationRequestDto
                    {
                        AttendanceRequestId = x.request.AttendanceRequestId,
                        EmployeeId = x.request.EmployeeId,
                        RequestDate = x.request.RequestDate,
                        Reason = x.request.Reason.Trim(),
                        StatusId = x.request.StatusId,
                        EmployeeName = x.employee.FULL_NAME
                            ?? (x.employee.FirstName != null && x.employee.LastName != null
                                ? $"{x.employee.FirstName} {x.employee.LastName}"
                                : x.employee.FirstName
                                    ?? x.employee.LastName
                                    ?? "Unknown"),
                        PunchIn = (TimeSpan)x.request.PunchIn,
                        PunchOut = (TimeSpan)x.request.PunchOut,
                        Remarks = x.request.Remarks.Trim(),
                        EmployeeRemarks = x.request.EmployeeRemarks.Trim(),
                        Attachment = x.request.FileUrl,
                        Ecode = x.employee != null ? x.employee.Ecode ?? "Unknown" : "Unknown",
                        ReportHeadEcode = x.employee.ReportHeadEcode ?? "Unknown",
                        ReportHeadName = _context.tblEmployees.AsNoTracking()
                                        .Where(row => row.Ecode == x.employee.ReportHeadEcode)
                                        .Select(row => row.FULL_NAME)
                                        .FirstOrDefault() ?? "Unknown",


                        // approvals
                        ManagerApprovalStatusId = x.request.ManagerApprovalStatusId,
                        ManagerApproverId = x.request.ManagerApproverId,
                        ManagerApprovalOn = x.request.ManagerApprovalOn,
                        ManagerRemarks = x.request.ManagerRemarks,
                        LpApprovalStatusId = x.request.LpApprovalStatusId,
                        LpApproverId = x.request.LpApproverId,
                        LpApprovalOn = x.request.LpApprovalOn,
                        LpRemarks = x.request.LpRemarks
                    })
                    .OrderByDescending(x => x.AttendanceRequestId)
                    .ToListAsync();

                return result;
            }
            catch (Exception ex)
            {
                // Log the exception
                throw new InvalidOperationException("Failed to retrieve regularization requests.", ex);
            }
        }

        private bool IsValidStatusId(int statusId)
        {

            return statusId is >= 1 and <= 4; // Example range; adjust as needed
        }

        #endregion
        #region Attendance Geo
        public async Task<AttendanceRecordGeo> GeoLocationAttendance(string employeeCode, PunchType type, decimal lat, decimal lon, string? device, string? ip, string? address)
        {
            var emp = await _context.tblEmployees.FirstOrDefaultAsync(e => e.Ecode == employeeCode && e.IsActive == true)
                ?? throw new InvalidOperationException("Employee not found or inactive.");

            (tblLocation office, int radiusMeters) = await GetActiveOfficeAsync(emp)
                ?? throw new InvalidOperationException("No active office location configured.");

            // Add null checks for StoreLat and StoreLong
            if (office.StoreLat == null || office.StoreLong == null)
                throw new InvalidOperationException("Office location coordinates not configured.");

            double dist = DistanceMeters((double)lat, (double)lon, (double)office.StoreLat, (double)office.StoreLong);
            bool within = dist <= radiusMeters;

            var rec = new AttendanceRecord
            {
                EmployeeId = emp.EmployeeId,
                PunchType = (int)type,
                PunchTimeUtc = DateTime.UtcNow,
                Latitude = lat,
                Longitude = lon,
                WithinGeofence = within,
                DeviceInfo = device,
                ClientIp = ip,
                Address = address
            };

            // Basic guard: don't allow consecutive same-type punches today
            var todayUtc = DateTime.UtcNow.Date;
            var last = await _context.AttendanceRecords
                .Where(a => a.EmployeeId == emp.EmployeeId && a.PunchTimeUtc >= todayUtc)
                .OrderByDescending(a => a.PunchTimeUtc)
                .FirstOrDefaultAsync();

            if (last != null && last.PunchType == (int)type)
                throw new InvalidOperationException($"Already punched {type} recently.");

            await _context.AttendanceRecords.AddAsync(rec);
            await _context.SaveChangesAsync();

            return new AttendanceRecordGeo
            {
                Id = rec.Id,
                EmployeeId = rec.EmployeeId,
                PunchType = (PunchType)rec.PunchType,
                PunchTimeUtc = rec.PunchTimeUtc,
                Latitude = rec.Latitude,
                Longitude = rec.Longitude,
                WithinGeofence = rec.WithinGeofence,
                DeviceInfo = rec.DeviceInfo,
                ClientIp = rec.ClientIp,
                Address = rec.Address,
            };
        }

        public async Task<AttendanceRecordGeo> GeoLocationAttendanceWithProc(string employeeCode, PunchType type, decimal lat, decimal lon, string? device, string? ip, string? address, IFormFile? proofFile = null)
        {
            try
            {
                // First, get employee details and validate
                var emp = await _context.tblEmployees.AsNoTracking().AsQueryable().FirstOrDefaultAsync(e => e.Ecode == employeeCode && e.IsActive == true)
                    ?? throw new InvalidOperationException("Employee not found or inactive.");

                // Get office location and validate
                (tblLocation office, int radiusMeters) = await GetActiveOfficeAsync(emp)
                    ?? throw new InvalidOperationException("No active office location configured.");

                if (office.StoreLat == null || office.StoreLong == null)
                    throw new InvalidOperationException("Office location coordinates not configured.");

                // Calculate distance and check if within geofence
                double dist = DistanceMeters((double)lat, (double)lon, (double)office.StoreLat, (double)office.StoreLong);
                bool within = dist <= radiusMeters;

                // Validation Logic moved to Stored Procedure sp_InsertAttendanceRecord to authorize load and prevent race conditions.
                // It handles:
                // 1. 5-minute cooldown check (Error 50001)
                // 2. Punch sequence validation - Out requires In (Error 50002)

                // Handle file upload if proof file is provided
                string? proofPath = null;
                if (proofFile != null && proofFile.Length > 0)
                {
                    // Validate file size (30 MB = 30 * 1024 * 1024 bytes)
                    const long maxFileSize = 30 * 1024 * 1024; // 30 MB
                    if (proofFile.Length > maxFileSize)
                        throw new InvalidOperationException($"File size exceeds maximum allowed size of 30 MB. Current size: {proofFile.Length / (1024.0 * 1024.0):F2} MB");

                    // Validate file type (image or video)
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".mp4", ".mov", ".avi", ".webm", ".mkv" };
                    var fileExtension = Path.GetExtension(proofFile.FileName).ToLowerInvariant();
                    if (!allowedExtensions.Contains(fileExtension))
                        throw new InvalidOperationException($"Invalid file type. Allowed types: {string.Join(", ", allowedExtensions)}");

                    // Save file with specified path structure
                    // GeoLocationAttendanceProof/YYYY/MMM/DD/EmployeeId/FileName_ddMMYYYYHHMMSSFFF.ext
                    var now = DateTime.Now;
                    var year = now.Year.ToString();
                    var month = now.ToString("MMM");
                    var day = now.Day.ToString("00");
                    var employeeIdStr = emp.EmployeeId.ToString();
                    var timestamp = now.ToString("ddMMyyyyHHmmssfff");
                    var fileName = $"{Path.GetFileNameWithoutExtension(proofFile.FileName)}_{timestamp}{fileExtension}";

                    var folderPath = Path.Combine(_webHostEnvironment.WebRootPath ?? "wwwroot", 
                        "GeoLocationAttendanceProof", year, month, day, employeeIdStr);
                    
                    // Create directory if it doesn't exist
                    if (!Directory.Exists(folderPath))
                    {
                        Directory.CreateDirectory(folderPath);
                    }

                    var filePath = Path.Combine(folderPath, fileName);

                    // Save the file
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await proofFile.CopyToAsync(stream);
                    }

                    // Store relative path (after wwwroot)
                    proofPath = Path.Combine("GeoLocationAttendanceProof", year, month, day, employeeIdStr, fileName)
                        .Replace('\\', '/'); // Use forward slashes for web paths
                }

                // Insert attendance record using stored procedure via ADO.NET to capture SCOPE_IDENTITY()
                // This avoids the secondary lookup and potential race conditions/performance issues.
                var punchTimeLocal = DateTime.Now; 
                int insertedId = 0;

                using (var connection = _context.Database.GetDbConnection())
                {
                    await connection.OpenAsync();
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "sp_InsertAttendanceRecord";
                        command.CommandType = CommandType.StoredProcedure;

                        command.Parameters.Add(new SqlParameter("@EmployeeId", emp.EmployeeId));
                        command.Parameters.Add(new SqlParameter("@PunchType", (int)type));
                        command.Parameters.Add(new SqlParameter("@PunchTimeUtc", punchTimeLocal));
                        command.Parameters.Add(new SqlParameter("@Latitude", lat));
                        command.Parameters.Add(new SqlParameter("@Longitude", lon));
                        command.Parameters.Add(new SqlParameter("@WithinGeofence", within));
                        command.Parameters.Add(new SqlParameter("@DeviceInfo", device ?? (object)DBNull.Value));
                        command.Parameters.Add(new SqlParameter("@ClientIp", ip ?? (object)DBNull.Value));
                        command.Parameters.Add(new SqlParameter("@Address", address ?? (object)DBNull.Value));
                        command.Parameters.Add(new SqlParameter("@ProofPath", proofPath ?? (object)DBNull.Value));
                        command.Parameters.Add(new SqlParameter("@StatusId", 1)); 
                        command.Parameters.Add(new SqlParameter("@LastUpdatedBy", "System"));
                        command.Parameters.Add(new SqlParameter("@LastUpdatedOn", punchTimeLocal));

                        // ExecuteScalarAsync allows us to get the select result (SCOPE_IDENTITY) from the SP
                        var resultObj = await command.ExecuteScalarAsync();
                        if (resultObj != null && int.TryParse(resultObj.ToString(), out int id))
                        {
                            insertedId = id;
                        }
                    }
                }

                return new AttendanceRecordGeo
                {
                    Id = insertedId, // Now populated directly from SP result
                    EmployeeId = emp.EmployeeId,
                    PunchType = type,
                    PunchTimeUtc = punchTimeLocal, 
                    Latitude = lat,
                    Longitude = lon,
                    WithinGeofence = within,
                    DeviceInfo = device,
                    ClientIp = ip,
                    Address = address,
                    ProofPath = proofPath,
                };
            }
            catch (SqlException ex)
            {
                if (ex.Number == 50001)
                {
                    throw new InvalidOperationException(ex.Message); // "You have punched recently..."
                }
                else if (ex.Number == 50002)
                {
                    throw new InvalidOperationException(ex.Message); // "You cannot Punch Out without Punching In..."
                }

                _logger.LogError(ex, "SQL Error saving attendance record for employee: {EmployeeCode}", employeeCode);
                throw new InvalidOperationException("Database error occurred while saving attendance.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving attendance record with stored procedure for employee: {EmployeeCode}", employeeCode);
                throw;
            }
        }
        public async Task<(tblLocation office, int radiusMeters)?> GetActiveOfficeAsync(
        tblEmployee emp,
        CancellationToken ct = default)
        {
            const int defaultRadius = 150;
            var nowUtc = DateTime.UtcNow;

            // Try to find a *currently active* temp assignment.
            // If none exists (or ReleasedOnDate <= now, or null), we will fall back to emp.LocationId.
            int? activeTempLocationId = await _context.AssignLocationHistories
                .AsNoTracking()
                .AsQueryable()
                .Where(h => h.EmployeeId == emp.EmployeeId
                            && h.ReleasedOnDate != null
                            && h.ReleasedOnDate > nowUtc)
                .OrderByDescending(h => h.ReleasedOnDate)
                .Select(h => (int?)h.AssignedLocation)
                .FirstOrDefaultAsync(ct);

            // Fallback to the employee's assigned office if no active temp assignment
            int targetLocationId = (int)(activeTempLocationId ?? emp.LocationId);

            // Fetch the effective office (temp-active if present, otherwise the employee's office)
            var office = await _context.tblLocations
                .AsNoTracking()
                .AsQueryable()
                .Where(l => l.LocationId == targetLocationId
                // && l.IsActive == true
                )
                .FirstOrDefaultAsync(ct);

            if (office is null)
            {
                // Mirror your original behavior: if the employee's own office isn't in DB, return null.
                // (This also covers the case where a temp-assigned location id is bad.)
                return null;
            }

            int radius = office.AllowedRadiusMeters ?? defaultRadius;
            return (office, radius);
        }

        public double DistanceMeters(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371000; // meters
            double dLat = ToRad(lat2 - lat1);
            double dLon = ToRad(lon2 - lon1);
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
            Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
            Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
            static double ToRad(double deg) => deg * Math.PI / 180;
        }
        public async Task<PagedResult<DailyAttendanceSummaryDto>> GetDailyAttendanceSummaryGeoAsync(
    long managerId,
    string role,
    int statusId = 0,
    int pageNumber = 1,
    int pageSize = 10,
    string? searchTerm = null,
    string timeZoneId = "UTC",
    CancellationToken ct = default)
        {
            if (managerId <= 0) throw new ArgumentException("Manager ID must be positive.", nameof(managerId));
            if (string.IsNullOrWhiteSpace(role)) throw new ArgumentException("Role is required.", nameof(role));
            if (pageNumber < 1) throw new ArgumentException("PageNumber must be >= 1.", nameof(pageNumber));
            if (pageSize < 1) throw new ArgumentException("PageSize must be >= 1.", nameof(pageSize));
            if (statusId < 0) throw new ArgumentException("StatusId must be >= 0.", nameof(statusId));
            if (string.IsNullOrWhiteSpace(timeZoneId)) timeZoneId = "UTC";

            await using var conn = _context.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open) await conn.OpenAsync(ct);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "dbo.usp_GetDailyAttendanceSummaryGeo";
            cmd.CommandType = CommandType.StoredProcedure;

            SqlParameter P(string name, SqlDbType type, object? val, int? size = null)
            {
                var p = new SqlParameter(name, type) { Value = val ?? DBNull.Value };
                if (size.HasValue) p.Size = size.Value;
                return p;
            }

            cmd.Parameters.Add(P("@ManagerId", SqlDbType.BigInt, managerId));
            cmd.Parameters.Add(P("@Role", SqlDbType.NVarChar, role, 50));
            cmd.Parameters.Add(P("@StatusId", SqlDbType.Int, statusId));
            cmd.Parameters.Add(P("@PageNumber", SqlDbType.Int, pageNumber));
            cmd.Parameters.Add(P("@PageSize", SqlDbType.Int, pageSize));
            cmd.Parameters.Add(P("@SearchTerm", SqlDbType.NVarChar, string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm, 100));
            cmd.Parameters.Add(P("@TimeZoneId", SqlDbType.NVarChar, timeZoneId, 64));

            var summaries = new List<DailyAttendanceSummaryDto>();

            await using var reader = await cmd.ExecuteReaderAsync(ct);

            // ---------- Result Set 1: Daily summaries ----------
            int Ord(string col) => reader.GetOrdinal(col);

            // Cache ordinals once (faster & safer)
            int oEmployeeId = Ord("EmployeeId");
            int oEcode = Ord("Ecode");
            int oEmployeeName = Ord("EmployeeName");
            int oPunchDate = Ord("PunchDate");
            int oPunchCount = Ord("PunchCount");
            int oPunchIn = Ord("PunchInCount");
            int oPunchOut = Ord("PunchOutCount");
            int oFirstUtc = Ord("FirstPunchUtc");
            int oLastUtc = Ord("LastPunchUtc");
            int oSummaryId = Ord("SummaryStatusId");
            int oStatusName = Ord("StatusName");
            int oTotalRecords = Ord("TotalRecords");
            int oRemarks1 = Ord("Remarks");
            int oAddress1 = Ord("Address");

            // 2-Level approval ordinals (safe lookup)
            bool TryOrdSummary(string col, out int ord)
            { try { ord = reader.GetOrdinal(col); return true; } catch { ord = -1; return false; } }

            TryOrdSummary("ManagerApprovalStatusId", out var oMgrStatusId);
            TryOrdSummary("ManagerApprovalStatusName", out var oMgrStatusName);
            TryOrdSummary("ManagerApproverId", out var oMgrApproverId);
            TryOrdSummary("ManagerApprovalOn", out var oMgrApprovalOn);
            TryOrdSummary("ManagerRemarks", out var oMgrRemarks);
            TryOrdSummary("MasterApprovalStatusId", out var oMasterStatusId);
            TryOrdSummary("MasterApprovalStatusName", out var oMasterStatusName);
            TryOrdSummary("MasterApproverId", out var oMasterApproverId);
            TryOrdSummary("MasterApprovalOn", out var oMasterApprovalOn);
            TryOrdSummary("MasterRemarks", out var oMasterRemarks);
            TryOrdSummary("ApprovalFinalStatusId", out var oApprovalFinalStatusId);

            while (await reader.ReadAsync(ct))
            {
                var dto = new DailyAttendanceSummaryDto
                {
                    EmployeeId = reader.GetInt64(oEmployeeId),
                    Ecode = reader.IsDBNull(oEcode) ? null : reader.GetString(oEcode),
                    EmployeeName = reader.IsDBNull(oEmployeeName) ? "Unknown" : reader.GetString(oEmployeeName),
                    PunchDate = reader.GetDateTime(oPunchDate),
                    PunchCount = reader.GetInt32(oPunchCount),
                    PunchInCount = reader.GetInt32(oPunchIn),
                    PunchOutCount = reader.GetInt32(oPunchOut),
                    Remarks = reader.IsDBNull(oRemarks1) ? null : reader.GetString(oRemarks1),
                    Address = reader.IsDBNull(oAddress1) ? null : reader.GetString(oAddress1),
                    // 2-Level approval info
                    ManagerApprovalStatusId = (oMgrStatusId >= 0 && !reader.IsDBNull(oMgrStatusId)) ? reader.GetInt32(oMgrStatusId) : null,
                    ManagerApprovalStatusName = (oMgrStatusName >= 0 && !reader.IsDBNull(oMgrStatusName)) ? reader.GetString(oMgrStatusName) : null,
                    ManagerApproverId = (oMgrApproverId >= 0 && !reader.IsDBNull(oMgrApproverId)) ? reader.GetString(oMgrApproverId) : null,
                    ManagerApprovalOn = (oMgrApprovalOn >= 0 && !reader.IsDBNull(oMgrApprovalOn)) ? reader.GetDateTime(oMgrApprovalOn) : null,
                    ManagerRemarks = (oMgrRemarks >= 0 && !reader.IsDBNull(oMgrRemarks)) ? reader.GetString(oMgrRemarks) : null,
                    MasterApprovalStatusId = (oMasterStatusId >= 0 && !reader.IsDBNull(oMasterStatusId)) ? reader.GetInt32(oMasterStatusId) : null,
                    MasterApprovalStatusName = (oMasterStatusName >= 0 && !reader.IsDBNull(oMasterStatusName)) ? reader.GetString(oMasterStatusName) : null,
                    MasterApproverId = (oMasterApproverId >= 0 && !reader.IsDBNull(oMasterApproverId)) ? reader.GetString(oMasterApproverId) : null,
                    MasterApprovalOn = (oMasterApprovalOn >= 0 && !reader.IsDBNull(oMasterApprovalOn)) ? reader.GetDateTime(oMasterApprovalOn) : null,
                    MasterRemarks = (oMasterRemarks >= 0 && !reader.IsDBNull(oMasterRemarks)) ? reader.GetString(oMasterRemarks) : null,
                    ApprovalFinalStatusId = (oApprovalFinalStatusId >= 0 && !reader.IsDBNull(oApprovalFinalStatusId)) ? reader.GetInt32(oApprovalFinalStatusId) : null,
                };
                summaries.Add(dto);
            }

            // ---------- Result Set 2: Details (optional) ----------
            if (await reader.NextResultAsync(ct))
            {
                // Quick lookup to attach details to existing summaries
                // Group by composite key to handle any potential duplicates before creating dictionary
                var index = summaries
                    .GroupBy(s => (s.EmployeeId, s.PunchDate))
                    .ToDictionary(
                        g => g.Key,
                        g => g.First()); // Take the first item if there are duplicates

                // Safe ordinal helper for optional columns
                bool TryOrd(string col, out int ord)
                {
                    try { ord = reader.GetOrdinal(col); return true; }
                    catch { ord = -1; return false; }
                }

                // Expected columns (all present per your proc)
                TryOrd("EmployeeId", out var dEmployeeId);
                TryOrd("PunchDate", out var dPunchDate);
                TryOrd("PunchTimeUtc", out var dPunchTimeUtc);
                TryOrd("PunchType", out var dPunchType);
                TryOrd("Latitude", out var dLat);
                TryOrd("Longitude", out var dLon);
                TryOrd("WithinGeofence", out var dGeo);
                TryOrd("DeviceInfo", out var dDev);
                TryOrd("ClientIp", out var dIp);
                TryOrd("StatusId", out var dStatusId);
                TryOrd("Remarks", out var dRemarks);  // <-- NEW (details)
                TryOrd("Address", out var dAddress);  // <-- NEW Address (details)
                TryOrd("ProofPath", out var dProofPath);

                while (await reader.ReadAsync(ct))
                {
                    var empId = reader.GetInt64(dEmployeeId);
                    var punchDate = reader.GetDateTime(dPunchDate);

                    if (!index.TryGetValue((empId, punchDate), out var summary))
                        continue; // detail for a row not in current page

                    summary.Details ??= new List<DailyPunchDetailDto>();

                    // Nullables for lat/lon & strings
                    decimal? lat = (dLat >= 0 && !reader.IsDBNull(dLat)) ? reader.GetDecimal(dLat) : (decimal?)null;
                    decimal? lon = (dLon >= 0 && !reader.IsDBNull(dLon)) ? reader.GetDecimal(dLon) : (decimal?)null;
                    string? dev = (dDev >= 0 && !reader.IsDBNull(dDev)) ? reader.GetString(dDev) : null;
                    string? ip = (dIp >= 0 && !reader.IsDBNull(dIp)) ? reader.GetString(dIp) : null;
                    string? rem = (dRemarks >= 0 && !reader.IsDBNull(dRemarks)) ? reader.GetString(dRemarks) : null;
                    string? addr = (dAddress >= 0 && !reader.IsDBNull(dAddress)) ? reader.GetString(dAddress) : null;
                    string? proof = (dProofPath >= 0 && !reader.IsDBNull(dProofPath)) ? reader.GetString(dProofPath) : null;

                    summary.Details.Add(new DailyPunchDetailDto
                    {
                        EmployeeId = empId,
                        PunchDate = punchDate,
                        PunchTimeUtc = reader.GetDateTime(dPunchTimeUtc),
                        PunchType = reader.GetInt32(dPunchType),
                        Latitude = Convert.ToDecimal(lat),
                        Longitude = Convert.ToDecimal(lon),
                        WithinGeofence = reader.GetBoolean(dGeo),
                        DeviceInfo = dev,
                        ClientIp = ip,
                        StatusId = reader.GetInt32(dStatusId),
                        Remarks = rem,                     // <-- NEW
                        Address = addr,
                        ProofPath = proof
                    });
                }
            }

            // Total count is constant across page; read it from the first summary row if present.
            var total = summaries.Count > 0 ? summaries[0].TotalRecords = summaries[0].TotalRecords == 0
                ? // when not explicitly set, grab from first row of result-set #1 we cached:
                  // we cached oTotalRecords ordinal; but we've already moved the reader. So instead:
                  // the proc already repeats the same number on each row; we captured none.
                  // Safer: run a simple calc here:
                  // (we can't; so fallback: set to summaries.Count if the proc didn't set)
                  summaries.Count
                : summaries[0].TotalRecords
                : 0;

            // If your proc's first result set puts @TotalRecords in each row,
            // you can also recompute safely like this:
            if (summaries.Count > 0 && summaries[0].TotalRecords == 0)
            {
                // It’s fine to set it once; caller uses the PagedResult total.
                // If you want the exact value from the proc, add a third result set with just @TotalRecords and read it.
                total = summaries.Count;
            }

            return new PagedResult<DailyAttendanceSummaryDto>(summaries, total);
        }

        public async Task<AttendanceStatusChangeResult> SetGeoAttendanceStatusAsync(
      long managerId,
      string role,
      long employeeId,
      DateTime punchDate,
      int statusId,
      string? remarks,
      string timeZoneId,
      string lastUpdatedBy,               // <-- new parameter
      CancellationToken ct = default)
        {
            await using var conn = _context.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open) await conn.OpenAsync(ct);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "dbo.usp_ApproveGeoAttendance";
            cmd.CommandType = CommandType.StoredProcedure;

            SqlParameter P(string name, SqlDbType type, object? val, int? size = null)
            {
                var p = new SqlParameter(name, type) { Value = val ?? DBNull.Value };
                if (size.HasValue) p.Size = size.Value;
                return p;
            }

            cmd.Parameters.Add(P("@ManagerId", SqlDbType.BigInt, managerId));
            cmd.Parameters.Add(P("@Role", SqlDbType.NVarChar, role, 50));
            cmd.Parameters.Add(P("@EmployeeId", SqlDbType.BigInt, employeeId));
            cmd.Parameters.Add(P("@PunchDate", SqlDbType.Date, punchDate.Date));
            cmd.Parameters.Add(P("@StatusId", SqlDbType.Int, statusId));
            cmd.Parameters.Add(P("@Remarks", SqlDbType.NVarChar, remarks));
            cmd.Parameters.Add(P("@TimeZoneId", SqlDbType.NVarChar, timeZoneId, 64));
            cmd.Parameters.Add(P("@LastUpdatedBy", SqlDbType.NVarChar, lastUpdatedBy, 100)); // <-- new param

            var result = new AttendanceStatusChangeResult();
            try
            {
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                if (await reader.ReadAsync(ct))
                {
                    int Ord(string col) => reader.GetOrdinal(col);

                    result.RowsUpdated = reader.GetInt32(Ord("RowsUpdated"));
                    result.EmployeeId = reader.GetInt64(Ord("EmployeeId"));
                    result.PunchDate = reader.GetDateTime(Ord("PunchDate"));
                    result.StatusIdApplied = reader.GetInt32(Ord("StatusIdApplied"));
                    result.StatusNameApplied = reader.IsDBNull(Ord("StatusNameApplied"))
                        ? null : reader.GetString(Ord("StatusNameApplied"));
                }
            }
            catch (Exception ex) { 
            }

            return result;
        }


        #endregion
        #region Attendance fetch and export
        public async Task FetchAndSavePunchesAsyncold12may(CancellationToken cancellationToken = default)
        {
            string connStr = _configuration.GetConnectionString("DefaultConnection");
            const int batchSize = 50000;
            int totalRecordsProcessed = 0;

            try
            {
                _logger.LogInformation("Starting FetchAndSavePunchesAsync.");

                // Step 1: Truncate TempEmployeePunches
                using (var conn = new SqlConnection(connStr))
                {
                    await conn.OpenAsync(cancellationToken);
                    using var truncateCmd = new SqlCommand("TRUNCATE TABLE TempEmployeePunches", conn)
                    {
                        CommandTimeout = 30
                    };
                    await truncateCmd.ExecuteNonQueryAsync(cancellationToken);
                    _logger.LogInformation("Truncated TempEmployeePunches.");
                }

                // Step 2: Read from SP and write in batches
                using (var conn = new SqlConnection(connStr))
                {
                    await conn.OpenAsync(cancellationToken);
                    using var cmd = new SqlCommand("prc_Daily_Attendance", conn)
                    {
                        CommandType = CommandType.StoredProcedure,
                        CommandTimeout = 600
                    };

                    using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                    var dataTable = CreateDataTableSchema();
                    int batchRecords = 0;

                    while (await reader.ReadAsync(cancellationToken))
                    {
                        string userId = reader["UserID"]?.ToString();
                        DateTime punchDate = reader.GetDateTime(reader.GetOrdinal("PDate"));

                        if (string.IsNullOrEmpty(userId) || punchDate == default)
                        {
                            _logger.LogWarning("Skipping invalid record: UserID={UserID}, PunchDate={PunchDate}", userId, punchDate);
                            continue;
                        }

                        dataTable.Rows.Add(
                            userId,
                            punchDate.Date,
                            ValidatePunch(reader["Punch1"]?.ToString()),
                            ValidatePunch(reader["Punch2"]?.ToString()),
                            ValidatePunch(reader["Punch3"]?.ToString()),
                            ValidatePunch(reader["Punch4"]?.ToString()),
                            ValidatePunch(reader["Punch5"]?.ToString()),
                            ValidatePunch(reader["Punch6"]?.ToString()),
                            ValidatePunch(reader["Punch7"]?.ToString()),
                            ValidatePunch(reader["Punch8"]?.ToString()),
                            ValidatePunch(reader["Punch9"]?.ToString()),
                            ValidatePunch(reader["Punch10"]?.ToString()),
                            ValidatePunch(reader["Punch11"]?.ToString()),
                            ValidatePunch(reader["Punch12"]?.ToString()),
                            reader.GetInt32(reader.GetOrdinal("NoOfPunches")),
                           /* reader.GetDouble(reader.GetOrdinal("TotalHours"))*/
                           Convert.ToDouble(reader["TotalHours"] ?? 0),
                            "System",
                            DateTimeOffset.UtcNow,
                            "System"
                        );

                        batchRecords++;
                        totalRecordsProcessed++;

                        if (batchRecords >= batchSize)
                        {
                            await WriteBatchToDatabaseAsync(connStr, dataTable, cancellationToken);
                            _logger.LogInformation("Inserted {BatchRecords} records into TempEmployeePunches.", batchRecords);
                            dataTable.Rows.Clear();
                            batchRecords = 0;
                        }
                    }

                    if (batchRecords > 0)
                    {
                        await WriteBatchToDatabaseAsync(connStr, dataTable, cancellationToken);
                        _logger.LogInformation("Inserted {BatchRecords} records into TempEmployeePunches.", batchRecords);
                    }

                    if (totalRecordsProcessed == 0)
                    {
                        _logger.LogWarning("No valid records processed.");
                        return;
                    }
                }

                // Step 3: Merge TempEmployeePunches into tblEmployeeMultiPunches
                using (var conn = new SqlConnection(connStr))
                {
                    await conn.OpenAsync(cancellationToken);
                    await MergeBatchesAsync(conn, batchSize, cancellationToken);
                }

                _logger.LogInformation("Completed FetchAndSavePunchesAsync with {TotalRecords} records processed.", totalRecordsProcessed);
            }
            catch (SqlException ex) when (ex.Number == -2)
            {
                _logger.LogError("SQL Timeout: {Message}\nStackTrace: {StackTrace}", ex.Message, ex.StackTrace);
                throw new TimeoutException("Database operation timed out.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error in FetchAndSavePunchesAsync: {Message}\nStackTrace: {StackTrace}", ex.Message, ex.StackTrace);
                throw;
            }
        }
        public async Task FetchAndSavePunchesAsync(CancellationToken cancellationToken = default)
        {
            string connStr = _configuration.GetConnectionString("DefaultConnection");
            const int batchSize = 50000;
            int totalRecordsProcessed = 0;

            try
            {
                _logger.LogInformation("Starting FetchAndSavePunchesAsync for date: {Today}", DateTime.Today.ToString("yyyy-MM-dd"));

                // Step 1: Truncate TempEmployeePunches
                using (var conn = new SqlConnection(connStr))
                {
                    await conn.OpenAsync(cancellationToken);
                    using var truncateCmd = new SqlCommand("TRUNCATE TABLE TempEmployeePunches", conn)
                    {
                        CommandTimeout = 30
                    };
                    await truncateCmd.ExecuteNonQueryAsync(cancellationToken);
                    _logger.LogInformation("Truncated TempEmployeePunches.");
                }

                // Step 2: Read from SP and write in batches
                using (var conn = new SqlConnection(connStr))
                {
                    await conn.OpenAsync(cancellationToken);
                    using var cmd = new SqlCommand("prc_Daily_Attendance", conn)
                    {
                        CommandType = CommandType.StoredProcedure,
                        CommandTimeout = 0
                    };

                    using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                    var dataTable = CreateDataTableSchema();
                    int batchRecords = 0;

                    while (await reader.ReadAsync(cancellationToken))
                    {
                        string userId = reader["UserID"]?.ToString();
                        DateTime punchDate = reader.GetDateTime(reader.GetOrdinal("PDate"));

                        if (string.IsNullOrEmpty(userId) || punchDate == default)
                        {
                            _logger.LogWarning("Skipping invalid record: UserID={UserID}, PunchDate={PunchDate}", userId, punchDate);
                            continue;
                        }

                        // Validate that the punch date is today
                        //if (punchDate.Date != DateTime.Today)
                        //{
                        //    _logger.LogWarning("Skipping record with unexpected date: UserID={UserID}, PunchDate={PunchDate}", userId, punchDate);
                        //    continue;
                        //}

                        dataTable.Rows.Add(
                            userId,
                            punchDate.Date,
                            ValidatePunch(reader["Punch1"]?.ToString()),
                            ValidatePunch(reader["Punch2"]?.ToString()),
                            ValidatePunch(reader["Punch3"]?.ToString()),
                            ValidatePunch(reader["Punch4"]?.ToString()),
                            ValidatePunch(reader["Punch5"]?.ToString()),
                            ValidatePunch(reader["Punch6"]?.ToString()),
                            ValidatePunch(reader["Punch7"]?.ToString()),
                            ValidatePunch(reader["Punch8"]?.ToString()),
                            ValidatePunch(reader["Punch9"]?.ToString()),
                            ValidatePunch(reader["Punch10"]?.ToString()),
                            ValidatePunch(reader["Punch11"]?.ToString()),
                            ValidatePunch(reader["Punch12"]?.ToString()),
                            reader.GetInt32(reader.GetOrdinal("NoOfPunches")),
                            Convert.ToDouble(reader["TotalHours"] ?? "0"),
                            "System",
                            DateTimeOffset.UtcNow,
                            "System"
                        );

                        batchRecords++;
                        totalRecordsProcessed++;

                        if (batchRecords >= batchSize)
                        {
                            await WriteBatchToDatabaseAsync(connStr, dataTable, cancellationToken);
                            _logger.LogInformation("Inserted {BatchRecords} records into TempEmployeePunches.", batchRecords);
                            dataTable.Rows.Clear();
                            batchRecords = 0;
                        }
                    }

                    if (batchRecords > 0)
                    {
                        await WriteBatchToDatabaseAsync(connStr, dataTable, cancellationToken);
                        _logger.LogInformation("Inserted {BatchRecords} records into TempEmployeePunches.", batchRecords);
                    }

                    if (totalRecordsProcessed == 0)
                    {
                        _logger.LogWarning("No valid records processed for date: {Today}", DateTime.Today.ToString("yyyy-MM-dd"));
                        return;
                    }
                }

                // Step 3: Merge TempEmployeePunches into tblEmployeeMultiPunches
                using (var conn = new SqlConnection(connStr))
                {
                    await conn.OpenAsync(cancellationToken);
                    await MergeBatchesAsync(conn, batchSize, cancellationToken);
                }

                _logger.LogInformation("Completed FetchAndSavePunchesAsync with {TotalRecords} records processed for date: {Today}", totalRecordsProcessed, DateTime.Today.ToString("yyyy-MM-dd"));
            }
            catch (SqlException ex) when (ex.Number == -2)
            {
                _logger.LogError("SQL Timeout: {Message}\nStackTrace: {StackTrace}", ex.Message, ex.StackTrace);
                throw new TimeoutException("Database operation timed out.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error in FetchAndSavePunchesAsync: {Message}\nStackTrace: {StackTrace}", ex.Message, ex.StackTrace);
                throw;
            }
        }

        public async Task FetchAndSavePunchesRangeAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
        {
            string connStr = _configuration.GetConnectionString("DefaultConnection");
            const int batchSize = 50000;
            int totalRecordsProcessed = 0;

            try
            {
                _logger.LogInformation("Starting FetchAndSavePunchesRangeAsync for date range: {FromDate} to {ToDate}", fromDate.ToString("yyyy-MM-dd"), toDate.ToString("yyyy-MM-dd"));

                // Validate date range
                if (fromDate > toDate)
                {
                    _logger.LogError("Invalid date range: FromDate {FromDate} is greater than ToDate {ToDate}", fromDate.ToString("yyyy-MM-dd"), toDate.ToString("yyyy-MM-dd"));
                    throw new ArgumentException("FromDate cannot be greater than ToDate");
                }

                // Step 1: Truncate TempEmployeePunches
                using (var conn = new SqlConnection(connStr))
                {
                    await conn.OpenAsync(cancellationToken);
                    using var truncateCmd = new SqlCommand("TRUNCATE TABLE TempEmployeePunches", conn)
                    {
                        CommandTimeout = 30
                    };
                    await truncateCmd.ExecuteNonQueryAsync(cancellationToken);
                    _logger.LogInformation("Truncated TempEmployeePunches.");
                }

                // Step 2: Read from SP and write in batches
                using (var conn = new SqlConnection(connStr))
                {
                    await conn.OpenAsync(cancellationToken);
                    using var cmd = new SqlCommand("prc_Daily_Attendance_range", conn)
                    {
                        CommandType = CommandType.StoredProcedure,
                        CommandTimeout = 0
                    };

                    // Add parameters
                    cmd.Parameters.Add(new SqlParameter("@FromDate", SqlDbType.Date) { Value = fromDate.Date });
                    cmd.Parameters.Add(new SqlParameter("@ToDate", SqlDbType.Date) { Value = toDate.Date });

                    using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                    var dataTable = CreateDataTableSchema();
                    int batchRecords = 0;
                    bool columnsLogged = false;

                    while (await reader.ReadAsync(cancellationToken))
                    {
                        // Log available columns on first read for debugging
                        if (!columnsLogged)
                        {
                            var columnNames = new List<string>();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                columnNames.Add(reader.GetName(i));
                            }
                            _logger.LogInformation("Available columns from stored procedure: {Columns}", string.Join(", ", columnNames));
                            columnsLogged = true;
                        }

                        // Safely read UserID - handle DBNull and empty strings
                        string userId = null;
                        try
                        {
                            var userIDOrdinal = reader.GetOrdinal("UserID");
                            if (!reader.IsDBNull(userIDOrdinal))
                            {
                                userId = reader.GetString(userIDOrdinal)?.Trim();
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning("Error reading UserID column: {Error}. Available columns logged above.", ex.Message);
                        }

                        DateTime punchDate = default;
                        try
                        {
                            punchDate = reader.GetDateTime(reader.GetOrdinal("PDate"));
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning("Error reading PDate column: {Error}", ex.Message);
                        }

                        if (string.IsNullOrWhiteSpace(userId) || punchDate == default)
                        {
                            _logger.LogWarning("Skipping invalid record: UserID={UserID}, PunchDate={PunchDate}", userId ?? "NULL", punchDate);
                            continue;
                        }

                        // Validate that the punch date is within the requested range
                        if (punchDate.Date < fromDate.Date || punchDate.Date > toDate.Date)
                        {
                            _logger.LogWarning("Skipping record with date outside range: UserID={UserID}, PunchDate={PunchDate}", userId, punchDate);
                            continue;
                        }

                        dataTable.Rows.Add(
                            userId,
                            punchDate.Date,
                            ValidatePunch(reader["Punch1"]?.ToString()),
                            ValidatePunch(reader["Punch2"]?.ToString()),
                            ValidatePunch(reader["Punch3"]?.ToString()),
                            ValidatePunch(reader["Punch4"]?.ToString()),
                            ValidatePunch(reader["Punch5"]?.ToString()),
                            ValidatePunch(reader["Punch6"]?.ToString()),
                            ValidatePunch(reader["Punch7"]?.ToString()),
                            ValidatePunch(reader["Punch8"]?.ToString()),
                            ValidatePunch(reader["Punch9"]?.ToString()),
                            ValidatePunch(reader["Punch10"]?.ToString()),
                            ValidatePunch(reader["Punch11"]?.ToString()),
                            ValidatePunch(reader["Punch12"]?.ToString()),
                            reader.IsDBNull(reader.GetOrdinal("NoOfPunches")) ? 0 : reader.GetInt32(reader.GetOrdinal("NoOfPunches")),
                            reader.IsDBNull(reader.GetOrdinal("TotalHours")) ? 0.0 : Convert.ToDouble(reader["TotalHours"] ?? "0"),
                            "System",
                            DateTimeOffset.UtcNow,
                            "System"
                        );

                        batchRecords++;
                        totalRecordsProcessed++;

                        if (batchRecords >= batchSize)
                        {
                            await WriteBatchToDatabaseAsync(connStr, dataTable, cancellationToken);
                            _logger.LogInformation("Inserted {BatchRecords} records into TempEmployeePunches.", batchRecords);
                            dataTable.Rows.Clear();
                            batchRecords = 0;
                        }
                    }

                    if (batchRecords > 0)
                    {
                        await WriteBatchToDatabaseAsync(connStr, dataTable, cancellationToken);
                        _logger.LogInformation("Inserted {BatchRecords} records into TempEmployeePunches.", batchRecords);
                    }

                    if (totalRecordsProcessed == 0)
                    {
                        _logger.LogWarning("No valid records processed for date range: {FromDate} to {ToDate}", fromDate.ToString("yyyy-MM-dd"), toDate.ToString("yyyy-MM-dd"));
                        return;
                    }
                }

                // Step 3: Merge TempEmployeePunches into tblEmployeeMultiPunches
                using (var conn = new SqlConnection(connStr))
                {
                    await conn.OpenAsync(cancellationToken);
                    await MergeBatchesAsync(conn, batchSize, cancellationToken);
                }

                _logger.LogInformation("Completed FetchAndSavePunchesRangeAsync with {TotalRecords} records processed for date range: {FromDate} to {ToDate}", totalRecordsProcessed, fromDate.ToString("yyyy-MM-dd"), toDate.ToString("yyyy-MM-dd"));
            }
            catch (SqlException ex) when (ex.Number == -2)
            {
                _logger.LogError("SQL Timeout: {Message}\nStackTrace: {StackTrace}", ex.Message, ex.StackTrace);
                throw new TimeoutException("Database operation timed out.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error in FetchAndSavePunchesRangeAsync: {Message}\nStackTrace: {StackTrace}", ex.Message, ex.StackTrace);
                throw;
            }
        }
        public async Task FetchAndSavePunchesRangeByEcodeAsync(DateTime fromDate, DateTime toDate, string ecode, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(ecode))
                throw new ArgumentException("Ecode is required.", nameof(ecode));

            string connStr = _configuration.GetConnectionString("DefaultConnection");
            const int batchSize = 50000;
            int totalRecordsProcessed = 0;

            try
            {

                if (fromDate > toDate)
                    throw new ArgumentException("FromDate cannot be greater than ToDate.");

                using (var conn = new SqlConnection(connStr))
                {
                    await conn.OpenAsync(cancellationToken);
                    using var truncateCmd = new SqlCommand("TRUNCATE TABLE TempEmployeePunches", conn)
                    {
                        CommandTimeout = 30
                    };
                    await truncateCmd.ExecuteNonQueryAsync(cancellationToken);
                }

                using (var conn = new SqlConnection(connStr))
                {
                    await conn.OpenAsync(cancellationToken);
                    using var cmd = new SqlCommand("prc_Daily_Attendance_range", conn)
                    {
                        CommandType = CommandType.StoredProcedure,
                        CommandTimeout = 0
                    };

                    cmd.Parameters.Add(new SqlParameter("@FromDate", SqlDbType.Date) { Value = fromDate.Date });
                    cmd.Parameters.Add(new SqlParameter("@ToDate", SqlDbType.Date) { Value = toDate.Date });
                    cmd.Parameters.Add(new SqlParameter("@Ecode", SqlDbType.NVarChar, 50) { Value = ecode });

                    using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                    var dataTable = CreateDataTableSchema();
                    int batchRecords = 0;
                    bool columnsLogged = false;

                    while (await reader.ReadAsync(cancellationToken))
                    {
                        if (!columnsLogged)
                        {
                            var columnNames = new List<string>();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                columnNames.Add(reader.GetName(i));
                            }
                            _logger.LogInformation("Available columns from prc_Daily_Attendance_range: {Columns}", string.Join(", ", columnNames));
                            columnsLogged = true;
                        }

                        string userId = reader["UserID"]?.ToString()?.Trim();
                        DateTime punchDate = reader.GetDateTime(reader.GetOrdinal("PDate"));

                        if (string.IsNullOrWhiteSpace(userId) || punchDate == default)
                        {
                            _logger.LogWarning("Skipping invalid record: UserID={UserID}, PunchDate={PunchDate}", userId ?? "NULL", punchDate);
                            continue;
                        }

                        if (punchDate.Date < fromDate.Date || punchDate.Date > toDate.Date)
                        {
                            _logger.LogWarning("Skipping record outside requested range: UserID={UserID}, PunchDate={PunchDate}", userId, punchDate);
                            continue;
                        }

                        dataTable.Rows.Add(
                            userId,
                            punchDate.Date,
                            ValidatePunch(reader["Punch1"]?.ToString()),
                            ValidatePunch(reader["Punch2"]?.ToString()),
                            ValidatePunch(reader["Punch3"]?.ToString()),
                            ValidatePunch(reader["Punch4"]?.ToString()),
                            ValidatePunch(reader["Punch5"]?.ToString()),
                            ValidatePunch(reader["Punch6"]?.ToString()),
                            ValidatePunch(reader["Punch7"]?.ToString()),
                            ValidatePunch(reader["Punch8"]?.ToString()),
                            ValidatePunch(reader["Punch9"]?.ToString()),
                            ValidatePunch(reader["Punch10"]?.ToString()),
                            ValidatePunch(reader["Punch11"]?.ToString()),
                            ValidatePunch(reader["Punch12"]?.ToString()),
                            reader.IsDBNull(reader.GetOrdinal("NoOfPunches")) ? 0 : reader.GetInt32(reader.GetOrdinal("NoOfPunches")),
                            reader.IsDBNull(reader.GetOrdinal("TotalHours")) ? 0.0 : Convert.ToDouble(reader["TotalHours"] ?? "0"),
                            "System",
                            DateTimeOffset.UtcNow,
                            "System"
                        );

                        batchRecords++;
                        totalRecordsProcessed++;

                        if (batchRecords >= batchSize)
                        {
                            await WriteBatchToDatabaseAsync(connStr, dataTable, cancellationToken);
                            _logger.LogInformation("Inserted {BatchRecords} records into TempEmployeePunches.", batchRecords);
                            dataTable.Rows.Clear();
                            batchRecords = 0;
                        }
                    }

                    if (batchRecords > 0)
                    {
                        await WriteBatchToDatabaseAsync(connStr, dataTable, cancellationToken);
                        _logger.LogInformation("Inserted {BatchRecords} records into TempEmployeePunches.", batchRecords);
                    }

                    if (totalRecordsProcessed == 0)
                    {
                        _logger.LogWarning("No valid records processed for date range: {FromDate} to {ToDate} and Ecode {Ecode}", fromDate.ToString("yyyy-MM-dd"), toDate.ToString("yyyy-MM-dd"), ecode);
                        return;
                    }
                }

                using (var conn = new SqlConnection(connStr))
                {
                    await conn.OpenAsync(cancellationToken);
                    await MergeBatchesAsync(conn, batchSize, cancellationToken);
                }

                _logger.LogInformation("Completed FetchAndSavePunchesRangeByEcodeAsync with {TotalRecords} records processed for date range: {FromDate} to {ToDate} and Ecode {Ecode}", totalRecordsProcessed, fromDate.ToString("yyyy-MM-dd"), toDate.ToString("yyyy-MM-dd"), ecode);
            }
            catch (SqlException ex) when (ex.Number == -2)
            {
                _logger.LogError("SQL Timeout: {Message}\nStackTrace: {StackTrace}", ex.Message, ex.StackTrace);
                throw new TimeoutException("Database operation timed out.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error in FetchAndSavePunchesRangeByEcodeAsync: {Message}\nStackTrace: {StackTrace}", ex.Message, ex.StackTrace);
                throw;
            }
        }

        private async Task WriteBatchToDatabaseAsync(string connStr, System.Data.DataTable dataTable, CancellationToken cancellationToken)
        {
            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync(cancellationToken);

            using var bulkCopy = new SqlBulkCopy(conn, SqlBulkCopyOptions.KeepIdentity, null)
            {
                DestinationTableName = "TempEmployeePunches",
                BulkCopyTimeout = 120
            };
            ConfigureBulkCopyMappings(bulkCopy);
            await bulkCopy.WriteToServerAsync(dataTable, cancellationToken);
        }

        private async Task MergeBatchesAsync(SqlConnection conn, int batchSize, CancellationToken cancellationToken)
        {
            // Count distinct UserID + PunchDate combinations (after grouping)
            using var countCmd = new SqlCommand(@"
                SELECT COUNT(*) 
                FROM (
                    SELECT UserID, PunchDate 
                    FROM TempEmployeePunches 
                    GROUP BY UserID, PunchDate
                ) AS GroupedData", conn)
            {
                CommandTimeout = 30
            };
            int totalRows = (int)await countCmd.ExecuteScalarAsync(cancellationToken);

            for (int offset = 0; offset < totalRows; offset += batchSize)
            {
                using var transaction = await conn.BeginTransactionAsync(cancellationToken);
                try
                {
                    using var mergeCmd = new SqlCommand(@"
                MERGE INTO tblEmployeeMultiPunches AS target
                USING (
                    SELECT 
                        UserID,
                        PunchDate,
                        MAX(Punch1) AS Punch1,
                        MAX(Punch2) AS Punch2,
                        MAX(Punch3) AS Punch3,
                        MAX(Punch4) AS Punch4,
                        MAX(Punch5) AS Punch5,
                        MAX(Punch6) AS Punch6,
                        MAX(Punch7) AS Punch7,
                        MAX(Punch8) AS Punch8,
                        MAX(Punch9) AS Punch9,
                        MAX(Punch10) AS Punch10,
                        MAX(Punch11) AS Punch11,
                        MAX(Punch12) AS Punch12,
                        MAX(NoOfPunches) AS NoOfPunches,
                        MAX(TotalHours) AS TotalHours,
                        MAX(CreatedBy) AS CreatedBy,
                        MAX(CreatedOn) AS CreatedOn,
                        MAX(LastUpdatedBy) AS LastUpdatedBy
                    FROM TempEmployeePunches
                    GROUP BY UserID, PunchDate
                    ORDER BY UserID, PunchDate
                    OFFSET @Offset ROWS FETCH NEXT @BatchSize ROWS ONLY
                ) AS source
                ON target.UserID = source.UserID AND target.PunchDate = source.PunchDate
                WHEN MATCHED THEN
                    UPDATE SET
                        Punch1 = source.Punch1,
                        Punch2 = source.Punch2,
                        Punch3 = source.Punch3,
                        Punch4 = source.Punch4,
                        Punch5 = source.Punch5,
                        Punch6 = source.Punch6,
                        Punch7 = source.Punch7,
                        Punch8 = source.Punch8,
                        Punch9 = source.Punch9,
                        Punch10 = source.Punch10,
                        Punch11 = source.Punch11,
                        Punch12 = source.Punch12,
                        NoOfPunches = source.NoOfPunches,
                        TotalHours = source.TotalHours,
                        LastUpdatedBy = source.LastUpdatedBy,
                        CreatedOn = source.CreatedOn
                WHEN NOT MATCHED THEN
                    INSERT (
                        UserID, PunchDate, Punch1, Punch2, Punch3, Punch4, Punch5, Punch6, 
                        Punch7, Punch8, Punch9, Punch10, Punch11, Punch12, 
                        NoOfPunches, TotalHours, CreatedBy, CreatedOn, LastUpdatedBy
                    )
                    VALUES (
                        source.UserID, source.PunchDate, source.Punch1, source.Punch2, source.Punch3, 
                        source.Punch4, source.Punch5, source.Punch6, source.Punch7, source.Punch8, 
                        source.Punch9, source.Punch10, source.Punch11, source.Punch12, 
                        source.NoOfPunches, source.TotalHours, source.CreatedBy, source.CreatedOn, source.LastUpdatedBy
                    );", conn, (SqlTransaction)transaction)
                    {
                        CommandTimeout = 120
                    };

                    mergeCmd.Parameters.AddWithValue("@Offset", offset);
                    mergeCmd.Parameters.AddWithValue("@BatchSize", batchSize);

                    await mergeCmd.ExecuteNonQueryAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw;
                }
            }
        }

        private static System.Data.DataTable CreateDataTableSchema()
        {
            var dt = new System.Data.DataTable();
            // dt.Columns.Add("Machine_Type", typeof(string));
            dt.Columns.Add("UserID", typeof(string));
            dt.Columns.Add("PunchDate", typeof(DateTime));
            dt.Columns.Add("Punch1", typeof(string));
            dt.Columns.Add("Punch2", typeof(string));
            dt.Columns.Add("Punch3", typeof(string));
            dt.Columns.Add("Punch4", typeof(string));
            dt.Columns.Add("Punch5", typeof(string));
            dt.Columns.Add("Punch6", typeof(string));
            dt.Columns.Add("Punch7", typeof(string));
            dt.Columns.Add("Punch8", typeof(string));
            dt.Columns.Add("Punch9", typeof(string));
            dt.Columns.Add("Punch10", typeof(string));
            dt.Columns.Add("Punch11", typeof(string));
            dt.Columns.Add("Punch12", typeof(string));
            dt.Columns.Add("NoOfPunches", typeof(int));
            dt.Columns.Add("TotalHours", typeof(double));
            dt.Columns.Add("CreatedBy", typeof(string));
            dt.Columns.Add("CreatedOn", typeof(DateTimeOffset));
            dt.Columns.Add("LastUpdatedBy", typeof(string));
            return dt;
        }

        private static void ConfigureBulkCopyMappings(SqlBulkCopy bulkCopy)
        {
            //   bulkCopy.ColumnMappings.Add("Machine_Type", "Machine_Type");
            bulkCopy.ColumnMappings.Add("UserID", "UserID");
            bulkCopy.ColumnMappings.Add("PunchDate", "PunchDate");
            bulkCopy.ColumnMappings.Add("Punch1", "Punch1");
            bulkCopy.ColumnMappings.Add("Punch2", "Punch2");
            bulkCopy.ColumnMappings.Add("Punch3", "Punch3");
            bulkCopy.ColumnMappings.Add("Punch4", "Punch4");
            bulkCopy.ColumnMappings.Add("Punch5", "Punch5");
            bulkCopy.ColumnMappings.Add("Punch6", "Punch6");
            bulkCopy.ColumnMappings.Add("Punch7", "Punch7");
            bulkCopy.ColumnMappings.Add("Punch8", "Punch8");
            bulkCopy.ColumnMappings.Add("Punch9", "Punch9");
            bulkCopy.ColumnMappings.Add("Punch10", "Punch10");
            bulkCopy.ColumnMappings.Add("Punch11", "Punch11");
            bulkCopy.ColumnMappings.Add("Punch12", "Punch12");
            bulkCopy.ColumnMappings.Add("NoOfPunches", "NoOfPunches");
            bulkCopy.ColumnMappings.Add("TotalHours", "TotalHours");
            bulkCopy.ColumnMappings.Add("CreatedBy", "CreatedBy");
            bulkCopy.ColumnMappings.Add("CreatedOn", "CreatedOn");
            bulkCopy.ColumnMappings.Add("LastUpdatedBy", "LastUpdatedBy");
        }

        private static string ValidatePunch(string punch)
        {
            if (string.IsNullOrEmpty(punch) || punch == "00:00:00") return punch;
            return punch.Length <= 8 && TimeSpan.TryParse(punch, out _) ? punch : "";
        }
        public async Task<List<PunchFetchDto>> FetchPunchesRange(DateTime fromDate, DateTime toDate, string? ecode)
        {
            List<PunchFetchDto> punches = new();

            using (var connection = _context.Database.GetDbConnection())
            {
                await connection.OpenAsync();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM fn_GetMonthlyPunchesRange_productionnewnick(@FromDate, @ToDate, @ECode)";
                    command.CommandType = CommandType.Text;

                    var fromDateParam = command.CreateParameter();
                    fromDateParam.ParameterName = "@FromDate";
                    fromDateParam.Value = fromDate;
                    command.Parameters.Add(fromDateParam);

                    var toDateParam = command.CreateParameter();
                    toDateParam.ParameterName = "@ToDate";
                    toDateParam.Value = toDate;
                    command.Parameters.Add(toDateParam);

                    var ecodeParam = command.CreateParameter();
                    ecodeParam.ParameterName = "@ECode";
                    ecodeParam.Value = string.IsNullOrWhiteSpace(ecode) ? DBNull.Value : (object)ecode;
                    command.Parameters.Add(ecodeParam);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            TimeSpan? GetNullableTimeSpan(string columnName)
                            {
                                if (reader.IsDBNull(reader.GetOrdinal(columnName)))
                                    return null;
                                string value = reader[columnName].ToString();
                                if (string.IsNullOrEmpty(value) || value == "00:00:00")
                                    return null;
                                try
                                {
                                    return TimeSpan.Parse(value);
                                }
                                catch (FormatException)
                                {
                                    return null;
                                }
                            }

                            string GetPunchTime(string columnName)
                            {
                                if (reader.IsDBNull(reader.GetOrdinal(columnName)))
                                    return "00:00:00";
                                string value = reader[columnName].ToString();
                                return string.IsNullOrEmpty(value) || value == "00:00:00" ? "00:00:00" : value;
                            }

                            punches.Add(new PunchFetchDto
                            {

                                EmployeeId = reader.IsDBNull(reader.GetOrdinal("EmployeeId"))
                                    ? 0
                                    : Convert.ToInt32(reader["EmployeeId"]),
                                EmployeeName = reader["EmployeeName"]?.ToString() ?? "NA",
                                ECode = reader["ECode"]?.ToString() ?? string.Empty,
                                AttendanceDate = reader.IsDBNull(reader.GetOrdinal("AttendanceDate"))
                                    ? null
                                    : Convert.ToDateTime(reader["AttendanceDate"]),
                                MachineType = reader["Machine_Type"]?.ToString() ?? "N/A",
                                DesignationName = reader["DesignationName"]?.ToString() ?? "N/A",
                                LocationName = reader["LocationName"]?.ToString() ?? "N/A",
                                STCode = reader["STCode"]?.ToString() ?? "N/A",
                                DepartmentName = reader["DepartmentName"]?.ToString() ?? "N/A",
                                Punch1 = GetPunchTime("Punch1"),
                                Punch2 = GetPunchTime("Punch2"),
                                Punch3 = GetPunchTime("Punch3"),
                                Punch4 = GetPunchTime("Punch4"),
                                Punch5 = GetPunchTime("Punch5"),
                                Punch6 = GetPunchTime("Punch6"),
                                Punch7 = GetPunchTime("Punch7"),
                                Punch8 = GetPunchTime("Punch8"),
                                Punch9 = GetPunchTime("Punch9"),
                                Punch10 = GetPunchTime("Punch10"),
                                Punch11 = GetPunchTime("Punch11"),
                                Punch12 = GetPunchTime("Punch12"),
                                PunchIn = GetNullableTimeSpan("PunchIn"),
                                PunchOut = GetNullableTimeSpan("PunchOut"),
                                RegularizePunchIn = GetPunchTime("RegularizePunchIn"),
                                RegularizePuncOut = GetPunchTime("RegularizePuncOut"),
                                IsRegularize = reader.IsDBNull(reader.GetOrdinal("IsRegularize"))
                                    ? false
                                    : Convert.ToBoolean(reader["IsRegularize"]),

                                TotalWorkingMinutes = reader["TotalWorkingMinutes"]?.ToString() ?? "0 hours and 00 minutes",
                                Status = reader["Status"]?.ToString() ?? "Absent",
                                LateMinutes = reader.IsDBNull(reader.GetOrdinal("LateMinutes"))
                                    ? 0
                                    : Convert.ToInt32(reader["LateMinutes"]),
                                EarlyMinutes = reader.IsDBNull(reader.GetOrdinal("EarlyMinutes"))
                                    ? 0
                                    : Convert.ToInt32(reader["EarlyMinutes"]),
                                TotalMonthlyWorkingHours = reader["TotalMonthlyWorkingHours"]?.ToString() ?? "0 hours and 00 minutes",
                                TotalWorkingDays = (int?)(reader.IsDBNull(reader.GetOrdinal("TotalWorkingDays"))
                                    ? 0.0
                                    : Convert.ToDouble(reader["TotalWorkingDays"]))
                            });
                        }
                    }
                }
            }

            return punches;
        }

        public async Task<List<MultiPunchAttendanceDto>> FetchPunchesRangeByEcodeAsync(DateTime fromDate, DateTime toDate, string ecode)
        {
            if (string.IsNullOrWhiteSpace(ecode))
                throw new ArgumentException("Ecode is required.", nameof(ecode));

            if (fromDate > toDate)
                throw new ArgumentException("FromDate cannot be greater than ToDate.");

            var records = new List<MultiPunchAttendanceDto>();

            using var connection = _context.Database.GetDbConnection();
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = "prc_Daily_Attendance_range";
            command.CommandType = CommandType.StoredProcedure;

            var fromParam = command.CreateParameter();
            fromParam.ParameterName = "@FromDate";
            fromParam.Value = fromDate.Date;
            command.Parameters.Add(fromParam);

            var toParam = command.CreateParameter();
            toParam.ParameterName = "@ToDate";
            toParam.Value = toDate.Date;
            command.Parameters.Add(toParam);

            var ecodeParam = command.CreateParameter();
            ecodeParam.ParameterName = "@Ecode";
            ecodeParam.Value = ecode;
            command.Parameters.Add(ecodeParam);

            using var reader = await command.ExecuteReaderAsync();

            string ReadPunch(string columnName)
            {
                var ordinal = reader.GetOrdinal(columnName);
                if (reader.IsDBNull(ordinal))
                    return string.Empty;
                var value = reader.GetValue(ordinal)?.ToString();
                return string.IsNullOrWhiteSpace(value) ? string.Empty : value;
            }

            while (await reader.ReadAsync())
            {
                var punchDate = reader.GetDateTime(reader.GetOrdinal("PDate"));

                records.Add(new MultiPunchAttendanceDto
                {
                    UserId = reader["UserID"]?.ToString() ?? string.Empty,
                    PunchDate = punchDate,
                    Punch1 = ReadPunch("Punch1"),
                    Punch2 = ReadPunch("Punch2"),
                    Punch3 = ReadPunch("Punch3"),
                    Punch4 = ReadPunch("Punch4"),
                    Punch5 = ReadPunch("Punch5"),
                    Punch6 = ReadPunch("Punch6"),
                    Punch7 = ReadPunch("Punch7"),
                    Punch8 = ReadPunch("Punch8"),
                    Punch9 = ReadPunch("Punch9"),
                    Punch10 = ReadPunch("Punch10"),
                    Punch11 = ReadPunch("Punch11"),
                    Punch12 = ReadPunch("Punch12"),
                    NoOfPunches = reader.IsDBNull(reader.GetOrdinal("NoOfPunches")) ? 0 : reader.GetInt32(reader.GetOrdinal("NoOfPunches")),
                    TotalHours = reader["TotalHours"]?.ToString() ?? "0.00"
                });
            }

            return records;
        }
            public async Task<(List<EmployeeAttendanceDetailDto> Employees, int TotalCount, int CurrentPageNumber, int ActiveCount, int InactiveCount, int AbscondCount, int LocCount)> GetEmployeeAttendanceDetailsAsync(
            int pageNumber = 1,
            int pageSize = 10,
            string? searchTerm = null,
            string mode = "all",
            string? managerId = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int? month = null,
            int? year = null)
        {
            string? monthYear = null;
            if (month.HasValue && year.HasValue)
            {
                var monthName = new DateTime(year.Value, month.Value, 1).ToString("MMM");
                var yearShort = year.Value.ToString().Substring(2); // e.g. 2025 → "25"
                monthYear = $"{monthName}-{yearShort}";
            }
            // If month/year provided, derive date range when from/to not supplied
            if ((!fromDate.HasValue || !toDate.HasValue) && month.HasValue && year.HasValue)
            {
                var start = new DateTime(year.Value, month.Value, 1);
                var end = start.AddMonths(1).AddDays(-1);
                fromDate ??= start;
                toDate ??= end;
            }

            var employees = new List<EmployeeAttendanceDetailDto>();

            await using var connection = _context.Database.GetDbConnection();
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "GetEmployeeDetailsWithCards_Attendance_Test"; // Stored Procedure name expected
            command.CommandType = CommandType.StoredProcedure;
            command.CommandTimeout = 0;
            // Inputs
            command.Parameters.Add(new SqlParameter("@PageNumber", SqlDbType.Int) { Value = pageNumber });
            command.Parameters.Add(new SqlParameter("@PageSize", SqlDbType.Int) { Value = pageSize });
            //command.Parameters.Add(new SqlParameter("@SearchTerm", SqlDbType.NVarChar, 100) { Value = searchTerm ?? string.Empty });
            //command.Parameters.Add(new SqlParameter("@Mode", SqlDbType.NVarChar, 10) { Value = mode ?? "all" });
            command.Parameters.Add(new SqlParameter("@ManagerId", SqlDbType.NVarChar, 10) { Value = managerId ?? string.Empty });
            command.Parameters.Add(new SqlParameter("@MonthYear", SqlDbType.NVarChar, 10)
            {
                Value = (object?)monthYear ?? DBNull.Value
            });
            //command.Parameters.Add(new SqlParameter("@FromDate", SqlDbType.Date) { Value = (object?)fromDate ?? DBNull.Value });
            //command.Parameters.Add(new SqlParameter("@ToDate", SqlDbType.Date) { Value = (object?)toDate ?? DBNull.Value });

            // Outputs
            //var totalEmployeesParam = new SqlParameter("@TotalEmployees", SqlDbType.Int) { Direction = ParameterDirection.Output };
            //var currentPageNumberParam = new SqlParameter("@CurrentPageNumber", SqlDbType.Int) { Direction = ParameterDirection.Output };
            //var activeCountParam = new SqlParameter("@ActiveCount", SqlDbType.Int) { Direction = ParameterDirection.Output };
            //var inactiveCountParam = new SqlParameter("@InactiveCount", SqlDbType.Int) { Direction = ParameterDirection.Output };
            //var abscondCountParam = new SqlParameter("@AbscondCount", SqlDbType.Int) { Direction = ParameterDirection.Output };
            //var locCountParam = new SqlParameter("@LocCount", SqlDbType.Int) { Direction = ParameterDirection.Output };

            //command.Parameters.Add(totalEmployeesParam);
            //command.Parameters.Add(currentPageNumberParam);
            //command.Parameters.Add(activeCountParam);
            //command.Parameters.Add(inactiveCountParam);
            //command.Parameters.Add(abscondCountParam);
            //command.Parameters.Add(locCountParam);

            int GetInt(IDataRecord r, string name)
                => r.IsDBNull(r.GetOrdinal(name)) ? 0 : Convert.ToInt32(r[name]);
            long GetLong(IDataRecord r, string name)
                => r.IsDBNull(r.GetOrdinal(name)) ? 0L : Convert.ToInt64(r[name]);
            decimal? GetDecimalNullable(IDataRecord r, string name)
                => r.IsDBNull(r.GetOrdinal(name)) ? (decimal?)null : Convert.ToDecimal(r[name]);
            DateTime? GetDateTimeNullable(IDataRecord r, string name)
                => r.IsDBNull(r.GetOrdinal(name)) ? (DateTime?)null : Convert.ToDateTime(r[name]);
            string GetString(IDataRecord r, string name)
                => r.IsDBNull(r.GetOrdinal(name)) ? null : r[name].ToString();
            bool GetBool(IDataRecord r, string name)
                => !r.IsDBNull(r.GetOrdinal(name)) && Convert.ToBoolean(r[name]);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var item = new EmployeeAttendanceDetailDto
                {
                    ZoneName = GetString(reader, "ZoneName"),
                    RegionName = GetString(reader, "RegionName"),
                    ClusterName = GetString(reader, "ClusterName"),
                    STCode = GetString(reader, "STCode"),
                    LocationName = GetString(reader, "LocationName"),
                    Ecode = GetString(reader, "Ecode"),
                    FullName = GetString(reader, "FullName"),
                    Gender = GetString(reader, "Gender"),
                    DOB = GetDateTimeNullable(reader, "DOB"),
                    AgeInYears = GetDecimalNullable(reader, "AgeInYears"),
                    DepartmentId = GetString(reader, "DepartmentId"),
                    DesignationId = GetString(reader, "DesignationId"),
                    DepartmentName = GetString(reader, "DepartmentName"),
                    DesignationName = GetString(reader, "DesignationName"),
                    DOJ = GetDateTimeNullable(reader, "DOJ"),
                    ResignationTypeName = GetString(reader, "ResignationTypeName"),
                    DateOfLeft = GetDateTimeNullable(reader, "DateOfLeft"),
                    BankName = GetString(reader, "BANK NAME"),
                    AccountNumber = GetString(reader, "A/C NO"),
                    BankIfscCode = GetString(reader, "BANK IFSC CODE"),
                    PermanentAddress = GetString(reader, "PERMANENT ADDRESS"),
                    PermanentAddressPinCode = GetString(reader, "PERMANENT ADDRESS PIN CODE"),
                    PresentAddress = GetString(reader, "PRESENT ADDRESS"),
                    PresentAddressPinCode = GetString(reader, "PRESENT ADDRESS PIN CODE"),
                    Mobile = GetString(reader, "MOBILE"),
                    EmailAddress = GetString(reader, "EMAIL ADDRESS"),
                    AadharNo = GetString(reader, "AADHAR NO"),
                    PanNo = GetString(reader, "PAN NO"),
                    HighestQualification = GetString(reader, "HIGHEST QUALIFICATION"),
                    FatherName = GetString(reader, "FATHER'S NAME"),
                    MotherName = GetString(reader, "MOTHER'S NAME"),
                    MaritalStatus = GetString(reader, "MARITIAL STATUS"),
                    ReportHeadEcode = GetString(reader, "ReportHeadEcode"),
                    ReportHeadFullName = GetString(reader, "ReportHeadFullName"),
                    ReportHeadDesignation = GetString(reader, "ReportHeadDesignation"),
                    CompanyName1 = GetString(reader, "COMPANY NAME-1"),
                    From1 = GetString(reader, "From-I"),
                    To1 = GetString(reader, "To-I"),
                    Years1 = GetDecimalNullable(reader, "YEARS-1"),
                    CompanyName2 = GetString(reader, "COMPANY NAME-2"),
                    From2 = GetString(reader, "From-II"),
                    To2 = GetString(reader, "To-II"),
                    Years2 = GetDecimalNullable(reader, "YEARS-2"),
                    CompanyName3 = GetString(reader, "COMPANY NAME-3"),
                    From3 = GetString(reader, "From-III"),
                    To3 = GetString(reader, "To-III"),
                    Years3 = GetDecimalNullable(reader, "YEARS-3"),
                    TotalExperience = GetDecimalNullable(reader, "TTL EXPERIENCE"),
                    LocStatus = reader.IsDBNull(reader.GetOrdinal("LocStatus")) ? (bool?)null : Convert.ToBoolean(reader["LocStatus"]),
                    EmployeeStatus = GetString(reader, "EmployeeStatus"),
                    EmployeeId = GetLong(reader, "EmployeeId"),
                    CandidateId = GetLong(reader, "CandidateId"),
                    LocBasedECode = GetString(reader, "LocBasedECode"),
                    IsActive = GetBool(reader, "IsActive"),
                    IsDeleted = GetBool(reader, "IsDeleted"),
                    DateOfJoining = GetDateTimeNullable(reader, "DateOfJoining"),
                    IsStore = GetBool(reader, "IsStore"),
                    CreatedOn = GetDateTimeNullable(reader, "CreatedOn"),
                    UpdatedOn = GetDateTimeNullable(reader, "UpdatedOn"),
                    CreatedBy = GetString(reader, "CreatedBy"),
                    UpdatedBy = GetString(reader, "UpdatedBy"),
                    PresentDays = GetInt(reader, "PresentDays"),
                    HalfDays = GetInt(reader, "HalfDays"),
                    AbsentDays = GetInt(reader, "AbsentDays"),
                    LeaveDays = GetInt(reader, "LeaveDays"),
                    MisPunchDays = GetInt(reader, "MisPunchDays"),
                    RegularisationDays = GetInt(reader, "RegularisationDays"),
                    TotalWorkingDays = GetDecimalNullable(reader, "TotalWorkingDays"),
                    TotalCalendarRows = GetInt(reader, "TotalCalendarRows"),
                    NonWorkingRows = GetInt(reader, "NonWorkingRows"),
                    TotalMonthlyWorkingHours = GetString(reader, "TotalMonthlyWorkingHours"),

                    // Attendance Count Approval fields
                    AttendanceCountApprovalId = reader.IsDBNull(reader.GetOrdinal("AttendanceCountApprovalId"))
                        ? (long?)null
                        : Convert.ToInt64(reader["AttendanceCountApprovalId"]),
                    ApprovalMonthYear = GetString(reader, "ApprovalMonthYear"),
                    ApprovalAttendanceCount = reader.IsDBNull(reader.GetOrdinal("ApprovalAttendanceCount"))
                        ? (int?)null
                        : Convert.ToInt32(reader["ApprovalAttendanceCount"]),
                    EmployeeRemarks = GetString(reader, "EmployeeRemarks"),
                    ApprovalStatus = GetString(reader, "ApprovalStatus"),
                    ApprovalStatusDescription = GetString(reader, "ApprovalStatusDescription"),
                    IsCMApproved = reader.IsDBNull(reader.GetOrdinal("IsCMApproved"))
                        ? (bool?)null
                        : Convert.ToBoolean(reader["IsCMApproved"]),
                    CMApprovedBy = GetString(reader, "CMApprovedBy"),
                    CMApprovedOn = GetDateTimeNullable(reader, "CMApprovedOn"),
                    CMRemarks = GetString(reader, "CMRemarks"),
                    IsRMApproved = reader.IsDBNull(reader.GetOrdinal("IsRMApproved"))
                        ? (bool?)null
                        : Convert.ToBoolean(reader["IsRMApproved"]),
                    RMApprovedBy = GetString(reader, "RMApprovedBy"),
                    RMApprovedOn = GetDateTimeNullable(reader, "RMApprovedOn"),
                    RMRemarks = GetString(reader, "RMRemarks"),
                    AttachmentCount = reader.IsDBNull(reader.GetOrdinal("AttachmentCount"))
                        ? (int?)null
                        : Convert.ToInt32(reader["AttachmentCount"]),
                    AttachmentFilePaths = GetString(reader, "AttachmentFilePaths")
                };

                employees.Add(item);
            }

            //int total = Convert.ToInt32(totalEmployeesParam.Value == DBNull.Value ? 0 : totalEmployeesParam.Value);
            //int currentPage = Convert.ToInt32(currentPageNumberParam.Value == DBNull.Value ? 0 : currentPageNumberParam.Value);
            //int active = Convert.ToInt32(activeCountParam.Value == DBNull.Value ? 0 : activeCountParam.Value);
            //int inactive = Convert.ToInt32(inactiveCountParam.Value == DBNull.Value ? 0 : inactiveCountParam.Value);
            //int abscond = Convert.ToInt32(abscondCountParam.Value == DBNull.Value ? 0 : abscondCountParam.Value);
            //int loc = Convert.ToInt32(locCountParam.Value == DBNull.Value ? 0 : locCountParam.Value);
            return (employees, employees.Count, pageNumber, 0, 0, 0, 0);
            //return (employees, total, currentPage, active, inactive, abscond, loc);
        }
        public async Task<List<PunchFetchDto>> FetchPunchesRangeExcel(DateTime fromDate, DateTime toDate, string? ecode)
        {
            var punches = new List<PunchFetchDto>();

            const string sql = @"
        SELECT
              EmpAttendanceId
            , EmployeeId
            , EmployeeName
            , ECode
            , AttendanceDate
            , Machine_Type          AS MachineType
            , DesignationName
            , LocationName
            , STCode
            , DepartmentName
            , ShiftName
            , ShiftStartTime
            , ShiftEndTime
            , IsHoliday
            , HolidayName
            , PunchIn
            , PunchOut
            , Punch1
            , Punch2
            , Punch3
            , Punch4
            , Punch5
            , Punch6
            , Punch7
            , Punch8
            , Punch9
            , Punch10
            , Punch11
            , Punch12
            , ValidPunchCount
            , RegularizePunchIn
            , RegularizePuncOut
            , IsRegularize
            , IsOnLeave
            , TotalWorkingMinutes
            , LateMinutes
            , EarlyMinutes
            , Status
            , TotalWorkingDays
            , TotalMonthlyWorkingHours
        FROM dbo.tbl_fn_GetMonthlyPunchesRange_productionnewnick_test WITH (NOLOCK)
        WHERE AttendanceDate BETWEEN @FromDate AND @ToDate
          AND (@ECode IS NULL OR ECode = @ECode);";

            using (var connection = _context.Database.GetDbConnection())
            {
                await connection.OpenAsync();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    command.CommandType = CommandType.Text;

                    var fromDateParam = command.CreateParameter();
                    fromDateParam.ParameterName = "@FromDate";
                    fromDateParam.Value = fromDate;
                    command.Parameters.Add(fromDateParam);

                    var toDateParam = command.CreateParameter();
                    toDateParam.ParameterName = "@ToDate";
                    toDateParam.Value = toDate;
                    command.Parameters.Add(toDateParam);

                    var ecodeParam = command.CreateParameter();
                    ecodeParam.ParameterName = "@ECode";
                    ecodeParam.Value = string.IsNullOrWhiteSpace(ecode) ? DBNull.Value : (object)ecode;
                    command.Parameters.Add(ecodeParam);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        TimeSpan? ReadNullableTimeSpan(string columnName)
                        {
                            var ordinal = reader.GetOrdinal(columnName);
                            if (reader.IsDBNull(ordinal))
                                return null;

                            var value = reader.GetValue(ordinal);

                            // If SQL type is TIME already
                            if (value is TimeSpan ts)
                                return ts;

                            var s = value.ToString();
                            if (string.IsNullOrEmpty(s) || s == "00:00:00")
                                return null;

                            return TimeSpan.TryParse(s, out var parsed) ? parsed : (TimeSpan?)null;
                        }

                        string ReadPunchString(string columnName)
                        {
                            var ordinal = reader.GetOrdinal(columnName);
                            if (reader.IsDBNull(ordinal))
                                return "00:00:00";

                            var s = reader.GetValue(ordinal)?.ToString() ?? "00:00:00";
                            return string.IsNullOrEmpty(s) ? "00:00:00" : s;
                        }

                        while (await reader.ReadAsync())
                        {
                            punches.Add(new PunchFetchDto
                            {
                                // if PunchFetchDto has EmpAttendanceId, set it; otherwise you can drop this
                                EmployeeId = reader.IsDBNull(reader.GetOrdinal("EmployeeId"))
                                    ? 0
                                    : Convert.ToInt32(reader["EmployeeId"]),

                                EmployeeName = reader["EmployeeName"]?.ToString() ?? "NA",
                                ECode = reader["ECode"]?.ToString() ?? string.Empty,

                                AttendanceDate = reader.IsDBNull(reader.GetOrdinal("AttendanceDate"))
                                    ? null
                                    : Convert.ToDateTime(reader["AttendanceDate"]),

                                MachineType = reader["MachineType"]?.ToString() ?? "N/A",
                                DesignationName = reader["DesignationName"]?.ToString() ?? "N/A",
                                LocationName = reader["LocationName"]?.ToString() ?? "N/A",
                                STCode = reader["STCode"]?.ToString() ?? "N/A",
                                DepartmentName = reader["DepartmentName"]?.ToString() ?? "N/A",

                                // Punch strings (00:00:00 means "no punch")
                                Punch1 = ReadPunchString("Punch1"),
                                Punch2 = ReadPunchString("Punch2"),
                                Punch3 = ReadPunchString("Punch3"),
                                Punch4 = ReadPunchString("Punch4"),
                                Punch5 = ReadPunchString("Punch5"),
                                Punch6 = ReadPunchString("Punch6"),
                                Punch7 = ReadPunchString("Punch7"),
                                Punch8 = ReadPunchString("Punch8"),
                                Punch9 = ReadPunchString("Punch9"),
                                Punch10 = ReadPunchString("Punch10"),
                                Punch11 = ReadPunchString("Punch11"),
                                Punch12 = ReadPunchString("Punch12"),

                                PunchIn = ReadNullableTimeSpan("PunchIn"),
                                PunchOut = ReadNullableTimeSpan("PunchOut"),

                                RegularizePunchIn = reader["RegularizePunchIn"]?.ToString(),
                                RegularizePuncOut = reader["RegularizePuncOut"]?.ToString(),

                                IsRegularize = !reader.IsDBNull(reader.GetOrdinal("IsRegularize")) &&
                                               Convert.ToBoolean(reader["IsRegularize"]),

                                TotalWorkingMinutes = reader["TotalWorkingMinutes"]?.ToString() ?? "00:00",
                                Status = reader["Status"]?.ToString() ?? "Absent",
                                LateMinutes = reader.IsDBNull(reader.GetOrdinal("LateMinutes")) ? 0 : Convert.ToInt32(reader["LateMinutes"]),
                                EarlyMinutes = reader.IsDBNull(reader.GetOrdinal("EarlyMinutes")) ? 0 : Convert.ToInt32(reader["EarlyMinutes"]),
                                TotalMonthlyWorkingHours = reader["TotalMonthlyWorkingHours"]?.ToString() ?? "0 hours and 00 minutes",

                                // In your old code you collapsed numeric(38,1) → int days
                                TotalWorkingDays = reader.IsDBNull(reader.GetOrdinal("TotalWorkingDays"))
                                    ? 0
                                    : Convert.ToInt32(Math.Round(Convert.ToDouble(reader["TotalWorkingDays"])))
                            });
                        }
                    }
                }
            }

            return punches;
        }

        #endregion


        #region Attendance Count Approval

        private async Task<List<AttachmentDto>> SaveFilesAsync(List<IFormFile> files, IWebHostEnvironment webHostEnvironment)
        {
            var attachments = new List<AttachmentDto>();
            
            if (files == null || !files.Any())
                return attachments;

            var now = DateTime.Now;
            var year = now.Year.ToString();
            var month = now.Month.ToString("D2");
            var day = now.Day.ToString("D2");
            var timestamp = now.ToString("ddMMyyyyHHmmssfff");

            // Create directory structure: wwwroot/AttendanceApproval/Year/Month/Day
            var relativePath = Path.Combine("AttendanceApproval", year, month, day);
            var fullPath = Path.Combine(webHostEnvironment.WebRootPath, relativePath);

            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
            }

            foreach (var file in files)
            {
                if (file.Length > 0)
                {
                    var extension = Path.GetExtension(file.FileName);
                    var fileName = $"{timestamp}_{Guid.NewGuid()}{extension}";
                    var filePath = Path.Combine(fullPath, fileName);
                    var fileUrl = $"/{relativePath.Replace("\\", "/")}/{fileName}";

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    attachments.Add(new AttachmentDto
                    {
                        FileUrl = fileUrl,
                        FileName = file.FileName,
                        FileSize = file.Length
                    });
                }
            }

            return attachments;
        }

        public async Task<long> CreateAttendanceCountApprovalAsync(CreateAttendanceCountApprovalDto dto, string createdBy)
        {
            try
            {
                // Validate if request already exists for this ECode and MonthYear
                var existingRequest = await _context.tblAttendanceCountApprovals
                    .FirstOrDefaultAsync(a => a.ECode == dto.ECode
                        && a.MonthYear == dto.MonthYear);

                if (existingRequest != null)
                {
                    throw new InvalidOperationException($"An attendance count approval request for {dto.ECode} for {dto.MonthYear} already exists.");
                }

                // Create the approval request
                var approval = new tblAttendanceCountApproval
                {
                    ECode = dto.ECode,
                    MonthYear = dto.MonthYear,
                    AttendanceCount = dto.AttendanceCount,
                    EmployeeRemarks = dto.EmployeeRemarks?.Trim(),
                    IsCMApproved = null, // Not reviewed yet
                    IsRMApproved = null, // Not reviewed yet
                    CreatedBy = createdBy,
                    CreatedOn = DateTime.UtcNow,
                    LastUpdatedBy = createdBy
                };

                _context.tblAttendanceCountApprovals.Add(approval);
                await _context.SaveChangesAsync();

                // Add attachments if provided
                if (dto.Attachments != null && dto.Attachments.Any())
                {
                    var attachments = dto.Attachments.Select(a => new tblAttendanceCountAttachment
                    {
                        AttendanceCountApprovalId = approval.AttendanceCountApprovalId,
                        FileUrl = a.FileUrl,
                        FileName = a.FileName,
                        FileSize = a.FileSize,
                        CreatedOn = DateTime.UtcNow,
                        CreatedBy = createdBy
                    }).ToList();

                    _context.tblAttendanceCountAttachments.AddRange(attachments);
                    await _context.SaveChangesAsync();
                }

                _logger.LogInformation("Attendance count approval created successfully. ID: {ApprovalId}", approval.AttendanceCountApprovalId);
                return approval.AttendanceCountApprovalId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating attendance count approval for ECode: {ECode}, MonthYear: {MonthYear}",
                    dto.ECode, dto.MonthYear);
                throw new InvalidOperationException("Failed to create attendance count approval request.", ex);
            }
        }

        public async Task<long> CreateAttendanceCountApprovalWithFilesAsync(CreateAttendanceCountApprovalDto dto, List<IFormFile> files, string createdBy)
        {
            try
            {
                // Save files first
                var attachmentDtos = await SaveFilesAsync(files, _webHostEnvironment);
                
                // Add saved files to dto
                if (dto.Attachments == null)
                    dto.Attachments = new List<AttachmentDto>();
                
                dto.Attachments.AddRange(attachmentDtos);

                // Call the regular create method
                return await CreateAttendanceCountApprovalAsync(dto, createdBy);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating attendance count approval with files for ECode: {ECode}, MonthYear: {MonthYear}", 
                    dto.ECode, dto.MonthYear);
                throw new InvalidOperationException("Failed to create attendance count approval request with files.", ex);
            }
        }

        public async Task<bool> CMApproveAttendanceCountAsync(CMApprovalDto dto, string approvedBy)
        {
            try
            {
                var approval = await _context.tblAttendanceCountApprovals
                    .FirstOrDefaultAsync(a => a.AttendanceCountApprovalId == dto.AttendanceCountApprovalId);

                if (approval == null)
                {
                    _logger.LogWarning("Attendance count approval not found. ID: {ApprovalId}", dto.AttendanceCountApprovalId);
                    return false;
                }

                // Check if already processed by CM
                if (approval.IsCMApproved.HasValue)
                {
                    throw new InvalidOperationException("This request has already been processed by CM.");
                }

                // Update CM approval details
                approval.IsCMApproved = dto.IsApproved;
                approval.CMApprovedBy = approvedBy;
                approval.CMApprovedOn = DateTime.UtcNow;
                approval.CMRemarks = dto.CMRemarks?.Trim();
                approval.LastUpdatedBy = approvedBy;
                approval.UpdatedOn = DateTime.UtcNow;

                if (dto.IsApproved)
                {
                    _logger.LogInformation("CM approved attendance count approval. ID: {ApprovalId}, Status: Pending RM", dto.AttendanceCountApprovalId);
                }
                else
                {
                    _logger.LogInformation("CM rejected attendance count approval. ID: {ApprovalId}, Status: Pending RM (RM can override)", dto.AttendanceCountApprovalId);
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CM approval for attendance count approval ID: {ApprovalId}", dto.AttendanceCountApprovalId);
                throw new InvalidOperationException("Failed to process CM approval.", ex);
            }
        }

        public async Task<bool> RMApproveAttendanceCountAsync(RMApprovalDto dto, string approvedBy)
        {
            try
            {
                var approval = await _context.tblAttendanceCountApprovals
                    .FirstOrDefaultAsync(a => a.AttendanceCountApprovalId == dto.AttendanceCountApprovalId);

                if (approval == null)
                {
                    _logger.LogWarning("Attendance count approval not found. ID: {ApprovalId}", dto.AttendanceCountApprovalId);
                    return false;
                }

                // RM can review at any time (doesn't need CM approval first)
                // RM is upper level and can override CM's decision

                // Check if already processed by RM
                if (approval.IsRMApproved.HasValue)
                {
                    throw new InvalidOperationException("This request has already been processed by RM.");
                }

                // Update RM approval details
                approval.IsRMApproved = dto.IsApproved;
                approval.RMApprovedBy = approvedBy;
                approval.RMApprovedOn = DateTime.UtcNow;
                approval.RMRemarks = dto.RMRemarks?.Trim();
                approval.LastUpdatedBy = approvedBy;
                approval.UpdatedOn = DateTime.UtcNow;

                if (dto.IsApproved)
                {
                    var overrideMsg = approval.IsCMApproved == false ? " (Overriding CM Rejection)" : "";
                    _logger.LogInformation("RM approved attendance count approval. ID: {ApprovalId}{Override}", dto.AttendanceCountApprovalId, overrideMsg);
                }
                else
                {
                    var overrideMsg = approval.IsCMApproved == true ? " (Overriding CM Approval)" : "";
                    _logger.LogInformation("RM rejected attendance count approval. ID: {ApprovalId}{Override}", dto.AttendanceCountApprovalId, overrideMsg);
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in RM approval for attendance count approval ID: {ApprovalId}", dto.AttendanceCountApprovalId);
                throw new InvalidOperationException("Failed to process RM approval.", ex);
            }
        }

        public async Task<PagedAttendanceCountApprovalDto> GetAttendanceCountApprovalsAsync(
            int pageNumber = 1,
            int pageSize = 10,
            string? searchTerm = null,
            int? statusId = null,
            string? ecode = null,
            string? approverRole = null,
            string? approverEcode = null)
        {
            try
            {
                var query = _context.tblAttendanceCountApprovals
                    .AsNoTracking()
                    .AsQueryable();

                // Filter by ECode
                if (!string.IsNullOrWhiteSpace(ecode))
                {
                    query = query.Where(a => a.ECode == ecode);
                }

                // Filter by Approver Role (CM or RM)
                if (!string.IsNullOrWhiteSpace(approverRole))
                {
                    if (approverRole.ToLower() == "cm")
                    {
                        // Show pending CM approvals (IsCMApproved is NULL)
                        query = query.Where(a => !a.IsCMApproved.HasValue);
                    }
                    else if (approverRole.ToLower() == "rm")
                    {
                        // Show pending RM approvals (IsRMApproved is NULL, regardless of CM status)
                        query = query.Where(a => !a.IsRMApproved.HasValue);
                    }
                }

                // Note: statusId parameter is kept for backward compatibility but not used
                // Status is now calculated dynamically based on CM and RM approval flags

                // Search functionality
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    searchTerm = searchTerm.Trim().ToLower();
                    query = query.Where(a =>
                        a.ECode.ToLower().Contains(searchTerm) ||
                        (a.EmployeeRemarks != null && a.EmployeeRemarks.ToLower().Contains(searchTerm)) ||
                        (a.CMRemarks != null && a.CMRemarks.ToLower().Contains(searchTerm)) ||
                        (a.RMRemarks != null && a.RMRemarks.ToLower().Contains(searchTerm))
                    );
                }

                // Get total count
                int totalRecords = await query.CountAsync();

                // Apply pagination
                var approvals = await query
                    .OrderByDescending(a => a.CreatedOn)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // Map to DTO
                var result = new List<AttendanceCountApprovalResponseDto>();
                foreach (var approval in approvals)
                {
                    var employee = await _context.tblEmployees
                        .AsNoTracking()
                        .FirstOrDefaultAsync(e => e.Ecode == approval.ECode);

                    // Load attachments manually (no navigation property)
                    var attachments = await _context.tblAttendanceCountAttachments
                        .AsNoTracking()
                        .Where(att => att.AttendanceCountApprovalId == approval.AttendanceCountApprovalId)
                        .Select(att => new AttachmentResponseDto
                        {
                            AttachmentId = att.AttachmentId,
                            FileUrl = att.FileUrl,
                            FileName = att.FileName,
                            FileSize = att.FileSize,
                            CreatedOn = att.CreatedOn
                        })
                        .ToListAsync();

                    // Calculate status dynamically
                    var (status, statusDescription) = AttendanceCountApprovalStatusHelper.CalculateStatus(
                        approval.IsCMApproved,
                        approval.IsRMApproved);

                    var dto = new AttendanceCountApprovalResponseDto
                    {
                        AttendanceCountApprovalId = approval.AttendanceCountApprovalId,
                        ECode = approval.ECode,
                        EmployeeName = employee?.FULL_NAME ?? "Unknown",
                        MonthYear = approval.MonthYear,
                        AttendanceCount = approval.AttendanceCount,
                        EmployeeRemarks = approval.EmployeeRemarks,
                        IsCMApproved = approval.IsCMApproved,
                        CMApprovedBy = approval.CMApprovedBy,
                        CMApprovedOn = approval.CMApprovedOn,
                        CMRemarks = approval.CMRemarks,
                        IsRMApproved = approval.IsRMApproved,
                        RMApprovedBy = approval.RMApprovedBy,
                        RMApprovedOn = approval.RMApprovedOn,
                        RMRemarks = approval.RMRemarks,
                        Status = status,
                        StatusDescription = statusDescription,
                        CreatedBy = approval.CreatedBy,
                        CreatedOn = approval.CreatedOn,
                        LastUpdatedBy = approval.LastUpdatedBy,
                        UpdatedOn = approval.UpdatedOn,
                        //DesignationName = employee?.DesignationName,
                        //DepartmentName = employee?.DepartmentName,
                        //LocationName = employee?.LocationName,
                        Attachments = attachments
                    };

                    result.Add(dto);
                }

                return new PagedAttendanceCountApprovalDto
                {
                    Data = result,
                    TotalRecords = totalRecords,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving attendance count approvals");
                throw new InvalidOperationException("Failed to retrieve attendance count approvals.", ex);
            }
        }

        public async Task<AttendanceCountApprovalResponseDto> GetAttendanceCountApprovalByIdAsync(long approvalId)
        {
            try
            {
                var approval = await _context.tblAttendanceCountApprovals
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.AttendanceCountApprovalId == approvalId);

                if (approval == null)
                {
                    throw new InvalidOperationException($"Attendance count approval with ID {approvalId} not found.");
                }

                var employee = await _context.tblEmployees
                    .AsNoTracking()
                    .FirstOrDefaultAsync(e => e.Ecode == approval.ECode);

                // Load attachments manually (no navigation property)
                var attachments = await _context.tblAttendanceCountAttachments
                    .AsNoTracking()
                    .Where(att => att.AttendanceCountApprovalId == approval.AttendanceCountApprovalId)
                    .Select(att => new AttachmentResponseDto
                    {
                        AttachmentId = att.AttachmentId,
                        FileUrl = att.FileUrl,
                        FileName = att.FileName,
                        FileSize = att.FileSize,
                        CreatedOn = att.CreatedOn
                    })
                    .ToListAsync();

                // Calculate status dynamically
                var (status, statusDescription) = AttendanceCountApprovalStatusHelper.CalculateStatus(
                    approval.IsCMApproved,
                    approval.IsRMApproved);

                return new AttendanceCountApprovalResponseDto
                {
                    AttendanceCountApprovalId = approval.AttendanceCountApprovalId,
                    ECode = approval.ECode,
                    EmployeeName = employee?.FULL_NAME ?? "Unknown",
                    MonthYear = approval.MonthYear,
                    AttendanceCount = approval.AttendanceCount,
                    EmployeeRemarks = approval.EmployeeRemarks,
                    IsCMApproved = approval.IsCMApproved,
                    CMApprovedBy = approval.CMApprovedBy,
                    CMApprovedOn = approval.CMApprovedOn,
                    CMRemarks = approval.CMRemarks,
                    IsRMApproved = approval.IsRMApproved,
                    RMApprovedBy = approval.RMApprovedBy,
                    RMApprovedOn = approval.RMApprovedOn,
                    RMRemarks = approval.RMRemarks,
                    Status = status,
                    StatusDescription = statusDescription,
                    CreatedBy = approval.CreatedBy,
                    CreatedOn = approval.CreatedOn,
                    LastUpdatedBy = approval.LastUpdatedBy,
                    UpdatedOn = approval.UpdatedOn,
                    //DesignationName = employee?.DesignationName,
                    //DepartmentName = employee?.DepartmentName,
                    //LocationName = employee?.LocationName,
                    Attachments = attachments
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving attendance count approval by ID: {ApprovalId}", approvalId);
                throw;
            }
        }

        #endregion

        #region Employee Attendance Request List

        public async Task<List<EmployeeAttendanceRequestSummaryDto>> GetEmployeeAttendanceRequestListAsync(long employeeId, DateTime? date = null)
        {
            try
            {
                // Get all attendance records for the employee with status and employee details using LEFT JOINs
                //var attendanceRequests = await _context.AttendanceRecords
                //    .Where(ar => ar.EmployeeId == employeeId)
                //    .GroupJoin(_context.tblStatuses,
                //        ar => ar.StatusId,
                //        s => s.StatusId,
                //        (ar, statuses) => new { AttendanceRecord = ar, Statuses = statuses })
                //    .SelectMany(
                //        ars => ars.Statuses.DefaultIfEmpty(),
                //        (ars, status) => new { AttendanceRecord = ars.AttendanceRecord, Status = status })
                //    .GroupJoin(_context.tblEmployees,
                //        ars => ars.AttendanceRecord.EmployeeId,
                //        emp => emp.EmployeeId,
                //        (ars, employees) => new { ars.AttendanceRecord, ars.Status, Employees = employees })
                //    .SelectMany(
                //        arse => arse.Employees.DefaultIfEmpty(),
                //        (arse, employee) => new EmployeeAttendanceRequestDetailDto
                //        {
                //            Id = arse.AttendanceRecord.Id,
                //            EmployeeId = arse.AttendanceRecord.EmployeeId,
                //            PunchTimeUtc = arse.AttendanceRecord.PunchTimeUtc,
                //            PunchType = arse.AttendanceRecord.PunchType,
                //            Latitude = arse.AttendanceRecord.Latitude,
                //            Longitude = arse.AttendanceRecord.Longitude,
                //            WithinGeofence = arse.AttendanceRecord.WithinGeofence,
                //            DeviceInfo = arse.AttendanceRecord.DeviceInfo,
                //            ClientIp = arse.AttendanceRecord.ClientIp,
                //            StatusId = arse.AttendanceRecord.StatusId,
                //            StatusName = arse.Status != null ? arse.Status.StatusName : null,
                //            LastUpdatedBy = arse.AttendanceRecord.LastUpdatedBy,
                //            LastUpdatedOn = arse.AttendanceRecord.LastUpdatedOn,
                //            Remarks = arse.AttendanceRecord.Remarks,
                //            Address = arse.AttendanceRecord.Address,  // Added Address field
                //        })
                //    .OrderByDescending(x => x.PunchTimeUtc)
                //    .ToListAsync();
                var attendanceRequests = await _context.GetProcedures().GetAttendanceRecordsByEmployeeAsync(employeeId, date);
                // Get employee details once
                var employee = _context.tblEmployees.FirstOrDefault(e => e.EmployeeId == employeeId);
                
                // Group by date and create summary
                var groupedByDate = attendanceRequests
                    .GroupBy(x => x.PunchTimeUtc.Date)
                    .Select(g => new EmployeeAttendanceRequestSummaryDto
                    {
                        EmployeeId = employeeId,
                        Ecode = employee?.Ecode,
                        EmployeeName = employee?.FULL_NAME,
                        PunchDate = g.Key,
                        Address = g.FirstOrDefault().Address,
                        Remarks = g.FirstOrDefault().Remarks,
                        ProofPath = g.FirstOrDefault().ProofPath,
                        PunchCount = g.Count(),
                        PunchInCount = g.Count(x => x.PunchType == 1),
                        PunchOutCount = g.Count(x => x.PunchType == 2),
                        Details = g.OrderByDescending(x => x.PunchTimeUtc).ToList()
                    })
                    .OrderByDescending(x => x.PunchDate);

                // Filter by date if provided
                if (date.HasValue)
                {
                    var targetDate = date.Value.Date;
                    groupedByDate = groupedByDate.Where(x => x.PunchDate.Date == targetDate).OrderByDescending(x => x.PunchDate);
                }

                return groupedByDate.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving attendance request list for employee ID: {EmployeeId}", employeeId);
                throw;
            }
        }

        #endregion

        #region Employee Attendance Snapshot

        private decimal? SafeGetDecimal(DbDataReader reader, string columnName)
        {
            if (reader.IsDBNull(reader.GetOrdinal(columnName)))
                return null;
            
            try
            {
                // Try to read as decimal first
                return reader.GetDecimal(columnName);
            }
            catch (InvalidCastException)
            {
                // If it's a string, try to parse it
                try
                {
                    var value = reader.GetString(columnName);
                    if (string.IsNullOrWhiteSpace(value))
                        return null;
                        
                    if (decimal.TryParse(value, out decimal result))
                        return result;
                        
                    return null;
                }
                catch
                {
                    return null;
                }
            }
        }

        private string? SafeGetString(DbDataReader reader, string columnName)
        {
            if (reader.IsDBNull(reader.GetOrdinal(columnName)))
                return null;
            
            try
            {
                return reader.GetString(columnName);
            }
            catch (InvalidCastException)
            {
                // If it's a decimal, Convert to string
                try
                {
                    return reader.GetDecimal(columnName).ToString();
                }
                catch
                {
                    return null;
                }
            }
        }

        public async Task<List<EmpAttendanceSnapshotDto>> GetEmpAttendanceSnapshotAsync(string? ecode = null, string? month = null, int? batchNo = null)
        {
            try
            {
                var snapshots = new List<EmpAttendanceSnapshotDto>();

                using (var connection = _context.Database.GetDbConnection())
                {
                    await connection.OpenAsync();

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "SELECT * FROM dbo.ufn_GetEmpAttendanceSnapshot(@Ecode, @Month, @BatchNo)";
                        command.CommandType = CommandType.Text;

                        // Add parameters
                        var ecodeParam = command.CreateParameter();
                        ecodeParam.ParameterName = "@Ecode";
                        ecodeParam.DbType = DbType.String;
                        ecodeParam.Value = string.IsNullOrWhiteSpace(ecode) ? DBNull.Value : ecode;
                        command.Parameters.Add(ecodeParam);

                        var monthParam = command.CreateParameter();
                        monthParam.ParameterName = "@Month";
                        monthParam.DbType = DbType.String;
                        monthParam.Value = string.IsNullOrWhiteSpace(month) ? DBNull.Value : month;
                        command.Parameters.Add(monthParam);

                        var batchNoParam = command.CreateParameter();
                        batchNoParam.ParameterName = "@BatchNo";
                        batchNoParam.DbType = DbType.Int32;
                        batchNoParam.Value = batchNo.HasValue ? (object)batchNo.Value : DBNull.Value;
                        command.Parameters.Add(batchNoParam);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                snapshots.Add(new EmpAttendanceSnapshotDto
                                {
                                    Ecode = reader.IsDBNull(reader.GetOrdinal("Ecode")) ? null : reader.GetString("Ecode"),
                                    LocationCode = reader.IsDBNull(reader.GetOrdinal("Location_Code")) ? null : reader.GetString("Location_Code"),
                                    LocationName = reader.IsDBNull(reader.GetOrdinal("Location Name")) ? null : reader.GetString("Location Name"),
                                    EmployeeName = reader.IsDBNull(reader.GetOrdinal("Employee Name")) ? null : reader.GetString("Employee Name"),
                                    Designation = reader.IsDBNull(reader.GetOrdinal("designation")) ? null : reader.GetString("designation"),
                                    Department = reader.IsDBNull(reader.GetOrdinal("department")) ? null : reader.GetString("department"),
                                    MonthYear = reader.IsDBNull(reader.GetOrdinal("Month-Year")) ? null : reader.GetString("Month-Year"),
                                    TtlBgtDays = SafeGetDecimal(reader, "ttl bgt days"),
                                    ActualTtlDays = SafeGetDecimal(reader, "actualttl days"),
                                    Machine = SafeGetDecimal(reader, "Machine"),
                                    Manual = SafeGetDecimal(reader, "MANUAL"),
                                    ActualWeekly = SafeGetDecimal(reader, "actualweekly"),
                                    PresentWeeklyOff = SafeGetDecimal(reader, "presentweeklyoff"),
                                    HolidayOff = reader.IsDBNull(reader.GetOrdinal("HolidayOff")) ? null : reader.GetInt32("HolidayOff"),
                                    PaybleDays = SafeGetDecimal(reader, "paybledays"),
                                    ExtraDays = SafeGetDecimal(reader, "extradays"),
                                    Absent = SafeGetDecimal(reader, "Absent"),
                                    LWP = SafeGetDecimal(reader, "LWP"),
                                    Status = reader.IsDBNull(reader.GetOrdinal("Status")) ? null : reader.GetString("Status"),
                                    
                                    // Budget Salary Components
                                    BasicSalaryBudget = SafeGetDecimal(reader, "BasicSalary(Bud.)"),
                                    HRABudget = SafeGetDecimal(reader, "HRA(Bud.)"),
                                    CCABudget = SafeGetDecimal(reader, "CCA(Bud.)"),
                                    SpecialAllowanceBudget = SafeGetDecimal(reader, "SpecialAllowance(Bud.)"),
                                    DABudget = SafeGetDecimal(reader, "DA(Bud.)"),
                                    ReimbursementBudget = SafeGetDecimal(reader, "Reimbersment(Bud.)"),
                                    FuelAndMaintenanceBudget = SafeGetDecimal(reader, "Fuel and Maintenance(Bud.)"),
                                    BooksAndPeriodicalsBudget = SafeGetDecimal(reader, "Books and Periodicals(Bud.)"),
                                    ProfessionalAttireBudget = SafeGetDecimal(reader, "Professional Attire(Bud.)"),
                                    DriverWagesBudget = SafeGetDecimal(reader, "Driver Wages(Bud.)"),
                                    MobileBillBudget = SafeGetDecimal(reader, "Mobile Bill(Bud.)"),
                                    MealVoucherBudget = SafeGetDecimal(reader, "Meal Voucher(Bud.)"),
                                    MonthlyGrossCTCBudget = SafeGetDecimal(reader, "Monthly Gross CTC(Bud.)"),
                                    
                                    // Actual Salary Components
                                    BasicSalaryActual = SafeGetDecimal(reader, "BasicSalary(Actual)"),
                                    HRAActual = SafeGetDecimal(reader, "HRA(Actual)"),
                                    CCAActual = SafeGetDecimal(reader, "CCA(Actual)"),
                                    SpecialAllowanceActual = SafeGetDecimal(reader, "SpecialAllowance(Actual)"),
                                    DAActual = SafeGetDecimal(reader, "DA(Actual)"),
                                    ExtraDayAllowance = reader.IsDBNull(reader.GetOrdinal("ExtraDayAllowance")) ? null : reader.GetString("ExtraDayAllowance"),
                                    ReimbursementActual = SafeGetDecimal(reader, "Reimbersment(Actual)"),
                                    FuelAndMaintenanceActual = SafeGetDecimal(reader, "Fuel and Maintenance(Actual)"),
                                    BooksAndPeriodicalsActual = SafeGetDecimal(reader, "Books and Periodicals(Actual)"),
                                    ProfessionalAttireActual = SafeGetDecimal(reader, "Professional Attire(Actual)"),
                                    DriverWagesActual = SafeGetDecimal(reader, "Driver Wages(Actual)"),
                                    MobileBillActual = SafeGetDecimal(reader, "Mobile Bill(Actual)"),
                                    MealVoucherActual = SafeGetDecimal(reader, "Meal Voucher(Actual)"),
                                    
                                    // Deductions
                                    PFEmployee = SafeGetDecimal(reader, "PF(Employee)"),
                                    PFEmployer = SafeGetDecimal(reader, "PF(Employeer)"),
                                    PFTotal = SafeGetString(reader, "PF(Total)"),
                                    ESICEmployee = SafeGetDecimal(reader, "ESIC(Employee)"),
                                    ESICEmployer = SafeGetDecimal(reader, "ESIC(Employeer)"),
                                    ESICTotal = SafeGetString(reader, "ESIC(Total)"),
                                    TDS = SafeGetString(reader, "TDS"),
                                    PTax = SafeGetString(reader, "PTax"),
                                    Loan = SafeGetString(reader, "Loan"),
                                    CashShort = SafeGetString(reader, "CashShort"),
                                    DieselDeduction = SafeGetString(reader, "DieselDeduction"),
                                    Penalty = SafeGetString(reader, "Penality"),
                                    LWF = SafeGetString(reader, "Lwf"),
                                    TotalDeductions = SafeGetDecimal(reader, "TotalDeductions"),
                                    
                                    // Additional Components
                                    Incentive = SafeGetString(reader, "Incentive"),
                                    Arrear = SafeGetString(reader, "ARREAR"),
                                    Overtime = SafeGetDecimal(reader, "Overtime"),
                                    FoodingAllowance = SafeGetDecimal(reader, "Fooding_Allowance"),
                                    MobileBill = SafeGetDecimal(reader, "Mobile_Bill"),
                                    MonthlyGrossCTCActual = SafeGetDecimal(reader, "Monthly Gross CTC(Actual)"),
                                    MonthlyGrossCTCActualAfterDeductionAndAddons = SafeGetDecimal(reader, "Monthly Gross CTC(Actual After Deduction AND AddONS)"),
                                    PaybleDaysFinal = SafeGetDecimal(reader, "Payble_Days"),
                                    LeaveUsed = SafeGetDecimal(reader, "Leave-Used"),
                                    
                                    // Leave Balances
                                    OpeningEL = SafeGetDecimal(reader, "Opening EL"),
                                    EarnedLeaveAcquired = SafeGetDecimal(reader, "EarnedLeaveAcquired"),
                                    EarnedLeaveUsed = SafeGetDecimal(reader, "EarnedLeaveUsed"),
                                    EarnedLeaveBalance = SafeGetDecimal(reader, "EarnedLeaveBalance"),
                                    OpeningCL = SafeGetDecimal(reader, "Opening CL"),
                                    CasualLeaveAcquired = SafeGetDecimal(reader, "CasualLeaveAcquired"),
                                    CasualLeaveUsed = SafeGetDecimal(reader, "CasualLeaveUsed"),
                                    CasualLeaveBalance = SafeGetDecimal(reader, "CasualLeaveBalance"),
                                    OpeningCompoOff = SafeGetDecimal(reader, "Opening CompoOff"),
                                    CompoOffAcquired = SafeGetDecimal(reader, "CompoOffAcquired"),
                                    CompoOffUsed = SafeGetDecimal(reader, "CompoOffUsed"),
                                    CompoOffBalance = SafeGetDecimal(reader, "CompoOffBalance"),
                                    
                                    // Additional Fields
                                    Month = reader.IsDBNull(reader.GetOrdinal("MONTH")) ? null : reader.GetString("MONTH"),
                                    BatchNo = reader.IsDBNull(reader.GetOrdinal("BatchNo")) ? null : reader.GetInt32("BatchNo"),
                                    RunAt = reader.IsDBNull(reader.GetOrdinal("RunAt")) ? null : reader.GetDateTime("RunAt"),
                                    ID = reader.IsDBNull(reader.GetOrdinal("ID")) ? null : reader.GetInt64("ID")
                                });
                            }
                        }
                    }
                }

                return snapshots;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving employee attendance snapshot for Ecode: {Ecode}, Month: {Month}, BatchNo: {BatchNo}", 
                    ecode, month, batchNo);
                throw new InvalidOperationException("Failed to retrieve employee attendance snapshot.", ex);
            }
        }

        #endregion

        public async Task<byte[]> ExportGeoAttendanceByRangeAsync(
            DateTime startDate,
            DateTime endDate,
            string? finalStatus,
            string? managerStatus,
            string? masterStatus,
            CancellationToken ct = default)
        {
            if (endDate < startDate)
                throw new ArgumentException("EndDate must be greater than or equal to StartDate.");

            var rows = new List<GeoAttendanceExportDto>();

            await using var conn = _context.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open) await conn.OpenAsync(ct);

            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "dbo.usp_GetGeoAttendanceByRange";
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.Add(new SqlParameter("@StartDate", SqlDbType.Date) { Value = startDate.Date });
                cmd.Parameters.Add(new SqlParameter("@EndDate", SqlDbType.Date) { Value = endDate.Date });
                cmd.Parameters.Add(new SqlParameter("@FinalStatus", SqlDbType.VarChar, 50)
                { Value = string.IsNullOrWhiteSpace(finalStatus) ? (object)DBNull.Value : finalStatus });
                cmd.Parameters.Add(new SqlParameter("@ManagerStatus", SqlDbType.VarChar, 50)
                { Value = string.IsNullOrWhiteSpace(managerStatus) ? (object)DBNull.Value : managerStatus });
                cmd.Parameters.Add(new SqlParameter("@MasterStatus", SqlDbType.VarChar, 50)
                { Value = string.IsNullOrWhiteSpace(masterStatus) ? (object)DBNull.Value : masterStatus });

                await using var reader = await cmd.ExecuteReaderAsync(ct);
                string? GetStr(string col)
                {
                    var i = reader.GetOrdinal(col);
                    return reader.IsDBNull(i) ? null : reader.GetString(i);
                }
                DateTime? GetDt(string col)
                {
                    var i = reader.GetOrdinal(col);
                    return reader.IsDBNull(i) ? (DateTime?)null : reader.GetDateTime(i);
                }
                int GetInt(string col)
                {
                    var i = reader.GetOrdinal(col);
                    return reader.IsDBNull(i) ? 0 : reader.GetInt32(i);
                }

                while (await reader.ReadAsync(ct))
                {
                    rows.Add(new GeoAttendanceExportDto
                    {
                        Ecode = GetStr("Ecode"),
                        EmployeeName = GetStr("EmployeeName"),
                        DepartmentName = GetStr("DepartmentName"),
                        DesignationName = GetStr("DesignationName"),
                        LocationName = GetStr("LocationName"),
                        STCode = GetStr("STCode"),
                        ReportingManagerEcode = GetStr("ReportingManagerEcode"),
                        ReportingManagerName = GetStr("ReportingManagerName"),
                        PunchDate = GetDt("PunchDate") ?? DateTime.MinValue,
                        PunchCount = GetInt("PunchCount"),
                        PunchInCount = GetInt("PunchInCount"),
                        PunchOutCount = GetInt("PunchOutCount"),
                        FirstPunchUtc = GetDt("FirstPunchUtc"),
                        LastPunchUtc = GetDt("LastPunchUtc"),
                        ManagerStatus = GetStr("ManagerStatus"),
                        ManagerApproverId = GetStr("ManagerApproverId"),
                        ManagerApprovalOn = GetDt("ManagerApprovalOn"),
                        ManagerRemarks = GetStr("ManagerRemarks"),
                        MasterStatus = GetStr("MasterStatus"),
                        MasterApproverId = GetStr("MasterApproverId"),
                        MasterApprovalOn = GetDt("MasterApprovalOn"),
                        MasterRemarks = GetStr("MasterRemarks"),
                        FinalStatus = GetStr("FinalStatus"),
                    });
                }
            }

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("GeoAttendance");

            var headers = new[]
            {
                "Ecode", "Employee Name", "Department", "Designation", "Location", "ST Code",
                "RM Ecode", "Reporting Manager",
                "Punch Date", "Punch Count", "Punch In Count", "Punch Out Count",
                "First Punch (UTC)", "Last Punch (UTC)",
                "Manager Status", "Manager Approver", "Manager Approved On", "Manager Remarks",
                "Master Status", "Master Approver", "Master Approved On", "Master Remarks",
                "Final Status"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(1, i + 1).Value = headers[i];
                ws.Cell(1, i + 1).Style.Font.Bold = true;
                ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                var row = i + 2;
                ws.Cell(row, 1).Value = r.Ecode ?? "";
                ws.Cell(row, 2).Value = r.EmployeeName ?? "";
                ws.Cell(row, 3).Value = r.DepartmentName ?? "";
                ws.Cell(row, 4).Value = r.DesignationName ?? "";
                ws.Cell(row, 5).Value = r.LocationName ?? "";
                ws.Cell(row, 6).Value = r.STCode ?? "";
                ws.Cell(row, 7).Value = r.ReportingManagerEcode ?? "";
                ws.Cell(row, 8).Value = r.ReportingManagerName ?? "";
                ws.Cell(row, 9).Value = r.PunchDate != DateTime.MinValue ? r.PunchDate.ToString("yyyy-MM-dd") : "";
                ws.Cell(row, 10).Value = r.PunchCount;
                ws.Cell(row, 11).Value = r.PunchInCount;
                ws.Cell(row, 12).Value = r.PunchOutCount;
                ws.Cell(row, 13).Value = r.FirstPunchUtc.HasValue ? r.FirstPunchUtc.Value.ToString("yyyy-MM-dd HH:mm:ss") : "";
                ws.Cell(row, 14).Value = r.LastPunchUtc.HasValue ? r.LastPunchUtc.Value.ToString("yyyy-MM-dd HH:mm:ss") : "";
                ws.Cell(row, 15).Value = r.ManagerStatus ?? "";
                ws.Cell(row, 16).Value = r.ManagerApproverId ?? "";
                ws.Cell(row, 17).Value = r.ManagerApprovalOn.HasValue ? r.ManagerApprovalOn.Value.ToString("yyyy-MM-dd HH:mm:ss") : "";
                ws.Cell(row, 18).Value = r.ManagerRemarks ?? "";
                ws.Cell(row, 19).Value = r.MasterStatus ?? "";
                ws.Cell(row, 20).Value = r.MasterApproverId ?? "";
                ws.Cell(row, 21).Value = r.MasterApprovalOn.HasValue ? r.MasterApprovalOn.Value.ToString("yyyy-MM-dd HH:mm:ss") : "";
                ws.Cell(row, 22).Value = r.MasterRemarks ?? "";
                ws.Cell(row, 23).Value = r.FinalStatus ?? "";
            }

            ws.ColumnsUsed().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

    }
}