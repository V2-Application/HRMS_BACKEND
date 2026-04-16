using System;

namespace HRMSAPI.DTO
{
    public class EmployeeAttendanceDetailDto
    {
        public string ZoneName { get; set; }
        public string RegionName { get; set; }
        public string ClusterName { get; set; }
        public string STCode { get; set; }
        public string LocationName { get; set; }
        public string Ecode { get; set; }
        public string FullName { get; set; }

        public string Gender { get; set; }
        public DateTime? DOB { get; set; }
        public decimal? AgeInYears { get; set; }

        public string DepartmentId { get; set; }
        public string DesignationId { get; set; }
        public string DepartmentName { get; set; }
        public string DesignationName { get; set; }

        public DateTime? DOJ { get; set; }
        public string ResignationTypeName { get; set; }
        public DateTime? DateOfLeft { get; set; }

        public string BankName { get; set; }
        public string AccountNumber { get; set; }
        public string BankIfscCode { get; set; }

        public string PermanentAddress { get; set; }
        public string PermanentAddressPinCode { get; set; }
        public string PresentAddress { get; set; }
        public string PresentAddressPinCode { get; set; }

        public string Mobile { get; set; }
        public string EmailAddress { get; set; }
        public string AadharNo { get; set; }
        public string PanNo { get; set; }
        public string HighestQualification { get; set; }
        public string FatherName { get; set; }
        public string MotherName { get; set; }
        public string MaritalStatus { get; set; }

        public string ReportHeadEcode { get; set; }
        public string ReportHeadFullName { get; set; }
        public string ReportHeadDesignation { get; set; }

        public string CompanyName1 { get; set; }
        public string From1 { get; set; }
        public string To1 { get; set; }
        public decimal? Years1 { get; set; }
        public string CompanyName2 { get; set; }
        public string From2 { get; set; }
        public string To2 { get; set; }
        public decimal? Years2 { get; set; }
        public string CompanyName3 { get; set; }
        public string From3 { get; set; }
        public string To3 { get; set; }
        public decimal? Years3 { get; set; }
        public decimal? TotalExperience { get; set; }
        public bool? LocStatus { get; set; }
        public string EmployeeStatus { get; set; }

        public long EmployeeId { get; set; }
        public long CandidateId { get; set; }
        public string LocBasedECode { get; set; }
        public bool IsActive { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DateOfJoining { get; set; }
        public bool IsStore { get; set; }
        public DateTime? CreatedOn { get; set; }
        public DateTime? UpdatedOn { get; set; }
        public string CreatedBy { get; set; }
        public string UpdatedBy { get; set; }

        public int PresentDays { get; set; }
        public int HalfDays { get; set; }
        public int MisPunchDays { get; set; }
        public int AbsentDays { get; set; }
        public int LeaveDays { get; set; }
        public int RegularisationDays { get; set; }
        public decimal? TotalWorkingDays { get; set; }
        public int TotalCalendarRows { get; set; }
        public int NonWorkingRows { get; set; }
        public string TotalMonthlyWorkingHours { get; set; }

        // Attendance Count Approval columns
        public long? AttendanceCountApprovalId { get; set; }
        public string ApprovalMonthYear { get; set; }
        public int? ApprovalAttendanceCount { get; set; }
        public string EmployeeRemarks { get; set; }
        public string ApprovalStatus { get; set; }
        public string ApprovalStatusDescription { get; set; }
        public bool? IsCMApproved { get; set; }
        public string CMApprovedBy { get; set; }
        public DateTime? CMApprovedOn { get; set; }
        public string CMRemarks { get; set; }
        public bool? IsRMApproved { get; set; }
        public string RMApprovedBy { get; set; }
        public DateTime? RMApprovedOn { get; set; }
        public string RMRemarks { get; set; }
        public int? AttachmentCount { get; set; }
        public string AttachmentFilePaths { get; set; }
    }
}



