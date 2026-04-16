using Newtonsoft.Json;

namespace HRMSAPI.DTO
{
    public sealed class FnfBulkUploadResponseDto
    {
        public bool Success { get; set; }
        public int ProcessedCount { get; set; }
        public int TotalRecords { get; set; }
        public List<string> DuplicateEcodes { get; set; } = new();
        public List<string> ErrorMessages { get; set; } = new();
        public List<string> AlreadyDoneEcodes { get; set; } = new();
        public string Message { get; set; } = string.Empty;
    }
}
