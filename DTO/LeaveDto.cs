using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace HRMSAPI.DTO
{
    public class LeaveRequestDto
    {
        public long LeaveRequestId { get; set; }
        public long? EmployeeId { get; set; }
        public string? EmployeeName { get; set; }
        public string? Ecode { get; set; }
        public int? StatusId { get; set; }
        public string? StatusName { get; set; }
        public int? LeaveTypeId { get; set; }
        public string? LeaveTypeName { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string? Reason { get; set; } = "NA";
        public string? Remarks { get; set; } = "NA";
        public bool? IsRevoked { get; set; }
        public long? ReportingManagerId { get; set; }
        public string? ReportHeadEcode { get; set; }
        public string? ReportHeadName { get; set; }
        public DateTime? CreatedOn { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? UpdatedOn { get; set; }
        public string? LastUpdatedBy { get; set; }
        public bool? FirstHalf { get;  set; }
        public bool? SecondHalf { get;  set; }
        public bool? FullDay { get;  set; }
        public string? LocationName { get;  set; }
        public string? STCode { get;  set; }
        public int LocationId { get;  set; }
        public string? RelieverName { get;  set; }
        public string? RelieverEcode { get; set; }
    }
    public class EmployeeLeaveBalanceDto
    {
        public long EmployeeLeaveBalanceId { get; set; }
        public long EmployeeId { get; set; }
        public decimal? Balance { get; set; }
        public decimal? Used { get; set; }
        public DateTime? LastUpdatedOn { get; set; }
        public decimal? CasualLeaveBalance { get; set; }
        public decimal? EarnedLeaveBalance { get; set; }
        public decimal? SickLeaveBalance { get; set; }
        public decimal? PaternityLeaveBalance { get; set; }
        public decimal? MaternityLeaveBalance { get; set; }
        public decimal? CompOffBalance { get; set; }
        public decimal? CasualLeaveAcquired { get; set; }
        public decimal? CasualLeaveUsed { get; set; }
        public decimal? EarnedLeaveAcquired { get; set; }
        public decimal? EarnedLeaveUsed { get; set; }
        public decimal? SickLeaveAcquired { get; set; }
        public decimal? SickLeaveUsed { get; set; }
        public decimal? PaternityLeaveAcquired { get; set; }
        public decimal? PaternityLeaveUsed { get; set; }
        public decimal? MaternityLeaveAcquired { get; set; }
        public decimal? MaternityLeaveUsed { get; set; }
        public decimal? CompOffAcquired { get; set; }
        public decimal? CompOffUsed { get; set; }
        public string LeaveType { get; set; }
        public decimal AvailableBalance { get; set; }
        public decimal Acquired { get; set; }
        public decimal Utilized { get; set; }
        public decimal AnnualAllotment { get; set; }

        public int? LeaveTypeId { get; set; }
    }


}