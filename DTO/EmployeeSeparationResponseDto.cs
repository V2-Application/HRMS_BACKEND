namespace HRMSAPI.DTO
{
    public class EmployeeSeparationResponseDto
    {
        public long EmployeeSeprationId { get; set; }
        public long EmpId { get; set; }
        public DateTime LastDay { get; set; }
        public int NoticePeriod { get; set; }
        public string ResignationType { get; set; } = string.Empty;
        public DateTime ResignationDate { get; set; }
        public string? Remarks { get; set; }
        public bool? IsApprovedByManager { get; set; }
        public string? ReportHeadEcode { get; set; }
        public string? ManagerRemarks { get; set; }
        public bool IsRevoked { get; set; }
        public string? Status { get; set; }
        public string? FullName { get; set; }
        public string? Firstname { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public bool? IsApprovedByHR { get; set; }
        public string ReportingHeadStatus { get; set; }
        public string HRStatus { get;  set; }
        public decimal? EarnedLeaveBalance { get; set; }
        public string? Ecode { get; set; }
        public string? Ename { get; set; }
    }

    public class EmployeeSeparationResponseDtos
    {
        public long EmployeeSeprationId { get; set; }
        public long EmpId { get; set; }
        public string? LastDay { get; set; }
        public int NoticePeriod { get; set; }
        public string ResignationType { get; set; } = string.Empty;
        public string? ResignationDate { get; set; }
        public string? Remarks { get; set; }
        public bool? IsApprovedByManager { get; set; }
        public string? ReportHeadEcode { get; set; }
        public string? ManagerRemarks { get; set; }
        public bool IsRevoked { get; set; }
        public string? Status { get; set; }
        public string? FullName { get; set; }
        public string? Firstname { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
    }
}
