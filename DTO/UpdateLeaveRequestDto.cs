using HRMSAPI.Models.Candidate;

namespace HRMSAPI.DTO
{
    public class UpdateLeaveRequestDto
    {
        public string? AssignLocationsListJson { get; set; }
        public int StatusId { get; set; } // Status ID (e.g., 1 for Approved, 2 for Rejected)
        public string? Remarks { get; set; } // Optional remarks for the approval/rejection
        public long? RelieverEmployeeId { get; set; }
        public List<AssignLocationHistoryrecord> assignLocations { get; set; } = new List<AssignLocationHistoryrecord>();
    }
}
