namespace HRMSAPI.DTO
{
    public class EmployeeAttendanceRequestListDto
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
        public string? EmployeeName { get; set; }
        public string? ECode { get; set; }
    }
}

