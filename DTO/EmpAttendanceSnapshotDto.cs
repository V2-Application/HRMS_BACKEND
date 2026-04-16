namespace HRMSAPI.DTO
{
    public class EmpAttendanceSnapshotDto
    {
        public string? Ecode { get; set; }
        public string? LocationCode { get; set; }
        public string? LocationName { get; set; }
        public string? EmployeeName { get; set; }
        public string? Designation { get; set; }
        public string? Department { get; set; }
        public string? MonthYear { get; set; }
        public decimal? TtlBgtDays { get; set; }
        public decimal? ActualTtlDays { get; set; }
        public decimal? Machine { get; set; }
        public decimal? Manual { get; set; }
        public decimal? ActualWeekly { get; set; }
        public decimal? PresentWeeklyOff { get; set; }
        public int? HolidayOff { get; set; }
        public decimal? PaybleDays { get; set; }
        public decimal? ExtraDays { get; set; }
        public decimal? Absent { get; set; }
        public decimal? LWP { get; set; }
        public string? Status { get; set; }
        
        // Budget Salary Components
        public decimal? BasicSalaryBudget { get; set; }
        public decimal? HRABudget { get; set; }
        public decimal? CCABudget { get; set; }
        public decimal? SpecialAllowanceBudget { get; set; }
        public decimal? DABudget { get; set; }
        public decimal? ReimbursementBudget { get; set; }
        public decimal? FuelAndMaintenanceBudget { get; set; }
        public decimal? BooksAndPeriodicalsBudget { get; set; }
        public decimal? ProfessionalAttireBudget { get; set; }
        public decimal? DriverWagesBudget { get; set; }
        public decimal? MobileBillBudget { get; set; }
        public decimal? MealVoucherBudget { get; set; }
        public decimal? MonthlyGrossCTCBudget { get; set; }
        
        // Actual Salary Components
        public decimal? BasicSalaryActual { get; set; }
        public decimal? HRAActual { get; set; }
        public decimal? CCAActual { get; set; }
        public decimal? SpecialAllowanceActual { get; set; }
        public decimal? DAActual { get; set; }
        public string? ExtraDayAllowance { get; set; }
        public decimal? ReimbursementActual { get; set; }
        public decimal? FuelAndMaintenanceActual { get; set; }
        public decimal? BooksAndPeriodicalsActual { get; set; }
        public decimal? ProfessionalAttireActual { get; set; }
        public decimal? DriverWagesActual { get; set; }
        public decimal? MobileBillActual { get; set; }
        public decimal? MealVoucherActual { get; set; }
        
        // Deductions
        public decimal? PFEmployee { get; set; }
        public decimal? PFEmployer { get; set; }
        public string? PFTotal { get; set; }
        public decimal? ESICEmployee { get; set; }
        public decimal? ESICEmployer { get; set; }
        public string? ESICTotal { get; set; }
        public string? TDS { get; set; }
        public string? PTax { get; set; }
        public string? Loan { get; set; }
        public string? CashShort { get; set; }
        public string? DieselDeduction { get; set; }
        public string? Penalty { get; set; }
        public string? LWF { get; set; }
        public decimal? TotalDeductions { get; set; }
        
        // Additional Components
        public string? Incentive { get; set; }
        public string? Arrear { get; set; }
        public decimal? Overtime { get; set; }
        public decimal? FoodingAllowance { get; set; }
        public decimal? MobileBill { get; set; }
        public decimal? MonthlyGrossCTCActual { get; set; }
        public decimal? MonthlyGrossCTCActualAfterDeductionAndAddons { get; set; }
        public decimal? PaybleDaysFinal { get; set; }
        public decimal? LeaveUsed { get; set; }
        
        // Leave Balances
        public decimal? OpeningEL { get; set; }
        public decimal? EarnedLeaveAcquired { get; set; }
        public decimal? EarnedLeaveUsed { get; set; }
        public decimal? EarnedLeaveBalance { get; set; }
        public decimal? OpeningCL { get; set; }
        public decimal? CasualLeaveAcquired { get; set; }
        public decimal? CasualLeaveUsed { get; set; }
        public decimal? CasualLeaveBalance { get; set; }
        public decimal? OpeningCompoOff { get; set; }
        public decimal? CompoOffAcquired { get; set; }
        public decimal? CompoOffUsed { get; set; }
        public decimal? CompoOffBalance { get; set; }
        
        // Additional Fields
        public string? Month { get; set; }
        public int? BatchNo { get; set; }
        public DateTime? RunAt { get; set; }
        public long? ID { get; set; }
    }
}
