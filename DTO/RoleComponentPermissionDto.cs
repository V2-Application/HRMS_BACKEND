namespace HRMSAPI.DTO
{
    public class RoleComponentPermissionDto
    {
        public int RoleId { get; set; }
        public string ComponentName { get; set; }
        public bool IsRead { get; set; }
        public bool IsWrite { get; set; }
       
    }
  
    public class RoleComponentPermissionResponseDto
    {
        public int RoleComponentId { get; set; }
        public int RoleId { get; set; }
        public string ComponentName { get; set; }
        public bool IsRead { get; set; }
        public bool IsWrite { get; set; }
        public DateTime CreatedOn { get; set; }
        public string CreatedBy { get; set; }
        public DateTime? UpdatedOn { get; set; }
        public string UpdatedBy { get; set; }
        public string? RoleName { get;  set; }
    }

}
