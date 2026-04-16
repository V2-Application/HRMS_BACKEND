namespace HRMSAPI.DTO
{
    public sealed class FnfEmployeeDropdownDto
    {
        public long EmployeeId { get; set; }
        public string EmployeeCode { get; set; } = "";
        public string Name { get; set; } = "";
        public string Department { get; set; } = "";
        public string Designation { get; set; } = "";
        public DateTime? DateOfLeaving { get; set; }
        public DateTime? DateOfJoining { get; set; }
        public bool? IsFNFCompleted { get; set; }
        public decimal? UnpaidSalaryAmount { get; set; }
        public int? UnpaidSalaryDays { get; set; }
        public string? UnpaidSalaryMonth { get; set; }
        public string? ResignationType { get; set; } = "NA";

        // ✅ NEW
        public string? ResignationAttachment { get; set; }
    }
    public sealed class FnfToggleRequestDto
    {
        public bool IsCompleted { get; set; }
        public DateTime? CompletedOn { get; set; }
        public string? Remarks { get; set; }
        public string? ChangedBy { get; set; }
    }
    public sealed class FnfAdditionsDto
    {
        public long EmployeeId { get; set; }
        public DateTime? FNFDate { get; set; }
        public DateTime? DateOfLeaving { get; set; }
        public decimal? UnpaidSalaryAmount { get; set; }
        public decimal? Rate { get; set; }
        public int? Days { get; set; }
        public string? SalaryMonth { get; set; }
        public decimal? Bonus { get; set; }
        public DateTime? BonusPeriodFrom { get; set; }
        public DateTime? BonusPeriodTill { get; set; }
        public decimal? Gratuity { get; set; }
        public string? CalculatedAs { get; set; }
        public decimal? E_LeaveAmount { get; set; }
        public int? ELDays { get; set; }
        public decimal? NoticeSalary { get; set; }
        public decimal? OtherAddition1 { get; set; }
        public decimal? OtherAddition2 { get; set; }
        public decimal? OtherAddition3 { get; set; }
        public decimal? OtherAddition4 { get; set; }
        public string? User { get; set; }
    }

    public sealed class FnfIdResponse { public long FNFId { get; set; } }
    public sealed class PaymentIdResponse { public long PaymentId { get; set; } }
    public sealed class FnfSaveAllDto
    {
        // Required
        public long EmployeeId { get; set; }
        public string? User { get; set; }

        // Additions
        public DateTime? FNFDate { get; set; }
        public DateTime? DateOfLeaving { get; set; }
        public decimal? UnpaidSalaryAmount { get; set; }
        public decimal? Rate { get; set; }
        public int? Days { get; set; }
        public string? SalaryMonth { get; set; }
        public decimal? Bonus { get; set; }
        public DateTime? BonusPeriodFrom { get; set; }
        public DateTime? BonusPeriodTill { get; set; }
        public decimal? Gratuity { get; set; }
        public string? CalculatedAs { get; set; }
        public decimal? E_LeaveAmount { get; set; }
        public int? ELDays { get; set; }
        public decimal? NoticeSalary { get; set; }
        public decimal? OtherAddition1 { get; set; }
        public decimal? OtherAddition2 { get; set; }
        public decimal? OtherAddition3 { get; set; }
        public decimal? OtherAddition4 { get; set; }

        // Deductions
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

        // Payment (optional)
        public decimal? SendForPaymentAmount { get; set; }
        public string? Remarks { get; set; }
        public string? ChequeNo { get; set; }
        public DateTime? ChequeDate { get; set; }
        public string? Status { get; set; }
        public decimal? AmountPaid { get; set; }
        public string? PaymentVoucherNo { get; set; }
    }

    public sealed class FnfSaveAllResponse { public long FNFId { get; set; } }
    public sealed class FnfAccountsListItemDto
    {
        public long FNFId { get; set; }
        public long EmployeeId { get; set; }
        public string Ecode { get; set; } = "";
        public string EmployeeName { get; set; } = "";
        public DateTime? FNFDate { get; set; }
        public DateTime? DateOfLeaving { get; set; }
        public decimal TotalAdditions { get; set; }
        public decimal TotalDeductions { get; set; }
        public decimal NetAmount { get; set; }
        public decimal? SendForPaymentAmount { get; set; }
        public decimal? AmountPaid { get; set; }
        public string? PaymentStatus { get; set; }
        public string? ChequeNo { get; set; }
        public DateTime? ChequeDate { get; set; }
        public string? PaymentVoucherNo { get; set; }
        public string? PaymentRemarks { get; set; }
        // ✅ New nullable field
        public string? ResignationAttachment { get; set; }
        public string? Designation { get; set; }
        public string? PanNo { get; set; }
        public DateTime? DateOfJoining { get; set; }
        public string? Department { get; set; }
        public string? Location { get; set; }
        public string? BankName { get; set; }
        public string? AccountNo { get; set; }
        public string? IFSC { get; set; }
        public decimal? UnPaidSalary { get; set; }
        public string? LastMonth { get; set; }
        public decimal? Rate { get; set; }
        public decimal? Bonus { get; set; }
        public decimal? Gratuity { get; set; }
        public decimal? NoticeSalary { get; set; }
        public decimal? PayableDays { get; set; }
        public decimal? AdvanceBalance { get; set; }
        public decimal? TDS { get; set; }
        public string? ESIC { get; set; }
        public string? PF { get; set; }
        public string? PTax { get; set; }
        public DateTime? BonusPeriodFrom { get; set; }
        public DateTime? BonusPeriodTill { get; set; }
    }

    public sealed class FnfBulkUploadRowDto
    {
        public string Ecode { get; set; } = string.Empty;
        public DateTime? FNFDate { get; set; }
        public DateTime? DateOfLeaving { get; set; }

        public decimal? UnpaidSalaryAmount { get; set; }
        public decimal? Rate { get; set; }
        public decimal? Days { get; set; }
        public string? SalaryMonth { get; set; }
        public decimal? Bonus { get; set; }
        public DateTime? BonusPeriodFrom { get; set; }
        public DateTime? BonusPeriodTill { get; set; }
        public decimal? Gratuity { get; set; }
        public string? CalculatedAs { get; set; }
        public decimal? E_LeaveAmount { get; set; }
        public decimal? ELDays { get; set; }
        public decimal? NoticeSalary { get; set; }
        public decimal? OtherAddition1 { get; set; }
        public decimal? OtherAddition2 { get; set; }
        public decimal? OtherAddition3 { get; set; }
        public decimal? OtherAddition4 { get; set; }

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

        public decimal? SendForPaymentAmount { get; set; }
        public decimal? AmountPaid { get; set; }
        public string? PaymentStatus { get; set; }
        public string? ChequeNo { get; set; }
        public DateTime? ChequeDate { get; set; }
        public string? PaymentVoucherNo { get; set; }
        public string? PaymentRemarks { get; set; }
    }

    public sealed class FnfBulkUploadRequestDto
    {
        public List<FnfBulkUploadRowDto> Rows { get; set; } = new();
        public string? User { get; set; }
    }

    public sealed class FnfAccountsListResponseDto
    {
        public int TotalCount { get; set; }
        public List<FnfAccountsListItemDto> Items { get; set; } = new();
    }
    public sealed class BonusCalcRequestDto
    {
        public long EmployeeId { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public decimal BonusRatePct { get; set; }
        public int MinWorkedDays { get; set; } = 0;
        // flags (optional; keep defaults same as SP)
        public bool Basic { get; set; } = true;
        public bool DA { get; set; } = true;
        public bool HRA { get; set; } = false;
        public bool Conveyance { get; set; } = false;
        public bool CCA { get; set; } = false;
        public bool MedicalAllowance { get; set; } = false;
        public bool Incentive { get; set; } = false;
        public bool FoodingAllowance { get; set; } = false;
        public bool SpecialAllowance { get; set; } = true;
        public bool ExtraAllowance { get; set; } = false;
        public bool LeaveEncashment { get; set; } = false;
        public bool MedicalReim { get; set; } = false;
        public bool LTA { get; set; } = false;
        public bool BonusExGratia { get; set; } = false;
        public bool Arrears { get; set; } = false;
    }

    public sealed class LeaveEncashmentRequestDto
    {
        public string Ecode { get; set; } = "";
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public decimal? OneLeaveNumberOfDays { get; set; }  // optional
        public int? DivideByDays { get; set; } = 26;        // 26 or 30
        public decimal? ELDaysOverride { get; set; }        // if UI supplies EL days
        public bool Basic { get; set; } = true;
        public bool DA { get; set; } = true;
        public bool HRA { get; set; } = false;
        public bool Conveyance { get; set; } = false;
        public bool CCA { get; set; } = false;
        public bool MedicalAllowance { get; set; } = false;
        public bool SpecialAllowance { get; set; } = false;
        public bool ExtraAllowance { get; set; } = false;
    }
    public sealed class GratuityRequestDto
    {
        public string? Ecode { get; set; }
        public long? EmployeeId { get; set; }
    }

}
