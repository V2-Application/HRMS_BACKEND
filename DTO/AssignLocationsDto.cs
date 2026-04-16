namespace HRMSAPI.DTO
{
    public class AssignLocationsDto
    {
        public int? AssignLocationHistoryId { get; set; }
        public long? EmployeeId { get; set; }
        public string? Ecode { get; set; }
        public string? EmployeeName { get; set; }
        public long? CandidateId { get; set; }
        public int? AssignedLocation { get; set; }
        public string? AssignedReason { get; set; }
        public bool? IsActive { get; set; }
        public bool? IsDeleted { get; set; }
        public DateTime? AssignedOnDate { get; set; }
        public DateTime? ReleasedOnDate { get; set; }
        public int? TransferApprovalStatus { get; set; }
        public int? IsReportingHeadApproval { get; set; }
        public int? IsHRApproval { get; set; }

        // 🔄 Updated Assigned Location Fields (from alh.AssignedLocation)
        public string? AssignLocationName { get; set; }
        public string? AssignLocationSTCode { get; set; }

        // 🏠 New Base Location Fields (from emp.LocationId)
        public string? BaseLocation { get; set; }
        public string? BaseLocationSTCode { get; set; }

        public bool? PermanentTransfer { get; set; }
        public bool? TemporaryTransfer { get; set; }
        public string? DesignationName { get; set; }
        public string? DepartmentName { get; set; }
        public int? DepartmentId { get; set; }
        public int? DesignationId { get; set; }
        public DateTime? CreatedOn { get; set; }
        public DateTime? UpdatedOn { get; set; }
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
    }

}
