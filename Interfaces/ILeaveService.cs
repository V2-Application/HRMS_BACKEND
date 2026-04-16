using HRMSAPI.Data;
using HRMSAPI.DTO;
using System.Numerics;
using static HRMSAPI.Implementation.EmpAttendanceService;

namespace HRMSAPI.Interfaces
{
    public interface ILeaveService
    {
       Task<tblLeaveRequest> LeaveRequest(LeaveRequestDto DtoObject);
       Task<List<LeaveRequestDto>> GetList(long id);
        Task<List<EmployeeLeaveBalanceDto>> GetEmployeeLeaveBalanceAsync(long employeeId);
        Task<List<EmployeeLeaveBalanceDto>> GetEmployeeLeaveBalanceById(long employeeId);
        Task<PagedResult<LeaveRequestDto>> GetLeaveRequestsAsync(
     long managerId,
     string role,
     int statusId = 0,
     int pageNumber = 1,
     int pageSize = 10,
     string? searchTerm = null);
        Task<bool> UpdateLeaveRequestStatusAsync(long requestId, UpdateLeaveRequestDto updateDto, string updatedBy);
    }

}
