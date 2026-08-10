namespace HRMSAPI.DTO
{
    public class GeoAttendanceExportDto
    {
        public string? Ecode { get; set; }
        public string? EmployeeName { get; set; }
        public string? DepartmentName { get; set; }
        public string? SubDepartment1 { get; set; }
        public string? SubDepartment2 { get; set; }
        public string? SubDepartment3 { get; set; }
        public string? DesignationName { get; set; }
        public string? LocationName { get; set; }
        public string? STCode { get; set; }
        public string? ReportingManagerEcode { get; set; }
        public string? ReportingManagerName { get; set; }
        public DateTime PunchDate { get; set; }
        public int PunchCount { get; set; }
        public int PunchInCount { get; set; }
        public int PunchOutCount { get; set; }
        public DateTime? FirstPunchUtc { get; set; }
        public DateTime? LastPunchUtc { get; set; }
        public string? ManagerStatus { get; set; }
        public string? ManagerApproverId { get; set; }
        public DateTime? ManagerApprovalOn { get; set; }
        public string? ManagerRemarks { get; set; }
        public string? MasterStatus { get; set; }
        public string? MasterApproverId { get; set; }
        public DateTime? MasterApprovalOn { get; set; }
        public string? MasterRemarks { get; set; }
        public string? FinalStatus { get; set; }

        /// <summary>
        /// wwwroot-relative proof paths for the punch day, pipe-separated (' | ').
        /// Null when no punch that day carried a proof file.
        /// </summary>
        public string? ProofPaths { get; set; }
    }
}
