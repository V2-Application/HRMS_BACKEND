using HRMSAPI.Data;
using HRMSAPI.Models.EvalutionForm;
using System.ComponentModel.DataAnnotations;

namespace HRMSAPI.Models.EvalutionForm
{

    public class FamilyDto
    {
        public string? Name { get; set; }
        public string? Relation { get; set; }
        public string? Occupation { get; set; }
        public string? Dependent { get; set; }
    }
    public class ExperienceDto
    {
        public int? TotalIndustryExperienceYears { get; set; }
        public int? TotalRetailExperienceYears { get; set; }
        public int? NoticePeriodDays { get; set; }
        public decimal? CurrentCTC { get; set; }
        public decimal? ExpectedCTC { get; set; }
    }

    public class KRAKPIDto
    {
        public string? KRA { get; set; }
        public string? KPI { get; set; }
    }

    public class StrengthWeaknessDto
    {
        public string? Strength { get; set; }
        public string? Weakness { get; set; }

        public string? Biggestchallenges { get; set; }
    }

    public class QuestionDto
    {
        public string? Question { get; set; }
        public string? Answer { get; set; }
    }

    public class ReferenceDto
    {
        public string? FullName { get; set; }
        public string? Company_Designation { get; set; }
        public string? Contact_Details { get; set; }
        public string? Bussiness_Details { get; set; }
    }



    public class InterviewFormRequest
    {
        public int? PositionAppliedId { get; set; }
        public int? ApplicantCode { get; set; }
        public string? PreferredWorkLocationIds { get; set; }
        public string? Name { get; set; }
        public string? MaritalStatus { get; set; }
        public string? PresentAddress { get; set; }
        public bool? DeclarationConfirmed { get; set; }
        public string? Place { get; set; }

        public string? Ques1 { get; set; }


        public string? Ques2 { get; set; }

        public string? Ques3 { get; set; }

        public string? BiggestChallenges { get; set; }

        public string? Strength1 { get; set; }

        public string? Strength2 { get; set; }


        public string? weakness1 { get; set; }

        public string? weakness2 { get; set; }

        public DateTime DateOfFilling { get; set; }
        public List<FamilyDto>? FamilyInfo { get; set; }
        public List<ExperienceDto>? ExperienceInfo { get; set; }
        public List<KRAKPIDto>? KRAKPIInfo { get; set; }
        //public List<StrengthWeaknessDto> StrengthWeaknessInfo { get; set; }
        //public List<QuestionDto> QuestionInfo { get; set; }

        public List<ReferenceDto>? ReferenceInfo { get; set; }
    }

    public class ApplicantDto
    {

        public int ApplicantId { get; set; }
        public bool IsApplicant { get; set; }
        public string FullName { get; set; }
        public int DesignationId { get; set; }
        public int LocationId { get; set; }
    }

    public class ApplicantInterviewFormDetail
    {
        public int PositionAppliedId { get; set; }
        public string PreferredWorkLocationIds { get; set; }
        public string Name { get; set; }
        public string MaritalStatus { get; set; }
        public string PresentAddress { get; set; }
        public bool DeclarationConfirmed { get; set; }
        public string Place { get; set; }
        public DateTime DateOfFilling { get; set; }
        public int ApplicantCode { get; set; }

        public List<FamilyDto> FamilyInfo { get; set; }
        public List<ExperienceDto> ExperienceInfo { get; set; }
        public List<KRAKPIDto> KRAKPIInfo { get; set; }
        public List<ReferenceDto> ReferenceInfo { get; set; }

        public string Ques1 { get; set; }
        public string Ques2 { get; set; }
        public string Ques3 { get; set; }

        public string Strength1 { get; set; }
        public string Strength2 { get; set; }
        public string Weakness1 { get; set; }
        public string Weakness2 { get; set; }
        public string BiggestChallenges { get; set; }
    }


   




   


}
