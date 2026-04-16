using HRMSAPI.DTO;
using System.Data;

namespace HRMSAPI.Data
{
    public static class VendorExtensions
    {
        public static DataTable ToDataTableContact(this List<CreateVendorContactDetailsDTO> list)
        {
            var dt = new DataTable();
            dt.Columns.Add("RegisteredAddress", typeof(string));
            dt.Columns.Add("SiteAddress", typeof(string));
            dt.Columns.Add("ContactPersonName", typeof(string));
            dt.Columns.Add("MobileNumber", typeof(string));
            dt.Columns.Add("Email", typeof(string));

            foreach (var item in list)
            {
                dt.Rows.Add(item.RegisteredAddress, item.SiteAddress, item.ContactPersonName, item.MobileNumber, item.Email);
            }
            return dt;
        }

        public static DataTable ToDataTableBank(this List<CreateBankDetailsDTO> list)
        {
            var dt = new DataTable();
            dt.Columns.Add("BankName", typeof(string));
            dt.Columns.Add("BranchName", typeof(string));
            dt.Columns.Add("AccountHolderName", typeof(string));
            dt.Columns.Add("AccountNumber", typeof(string));
            dt.Columns.Add("IFSCCode", typeof(string));
            dt.Columns.Add("AccountType", typeof(string));
            dt.Columns.Add("PaymentMode", typeof(string));
            dt.Columns.Add("BeneficiaryName", typeof(string));
            dt.Columns.Add("GSTApplicability", typeof(string));
            dt.Columns.Add("BankVerificationStatus", typeof(bool));

            foreach (var item in list)
            {
                dt.Rows.Add(item.BankName, item.BranchName, item.AccountHolderName, item.AccountNumber, item.IFSCCode,
                            item.AccountType, item.PaymentMode, item.BeneficiaryName, item.GSTApplicability, item.BankVerificationStatus);
            }
            return dt;
        }

        public static DataTable ToDataTableCompliance(this List<CreateVendorComplianceDetailsDTO> list)
        {
            var dt = new DataTable();
            dt.Columns.Add("PAN", typeof(string));
            dt.Columns.Add("GSTIN", typeof(string));
            dt.Columns.Add("PFRegistrationNumber", typeof(string));
            dt.Columns.Add("ESICRegistrationNumber", typeof(string));
            dt.Columns.Add("LabourLicenseNumber", typeof(string));

            foreach (var item in list)
            {
                dt.Rows.Add(item.PAN, item.GSTIN, item.PFRegistrationNumber, item.ESICRegistrationNumber, item.LabourLicenseNumber);
            }
            return dt;
        }
    }
}
