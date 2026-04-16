using System;

namespace HRMSAPI.DTO
{
    public class EligibleEmployeeResponseDto
    {
        public string Ecode { get; set; }
        public string EmployeeName { get; set; }
        public string STCode { get; set; }
        public string LocationName { get; set; }
        public bool? IsActive { get; set; }
        public string DepartmentName { get; set; }
        public string DesignationName { get; set; }
    }
}


