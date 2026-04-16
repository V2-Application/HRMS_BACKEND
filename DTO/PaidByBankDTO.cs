
// DTO class for tblPaidByBank
public class PaidByBankDTO
{
    public string Ecode { get; set; }
    public string AC { get; set; }
    public string PaidByBank { get; set; }
    public string CreatedBy { get; set; }
    public DateTime? CreatedOn { get; set; }
    public string LastUpdatedBy { get; set; }
    public DateTime? LastUpdatedOn { get; set; }
    public int tblPaidByBankId { get; set; }
    public DateTime? Date { get; set; }
    public string UTR { get; set; }
    public string? Remarks { get; set; }
}
