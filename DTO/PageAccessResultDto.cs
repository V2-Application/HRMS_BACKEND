namespace HRMSAPI.DTO
{
    /// <summary>
    /// Result of a page access check. Returned by /api/Rbac/CheckPageAccess.
    /// </summary>
    public class PageAccessResultDto
    {
        public bool Allowed { get; set; }
        public string Reason { get; set; }
        public string RoutePath { get; set; }
        public int? SubModuleId { get; set; }
        public string RoleName { get; set; }
    }
}
