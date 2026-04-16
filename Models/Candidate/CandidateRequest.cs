using DocumentFormat.OpenXml.Office.CoverPageProps;
using HRMSAPI.Data;
using HRMSAPI.DTO;
using HRMSAPI.Models.EvalutionForm;

namespace HRMSAPI.Models.Candidate
{
    public class CandidateRequest
    {
        public Attachments? Attachments { get; set; }

        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Title { get; set; }
        public string? MiddleName { get; set; }
        public string? FullName { get; set; }
        public string? HusbandName { get; set; }
        public string? AadharNo { get; set; }
        public string? FathersName { get; set; }
        public string? PlaceOfBirth { get; set; }
        public string? NameOnAadhar { get; set; }
        public string? MothersName { get; set; }
        public string? PanNo { get; set; }
        public DateTime? Dob { get; set; }
        public string? PresentAddress { get; set; }
        public string? PermanentAddress { get; set; }
        public string? PresentAddressPinCode { get; set; }
        public string? PermanentAddressPinCode { get; set; }
        public string? MaritalStatus { get; set; }
        public string? Mobile { get; set; }
        public string? EmailAddress { get; set; }
        public string? BeneficiaryAddress { get; set; }
        public string? Nationality { get; set; }
        public string? Religion { get; set; }
        public string? BankName { get; set; }
        public string? AccountNo { get; set; }
        public string? BankIfscCode { get; set; }
        public bool? IsRelativeInCompany { get; set; }

        public List<FamilyMemberDetail>? FamilyMemberDetails { get; set; }
        public List<ExperienceDetail>? ExperienceDetails { get; set; }
        public List<QualificationDetail>? QualificationDetails { get; set; }
    }

    public class Attachments
    {
        public List<string>? PassportPhoto { get; set; }
        public List<string>? Pan { get; set; }
        public List<string>? Aadhar { get; set; }
        public List<string>? SalarySlip { get; set; }
        public List<string>? BankPassbook { get; set; }
        
        public List<string>? BankStatement { get; set; }

        public List<string>? PrevOfferLetter { get; set; }

        public List<string>? Education { get; set; }

        public List<string>? Resume { get; set; }

        public List<string>? OfferLetter { get; set; }

        public List<string>? Others { get; set; }

    }

    public class FamilyMemberDetail
    {
        public long? ID { get; set; }

        //public long CID { get; set; }
        public string? FamilyMemberName { get; set; }
        public string? Relation { get; set; }
        public DateTime? Dob { get; set; }
    }

    public class ExperienceDetail
    {
        public long? ID { get; set; }

        //public long CID { get; set; }
        public string? NameOfCompany { get; set; }
        public string? WorkLocation { get; set; }
        public string? PositionHeld { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public decimal? LastCtc { get; set; }
    }

    public class QualificationDetail
    {
        public long? ID { get; set; }

        //public long CID { get; set; }
        public string? Education { get; set; }
        public string? Yop { get; set; }
        public string? Grade { get; set; }
        public string? Type { get; set; }
    }

    public class EmployeeUpdateInfo
    {
        public string EmployeeId { get; set; }
        public string CandidateId { get; set; }
        //public string EmployeeName { get; set; }

        public string FirstName { get; set; }

        public string MiddleName { get; set; }

        public string LastName { get; set; }


        public string Department { get; set; }
        public string ReportingHeadName { get; set; }

        public string ReportingHeadECode { get; set; }
        public string EmailAddress { get; set; }
        public string Mobile { get; set; }
    }
    /*
    public class EmployeeDataDto
    {
        public tblEmployee Employee { get; set; }
        public List<tblFamily> Family { get; set; }
        public List<tblExperience> Experience { get; set; }
        public List<tblQualification> Qualification { get; set; }
        public List<HRMSAPI.Data.CanidateDoc> Documents { get; set; }
    }
    */

    /*
    public class TempEmployeeDataDto
    {
        public tempTblEmployee Employee { get; set; }
        public List<tempTblFamily> Family { get; set; }
        public List<tempTblExperience> Experience { get; set; }
        public List<tempTblQualification> Qualification { get; set; }
        public List<tempCandidateDoc> Documents { get; set; }
    }
    */

