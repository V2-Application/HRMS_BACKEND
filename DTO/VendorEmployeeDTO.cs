using HRMSAPI.DTO;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Net;

namespace HRMSAPI.DTO
{
    public class VendorEmployeeDTO
    {
        public long EmployeeId { get; set; }
        public string Ecode { get; set; }
        public string? FullName { get; set; }
        public DateTime? DOJ { get; set; }
        public bool IsActive { get; set; }
        public string? DepartmentName { get; set; }
        public string? SubDepartmentName1 { get; set; }
        public string? SubDepartmentName2 { get; set; }
        public string? SubDepartmentName3 { get; set; }
        public string DesignationName { get; set; }
        //public string ContractorName { get; set; }
        public string? ShiftName { get; set; }
        public DateTime? ContractStartDate { get; set; }
        public DateTime? ContractEndDate { get; set; }
        //public string ContractorName { get; set; }
    }


    public class PagedEmployeeListDto
    {
        public List<VendorEmployeeDTO>? Employees { get; set; }
        public int TotalRecords { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }
}




   














