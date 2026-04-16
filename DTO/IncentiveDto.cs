namespace HRMSAPI.DTO
{
    public sealed class IncentiveAttachmentDto
    {
        public long? AttachmentId { get; set; }
        public long? IncentiveId { get; set; }
        public string? FileName { get; set; }
        public string? FileType { get; set; }
        public long? FileSizeBytes { get; set; }
        public string? FilePath { get; set; }
        public DateTime? UploadedAt { get; set; }
    }

    public sealed class IncentiveUpsertForm
    {
        // master fields
        public long? IncentiveId { get; set; }
        public string? Ecode { get; set; }
        public DateTime? Month { get; set; }        // first day of month (e.g., 2025-09-01)
        public decimal? Amount { get; set; }
        public string? Remarks { get; set; }
        public string? CreatedBy { get; set; }

        // stage-specific statuses (map to tblStatus.StatusId)
        public int? CmdStatusId { get; set; }
        public int? HrStatusId { get; set; }

        // stage remarks
        public string? CmdRemarks { get; set; }
        public string? HrRemarks { get; set; }

        // attachments (multipart/form-data)
        public IFormFileCollection? Attachments { get; set; }
        public bool? ReplaceAttachments { get; set; }
    }

    public sealed class IncentiveDto
    {
        public long? IncentiveId { get; set; }
        public string? Ecode { get; set; }
        public DateTime? Month { get; set; }
        public decimal? Amount { get; set; }
        public string? Remarks { get; set; }
        public string? CreatedBy { get; set; }

        // Overall roll-up status
        public int? StatusId { get; set; }
        public string? StatusName { get; set; }   // maps to OverallStatusName in SP

        // Stage-specific status
        public int? CmdStatusId { get; set; }
        public string? CmdStatusName { get; set; }
        public int? HrStatusId { get; set; }
        public string? HrStatusName { get; set; }

        // Stage remarks
        public string? CmdRemarks { get; set; }
        public string? HrRemarks { get; set; }

        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public List<IncentiveAttachmentDto>? Attachments { get; set; }
    }


    public sealed class BulkSkipRow
    {
        public int RowNo { get; set; }
        public string? Ecode { get; set; }
        public DateTime? Month { get; set; }
        public string? Reason { get; set; }
    }
}
