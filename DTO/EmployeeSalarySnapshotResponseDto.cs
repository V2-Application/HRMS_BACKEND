using System;

namespace HRMSAPI.DTO
{
    public class EmployeeSalarySnapshotResponseDto
    {
        // Base fields
        public string Ecode { get; set; }
        public string Location_Code { get; set; }
        public string Location_Name { get; set; }
        public string Employee_Name { get; set; }
        public string designation { get; set; }
        public string department { get; set; }

        // Snapshot fields
        public string Month_Year { get; set; }
        public decimal? ttl_bgt_days { get; set; }
        public decimal? actualttl_days { get; set; }
        public string GF { get; set; }
        public decimal? Machine { get; set; }
        public decimal? MachineWP { get; set; }
        public decimal? MANUAL { get; set; }
        public decimal? actualweekly { get; set; }
        public decimal? presentweeklyoff { get; set; }
        public decimal? HolidayOff { get; set; }
        public decimal? paybledays { get; set; }
        public decimal? extradays { get; set; }
        public decimal? Absent { get; set; }
        public decimal? LWP { get; set; }
        public decimal? AdjustedDays { get; set; }
        public int? Status { get; set; }
        public decimal? BasicSalary_Bud_ { get; set; }
        public decimal? HRA_Bud_ { get; set; }
        public decimal? CCA_Bud_ { get; set; }
        public decimal? SpecialAllowance_Bud_ { get; set; }
        public decimal? DA_Bud_ { get; set; }
        public decimal? Reimbersment_Bud_ { get; set; }
        public decimal? Fuel_and_Maintenance_Bud_ { get; set; }
        public decimal? Books_and_Periodicals_Bud_ { get; set; }
        public decimal? Professional_Attire_Bud_ { get; set; }
        public decimal? Driver_Wages_Bud_ { get; set; }
        public decimal? Mobile_Bill_Bud_ { get; set; }
        public decimal? Meal_Voucher_Bud_ { get; set; }
        public decimal? Monthly_Gross_CTC_Bud_ { get; set; }
        public decimal? BasicSalary_Actual_ { get; set; }
        public decimal? HRA_Actual_ { get; set; }
        public decimal? CCA_Actual_ { get; set; }
        public decimal? SpecialAllowance_Actual_ { get; set; }
        public decimal? DA_Actual_ { get; set; }
        public decimal? ExtraDayAllowance { get; set; }
        public decimal? Reimbersment_Actual_ { get; set; }
        public decimal? Fuel_and_Maintenance_Actual_ { get; set; }
        public decimal? Books_and_Periodicals_Actual_ { get; set; }
        public decimal? Professional_Attire_Actual_ { get; set; }
        public decimal? Driver_Wages_Actual_ { get; set; }
        public decimal? Mobile_Bill_Actual_ { get; set; }
        public decimal? Meal_Voucher_Actual_ { get; set; }
        public decimal? PF_Employee_ { get; set; }
        public decimal? PF_Employeer_ { get; set; }
        public decimal? PF_Total_ { get; set; }
        public decimal? ESIC_Employee_ { get; set; }
        public decimal? ESIC_Employeer_ { get; set; }
        public decimal? ESIC_Total_ { get; set; }
        public decimal? TDS { get; set; }
        public decimal? PTax { get; set; }
        public decimal? Loan { get; set; }
        public decimal? CashShort { get; set; }
        public decimal? DieselDeduction { get; set; }
        public decimal? Penality { get; set; }
        public decimal? Lwf { get; set; }
        public decimal? TotalDeductions { get; set; }
        public decimal? Incentive { get; set; }
        public decimal? ARREAR { get; set; }
        public decimal? Overtime { get; set; }
        public decimal? Fooding_Allowance { get; set; }
        public decimal? Mobile_Bill { get; set; }
        public decimal? Monthly_Gross_CTC_Actual_ { get; set; }
        public decimal? Monthly_Gross_CTC_Actual_After_Deduction_AND_AddONS_ { get; set; }
        public decimal? Payble_Days { get; set; }
        public decimal? Leave_Used { get; set; }
        public decimal? Opening_EL { get; set; }
        public decimal? EarnedLeaveAcquired { get; set; }
        public decimal? EarnedLeaveUsed { get; set; }
        public decimal? EarnedLeaveBalance { get; set; }
        public decimal? Opening_CL { get; set; }
        public decimal? CasualLeaveAcquired { get; set; }
        public decimal? CasualLeaveUsed { get; set; }
        public decimal? CasualLeaveBalance { get; set; }
        public decimal? Opening_CompoOff { get; set; }
        public decimal? CompoOffAcquired { get; set; }
        public decimal? CompoOffUsed { get; set; }
        public decimal? CompoOffBalance { get; set; }
        public string MONTH { get; set; }
        public string BatchNo { get; set; }
        public DateTime? RunAt { get; set; }
        public int? SalaryStatus { get; set; }
        public long? ID { get; set; }
    }
}
