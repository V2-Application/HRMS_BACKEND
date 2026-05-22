using System;
using System.Collections.Generic;

namespace HRMSAPI.DTO
{
    public class EmployeeShiftAndHistoryResponse
    {
        public EmployeeShiftInfo EmployeeInfo { get; set; }
        public List<ShiftHistoryItem> ShiftHistory { get; set; }
    }

    public class EmployeeShiftInfo
    {
        public long EmployeeId { get; set; }
        public string Ecode { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string FullName { get; set; }
        public string ReportHeadEcode { get; set; }
        public int? ReportHeadEmployeeId { get; set; }
        public string ReportHeadFullName { get; set; }
        public int? CurrentShiftId { get; set; }
        public ShiftMasterDTO CurrentShift { get; set; }
    }

    public class ShiftHistoryItem
    {
        public long HistoryId { get; set; }
        public long EmployeeId { get; set; }
        public int? ShiftId { get; set; }
        public DateTime? EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
        public DateTime? AssignedOn { get; set; }
        public string AssignedBy { get; set; }
        public string AssignedByEcode { get; set; }
        public string AssignedByName { get; set; }
        public string Remarks { get; set; }
        public DateTime? AppliedOn { get; set; }
        public string ShiftStatus { get; set; }
        public ShiftMasterDTO ShiftDetails { get; set; }
    }

    public class ShiftMasterDTO
    {
        public int ShiftID { get; set; }
        public string ShiftName { get; set; }
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }
        public bool? IsActive { get; set; }
        public string CreatedBy { get; set; }
        public DateTime? CreatedOn { get; set; }
        public DateTime? LastUpdatedOn { get; set; }
        public string LastUpdatedBy { get; set; }
    }
}

