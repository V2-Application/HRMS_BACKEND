namespace HRMSAPI.DTO
{
    public class UpdateBankTransferStatusRequestDto
    {
        public long Id { get; set; }
        public int StatusId { get; set; }
        public string BatchId { get; set; }
    }
}
