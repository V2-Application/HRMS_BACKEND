using System;
using System.Collections.Generic;
namespace HRMSAPI.DTO
{
    public class EmployeeRoleDto
    {
        public long EmployeeRoleId { get; set; }
        public long EmployeeId { get; set; }
        public int RoleId { get; set; }
        public string AssignedBy { get; set; }
        public string LastUpdatedBy { get; set; }
    }
}

namespace HRMSAPI.DTO
{
    public class EmployeeRoleUpsertDto
    {
        public string Ecode { get; set; }
        public string RoleName { get; set; }
    }

    public class EmployeeRoleBulkUpsertDto
    {
        public List<EmployeeRoleUpsertDto> EmployeeRoles { get; set; }
    }

    public class EmployeeRoleResponseDtoo
    {
        public long EmployeeRoleId { get; set; }
        public long EmployeeId { get; set; }
        public string Ecode { get; set; }
        public string EmployeeName { get; set; }
        public int RoleId { get; set; }
        public string RoleName { get; set; }
        public DateTime? AssignedOn { get; set; }
        public string AssignedBy { get; set; }
        public string LastUpdatedBy { get; set; }
        public DateTime? LastUpdatedOn { get; set; }
    }
}
