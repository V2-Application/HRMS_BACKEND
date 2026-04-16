namespace HRMSAPI.DTO
{
    public class CandidateChecklistDto
    {
        public long? CandidateId { get; set; }
        public bool? IsSalarySlipUploaded { get; set; }
        public bool? IsBankStatementUploaded { get; set; }
        public bool? IsPrevOfferLetterUploaded { get; set; }
        public bool? IsPassportPhotoUploaded { get; set; }
        public bool? IsPanAttachmentUploaded { get; set; }
        public bool? IsAadharAttachmentUploaded { get; set; }
        public bool? IsBankPassbookAttachmentUpoaded { get; set; }
        public bool? IsEducationAttachmentUploaded { get; set; }
        public bool? IsEvaluationAttachmentUploaded { get; set; }
        public bool? IsOfferLetterAttachmentUploaded { get; set; }
        public bool? IsInterviewVideoUploaded { get; set; }
        public bool? IsResumeUploaded { get; set; }
    }

}
