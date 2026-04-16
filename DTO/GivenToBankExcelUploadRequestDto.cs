namespace HRMSAPI.DTO
{
    public class GivenToBankExcelUploadRequestDto
    {
        public IFormFile File { get; set; }
    }

    public class GivenToBankExcelUploadRowDto
    {
        public string BatchId { get; set; } // ProcessId - formatted batch number
        public string TransactionId { get; set; } // FormattedId - GTB_00000001 format
        public string PaidByBank { get; set; } // true/false
        public string ReturnByBank { get; set; } // true/false
    }
}

