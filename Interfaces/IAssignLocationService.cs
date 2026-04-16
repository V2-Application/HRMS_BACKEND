using HRMSAPI.DTO;

namespace HRMSAPI.Interfaces
{
    public interface IAssignLocationService
    {
        Task<bool> CreateLocationAssignmentAsync(List<AssignLocationsDto> assignLocations, string createdBy);
        Task<List<AssignLocationsDto>> GetLocationAssignmentsAsync(JwtLoginDetailDto loginDetail, bool activeOnly = false, long? employeeId = null, bool isHR = false);
        Task<bool> ApproveLocationAssignmentAsync(AssignLocationApprovalDto approvalDto, string updatedBy);
    }
}
