namespace HRMSAPI.DTO
{
    public class BankTransferAndPaidInCashResponseDto
    {
        public long Id { get; set; } // Single ID field for all tables
        public string Ecode { get; set; }
        public string EmployeeName { get; set; }
        public string? Month { get; set; }
        public string A_C { get; set; }
        public string BankTransfer { get; set; }
        public string ReturnByBank1 { get; set; } // For ReturnByBank table
        public string CreatedBy { get; set; }
        public string CreatedByName { get; set; }
        public string CreatedByEcode { get; set; }
        public DateTime? CreatedOn { get; set; }
        public string LastUpdatedBy { get; set; }
        public DateTime? LastUpdatedOn { get; set; }
        public bool? IsActive { get; set; }
        public bool? IsDeleted { get; set; }
        public string? BatchId { get; set; }
        public string FormattedId { get; set; } // New formatted ID column
    }
}

