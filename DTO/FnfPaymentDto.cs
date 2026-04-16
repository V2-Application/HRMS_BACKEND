namespace HRMSAPI.DTO
{
    public sealed class FnfPaymentDto
    {
        public long FNFId { get; set; }
        public decimal? SendForPaymentAmount { get; set; }
        public string? Remarks { get; set; }
        public string? ChequeNo { get; set; }
        public DateTime? ChequeDate { get; set; }
        public string? Status { get; set; }
        public decimal? AmountPaid { get; set; }
        public string? PaymentVoucherNo { get; set; }
        public string? CreatedBy { get; set; }
    }
}
