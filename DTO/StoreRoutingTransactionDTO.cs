namespace HRMSAPI.DTO
{

    public class StoreRoutingTransactionDTO
    {
        public int LocationId { get; set; }
        public int StoreRoutingMasterId { get; set; }
        public string? Remarks { get; set; }
        public int ActionById { get; set; }
        public IFormFileCollection? Attachments { get; set; }
    }
    
    public class Attachments1
    {
        public int AttachmentId { get; set; }
        public string Attachment { get; set; }
    }

    public class StoreRoutingDetail
    {
        public int StoreRoutingMasterId { get; set; }
        public string StagingName { get; set; }
        public string RoutingName { get; set; }
        public string BgtTimeline { get; set; }
        public int? StagingSequence { get; set; }
        public int? RoutingSequence { get; set; }
        public int? TransactionId { get; set; }
        public int? LocationId { get; set; }
        public string Remarks { get; set; }
        public DateTime? ActionDate { get; set; }
        public int? ActionById { get; set; }
        public List<Attachments1> Attachments { get; set; }
        public string Status { get; set; }
    }

    public class StoreRoutingSummary
    {
        public int LocationId { get; set; }
        public int TotalRoutingSteps { get; set; }
        public int CompletedSteps { get; set; }
        public int PendingSteps { get; set; }
        public string OverallStatus { get; set; }
    }

    public class StoreRoutingResponse
    {
        public List<StoreRoutingDetail> Details { get; set; }
        public StoreRoutingSummary Summary { get; set; }
    }
}
