using HRMSAPI.Data;
using HRMSAPI.DTO;
using HRMSAPI.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HRMSAPI.Implementation
{
    public class PageAccessService : IPageAccessService
    {
        // Roles that bypass the SubModule permission check (always allowed).
        // Keep this aligned with login-time role buckets used elsewhere
        // (e.g. SalarySlips.jsx UI checks "Master" / "IT Superadmin").
        private static readonly HashSet<string> AdminRoles = new(StringComparer.OrdinalIgnoreCase)
        {
            "Master",
            "SuperAdmin",
            "IT Superadmin"
        };

        private readonly HRMSContext _context;

        public PageAccessService(HRMSContext context)
        {
            _context = context;
        }

        public async Task<PageAccessResultDto> HasPageAccessAsync(long employeeId, string routePath)
        {
            var result = new PageAccessResultDto { RoutePath = routePath };

            if (string.IsNullOrWhiteSpace(routePath))
            {
                result.Allowed = true;
                result.Reason = "empty route";
                return result;
            }

            // Lookup the route in the map. Routes are stored verbatim from
            // routes.js (e.g. "/employee/update/:id"), so the frontend MUST
            // send the pattern, not the resolved URL.
            var map = await _context.tblPageRouteMaps
                .AsNoTracking()
                .Where(m => m.RoutePath == routePath)
                .Select(m => new { m.SubModuleId, m.IsActive })
                .FirstOrDefaultAsync();

            // Not in map OR map row is inactive → ungated (safer rollout).
            if (map == null)
            {
                result.Allowed = true;
                result.Reason = "route not in map";
                return result;
            }
            if (!map.IsActive)
            {
                result.Allowed = true;
                result.Reason = "route gating disabled (IsActive=0)";
                return result;
            }
            if (!map.SubModuleId.HasValue)
            {
                result.Allowed = true;
                result.Reason = "route has no SubModule mapping";
                return result;
            }
            result.SubModuleId = map.SubModuleId;

            // Resolve the employee's role.
            var roleInfo = await (
                from emp in _context.tblEmployees.AsNoTracking()
                join er in _context.tblEmployeeRoles.AsNoTracking() on emp.EmployeeId equals er.EmployeeId
                join r in _context.tblRoles.AsNoTracking() on er.RoleId equals r.RoleId
                where emp.EmployeeId == employeeId
                select new { r.RoleId, r.RoleName }
            ).FirstOrDefaultAsync();

            if (roleInfo == null)
            {
                result.Allowed = false;
                result.Reason = "No role assigned to this employee.";
                return result;
            }
            result.RoleName = roleInfo.RoleName;

            // Admin bypass — Master / SuperAdmin / IT Superadmin always allowed.
            if (!string.IsNullOrWhiteSpace(roleInfo.RoleName) && AdminRoles.Contains(roleInfo.RoleName.Trim()))
            {
                result.Allowed = true;
                result.Reason = $"role bypass: {roleInfo.RoleName}";
                return result;
            }

            // Check the SubModule node in RBACNode for this role.
            var hasAccess = await _context.RBACNodes
                .AsNoTracking()
                .AnyAsync(n => n.RoleId == roleInfo.RoleId
                            && n.NodeType == "SubModule"
                            && n.RefId == map.SubModuleId.Value
                            && n.IsChecked == true);

            result.Allowed = hasAccess;
            result.Reason = hasAccess
                ? $"granted via role {roleInfo.RoleName}"
                : "You do not have access to this page.";
            return result;
        }
    }
}
