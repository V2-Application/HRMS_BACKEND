using HRMSAPI.Data;

namespace HRMSAPI.DTO
{
    public class EmployeeAttendanceRequestSummaryDto
    {
        public long EmployeeId { get; set; }
        public string? Ecode { get; set; }
        public string? EmployeeName { get; set; }
        public DateTime PunchDate { get; set; }
        
        public int PunchCount { get; set; }
        public int PunchInCount { get; set; }
        public int PunchOutCount { get; set; }

        public string? Remarks { get; set; }
        public string? Address { get; set; }
        public string? ProofPath { get; set; }
        
        public List<GetAttendanceRecordsByEmployeeResult>? Details { get; set; }
    }

    public class EmployeeAttendanceRequestDetailDto
    {
        public int Id { get; set; }
        public long EmployeeId { get; set; }
        public DateTime PunchTimeUtc { get; set; }
        public int PunchType { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public bool WithinGeofence { get; set; }
        public string? DeviceInfo { get; set; }
        public string? ClientIp { get; set; }
        public int StatusId { get; set; }
        public string? StatusName { get; set; }
        public string? LastUpdatedBy { get; set; }
        public DateTime? LastUpdatedOn { get; set; }
        public string? Remarks { get; set; }
        public string? Address { get; set; }  // Added Address field
    }
}

