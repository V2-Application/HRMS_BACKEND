using HRMSAPI.Extension;
using HRMSAPI.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace HRMSAPI.Extension
{
    /// <summary>
    /// Controller/action-level guard that mirrors the frontend page guard.
    /// Closes the curl-style bypass where a user with a JWT but no page
    /// permission calls the data API directly.
    ///
    /// Usage:
    ///   [RequirePageAccess("/salary_recal")]
    ///   public class SalaryRecalculateController : ControllerBase { ... }
    ///
    /// Resolution mirrors IPageAccessService.HasPageAccessAsync:
    ///   - Master / SuperAdmin / IT Superadmin bypass
    ///   - tblPageRouteMap row missing OR IsActive=0 → allow (fail-open;
    ///     same as the frontend guard so behavior is consistent)
    ///   - Otherwise: deny unless RBACNode has the SubModule for the role.
    /// </summary>
    public class RequirePageAccessAttribute : TypeFilterAttribute
    {
        public RequirePageAccessAttribute(string routePath)
            : base(typeof(RequirePageAccessFilter))
        {
            Arguments = new object[] { routePath };
        }
    }

    public class RequirePageAccessFilter : IAsyncAuthorizationFilter
    {
        private readonly string _routePath;
        private readonly IPageAccessService _pageAccessService;

        public RequirePageAccessFilter(string routePath, IPageAccessService pageAccessService)
        {
            _routePath = routePath;
            _pageAccessService = pageAccessService;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var identity = context.HttpContext.User.Identity as ClaimsIdentity;
            var userClaims = AuthenticUserDetails.GetCurrentUserDetails(identity);

            if (userClaims == null
                || string.IsNullOrEmpty(userClaims.EmployeeId)
                || !long.TryParse(userClaims.EmployeeId, out var employeeId))
            {
                context.Result = new UnauthorizedObjectResult(new
                {
                    Status = false,
                    Message = "Invalid or missing authentication."
                });
                return;
            }

            var result = await _pageAccessService.HasPageAccessAsync(employeeId, _routePath);
            if (!result.Allowed)
            {
                context.Result = new ObjectResult(new
                {
                    Status = false,
                    Message = result.Reason ?? "You do not have access to this page.",
                    RoutePath = _routePath,
                })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
            }
        }
    }
}
