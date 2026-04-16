namespace HRMSAPI.DTO
{
    public sealed class FnfDeductionsDto
    {
        public long EmployeeId { get; set; }
        public decimal? LoanBalance { get; set; }
        public decimal? AdvanceBalance { get; set; }
        public decimal? OtherDeduction1 { get; set; }
        public decimal? OtherDeduction2 { get; set; }
        public decimal? OtherDeduction3 { get; set; }
        public decimal? OtherDeduction4 { get; set; }
        public decimal? TotalPayable { get; set; }
        public decimal? TDS { get; set; }
        public decimal? NetPayable { get; set; }
        public decimal? DepositOn { get; set; }
        public string? User { get; set; }
    }
}
