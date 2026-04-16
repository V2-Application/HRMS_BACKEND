namespace HRMSAPI.DTO
{
    public class BulkUploadResult
    {
        public int TotalRows { get; set; }
        public int Inserted { get; set; }
        public int Updated { get; set; }
        public List<string> Errors { get; set; } = new();
    }

}
