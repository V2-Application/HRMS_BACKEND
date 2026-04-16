namespace HRMSAPI.DTO
{
    public class PayRollSummaryDto
    {
        public string? LocationName { get; set; }
        public string? STCode { get; set; }
        public string? Ecode { get; set; }
        public string? MonthYear { get; set; }
        public decimal? PayableSalary { get; set; }
        public decimal? GiventoBank { get; set; }
        public decimal? PaidByBank { get; set; }
        public decimal? ReturnByBank { get; set; }
        public decimal? DifferencePayableMinusGiven { get; set; }
        public decimal? DifferencePayableMinusPaid { get; set; }
        public decimal? DifferencePayableMinusReturned { get; set; }
        public decimal? DifferenceGivenMinusPaid { get; set; }
        public decimal? DifferenceGivenMinusReturned { get; set; }
    }

    public class TotalSummary
    {
        public decimal? TotalPayableSalary { get; set; }
        public decimal? TotalGivenToBank { get; set; }
        public decimal? TotalPaidByBank { get; set; }
        public decimal? TotalReturnByBank { get; set; }
        public decimal? TotalDifferencePayableMinusGiven { get; set; }
        public decimal? TotalDifferencePayableMinusPaid { get; set; }
        public decimal? TotalDifferencePayableMinusReturned { get; set; }
        public decimal? TotalDifferenceGivenMinusPaid { get; set; }
        public decimal? TotalDifferenceGivenMinusReturned { get; set; }
    }

    public class PayrollSummaryResponseDto
    {
        public List<PayRollSummaryDto> PayrollRecords { get; set; } = new List<PayRollSummaryDto>();
        public TotalSummary Totals { get; set; } = new TotalSummary();
    }

}

