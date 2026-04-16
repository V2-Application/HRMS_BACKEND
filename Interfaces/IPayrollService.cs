using HRMSAPI.Data;
using HRMSAPI.DTO;
using Microsoft.AspNetCore.Http;
using Roomsy.DTOS.GenericsResponses;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HRMSAPI.Interfaces
{
    public interface IPayrollService
    {
        Task<(List<EmployeePayrollDTO> Records, int TotalRecords)> GetPayrollRecordsAsync(
     string? searchTerm = null,
     string? ecode = null,
     long? employeeId = null,
     string? location = null,
     int page = 1,
     int pageSize = 10,
     bool fetchAll = false);

        Task<(bool Success, string Message)> UploadPayrollDataAsync(IFormFile file, string createdBy);
        Task<PayrollSummaryResponseDto> GetPayrollSummaryAsync(DateTime startDate, DateTime endDate, int pageNumber, int pageSize);
        Task<ExecuteAndReponse> UpsertPFApprovalAsync(tblPF_Approval dto, CancellationToken ct = default);

        Task<(List<SalaryProcessDTO> Records, int TotalRecords)> GetSalaryProcessListAsync(string? searchTerm, int pageNumber, int pageSize);
        Task<List<SalaryProcessDTO>> GetSalaryProcessExportDataAsync(string? searchTerm);
        Task<(bool Success, string Message)> UploadSalaryProcessAsync(IFormFile file, string createdBy);
    }
}