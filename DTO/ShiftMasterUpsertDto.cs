using System;
using System.ComponentModel.DataAnnotations;

namespace HRMSAPI.DTO
{
    public class ShiftMasterUpsertDto
    {
        public int ShiftID { get; set; }

        [Required(ErrorMessage = "Shift Name is required")]
        [StringLength(50, ErrorMessage = "Shift Name cannot exceed 50 characters")]
        public string ShiftName { get; set; }

        [Required(ErrorMessage = "Start Time is required")]
        public TimeSpan StartTime { get; set; }

        [Required(ErrorMessage = "End Time is required")]
        public TimeSpan EndTime { get; set; }

        public bool IsActive { get; set; } = true;
    }
}

