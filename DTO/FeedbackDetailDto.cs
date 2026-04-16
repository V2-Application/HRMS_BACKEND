namespace HRMSAPI.DTO
{
    public class FeedbackDetailDto
    {
        public string? FinalRemarks { get; set; }
        public List<FeedbackReviewDto>? Reviews { get; set; }
    }
}
