using System;
using System.Collections.Generic;

namespace HRMSAPI.DTO
{
    /// <summary>
    /// One timing entry in a shift's history, shaped to match what the Emp Shift
    /// Alignment history table renders (window, timing, Past/Current/Future tag,
    /// who changed it and when).
    /// </summary>
    public class ShiftMasterHistoryDto
    {
        public long ShiftHistoryId { get; set; }

        public int ShiftID { get; set; }

        public string ShiftName { get; set; }

        public TimeSpan StartTime { get; set; }

        public TimeSpan EndTime { get; set; }

        public DateTime? EffectiveFrom { get; set; }

        public DateTime? EffectiveTo { get; set; }

        public string Remarks { get; set; }

        /// <summary>Baseline | Created | Updated.</summary>
        public string ChangeType { get; set; }

        public string ChangedBy { get; set; }

        public DateTime ChangedOn { get; set; }

        /// <summary>Past | Current | Future, worked out from the effective window.</summary>
        public string ShiftStatus { get; set; }
    }

    /// <summary>
    /// Payload behind the Shift Master detail panel: the shift as it stands now
    /// plus its timing history, newest first.
    /// </summary>
    public class ShiftMasterDetailDto
    {
        public ShiftMasterDto Shift { get; set; }

        public List<ShiftMasterHistoryDto> TimingHistory { get; set; } = new List<ShiftMasterHistoryDto>();
    }
}
