using HRMSAPI.DTO;

namespace HRMSAPI.Interfaces
{
    public interface IPageAccessService
    {
        /// <summary>
        /// Decides whether the given employee can access the page identified
        /// by <paramref name="routePath"/>. Route paths are the route-pattern
        /// strings from frontend routes.js (e.g. "/salary_recal",
        /// "/employee/update/:id"), NOT live URLs.
        /// </summary>
        Task<PageAccessResultDto> HasPageAccessAsync(long employeeId, string routePath);
    }
}