    /*
    public class EmployeeCombinedDto
    {
        public EmployeeDataDto Original { get; set; }
        public TempEmployeeDataDto Updated { get; set; }
    }
    */
    public class EmployeeDetailsUpdateView
    {
        public long EmployeeId { get; set; }
        public  List<ChangedFieldDto>? EmployeeDetailsForUpdate { get; set; }

        public List<FamilyChangeDto>? FamilyDetailsForUpdate { get; set; }

        public List<ExperienceChangeDto>? ExperienceDetailsForUpdate { get; set; }

        public List<QualificationChangeDto>? QualificationDetailsForUpdate { get; set; }

        public List<DocumentChangeDto>? DocumentsDetailsForUpdate { get; set; }
    }

    public class ChangedFieldDto
    {
        public string FieldName { get; set; }
        public string OldValue { get; set; }
        public string NewValue { get; set; }
        public string IsApprovedField { get; set; }
        public bool? IsApproved { get; set;}
    }

    public class FamilyChangeDto
    {
        public long? FID { get; set; }
        public string ChangeType { get; set; } // "Added", "Deleted", or "Updated"
        public bool? Is_Approved { get; set; }
        public FamilyDDto OldData { get; set; }
        public FamilyDDto NewData { get; set; }
    }

    public class FamilyDDto
    {
        public long? EmpId { get; set; }
        public long? FID { get; set; }
        public string Family_Member_Name { get; set; }
        public string Relation { get; set; }
        public DateTime DOB { get; set; } // or DateTime if both are consistent

        public bool? Is_FamilyMemberName_Approved { get; set; }

        public bool? Is_Relation_Approved { get; set; }

        public bool? Is_DOB_Approved { get; set; }
    }

    public class ExperienceChangeDto
    {
        public long? EID { get; set; }
        public string ChangeType { get; set; } // "Added", "Deleted", or "Updated"

        public bool? Is_Approved { get; set; }
        public ExperienceDataDto OldData { get; set; }
        public ExperienceDataDto NewData { get; set; }
    }

    public class ExperienceDataDto
    {
        public long? EmpId { get; set; }
        public long? EID { get; set; }
        public string Name_of_Company { get; set; }
        public string Work_Location { get; set; }
        public string Position_Held { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public decimal? Last_CTC { get; set; }
        public bool? Is_NameOfCompany_Approved { get; set; }
        public bool? Is_WorkLocation_Approved { get; set; }
        public bool? Is_PositionHeld_Approved { get; set; }
        public bool? Is_FromDate_Approved { get; set; }
        public bool? Is_ToDate_Approved { get; set; }
        public bool? Is_LastCTC_Approved { get; set; }

    }

    public class QualificationChangeDto
    {
        public long? QID { get; set; }
        public string ChangeType { get; set; } // "Added", "Deleted", or "Updated"

        public bool? Is_Approved { get; set; }
        public QualificationDataDto OldData { get; set; }
        public QualificationDataDto NewData { get; set; }
    }

    public class QualificationDataDto
    {

        public long? EmpId { get; set; }
        public long? QID { get; set; }
        public string Education { get; set; }

        public string YOP { get; set; }

        public string Grade { get; set; }

        public string Type { get; set; }

        public bool? Is_Education_Approved { get; set; }


        public bool? Is_YOP_Approved { get; set; }


        public bool? Is_Grade_Approved { get; set; }


        public bool? Is_Type_Approved { get; set; }

    }

    public class DocumentChangeDto
    {
        public long? DID { get; set; }
        public string ChangeType { get; set; } // "Added", "Deleted", or "Updated"

        public bool? Is_Approved { get; set; }
        public DocumentDataDto OldData { get; set; }
        public DocumentDataDto NewData { get; set; }
    }

    public class DocumentDataDto
    {
        public long? EmpId { get; set; }
        public long? DID { get; set; }

        public string FilePath { get; set; }

        public string FileType { get; set; }

        public string FileSize { get; set; }
        //public bool? Is_Approved { get; set; }

    }


}
