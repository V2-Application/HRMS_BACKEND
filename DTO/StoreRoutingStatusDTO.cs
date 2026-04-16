namespace HRMSAPI.DTO
{
    public class StoreRoutingStatusDTO
    {
        public int StoreRoutingMasterId { get; set; }
        public string StagingName { get; set; }
        public string RoutingName { get; set; }
        public string? BgtTimeline { get; set; }
        public int? TransactionId { get; set; }
        public string Remarks { get; set; }
        public DateTime? ActionDate { get; set; }
        public int? ActionById { get; set; }
        public List<string> Attachments { get; set; } = new List<string>();
    }
}
