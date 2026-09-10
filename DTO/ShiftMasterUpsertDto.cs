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

        /// <summary>
        /// Day the shift timing starts applying. Required on create and edit,
        /// the same way Emp Shift Alignment requires Effective From.
        /// </summary>
        [Required(ErrorMessage = "Effective From is required")]
        public DateTime? EffectiveFrom { get; set; }

        /// <summary>
        /// Last day the timing applies. Leave null for an open-ended shift.
        /// </summary>
        public DateTime? EffectiveTo { get; set; }

        /// <summary>
        /// Why the timing changed. Stored on the history row only, the same way
        /// Emp Shift Alignment keeps remarks against each assignment.
        ///
        /// MUST stay nullable (string?). The project builds with nullable
        /// reference types on, so a plain `string` here is treated as required by
        /// model validation and every save without remarks is rejected with
        /// "The Remarks field is required." -- including the Add/Edit modal,
        /// which does not send the field at all.
        /// </summary>
        [StringLength(200, ErrorMessage = "Remarks cannot exceed 200 characters")]
        public string? Remarks { get; set; }

        public bool IsActive { get; set; } = true;
    }
}

