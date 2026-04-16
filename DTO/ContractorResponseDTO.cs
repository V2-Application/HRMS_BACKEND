namespace HRMSAPI.DTO
{
    public class ContractorResponseDTO
    {
        public long ContractId { get; set; }
        public string ContractorName { get; set; }
        public string ContractorCode { get; set; }
        public string ServiceCategory { get; set; }
        public string NatureOfWork { get; set; }
        public DateTime? ContractStartDate { get; set; }
        public DateTime? ContractEndDate { get; set; }
        public string ContractStatus { get; set; }
        public string RegisteredAddress { get; set; }
        public string SiteAddress { get; set; }
        public string ContactPersonName { get; set; }
        public string MobileNumber { get; set; }
        public string EmailID { get; set; }
        public string PAN { get; set; }
        public string GSTIN { get; set; }
        public string PFRegistrationNumber { get; set; }
        public string ESICRegistrationNumber { get; set; }
        public string LabourLicenseNumber { get; set; }
        public string BankName { get; set; }
        public string BranchName { get; set; }
        public string AccountHolderName { get; set; }
        public string AccountNumber { get; set; }
        public string IFSCCode { get; set; }
        public string AccountType { get; set; }
        public string PaymentMode { get; set; }
        public string BeneficiaryName { get; set; }
        public bool? GSTApplicability { get; set; }
        public bool? BankVerificationStatus { get; set; }
    }

}
