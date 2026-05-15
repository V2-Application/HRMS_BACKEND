namespace HRMSAPI.DTO
{
    public class BulkInactivateRequest
    {
        public string? EmployeeIdsCsv { get; set; }
        public string? EcodesCsv { get; set; }
        public IFormFile? EcodeExcel { get; set; }

        public int ResignationTypeId { get; set; }
        public int? AbscondingReasonId { get; set; }
        public int? BlackListReasonId { get; set; }
        public int? reasonid { get; set; }

        public DateTime? LeavingDate { get; set; }
        public string? Remarks { get; set; }
        public string? LastUpdatedBy { get; set; }

        public string? ChecklistResponsesJson { get; set; }

        public IFormFile? Attachment { get; set; }
    }

    public class BulkInactivateChecklistItem
    {
        public int MasterId { get; set; }
        public bool Response { get; set; }
    }

    public class BulkInactivateResult
    {
        public int InactivatedCount { get; set; }
        public List<string> FailedEcodes { get; set; } = new();
        public List<string> Messages { get; set; } = new();
    }
}
