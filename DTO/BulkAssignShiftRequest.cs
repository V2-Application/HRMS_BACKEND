namespace HRMSAPI.DTO
{
    public class BulkAssignShiftRequest
    {
        public string? EcodesCsv { get; set; }
        public IFormFile? EcodeExcel { get; set; }

        public int ShiftId { get; set; }
        public DateTime EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
        public string? AssignedBy { get; set; }
        public string? Remarks { get; set; }
    }

    public class BulkAssignShiftResult
    {
        public int TotalSubmitted { get; set; }
        public int Processed { get; set; }
        public int AlreadyOnShift { get; set; }
        public List<string> NotFoundEcodes { get; set; } = new();
        public List<BulkAssignShiftError> Errors { get; set; } = new();
    }

    public class BulkAssignShiftError
    {
        public string Ecode { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
