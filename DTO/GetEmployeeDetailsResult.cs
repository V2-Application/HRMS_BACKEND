namespace HRMSAPI.DTO
{
    public class GetEmployeeDetailsResult
    {
        public int TotalEmployees { get; set; }
        public int EmployeeId { get; set; }
        public string FullName { get; set; }
        public string DepartmentName { get; set; }
        public string DesignationName { get; set; }
        public string LocationName { get; set; }
        public string StoreCode { get; set; }
        public string Ecode { get; set; }
        public string EmailAddress { get; set; }
        public object ReportHeadEcode { get; internal set; }
        public string STCode { get; internal set; }
        public bool IsActive { get; internal set; }
        public bool IsDeleted { get; internal set; }

        public string dateOfJoining { get; internal set; }
        public string ReportHeadName { get; internal set; }
    }
    public class GetEmployeeDetailsResultNew
    {
        public int? TotalEmployees { get; set; } // Nullable to handle output parameter
        public long EmployeeId { get; set; } // Changed to long for BIGINT
        public long CandidateId { get; set; } // Changed to long for BIGINT
        public string FullName { get; set; }
        public string DepartmentName { get; set; }
        public string DesignationName { get; set; }
        public string LocationName { get; set; }
        public string StoreCode { get; set; } // Maps to STCode in stored procedure
        public string RegionName { get; set; } // Maps to STCode in stored procedure
        public string ZoneName { get; set; } // Maps to STCode in stored procedure
        public string ClusterName { get; set; } // Maps to STCode in stored procedure
        public string Ecode { get; set; }
        public object ReportHeadEcode { get; internal set; } // Kept as object per request
        public string STCode { get; internal set; } // Kept per request, can map to same STCode
        public bool IsActive { get; internal set; }
        public bool IsDeleted { get; internal set; }
        public string LocBasedECode { get; set; }
        public DateTime? DateOfJoining { get; set; }
        public bool? IsStore { get; set; }
        public DateTime? CreatedOn { get; set; }
        public DateTime? UpdatedOn { get; set; }
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
        //public string? Gender { get; set; }
    }

    public class GetEmployeeDetailsResultNew_Hold
    {
        public int TotalEmployees { get; set; }
        public int EmployeeId { get; set; }
        public int CandidateId { get; set; }
        public string FullName { get; set; }
        public string DepartmentName { get; set; }
        public string DesignationName { get; set; }
        public string LocationName { get; set; }
        public string StoreCode { get; set; }
        public string Ecode { get; set; }
        public object ReportHeadEcode { get; internal set; }
        public string STCode { get; internal set; }
        public bool IsActive { get; internal set; }
        public bool IsDeleted { get; internal set; }
        public bool IsResigned { get; internal set; }

        public string LocBasedECode { get; set; }
        public DateTime? DateOfJoining { get; set; }
        public DateTime? DateOfLeft { get; set; }
        public DateTime? DateOfResignation { get; set; }
        public decimal Payble_Days { get; set; }
        public decimal Final_Amount { get; set; }


    }

    public class GetEmployeeDetailsResultNew_Test
    {
        public string ZoneName { get; set; }
        public string RegionName { get; set; }
        public string ClusterName { get; set; }
        public string STCode { get; set; }
        public string LocationName { get; set; }
        public string Ecode { get; set; }
        public string FullName { get; set; }

        // Personal/HR fields
        public string Gender { get; set; }
        public DateTime? DOB { get; set; }
        public decimal? AgeInYears { get; set; }

        public string DepartmentId { get; set; }
        public string DesignationId { get; set; }
        public string DepartmentName { get; set; }
        public string DesignationName { get; set; }

        // Sub-department names (resolved from tblEmployee.SubDepartmentId1/2/3 via tblSubDepartment)
        public string? SubDepartment1Name { get; set; }
        public string? SubDepartment2Name { get; set; }
        public string? SubDepartment3Name { get; set; }

        public DateTime? DOJ { get; set; }
        public string ResignationTypeName { get; set; }
        public DateTime? DateOfLeft { get; set; }

        // Bank details
        public string BankName { get; set; }
        public string AccountNumber { get; set; }
        public string BankIfscCode { get; set; }

        // Address details
        public string PermanentAddress { get; set; }
        public string PermanentAddressPinCode { get; set; }
        public string PresentAddress { get; set; }
        public string PresentAddressPinCode { get; set; }

        // Contact and personal details
        public string Mobile { get; set; }
        public string EmailAddress { get; set; }
        public string AadharNo { get; set; }
        public string PanNo { get; set; }
        public string HighestQualification { get; set; }
        public string FatherName { get; set; }
        public string MotherName { get; set; }
        public string MaritalStatus { get; set; }

        // Reporting details
        public string ReportHeadEcode { get; set; }
        public string ReportHeadFullName { get; set; }
        public string ReportHeadDesignation { get; set; }

        // Experience details
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
        public string? EmployeeStatus { get; set; }

        // Audit fields
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
    }
}
