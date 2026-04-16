namespace HRMSAPI.DTO
{
    public class ComprehensiveSalaryStatusResponseDto
    {
        public long Id { get; set; } // EmpAttendanceViewSnapshot ID
        public string Ecode { get; set; }
        public string Location_Code { get; set; }
        public string Location_Name { get; set; }
        public string Employee_Name { get; set; }
        public string Month_Year { get; set; }
        public decimal PayableSalary { get; set; } // Monthly_Gross_CTC_Actual_After_Deduction_AND_AddONS_
        public string GivenToBankAmount { get; set; }
        public string PaidByBankAmount { get; set; }
        public string PaidByCashAmount { get; set; }
        public string ReturnByBankAmount { get; set; }
        public decimal Difference { get; set; } // Calculated difference
        public int SalaryStatus { get; set; }
        public string BatchId { get; set; } // Formatted batch number
        public string FormattedId { get; set; } // Formatted ID based on status
        public DateTime RunAt { get; set; }
    }

    public class ComprehensiveSalaryStatusSummaryDto
    {
        public decimal TotalPayableSalary { get; set; }
        public decimal TotalGivenToBank { get; set; }
        public decimal TotalPaidByBank { get; set; }
        public decimal TotalReturnByBank { get; set; }
        public decimal TotalDifference { get; set; }
    }

    public class ComprehensiveSalaryStatusResponseWithSummary
    {
        public List<ComprehensiveSalaryStatusResponseDto> Data { get; set; }
        public ComprehensiveSalaryStatusSummaryDto Summary { get; set; }
    }

    public class ComprehensiveSalaryStatusPaginatedResponse
    {
        public PaginatedResponse<ComprehensiveSalaryStatusResponseDto> Pagination { get; set; }
        public List<ComprehensiveSalaryStatusResponseDto> Data { get; set; }
        public ComprehensiveSalaryStatusSummaryDto Summary { get; set; }
    }
}
