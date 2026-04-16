using System.ComponentModel.DataAnnotations;

namespace HRMSAPI.DTO
{
    public class UpdateVendorDTO
    {
        public long VendorId { get; set; }
        public string? ContractorName { get; set; }
        public string? ContractorCode { get; set; }
        public DateTime? ContractStartDate { get; set; }
        public DateTime? ContractEndDate { get; set; }
        public int ContarctStatusId { get; set; }
        public int ServiceCategoryId { get; set; }
        public List<UpdateBankDetailsDTO>? VendorBankDetails { get; set; }
        public List<UpdateVendorContactDetailsDTO>? VendorContactDetails { get; set; }
        public List<UpdateVendorComplianceDetailsDTO>? VendorComplianceDetails { get; set; }

    }

    public class UpdateBankDetailsDTO
    {
        public long VendorBankId { get; set; }
        public string? BankName { get; set; }
        public string? BranchName { get; set; }
        public string? AccountHolderName { get; set; }
        public long? AccountNumber { get; set; }
        public string? IFSCCode { get; set; }
        public string? AccountType { get; set; }
        public string? PaymentMode { get; set; }
        public string? BeneficiaryName { get; set; }
        public bool? GSTApplicability { get; set; }
        public bool? BankVerificationStatus { get; set; }
        public long VendorId { get; set; }
    }
    public class UpdateVendorContactDetailsDTO
    {
        public long VendorContactId { get; set; }
        public string? RegisteredAddress { get; set; }
        public string? SiteAddress { get; set; }
        public string? ContactPersonName { get; set; }
        public long? MobileNumber { get; set; }
        [DataType(DataType.EmailAddress)]
        public string? Email { get; set; }
        public long VendorId { get; set; }
    }
    public class UpdateVendorComplianceDetailsDTO
    {
        public long VendorComplianceId { get; set; }
        public string? PAN { get; set; }
        public string? GSTIN { get; set; }
        public string? PFRegistrationNumber { get; set; }
        public long? ESICRegistrationNumber { get; set; }
        public string? LabourLicenseNumber { get; set; }
        public long VendorId { get; set; }
    }
}
