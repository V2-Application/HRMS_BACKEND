namespace HRMSAPI.DTO
{
    public class BgvCandidateDetailDTO
    {
        public string? CandidateName { get; set; }
        public string? CandidateDocs { get; set; }
        public string? CandidateExperience { get; set; }
        public string? Designation { get; set; }
        public DateTime? JoiningDate { get; set; }

        public decimal? CTC { get; set; }
    }
}
