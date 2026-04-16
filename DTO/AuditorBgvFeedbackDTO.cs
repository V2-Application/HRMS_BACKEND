namespace HRMSAPI.DTO
{
    public class AuditorBgvFeedbackDTO
    {
        public long BgvId { get; set; }
        public int Status { get; set; }
        public string Remarks { get; set; }

        public DateTime AuditDate { get; set; }
    }
}
