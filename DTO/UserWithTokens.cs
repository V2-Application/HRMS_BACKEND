using HRMSAPI.Data;

using System.Security.Principal;

namespace HRMSAPI.DTO
{
    public class ComponentPermission
    {
        public string ComponentName { get; set; }
        public bool IsRead { get; set; }
        public bool IsWrite { get; set; }
    }
    public class UserWithTokens
    {
        public string Username { get; set; }
        public String Role { get; set; }
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string EmailAddress { get; set; }
        public string Ecode { get; set; }
        public string ReportHeadEcode { get; set; }
        public long EmployeeId { get; internal set; }
        public long? Reportheadid { get; internal set; }
        public string ? ReportHeadName { get; set; }
        public string StoreCode { get; set; }
        public string LocationName { get; set; }
        public string DepartmentName { get; set; }
        public string DesignationName { get; set; }
        public DateTime? Joiningdate { get; set; }
        public List<usp_GetLocationByRoleResult>? LocationList { get; set; }
        public bool IsStore { get; set; }
        public bool HasReports { get; set; }
        public bool IsActive { get; set; }
        public List<ComponentPermission> ComponentPermissions { get; set; } = new List<ComponentPermission>();
        public dynamic Permissions { get; set; }
        public string AssignedLocation { get; set; } = string.Empty;
   
        public bool IsGeofenceEnabled { get; set; }  // 👈 new
    }
}
