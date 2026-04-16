using HRMSAPI.DTO;
using HRMSAPI.Models.Auth;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;

namespace HRMSAPI.Interfaces
{
    public interface IVendorService
    {
        Task<Response> GetVendorListAsync(int pageNumber = 1, int pageSize = 10, DateTime? contractStartDate = null, DateTime? contractEndDate = null, string searchTerm = "");
        Task<Response> GetVendorByIdAsync(long vendorId);
        Task<Response> CreateVendor(CreateVendorDTO vendorDTO, long employeeId);
        Task<Response> UpdateVendor(long vendorId, UpdateVendorDTO vendorDTO, long employeeId);
        Task<Response> DeletevVendor(long id, long employeeId);
        Task<Response> GetServiceCategory();
        Task<Response> GetContractStatus();
        Task<Response> GetNatureOfWork();
        Task<Response> CreateServiceCategory(RequestServiceDTO serviceDTO);
        Task<Response> GetVendorEmployeesListAsync(string contractorCode, string searchTerm = "",
         int? isActiveFilter = null,
         DateTime? contractStartDate = null,
         DateTime? contractEndDate = null,
         int pageNumber = 1,
         int pageSize = 10);
        Task<Response> InsertVendorEmployee(VendorEmployeeRequestDTO request, string CreatedBy);

        Task<Response> InsertVendorEmployee2(VendorEmployeeRequestDTO request, string CreatedBy, SqlConnection connection, SqlTransaction transaction);
        Task<Response> UpdateVendorEmployeeAsync(string Ecode, string ContractorCode, UpdateVendorEmployeeRequestDTO request, string updateBy);
        Task<Response> GetVendorEmployeesByIdAsync(string ecode, string contractorCode);

        // new Implementation
        Task<Response> GetContractorDetailsAsync(
           string contractorCode = null,
           string contractorName = null,
            string searchTerm = "",
           int pageNumber = 1,
           int pageSize = 10);
        Task<Response> GetContractorByCodeAsync(string contractorCode);
        Task<Response> GetVendorEmployeesListAsync1(
       string contractorCode,
       string searchTerm = "",
       int? isActiveFilter = null,
       DateTime? contractStartDate = null,
       DateTime? contractEndDate = null,
       int pageNumber = 1,
       int pageSize = 10);
        Task<Response> ImportVendorEmployeesBulk(IFormFile file, string createdBy, string contractorCode);
    }

}

