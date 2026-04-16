namespace HRMSAPI.DTO
{
    public class AttendanceRegularizationResultDto
    {
        public string? Ecode { get; set; }
        public string? EmpName { get; set; }
        public string? STCode { get; set; }
        public string? LocationName { get; set; }
        public string? DepartmentName { get; set; }
        public string? DesignationName { get; set; }
        public DateTime RequestDate { get; set; }
        public string? Reason { get; set; }
        public string? RM_ECODE { get; set; }
        public string? ReportManagerName { get; set; }
        public TimeSpan? PunchIn { get; set; }
        public TimeSpan? PunchOut { get; set; }
        public string? StatusName { get; set; }
        public string? FileUrl { get; set; }
        public int? PunchTypeId { get; set; }
        public string? RequestTypeName { get; set; }
        public string? EmployeeRemarks { get; set; }
        public string? ManagerStatus { get; set; }
        public DateTime? ManagerApprovalOn { get; set; }
        public string? ManagerRemarks { get; set; }
        public string? LpApprovalStatus { get; set; }
        public DateTime? LpApprovalOn { get; set; }
        public string? LpRemarks { get; set; }
    }
}

