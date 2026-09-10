#nullable disable
using System;

namespace HRMSAPI.Data;

/// <summary>
/// One row per create/edit of a shift's timing, so a past timing is never lost.
/// The Shift Master detail panel renders these the way Emp Shift Alignment
/// renders EmployeeShiftHistory.
///
/// Note there is no navigation property to tblShiftMaster on purpose: Shift
/// Master can delete an unused shift, and an FK would block that. ShiftName is
/// copied onto the row so history survives a rename or a delete.
/// </summary>
public partial class tblShiftMasterHistory
{
    public long ShiftHistoryId { get; set; }

    public int ShiftID { get; set; }

    /// <summary>Shift name as it was at the time of the change.</summary>
    public string ShiftName { get; set; }

    public TimeSpan StartTime { get; set; }

    public TimeSpan EndTime { get; set; }

    public DateTime? EffectiveFrom { get; set; }

    /// <summary>NULL = open ended.</summary>
    public DateTime? EffectiveTo { get; set; }

    public string Remarks { get; set; }

    /// <summary>Baseline | Created | Updated.</summary>
    public string ChangeType { get; set; }

    public string ChangedBy { get; set; }

    public DateTime ChangedOn { get; set; }
}
