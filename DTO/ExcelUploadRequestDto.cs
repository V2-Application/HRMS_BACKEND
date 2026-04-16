namespace HRMSAPI.DTO
{
    public class ExcelUploadRequestDto
    {
        public IFormFile File { get; set; }
    }

    public class ExcelUploadRowDto
    {
        public string ProcessId { get; set; }
        public string GivenToBank { get; set; }
        public string PaidByCash { get; set; }
    }
}

