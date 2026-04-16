using System.ComponentModel.DataAnnotations;

namespace HRMSAPI.Models
{
    public class EmpAttendance
    {
        [Key]
        public int EmpAttendanceId { get; set; }

        [Required]
        public string EmpCode { get; set; }

        [Required]
        public DateTime AttendanceDate { get; set; }

        [Required]
        public TimeSpan PunchIn { get; set; }

        [Required]
        public TimeSpan PunchOut { get; set; }

        public string CreatedBy { get; set; } = "System";
        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
        public string LastUpdatedBy { get; set; } = "System";
    }
}
