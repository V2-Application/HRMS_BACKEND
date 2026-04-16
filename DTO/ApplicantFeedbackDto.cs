namespace HRMSAPI.DTO
{
    public class ApplicantFeedbackDto
    {
        public string? CandidateName { get; set; }

        public string? Email { get; set; }

        public string? Designation { get; set; }

        public string? Mobile { get; set; }

        public int? RoundId { get; set; }

        public string? Interviewer { get; set; }

        public FeedbackDetailDto? Feedback { get; set; }

        public string? Status { get; set; }

        public string? InterviewerStatus { get; set; }

        public string? CreatedBy { get; set; }

        public DateTime? CreatedOn { get; set; }
    }
}
