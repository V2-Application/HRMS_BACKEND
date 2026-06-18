using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace HRMSAPI.DTO
{
    public class AttendanceGetDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string? ECode { get; set; } // Optional
        // When true, return the pay-cycle window (26th of previous month -> 25th of the
        // given month) instead of the calendar month (1st -> last). Opt-in; defaults to false
        // so existing callers (e.g. the calendar view) are unchanged.
        public bool UseCycle { get; set; } = false;
    }
    public class AttendanceFetchDto
    {
        public int EmpAttendanceId { get; set; }
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; }
        public string ECode { get; set; }
        public DateTime? AttendanceDate { get; set; }
        public TimeSpan? PunchIn { get; set; }
        public TimeSpan? PunchOut { get; set; }
        public TimeSpan? RegularizePunchIn { get; set; }
        public TimeSpan? RegularizePunchOut { get; set; }
        public bool IsRegularize { get; set; }
        public bool IsOnLeave { get; set; }
        public string TotalWorkingMinutes { get; set; }
        public string Status { get; set; }
        public TimeSpan? Punch1 { get; set; }
        public TimeSpan? Punch2 { get; set; }
        public TimeSpan? Punch3 { get; set; }
        public TimeSpan? Punch4 { get; set; }
        public TimeSpan? Punch5 { get; set; }
        public TimeSpan? Punch6 { get; set; }
        public TimeSpan? Punch7 { get; set; }
        public TimeSpan? Punch8 { get; set; }
        public TimeSpan? Punch9 { get; set; }
        public TimeSpan? Punch10 { get; set; }
        public TimeSpan? Punch11 { get; set; }
        public TimeSpan? Punch12 { get; set; }
        public double TotalWorkingDays { get; set; } // Changed from int to double
        public int LateMinutes { get; set; }
        public int EarlyMinutes { get; set; }
        public string TotalMonthlyWorkingHours { get; set; }
        public int? ValidPunchCount { get; set; }
        public string? Location { get; set; }
    }
    public class EmpAttendanceDto
    {
        public string EmpCode { get; set; }
        public DateTime AttendanceDate { get; set; }
        public string PunchIn { get; set; }
        public string PunchOut { get; set; }
    }
    public class AttendanceRegularizationRequestDto
    {
        public long AttendanceRequestId { get; set; }
        public long? EmployeeId { get; set; }
        public DateTime RequestDate { get; set; }
        public string? Reason { get; set; }
        
        public TimeSpan PunchIn { get; set; }
        public TimeSpan PunchOut { get; set; }
        public int? StatusId { get; set; } // Map to tblStatus.StatusId
        public string? Remarks { get; set; }
        public string? EmployeeName { get; set; }
        public string? Attachment { get; set; }
        public string? Ecode { get; set; }
        public string? ReportHeadEcode { get; set; }
        public string? ReportHeadName { get; set; }
        public int ? PunchTypeId { get; set; }
        public string? EmployeeRemarks { get; set; }
        // NEW: Manager approval fields
        public int? ManagerApprovalStatusId { get; set; }
        public long? ManagerApproverId { get; set; }
        public DateTime? ManagerApprovalOn { get; set; }
        public string? ManagerRemarks { get; set; }

        // NEW: LP approval fields
        public int? LpApprovalStatusId { get; set; }
        public long? LpApproverId { get; set; }
        public DateTime? LpApprovalOn { get; set; }
        public string? LpRemarks { get; set; }
        public string STCode { get; set; } = "NA";
        public string LocationName { get; set; } = "NA";
        public string? LpEcode { get; set; }
        public string? ManagerEcode { get; set; }
    }

    public class UpdateAttendanceRequestDto
    {
        public int AttendanceRequestId { get; set; }
        [Required(ErrorMessage = "StatusId is required.")]
        public int StatusId { get; set; }
        public string? Remarks { get; set; }
    }
    public static class AttendanceStatuses
    {
        public const int Approved = 1;
        public const int Rejected = 2;
        public const int Pending = 4;
     
    }

    public class MergeMonthlyPunchesRangeDto
    {
        //[Required(ErrorMessage = "FromDate is required.")]
        public DateTime FromDate { get; set; }

        //[Required(ErrorMessage = "ToDate is required.")]
        public DateTime ToDate { get; set; }

        //[Required(ErrorMessage = "Ecode is required.")]
        //[StringLength(50, ErrorMessage = "Ecode cannot exceed 50 characters.")]
        public string Ecode { get; set; } = string.Empty;
    }

    public class DateRangeDto
    {
        [Required(ErrorMessage = "FromDate is required.")]
        public DateTime FromDate { get; set; }

        [Required(ErrorMessage = "ToDate is required.")]
        public DateTime ToDate { get; set; }
    }

    public class EcodeDateRangeDto : DateRangeDto
    {
        [Required(ErrorMessage = "Ecode is required.")]
        public string Ecode { get; set; }
    }

    public class RefreshAttendanceByEcodeListDto
    {
        [Required(ErrorMessage = "Mode is required. Must be 'table' or 'machine'.")]
        public string Mode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ecode list is required.")]
        public List<string> Ecodes { get; set; } = new List<string>();

        [Required(ErrorMessage = "FromDate is required.")]
        public DateTime FromDate { get; set; }

        [Required(ErrorMessage = "ToDate is required.")]
        public DateTime ToDate { get; set; }
    }

}
