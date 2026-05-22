using System;
using System.ComponentModel.DataAnnotations;

namespace HRMSAPI.DTO
{
    public class AssignEmployeeShiftRequest
    {
        [Required]
        public int EmployeeId { get; set; }

        [Required]
        public int ShiftId { get; set; }

        [Required]
        public DateTime EffectiveFrom { get; set; }

        public DateTime? EffectiveTo { get; set; }

        public string AssignedBy { get; set; }

        [MaxLength(200)]
        public string Remarks { get; set; }
    }
}

