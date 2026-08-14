namespace HRMSAPI.DTO
{
    public class VendorEmployeeResponseDTO
    {

        public long EmployeeId { get; set; }
        public string Ecode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string MiddleName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string FatherName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Mobile { get; set; } = string.Empty;
        public string PresentAddress { get; set; } = string.Empty;
        public string PresentAddressPinCode { get; set; } = string.Empty;
        public DateTime? DOB { get; set; }
        public string Gender { get; set; } = string.Empty;
        public string UAN { get; set; } = string.Empty;
        public string PAN { get; set; } = string.Empty;
        public string Aadhar { get; set; } = string.Empty;
        public bool PFApplicable { get; set; }
        public bool ESICApplicable { get; set; }
        public bool IsActive { get; set; } = true;
        public int? ShiftID { get; set; }
        public string? ShiftName { get; set; }
        public string ESICNO { get; set; } = string.Empty;
        public string HusbandName { get; set; } = string.Empty;
        public string ContractorCode { get; set; } = string.Empty;
        public int? DepartmentId { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        public int? SubDepartmentId1 { get; set; }
        public string? SubDepartmentName1 { get; set; }
        public int? SubDepartmentId2 { get; set; }
        public string? SubDepartmentName2 { get; set; }
        public int? SubDepartmentId3 { get; set; }
        public string? SubDepartmentName3 { get; set; }
        public int? DesignationId { get; set; }
        public string DesignationName { get; set; } = string.Empty;
        public string ContractorName { get; set; } = string.Empty;
        public int? LocationId { get; set; }
        public  DateTime? DOJ { get; set; }
        public DateTime? ContractStartDate { get; set; }
        public DateTime? ContractEndDate { get; set; }
        public  string?  LocationName{ get; set; }

        // Salary Fields
        public decimal? BasicSalary { get; set; }
        public decimal? CCA { get; set; }
        public decimal? DA { get; set; }
        public decimal? ExtraAllowance { get; set; }
        public decimal? SpecialAllowance { get; set; }
        public decimal? HRA { get; set; }
        public decimal? GROSS_SALARY { get; set; }
        public decimal? monthlyGrossCTC { get; set; }
        public decimal? annuallyNetCTC { get; set; }

    }
}
