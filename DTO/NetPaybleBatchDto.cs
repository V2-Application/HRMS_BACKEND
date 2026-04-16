namespace HRMSAPI.DTO
{
    public class NetPaybleBatchDto
    {
        public string? UniqueId { get; set; }
        public string? Ecode { get; set; }
        public string? LocationCode { get; set; }
        public string? LocationName { get; set; }
        public string? EmployeeName { get; set; }
        public string? Designation { get; set; }
        public string? Department { get; set; }
        public string? MonthYear { get; set; }
        public decimal? BgtSalary { get; set; }
        public decimal? GrossEarnings { get; set; }
        public decimal? Additions { get; set; }
        public decimal? Deductions { get; set; }
        public decimal? Reimbursement { get; set; }
        public decimal? NetPaybleAfterDeductionWithAddition { get; set; }
        public decimal? NetPaybleWithoutReimbursement { get; set; }
        public DateTime? RunAt { get; set; }
        public int? BatchNo { get; set; }
    }
}
