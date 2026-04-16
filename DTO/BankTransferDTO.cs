namespace HRMSAPI.DTO
{
    // DTO class for tblBankTransfer
    public class BankTransferDTO
    {
        public int BankTransferId { get; set; }
        public string Ecode { get; set; }
        public string AC { get; set; }
        public string BankTransfer { get; set; }
        public string CreatedBy { get; set; }
        public DateTime? CreatedOn { get; set; }
        public string LastUpdatedBy { get; set; }
        public DateTime? LastUpdatedOn { get; set; }
        public DateTime? Date { get; set; }
    }

}
