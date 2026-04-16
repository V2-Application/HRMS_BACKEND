namespace HRMSAPI.DTO
{
    public class InterviewAssignedDto
    {
        public DateTime? AppliedDate { get; set; }
        public string? CurrentLocation { get; set; }
        public string? PreferredLocation { get; set; }
        public string? PreferredState { get; set; }
        public string? StoreCode { get; set; }
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Designation { get; set; }
        public string? Mobile { get; set; }
        public string? Experience { get; set; }
        public string? CurrentCompany { get; set; }
        public decimal? CurrentSalary { get; set; }
        public string? InterviewMode { get; set; }
        public DateTime? InterviewDate { get; set; }
        public bool? IsResumeUploaded { get; set; }
        public string? AssignBy { get; set; }
        public string? AssignTo { get; set; }
        public int? RoundId { get; set; }
        public DateTime? CreatedOn { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTime? UpdatedOn { get; set; }
    }
}
