using HRMSAPI.Data;
using HRMSAPI.Models.Abstract;
using System.Text.Json.Serialization;

namespace HRMSAPI.Models.Candidate
{
    public class CandidateUpdate : CandidateAbstract
    {
        public long? reportingHeadId { get; set; }
        public string? husbandName { get; set; }
        public string? location { get; set; }
        public string? department { get; set; }
        // Sub-department chain selections (ids from tblSubDepartment). Optional; arrive as
        // form-field strings like `department`. Parsed to int? when persisted.
        public string? subDepartmentId1 { get; set; }
        public string? subDepartmentId2 { get; set; }
        public string? subDepartmentId3 { get; set; }
        public DateTime? joiningDate { get; set; }
        public string? grossSalary { get; set; }
        [JsonPropertyName("uanNo")]
        public string? uanNo { get; set; }
        public string? status { get; set; }
        public string? permanentAddressPinCode { get; set; }
        public string? empCode { get; set; }
        public string? applicantCode { get; set; }
        public string? weeklyOff { get; set; }
        public bool? isAnyRelative { get; set; }
        public string? beneficaryAddress { get; set; }
        public string? previousEstno { get; set; }
        public string? reference { get; set; }
        //public int? statusid { get; set; }
        // ... other properties ...
        public string? FamilyMembersListJson { get; set; }  // Renamed for clarity
        public string? ExperienceListJson { get; set; }
        public string? QualificationListJson { get; set; }
        public string? AssignLocationsListJson { get; set; }
        // Keep the list properties for internal use
        public List<CandidateUpdateFamilyMember> familyMembersList { get; set; } = new List<CandidateUpdateFamilyMember>();
        public List<CandidateUpdateExperience> experienceList { get; set; } = new List<CandidateUpdateExperience>();
        public List<CandidateUpdateQualification> qualificationList { get; set; } = new List<CandidateUpdateQualification>();
        public List<AssignLocationHistoryrecord> assignLocations { get; set; } = new List<AssignLocationHistoryrecord>();
        // new properties 
        public decimal? BasicSalary { get; set; }
        public decimal? HRA { get; set; }
        public decimal? CCA { get; set; }
        public decimal? SpecialAllowance { get; set; }
        public decimal? DA { get; set; }
        public decimal? ExtraAllowance { get; set; }

        public decimal? monthlyGrossCTC { get; set; }

        public decimal? annuallyNetCTC { get; set; }
        // for applicant
        public decimal? TotalExperience { get; set; }
        public decimal? SalaryExpectation { get; set; }
        public string? AdditionalInfoApplicant { get; set; }
        public bool? Aggreement { get; set; } = false;
        public bool? IsApplicant { get; set; }
        public string? CurrentLocation { get; set; }
        public decimal? NoticePeriod { get; set; }

        // Current employment as captured on the applicant form. These write the
        // Candidate columns the applicant grid/list SP reads ([COMPANY 1],
        // [POSITION HELD IN PREVIOUS COMPANY], [LAST CTC(ANNUAL)]). The form also
        // posts the same values via experienceList, which persists to tblExperience
        // for the Excel export — both views are fed so they agree.
        public string? company1 { get; set; }
        public string? positionHeldInPreviousCompany { get; set; }
        public string? lastCtcAnnual { get; set; }

    }
    public class CandidateUpdateFamilyMember
    {
        public string familyMemberName { get; set; } = string.Empty;
        public string relation { get; set; } = string.Empty;
        public DateTime dob { get; set; }
    }
    public class CandidateUpdateFamilyMemberNew : CandidateUpdateFamilyMember
    {
        public long Id { get; set; }
    }
    public class CandidateUpdateExperience
    {
        // Nullable: the applicant form captures only company / position / last CTC.
        // As non-nullable reference types these were implicitly required, so posting a
        // partial experience row failed model validation with a 400.
        public string? nameOfCompany { get; set; }
        public string? workLocation { get; set; }
        public string? positionHeld { get; set; }
        // Nullable to match tblExperience.From/To (both DateTime?). As non-nullable
        // DateTime, a JSON "from":null threw inside DeserializeList, which swallows
        // JsonException and returns an EMPTY list — so one blank date silently
        // discarded every experience row for that candidate.
        public DateTime? from { get; set; }
        public DateTime? to { get; set; }
        public decimal? lastCtc { get; set; }
        public decimal? inHand { get; set; } // Add this property
    }
    public class CandidateUpdateExperienceNew : CandidateUpdateExperience
    { 
        public long Id { get; set; }
    }
        public class CandidateUpdateQualification
    {
        public string education { get; set; } = string.Empty;
        public string yop { get; set; } = string.Empty;
        public string grade { get; set; } = string.Empty;
        public string type { get; set; } = string.Empty;
    }
    public class CandidateUpdateQualificationNew : CandidateUpdateQualification { 
        public long Id { get; set; }
    }

   public class AssignLocationHistoryrecord
    {
        public long? CandidateId { get; set; }

        public int? assignedLocation { get; set; }

        public string assignedReason { get; set; }

        public bool? isActive { get; set; }

        public DateTime assignedOnDate { get; set; }

        public DateTime? releasedOnDate { get; set; }
        public int TransferApprovalStatus { get; set; }
        public int IsReportingHeadApproval { get; set; }
        public int IsHRApproval { get; set; }

    }

    public class CandidateOfferLetter
    {
       
        public int ApplicantId { get; set; }
        public string? Email { get; set; }
    

       

    }
    public class InterviewScheduleDto
    {
        public int ApplicantId { get; set; }
        public string CandidateName { get; set; }
        public DateTime InterviewDateTime { get; set; }
        public int RoundId { get; set; }
        public string Status { get; set; }
    }
}