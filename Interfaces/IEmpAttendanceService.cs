using HRMSAPI.Data;
using HRMSAPI.DTO;
using HRMSAPI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using static HRMSAPI.Enum.Enums;
using static HRMSAPI.Implementation.EmpAttendanceService;

namespace HRMSAPI.Interfaces
{
    public interface IEmpAttendanceService
    {
        Task FetchAndSaveAttendanceAsync();
        Task FetchAndSavePunchesAsync(CancellationToken cancellationToken = default);
        Task FetchAndSavePunchesRangeAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);
        Task<List<AttendanceFetchDto>> FetchAttendance(int month, int year, string? ecode);
        Task<List<GetAttendanceProcResult>> FetchAttendance_Ishu(int month, int year, string ecode);

        Task<int> CreateAttendanceRequestAsync(AttendanceRegularizationRequestDto requestDto, JwtLoginDetailDto loginDetail, string? fileUrl);

        Task<List<PunchFetchDto>> FetchPunchesRange(DateTime fromDate, DateTime toDate, string? ecode);
        Task<List<MultiPunchAttendanceDto>> FetchPunchesRangeByEcodeAsync(DateTime fromDate, DateTime toDate, string ecode);
        Task FetchAndSavePunchesRangeByEcodeAsync(DateTime fromDate, DateTime toDate, string ecode, CancellationToken cancellationToken = default);
        public Task<PagedResult<AttendanceRegularizationRequestDto>> GetRegularizationRequestsAsync(
     long managerId,
     string role,
     long currentEmployeeId,
     int statusId = 0,
     int pageNumber = 1,
     int pageSize = 10,
     string? searchTerm = null);

        Task<ApiResponse<object>> ApproveRegularizationAsync(
         int requestId,
         UpdateAttendanceRequestDto dto,
         long callerEmployeeId,
         string role);


        Task<List<AttendanceRegularizationRequestDto>> GetRegularizationRequestsSelfAsync(long EmployeeId);
        Task<AttendanceRecordGeo> GeoLocationAttendance(string employeeCode, PunchType type, decimal lat, decimal lon, string? device, string? ip, string? address);
        Task<AttendanceRecordGeo> GeoLocationAttendanceWithProc(string employeeCode, PunchType type, decimal lat, decimal lon, string? device, string? ip, string? address, IFormFile? proofFile = null);

        Task<(List<EmployeeAttendanceDetailDto> Employees, int TotalCount, int CurrentPageNumber, int ActiveCount, int InactiveCount, int AbscondCount, int LocCount)> GetEmployeeAttendanceDetailsAsync(
            int pageNumber = 1,
            int pageSize = 10,
            string? searchTerm = null,
            string mode = "all",
            string? managerId = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int? month = null,
            int? year = null);
        Task<PagedResult<DailyAttendanceSummaryDto>> GetDailyAttendanceSummaryGeoAsync(
      long managerId,
      string role,
      int statusId = 0,
      int pageNumber = 1,
      int pageSize = 10,
      string? searchTerm = null,
      string timeZoneId = "UTC",
      CancellationToken ct = default);
        Task<AttendanceStatusChangeResult> SetGeoAttendanceStatusAsync(
          long managerId,
          string role,
          long employeeId,
          DateTime punchDate,
          int statusId,
          string? remarks,
          string timeZoneId,
          string lastUpdatedBy,               // <-- new parameter
          CancellationToken ct = default);

        Task<byte[]> ExportGeoAttendanceByRangeAsync(
            DateTime startDate,
            DateTime endDate,
            string? finalStatus,
            string? managerStatus,
            string? masterStatus,
            CancellationToken ct = default);

        // Attendance Count Approval Methods
        Task<long> CreateAttendanceCountApprovalAsync(CreateAttendanceCountApprovalDto dto, string createdBy);
        Task<long> CreateAttendanceCountApprovalWithFilesAsync(CreateAttendanceCountApprovalDto dto, List<IFormFile> files, string createdBy);
        Task<bool> CMApproveAttendanceCountAsync(CMApprovalDto dto, string approvedBy);
        Task<bool> RMApproveAttendanceCountAsync(RMApprovalDto dto, string approvedBy);
        Task<PagedAttendanceCountApprovalDto> GetAttendanceCountApprovalsAsync(
            int pageNumber = 1,
            int pageSize = 10,
            string? searchTerm = null,
            int? statusId = null,
            string? ecode = null,
            string? approverRole = null,
            string? approverEcode = null);
        Task<AttendanceCountApprovalResponseDto> GetAttendanceCountApprovalByIdAsync(long approvalId);

        // Employee Attendance Request List Method
        Task<List<EmployeeAttendanceRequestSummaryDto>> GetEmployeeAttendanceRequestListAsync(long employeeId, DateTime? date = null);

        // Employee Attendance Snapshot Method
        Task<List<EmpAttendanceSnapshotDto>> GetEmpAttendanceSnapshotAsync(string? ecode = null, string? month = null, int? batchNo = null);
        //by nikhil sharma for excel only
        Task<List<PunchFetchDto>> FetchPunchesRangeExcel(DateTime fromDate, DateTime toDate, string? ecode);

        // Merge Monthly Punches Range
        Task<int> MergeMonthlyPunchesRangeAsync(DateTime fromDate, DateTime toDate, string ecode);

    }
}
