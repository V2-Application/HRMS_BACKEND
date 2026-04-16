namespace HRMSAPI.DTO
{
    public class EmployeePayrollDTO
    {
        public long EmployeePayRollId { get; set; }
        public string? Location { get; set; }
        public string? Ecode { get; set; }
        public decimal BGT_Salary { get; set; }
        public decimal? Payable_Days { get; set; }
        public decimal Gross_Salary { get; set; }
        public decimal Total_Deduction { get; set; }
        public decimal Payable_Salary { get; set; }
        public decimal PF { get; set; }
        public decimal ESI { get; set; }
        public decimal TDS { get; set; }
        public decimal P_TAX { get; set; }
        public decimal CASH_SHORT { get; set; }
        public decimal DIESEL { get; set; }
        public decimal PENALTY { get; set; }
        public decimal LOAN { get; set; }
        public decimal OT_AMT { get; set; }
        public decimal INCENTIVE_AMT { get; set; }
        public decimal FOODING_ALL { get; set; }
        public decimal ARRERS { get; set; }
        public decimal EXTRA_DAYS_ALLOWANCE { get; set; }
        public DateTime CretedOn { get; set; }
        public string? CreatedBy { get; set; }
        public string? LastUpdatedBy { get; set; }
        public DateTime? LastUpdatedOn { get; set; }
        public long? EmployeeId { get; set; }
        public string? FullName { get; set; }
        public string? EmailAddress { get; set; }
        public DateTime? MonthYear { get; set; }
    }

}
