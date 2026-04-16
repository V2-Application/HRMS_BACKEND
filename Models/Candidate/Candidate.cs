using HRMSAPI.Data;
using HRMSAPI.Models.Abstract;
using System.ComponentModel.DataAnnotations;

namespace HRMSAPI.Models.Candidate
{

    public class Candidate : CandidateAbstract
    {
       

        public string? applicandId {  get; set; }
        public bool isApplicant { get; set; } = false;
        public string designationName { get; internal set; }
        public string? companyName { get; internal set; }
        public string? esicno { get; set; }
    }
    public class CandidateDocs
    {
        public List<IFormFile>? Last3SalarySlip { get; set; }
        
        public IFormFile? Last3BankStatement { get; set; }
       
        public IFormFile? PrevOfferLetter { get; set; }
        public IFormFile? PassportPhoto { get; set; }

       
        public List<IFormFile>? PanAttachment { get; set; }

     
        public List<IFormFile>? AadharAttachment { get; set; }
        public List<IFormFile>? AadharBackAttachment { get; set; }


        public List<IFormFile>? BankPassbookAttachment { get; set; }

       
        public List<IFormFile>? EducationAttachment { get; set; }
        public List<IFormFile>? ResumeAttachment { get; set; }
        public List<IFormFile>? EvaluationAttachment { get; set; }
        public List<IFormFile>? OfferLetterAttachment  { get; set; }
        public List<IFormFile>? InterviewVideo { get; set; }
        public List<IFormFile>? OtherAttachment { get; set; }
        public List<IFormFile>? BankStatementVideo { get; set; }
        public List<IFormFile>? Form11Attachment { get; set; }
        public List<IFormFile>? GratuityFormAttachment { get; set; }
        public List<IFormFile>? Form2Attachment { get; set; }

    }

    public class CandidateOfferLetterDoc
    {

        public List<IFormFile>? OfferLetterAttachment { get; set; }
   

    }

    public class CandidateDocument
    {
        // public List<IFormFile>? documentAttachment { get; set; }
        public IFormFile? documentAttachment { get; set; }
    }
    // DTO for document details
    public class CandidateDocumentDto
    {
        public long Id { get; set; }
        public long CandidateId { get; set; }
        public string FilePath { get; set; }
        public string DocumentType { get; set; }
        public string FileSize { get; set; }
       
    }
    public class ValidateFileListAttribute : ValidationAttribute
    {
        private readonly int _requiredCount;

        public ValidateFileListAttribute(int requiredCount)
        {
            _requiredCount = requiredCount;
        }

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value is List<IFormFile> fileList)
            {
                if (fileList.Count == 0 || fileList.Count == _requiredCount)
                {
                    return ValidationResult.Success;
                }
                else
                {
                    return new ValidationResult($"The list must contain exactly {_requiredCount} files.");
                }
            }
            else if (value == null)
            {
                return ValidationResult.Success;
            }

            return new ValidationResult("Invalid file list.");
        }
    }
}
