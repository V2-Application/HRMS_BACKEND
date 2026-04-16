namespace HRMSAPI.DTO
{
    public class DeleteEmployeeRoleDto
    {
        public long EmployeeId { get; set; }
        public int RoleId { get; set; }
        public string DeletedBy { get; set; }
    }
}
