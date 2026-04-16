using HRMSAPI.DTO;
using System.Collections.Generic;

namespace HRMSAPI.Interfaces
{

    public interface IEmployeeSeparationService
    {
        Task<bool> CreateEmployeeSeparationAsync(EmployeeSeparationDto model);
        Task<List<EmployeeSeparationResponseDto>> GetEmployeeSeparationsAsync(long empId, CancellationToken ct = default);
        //Task<(List<EmployeeSeparationResponseDto> PaginatedResignations, int TotalCount)> GetResignationsByManagerAsync(long? managerId, int pageNumber, int pageSize, string searchTerm);
        Task<(List<EmployeeSeparationResponseDto>? Data,int TotalCount,byte[]? ExcelBytes)> GetResignationsByManagerAsync(long? managerId,int pageNumber,int pageSize,string searchTerm,bool isExcel);
        Task<bool> ProcessSeparationActionAsync(int employeeSeprationId, long userId, string actionType, string remarks, string role,DateTime lastDay, string EmployeeId);

        Task<EmployeeSeparationResponseSDto?> GetEmployeeSeparationByIdAsync(
      int separationId,
      CancellationToken ct = default);
    }
}

   
    

