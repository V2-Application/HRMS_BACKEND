using HRMSAPI.Data;
using HRMSAPI.Models.Candidate;

namespace HRMSAPI.DTO
{
    public class UpdateEmployeeDto
    {

    }

    public class CandidateInfo
    {
        public bool? isUanRegistered { get; set; }

        public string? reportHeadEcode;
        public bool fingerprintRegistered { get; set; } = false;
        public int? applicandId { get; set; }
        public bool isApplicant { get; set; }
        public string designationName { get; set; }
        public long id { get; set; }
        public long cid { get; set; }
        public string title { get; set; }
        public string fullName { get; set; }
        public string firstName { get; set; }
        public string middleName { get; set; }
        public string lastName { get; set; }
        public string husbandName { get; set; }
        public string fathersName { get; set; }
        public string mothersName { get; set; }
        public string designation { get; set; }
        public string location { get; set; }
        public DateTime dob { get; set; }
        public string gender { get; set; }
        public string department { get; set; }
        public DateTime joiningDate { get; set; }
        public string grossSalary { get; set; }
        public string uanNo { get; set; }
        public string panNo { get; set; }
        public string aadharNo { get; set; }
        public string nameOnAadhar { get; set; }
        public string udf1 { get; set; }
        public string placeOfBirth { get; set; }
        public string presentAddress { get; set; }
        public string presentAddressPinCode { get; set; }
        public string permanentAddress { get; set; }
        public string permanentAddressPinCode { get; set; }
        public string empCode { get; set; }
        public string applicantCode { get; set; }
        public string weeklyOff { get; set; }
        public string maritalStatus { get; set; }
        public string mobile { get; set; }
        public string emailAddress { get; set; }
        public bool isRelativeInCompany { get; set; }
        public string nationality { get; set; }
        public string religion { get; set; }
        public string bankName { get; set; }
        public string accountNo { get; set; }
        public string bankIfscCode { get; set; }
        public string beneficiaryAddress { get; set; }
        public string prevEstNo { get; set; }
        public string reference { get; set; }
        public string reference1LastCompany { get; set; }
        public string contact1LastCompany { get; set; }
        public string reference2LastCompany { get; set; }
        public string contact2LastCompany { get; set; }
        public string reference3LastCompany { get; set; }
        public string contact3LastCompany { get; set; }
        public string reference4LastCompany { get; set; }
        public string contact4LastCompany { get; set; }
        public string reference5LastCompany { get; set; }
        public string contact5LastCompany { get; set; }
        public string familyMemberName { get; set; }
        public string familyMemberRelation { get; set; }
        public DateTime? familyMemberDob { get; set; }
        public string company1 { get; set; }
        public string company2 { get; set; }
        public string company3 { get; set; }
        public string workLocation { get; set; }
        public string positionHeldInPreviousCompany { get; set; }
        public DateTime? from { get; set; }
        public DateTime? to { get; set; }
        public decimal? inHandSalary { get; set; }
        public string lastCtcAnnual { get; set; }
        public string highestQualification { get; set; }
        public DateTime createdOn { get; set; }
        public string createdBy { get; set; }
        public string updatedBy { get; set; }
        public DateTime updatedOn { get; set; }
        public string deletedBy { get; set; }
        public DateTime? deletedOn { get; set; }
        public bool isActive { get; set; }
        public bool isDeleted { get; set; }
        public bool isSalarySlipUploaded { get; set; }
        public bool isBankStatementUploaded { get; set; }
        public bool isPrevOfferLetterUploaded { get; set; }
        public bool isOfferLetterAttachmentUploaded { get; set; }
        public bool isPassportPhotoUploaded { get; set; }
        public bool isPanAttachmentUploaded { get; set; }
        public bool isAadharAttachmentUploaded { get; set; }
        public bool isAadharBackAttachmentUploaded { get; set; }
        public bool isBankPassbookAttachmentUploaded { get; set; }
        public bool isEducationAttachmentUploaded { get; set; }
        public bool isResumeAttachmentUploaded { get; set; }
        public bool IsOtherAttachmentUploaded { get; set; }
        public int statusId { get; set; }
        public decimal basicSalary { get; set; }
        public decimal hra { get; set; }
        public decimal cca { get; set; }
        public decimal specialAllowance { get; set; }
        public decimal da { get; set; }
        public decimal extraAllowance { get; set; }
        public decimal monthlyGrossCtc { get; set; }
        public decimal annuallyNetCtc { get; set; }
        public bool pfApplicable { get; set; }
        public bool esicApplicable { get; set; }
        public string? bonusApplicable { get; set; }
        public string statusHistory { get; set; }
        public string interviewRounds { get; set; }
        public string? esicno { get; set; }
        public long? reportingHeadId { get; set; }
        public string SkillType { get; set; }

        public string DifferentlyAbledRemarks { get; set; }

        public string DifferentlyAbledReason { get; set; }

        public bool? DifferentlyAbled { get; set; }
        public int? ShiftID { get; set; }
        public string? Source { get; set; }
        public string? ReferenceEmployee { get; set; }
        public string? PreferredLocation { get; set; }
        public string? AoCode { get; set; }
    }

    public class FamilyMember
    {
        //tblFamily
        public long? id { get; set; }
        public string familyMemberName { get; set; }
        public string relation { get; set; }
        public DateTime dob { get; set; }
        public string? key { get; set; }
    }

    public class Experience
    {
        public long? id { get; set; }
        public string nameOfCompany { get; set; }
        public string workLocation { get; set; }
        public string positionHeld { get; set; }
        public DateTime from { get; set; }
        public DateTime to { get; set; }
        public decimal lastCtc { get; set; }
        public string? key { get; set; }
        //public decimal inHand { get; set; }
    }

    public class Qualification
    {
        public long? id { get; set; }
        public string education { get; set; }
        public string yop { get; set; }
        public string grade { get; set; }
        public string type { get; set; }
        public string? key { get; set; }
    }

    public class Document
    {
        public int id { get; set; }
        public int candidateId { get; set; }
        public string filePath { get; set; }
        public string documentType { get; set; }
        public string fileSize { get; set; }
    }

    public class AssignedLocationDTO
    {
        public long? id { get; set; }
        public int assignedLocation { get; set; }
        public string assignedReason { get; set; }
        public bool isActive { get; set; }
        public DateTime assignedOnDate { get; set; }
        public DateTime? releasedOnDate { get; set; }
        public bool? PermanentTransfer { get; set; }
        public bool? TemporaryTransfer { get; set; }
        public int? DepartmentId { get; set; }
        public int? DesignationId { get; set; }
        public int? TransferApprovalStatus { get; set; }
        public int? IsReportingHeadApproval { get; set; }
        public int? IsHRApproval { get; set; }

    }

    public class UpdateEmployee
    {
       
        public bool? fingerprintRegistered { get; set; } 

        public CandidateInfo? candidateInfo { get; set; }
        public List<FamilyMember>? familyMembersList { get; set; }
        public List<Experience>? experienceList { get; set; }
        public List<Qualification>? qualificationList { get; set; }
        //public List<Document> documents { get; set; }
        public AssignedLocationDTO? assignLocations { get; set; }
        //public List<AssignedLocationDTO> assignLocations { get; set; }
        public CandidateDocs? NewDocs { get; set; }
    }
    public class UpdateEmployeeDocs { 
        public string Type { get; set; }
        public IFormFile File { get; set; }
    }
}
