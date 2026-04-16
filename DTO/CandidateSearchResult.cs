namespace HRMSAPI.DTO
{
   public class CandidateSearchResult
    {
        public long Id { get; set; }
        public string FirstName { get; set; }
        public string MiddleName { get; set; }
        public string LastName { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public string Designation { get; set; }
        public DateTime Dob { get; set; }
        public int StatusId { get; set; }
        public int? HrApprovalStatus { get; set; }
        public int? AuditApprovalStatus { get; set; }
        public int? ClusterManagerApprovalStatus { get; set; }
        public string StoreLocationName { get; set; }
        public string StoreLocationCode { get; set; }
    
    }
    public class SearchCandidatesRequest
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public List<string> LocationIds { get; set; }
        public List<string> DesignationIds { get; set; }
        public List<string> DepartmentIds { get; set; }
        public List<int> StatusIds { get; set; }
        public List<int> HrApprovalStatuses { get; set; }
        public List<int> AuditApprovalStatuses { get; set; }
        public List<int> ClusterManagerApprovalStatuses { get; set; }
    }
}
