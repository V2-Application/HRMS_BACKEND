using HRMSAPI.Data;
using System;
using System.ComponentModel.DataAnnotations;

namespace HRMSAPI.Models.Abstract
{
    public abstract class CandidateAbstract
    {
        public string? Source { get; set; }
        public string? ReferenceEmployee { get; set; }
        public int? ShiftID { get; set; }
        public string? differentlyAbledRemarks { get; set; }
        public bool? differentlyAbled { get; set; }
        public string? differentlyAbledReason { get; set; }
        public string? skillType { get; set; }
        public bool? isOtherAttachmentUploaded { get; set; } = false;
        public bool fingerprintRegistered { get; set; } = false;
        public long id { get; set; } = 0;
        public long cid { get; set; } = 0;
        public string? title { get; set; }
        public string? fullName { get; set; }
        public string? firstName { get; set; }
        public string? middleName { get; set; }
        public string? lastName { get; set; }
        public string? husbandName { get; set; }
        public string? fathersName { get; set; }
        public string? mothersName { get; set; }
        public string? designation { get; set; }
        public string? location { get; set; }
        public DateTime? dob { get; set; }
        public string? gender { get; set; }
        public string? department { get; set; }
        public DateTime? joiningDate { get; set; }
        public string? grossSalary { get; set; }
        public string? uanNo { get; set; }
        public string? panNo { get; set; }
        public string? aadharNo { get; set; }
        public string? nameOnAadhar { get; set; }
        public string? udf1 { get; set; }
        public string? placeOfBirth { get; set; }
        public string? presentAddress { get; set; }
        public string? presentAddressPinCode { get; set; }
        public string? permanentAddress { get; set; }
        public string? permanentAddressPinCode { get; set; }
        public string? empCode { get; set; }
        public string? applicantCode { get; set; }
        public string? weeklyOff { get; set; }
        public string? maritalStatus { get; set; }
        public string? mobile { get; set; }
        public string? emailAddress { get; set; }
        public bool? isRelativeInCompany { get; set; }
        public string? nationality { get; set; }
        public string? religion { get; set; }
        public string? bankName { get; set; }
        public string? accountNo { get; set; }
        public string? bankIfscCode { get; set; }
        public string? beneficiaryAddress { get; set; }
        public string? prevEstNo { get; set; }
        public string? reference { get; set; }
        public string? reference1LastCompany { get; set; }
        public string? contact1LastCompany { get; set; }
        public string? reference2LastCompany { get; set; }
        public string? contact2LastCompany { get; set; }
        public string? reference3LastCompany { get; set; }
        public string? contact3LastCompany { get; set; }
        public string? reference4LastCompany { get; set; }
        public string? contact4LastCompany { get; set; }
        public string? reference5LastCompany { get; set; }
        public string? contact5LastCompany { get; set; }
        public string? familyMemberName { get; set; }
        public string? familyMemberRelation { get; set; }
        public DateTime? familyMemberDob { get; set; }
        public string? PreferredLocation { get; set; }
        public string? company1 { get; set; }
        public string? company2 { get; set; }
        public string? company3 { get; set; }
        public string? workLocation { get; set; }
        public string? positionHeldInPreviousCompany { get; set; }
        public DateTime? from { get; set; }
        public DateTime? to { get; set; }
        public string? inHandSalary { get; set; }
        public string? lastCtcAnnual { get; set; }
        public string? highestQualification { get; set; }
        public DateTime? createdOn { get; set; }
        public string? createdBy { get; set; }
        public string? updatedBy { get; set; }
        public DateTime? updatedOn { get; set; }
        public string? deletedBy { get; set; }
        public DateTime? deletedOn { get; set; }
        public bool isActive { get; set; } = true;
        public bool isDeleted { get; set; } = false;
        public bool? isSalarySlipUploaded { get; set; }
        public bool? isBankStatementUploaded { get; set; }
        public bool? isPrevOfferLetterUploaded { get; set; }
        public bool? isPassportPhotoUploaded { get; set; }
        public bool? isPanAttachmentUploaded { get; set; }
        public bool? isAadharAttachmentUploaded { get; set; }
        public bool? isAadharBackAttachmentUploaded { get; set; }
        public bool? isBankPassbookAttachmentUploaded { get; set; }
        public bool? isEducationAttachmentUploaded { get; set; }
        public bool? isResumeAttachmentUploaded { get; set; } = false;
        public bool? isOfferLetterAttachmentUploaded { get; set; } = false;
      
        public int? statusId { get; set; } = 4;
        public decimal? BasicSalary { get; set; }
        public decimal? HRA { get; set; }
        public decimal? CCA { get; set; }
        public decimal? SpecialAllowance { get; set; }
        public decimal? DA { get; set; }
        public decimal? ExtraAllowance { get; set; }

        public decimal? monthlyGrossCTC { get; set; }

        public decimal? annuallyNetCTC { get; set; }
     
        public bool? PFApplicable { get; set; } = false;
        public bool? ESICApplicable { get; set; } = false;
        public bool? bonusApplicable { get; set; } = false;
        public List<CandidateStatusHistory>? StatusHistory { get; set; }
        public List<InterviewRound>? InterviewRounds { get; set; }
        public int? companyId { get; set; }
        public long? reportingHeadId { get; set; }
        public string? reportingHeadName { get; set; }
        public string? reportinHeadEcode { get; set; }
        public DateTime? LastWorkingDay { get; set; }
        public bool? IsUANRegistered { get; set; }
        public string? AoCode { get; set; }
        public int? StateId { get; set; }
    }
}