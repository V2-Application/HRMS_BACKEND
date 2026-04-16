using HRMSAPI.Data;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using static HRMSAPI.Enum.Enums;

namespace HRMSAPI.DTO
{
    public class AttendanceRecordGeo
    {
        public int Id { get; set; }


        [Required]
        public long EmployeeId { get; set; }
        public tblEmployee Employee { get; set; } = default!;


        [Required]
        public DateTime PunchTimeUtc { get; set; }


        [Required]
        public PunchType PunchType { get; set; }


        // captured client geolocation
        [Column(TypeName = "decimal(9,6)")]
        public decimal Latitude { get; set; }


        [Column(TypeName = "decimal(9,6)")]
        public decimal Longitude { get; set; }


        // server-side computed validation
        public bool WithinGeofence { get; set; }


        // Optional: metadata
        [MaxLength(100)] public string? DeviceInfo { get; set; }
        [MaxLength(45)] public string? ClientIp { get; set; }
        [MaxLength(500)] public string? Address { get; set; }
        
        // Proof file path (relative to wwwroot)
        [MaxLength(500)] public string? ProofPath { get; set; }
    }
    public class OfficeLocation
    {
        public int Id { get; set; }


        [Required, MaxLength(100)]
        public string Name { get; set; } = default!;


        [Required]
        public double Latitude { get; set; }


        [Required]
        public double Longitude { get; set; }


        // Optional: per-location radius override (meters)
        public int? AllowedRadiusMeters { get; set; }


        public bool IsActive { get; set; } = true;
    }
}
