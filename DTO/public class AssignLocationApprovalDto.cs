namespace HRMSAPI.DTO
{
    public class AssignLocationApprovalDto
    {
        public int AssignLocationHistoryId { get; set; }
        public int? IsReportingHeadApproval { get; set; } // 1 = Approved, 2 = Rejected
        public int? IsHRApproval { get; set; }            // 1 = Approved, 2 = Rejected
    }

}
