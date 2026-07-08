using Newtonsoft.Json;

namespace HRMSAPI.DTO
{
    public sealed class FnfBulkUploadResponseDto
    {
        public bool Success { get; set; }
        public int ProcessedCount { get; set; }          // brand-new FNFs inserted
        public int UpdatedCount { get; set; }             // Processed -> Completed updates
        public int TotalRecords { get; set; }
        public List<string> DuplicateEcodes { get; set; } = new();
        public List<string> ErrorMessages { get; set; } = new();
        public List<string> AlreadyDoneEcodes { get; set; } = new();
        // Skipped rows (duplicate-in-file / unknown ecode / already-completed) with reason,
        // so the UI can show them and let the user download the duplicate data.
        public List<FnfDuplicateRowDto> DuplicateRows { get; set; } = new();
        public string Message { get; set; } = string.Empty;
    }

    public sealed class FnfDuplicateRowDto
    {
        public string Ecode { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;   // why it was skipped
        public decimal? TotalPayable { get; set; }
        public decimal? NetPayable { get; set; }
        public string? PaymentStatus { get; set; }
        public string? ChequeNo { get; set; }
        public string? PaymentVoucherNo { get; set; }
        public string? PaymentRemarks { get; set; }
    }
}
