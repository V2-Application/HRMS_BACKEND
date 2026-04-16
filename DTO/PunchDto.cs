using Microsoft.AspNetCore.Http;
using static HRMSAPI.Enum.Enums;

namespace HRMSAPI.DTO
{
    public class PunchDto
    {
        public string EmployeeCode { get; set; } = string.Empty;
        public PunchType Type { get; set; }
        public decimal Lat { get; set; }
        public decimal Lon { get; set; }
        public string? Device { get; set; }
        public string? Address { get; set; }
        public IFormFile? Proof { get; set; }
    }
    public sealed class DailyAttendanceSummaryDto
    {
        public long EmployeeId { get; set; }
        public string? Ecode { get; set; }
        public string? Remarks { get; set; }
        public string? Address { get; set; }
        public string EmployeeName { get; set; } = "Unknown";
        public DateTime PunchDate { get; set; }

        public int PunchCount { get; set; }
        public int PunchInCount { get; set; }
        public int PunchOutCount { get; set; }

        public int TotalRecords { get; set; }
        public string? StatusName { get; set; }

        // 2-Level Approval Info
        public int? ManagerApprovalStatusId { get; set; }
        public string? ManagerApprovalStatusName { get; set; }
        public string? ManagerApproverId { get; set; }
        public DateTime? ManagerApprovalOn { get; set; }
        public string? ManagerRemarks { get; set; }
        public int? MasterApprovalStatusId { get; set; }
        public string? MasterApprovalStatusName { get; set; }
        public string? MasterApproverId { get; set; }
        public DateTime? MasterApprovalOn { get; set; }
        public string? MasterRemarks { get; set; }
        public int? ApprovalFinalStatusId { get; set; }

        public List<DailyPunchDetailDto>? Details { get; set; }
    }

    public sealed class DailyPunchDetailDto
    {
        public long EmployeeId { get; set; }
        public DateTime PunchDate { get; set; }   // same day bucket as summary
        public DateTime PunchTimeUtc { get; set; }
        public int PunchType { get; set; }        // 1 = IN, 2 = OUT
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public bool WithinGeofence { get; set; }
        public string? DeviceInfo { get; set; }
        public string? ClientIp { get; set; }
        public int StatusId { get; set; }
        public string? Remarks { get; set; }   // <-- NEW
        public string? Address { get; set; }   // Added Address field
        public string? ProofPath { get; set; } // Added ProofPath for media proof
    }
    public sealed class SetGeoAttendanceStatusDto
    {
        public long EmployeeId { get; set; }
        public DateTime PunchDate { get; set; }
        public int StatusId { get; set; }
        public string? Remarks { get; set; }
        public string TimeZoneId { get; set; } = "UTC";
    }

    public sealed class AttendanceStatusChangeResult
    {
        public int RowsUpdated { get; set; }
        public long EmployeeId { get; set; }
        public DateTime PunchDate { get; set; }
        public int StatusIdApplied { get; set; }
        public string? StatusNameApplied { get; set; }
        public DateTime? WindowUtcStart { get; set; }
        public DateTime? WindowUtcEnd { get; set; }
    }



}
