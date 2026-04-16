namespace HRMSAPI.DTO
{
    public class CandidateApprovalDto
    {
        public long CandidateId { get; set; }

        // HR
        public int? HRApprovalStatus { get; set; }
        public string? HRReviewedBy { get; set; }
        public string? HRRemarks { get; set; }

        // Audit
        public int? AuditApprovalStatus { get; set; }
        public string? AuditReviewedBy { get; set; }
        public string? AuditRemarks { get; set; }

        // Cluster Manager
        public int? ClusterManagerApprovalStatus { get; set; }
        public string? ClusterManagerReviewedBy { get; set; }
        public string? ClusterManagerRemarks { get; set; }

        // SuperAdmin remark (used as base text)
        public string? SuperAdminRemarks { get; set; }

        public long Employeeid { get; set; }
        public string? ReportHeadEcode { get; set; }
      
    }



    public class UpdateStatusRequest
    {
        public int ApplicantId { get; set; }
        public int StatusId { get; set; }
    }

    public class ApplicantStatusTypeDto
    {
        public int StatusId { get; set; }
        public string StatusName { get; set; } = string.Empty;
      
    }

    public class TransferEmployeeDto
    {
        public int CandidateId { get; set; }
        public string CandidateName { get; set; }
        public int? AssignedLocation { get; set; }
        public string AssignedReason { get; set; }
        public bool IsActive { get; set; }
        public DateTime AssignedOnDate { get; set; }
        public DateTime? ReleasedOnDate { get; set; }
        public int? TransferApprovalStatus { get; set; }
        public int? IsReportingHeadApproval { get; set; }
        public int? IsHRApproval { get; set; }
        public string ReportHeadEcode { get; set; }
    }


    public class ResponseWithList<T>
    {
        public string Message { get; set; }
        public bool Status { get; set; }
        public List<T> Data { get; set; }
    }

    public class TransferApprovalRequestDto
    {
        public long CandidateId { get; set; }
        public int StatusId { get; set; }
        public string Remark { get; set; }
    }

    public class ApplicantDetailDto
    {
        public int ID { get; set; }
        public string FirstName { get; set; }
        public string MiddleName { get; set; }
        public string LastName { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public string Designation { get; set; }
        public DateTime? DOB { get; set; }
      
        public int StatusId { get; set; }
        public string DesignationName { get; set; }
        public string PositionHeldInPreviousCompany { get; set; }
        public string ApplicantCode { get; set; }
        public bool IsApplicant { get; set; }
        public string LocationName { get; set; }
        public string ResumeLink { get; set; }
        public string OfferLetterLink { get; set; }

        public string InterviewRounds { get; set; }           
        public string Type { get; set; }                      
        public int CurrentRound { get; set; }                 
        public string LastInterviewDateTime { get; set; }     
        public int LastScheduleId { get; set; }           
        public string FinalResult { get; set; }
        public bool IsStatus { get; set; }
        public bool? IsActive { get; set; }
        public bool? IsDeleted { get; set; }
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTime? CreatedOn { get; set; }
        public DateTime? UpdatedOn { get; set; }
        public DateTime? DateOfApply { get; set; }
        public string WorkLocation { get; set; }
        public string ApplicantCodeNew { get; set; }
        public string Company1 { get; set; }
        public string Company2 { get; set; }
        public string Company3 { get; set; }
        public string InHandSalary { get; set; }
        public string LastCTCAnnual { get; set; }
        // ✅ New experience properties
        public decimal? TotalIndustryExperienceYrs { get; set; }
        public decimal? TotalRetailExperienceYrs { get; set; }
        public string? CurrentLocation { get; set; }
        public string? PreferredLocation { get; set; }
        public int? StateId { get; set; }
        public string? StateName { get; set; }
        public bool? IsReopenAllowed { get; set; }
        public decimal? NoticePeriod { get; set; } = 0;
    }

    public class ReopenCandidateDto
    {
        public int CandidateId { get; set; }
        public string? Remarks { get; set; }
    }



}
