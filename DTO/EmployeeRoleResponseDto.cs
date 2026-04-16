namespace HRMSAPI.DTO
{
    public class EmployeeRoleResponseDto
    {
        public long EmployeeRoleId { get; set; }
        public long EmployeeId { get; set; }
        public int RoleId { get; set; }
        public string RoleName { get; set; }
        public string RoleDescription { get; set; }
        public DateTime? AssignedOn { get; set; }
        public string AssignedBy { get; set; }
        public DateTime? LastUpdatedOn { get; set; }
        public string LastUpdatedBy { get; set; }
    }
}
