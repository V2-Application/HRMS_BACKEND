using System;

namespace HRMSAPI.DTO
{
    public class ShiftMasterDto
    {
        public int ShiftID { get; set; }

        public string ShiftName { get; set; }

        public TimeSpan StartTime { get; set; }

        public TimeSpan EndTime { get; set; }

        public bool IsActive { get; set; }

        public string CreatedBy { get; set; }

        public DateTime CreatedOn { get; set; }

        public DateTime? LastUpdatedOn { get; set; }

        public string LastUpdatedBy { get; set; }
    }
}
