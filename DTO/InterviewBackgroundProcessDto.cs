namespace HRMSAPI.DTO
{
    public class InterviewBackgroundProcessDto
    {
        public long CandidateId { get; set; }
        public string? Status { get; set; }        // optional → default PENDING
        public string? Remarks { get; set; }
    }
}
