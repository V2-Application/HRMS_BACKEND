using HRMSAPI.DTO;
using HRMSAPI.Extension;
using HRMSAPI.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Roomsy.DTOS.GenericsResponses;
using System.Net;
using System.Security.Claims;

namespace HRMSAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [AllowAnonymous]
    public class RBACController : ControllerBase
    {
        private readonly IRBACService _rbacService;
        private readonly IPageAccessService _pageAccessService;

        public RBACController(IRBACService rbacService, IPageAccessService pageAccessService)
        {
            _rbacService = rbacService;
            _pageAccessService = pageAccessService;
        }

        /// <summary>
        /// Frontend route-guard hits this before mounting any gated page.
        /// Returns { allowed, reason, ... }. The route path is the React Router
        /// pattern (e.g. "/employee/update/:id"), NOT a live URL with IDs.
        /// </summary>
        [HttpGet("CheckPageAccess"), Authorize]
        public async Task<IActionResult> CheckPageAccess([FromQuery] string path)
        {
            var identity = HttpContext.User.Identity as ClaimsIdentity;
            var userClaims = AuthenticUserDetails.GetCurrentUserDetails(identity);

            if (userClaims == null || string.IsNullOrEmpty(userClaims.EmployeeId)
                || !long.TryParse(userClaims.EmployeeId, out var employeeId))
            {
                return Unauthorized(new PageAccessResultDto
                {
                    Allowed = false,
                    Reason = "Invalid or missing authentication."
                });
            }

            var result = await _pageAccessService.HasPageAccessAsync(employeeId, path);
            return Ok(result);
        }

        [HttpPost("upsert-modules"), Authorize, RequirePageAccess("/rbac-panel")]
        public async Task<ActionResult<ExecuteAndReponse>> UpsertModules([FromBody] List<ModuleDto> modules)
        {
            if (modules == null || modules.Count == 0)
            {
                return BadRequest(new ExecuteAndReponse
                {
                    Status = false,
                    Message = "Module list cannot be empty",
                    Code = HttpStatusCode.BadRequest
                });
            }

            var result = await _rbacService.UpsertModules(modules);

            // Send back appropriate HTTP status code based on ExecuteAndReponse.Code
            return StatusCode((int)result.Code, new { 
                Status = result.Status,
                Message = result.Message
            });
        }

        [HttpGet("hierarchy")]
        public async Task<ActionResult<FetchAndResponse>> GetRbacHierarchy()
        {
            try
            {
                var hierarchy = await _rbacService.GetRbacHierarchyAsync();

                return Ok(new FetchAndResponse
                {
                    Status = true,
                    Message = "RBAC hierarchy fetched successfully",
                    Code = HttpStatusCode.OK,
                    Data = hierarchy
                });
            }
            catch (Exception ex)
            {
                return StatusCode((int)HttpStatusCode.InternalServerError, new FetchAndResponse
                {
                    Status = false,
                    Message = ex.Message,
                    Code = HttpStatusCode.InternalServerError
                });
            }
        }

        [HttpPost("upsert-rbac-nodes"), Authorize, RequirePageAccess("/rbac-panel")]
        public async Task<ActionResult<ExecuteAndReponse>> UpsertRbacNodes([FromBody] List<RolePermissionPost> rolePermissions)
        {
            if (rolePermissions == null || rolePermissions.Count == 0)
            {
                return BadRequest(new ExecuteAndReponse
                {
                    Status = false,
                    Message = "Role permissions list cannot be empty",
                    Code = HttpStatusCode.BadRequest
                });
            }

            try
            {
                var result = await _rbacService.UpsertRbacNodes(rolePermissions);

                // Send back appropriate HTTP status code based on ExecuteAndReponse.Code
                return StatusCode((int)result.Code, new { 
                    Status = result.Status,
                    Message = result.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode((int)HttpStatusCode.InternalServerError, new ExecuteAndReponse
                {
                    Status = false,
                    Message = ex.Message,
                    Code = HttpStatusCode.InternalServerError
                });
            }
        }

		[HttpGet("modules-catalog")]
		public async Task<ActionResult<FetchAndResponse>> GetModulesForUpsert()
		{
			var result = await _rbacService.GetModulesForUpsertAsync();
			return StatusCode((int)result.Code, result);
		}

		[HttpGet("module/{id:int}"), Authorize, RequirePageAccess("/rbac-panel")]
		public async Task<ActionResult<ExecuteAndReponse>> DeleteModule(int id)
		{
			var result = await _rbacService.DeleteModuleAsync(id);
			return StatusCode((int)result.Code, result);
		}

		[HttpGet("submodule/{id:int}"), Authorize, RequirePageAccess("/rbac-panel")]
		public async Task<ActionResult<ExecuteAndReponse>> DeleteSubModule(int id)
		{
			var result = await _rbacService.DeleteSubModuleAsync(id);
			return StatusCode((int)result.Code, result);
		}

		[HttpGet("action/{id:int}"), Authorize, RequirePageAccess("/rbac-panel")]
		public async Task<ActionResult<ExecuteAndReponse>> DeleteAction(int id)
		{
			var result = await _rbacService.DeleteActionAsync(id);
			return StatusCode((int)result.Code, result);
		}

		[HttpGet("further-part/{id:int}"), Authorize, RequirePageAccess("/rbac-panel")]
		public async Task<ActionResult<ExecuteAndReponse>> DeleteFurtherPart(int id)
		{
			var result = await _rbacService.DeleteFurtherPartAsync(id);
			return StatusCode((int)result.Code, result);
		}

		[HttpPost("upsert-role"), Authorize, RequirePageAccess("/role-assign")]
		public async Task<ActionResult<ExecuteAndReponse>> UpsertRole([FromBody] RoleDto role)
		{
			if (role == null)
			{
				return BadRequest(new ExecuteAndReponse
				{
					Status = false,
					Message = "Role data cannot be empty",
					Code = HttpStatusCode.BadRequest
				});
			}

			var result = await _rbacService.UpsertRoleAsync(role);
			return StatusCode((int)result.Code, result);
		}

		[HttpGet("role/{id:int}"), Authorize, RequirePageAccess("/role-assign")]
		public async Task<ActionResult<ExecuteAndReponse>> DeleteRole(int id)
		{
			var result = await _rbacService.DeleteRoleAsync(id);
			return StatusCode((int)result.Code, result);
		}

		[HttpPost("upsert-employee-role"), Authorize, RequirePageAccess("/employee-role_list")]
		public async Task<ActionResult<ExecuteAndReponse>> UpsertEmployeeRole([FromBody] EmployeeRoleDto employeeRoleDto)
		{
			if (employeeRoleDto == null)
			{
				return BadRequest(new ExecuteAndReponse
				{
					Status = false,
					Message = "Employee role data cannot be empty",
					Code = HttpStatusCode.BadRequest
				});
			}

			if (employeeRoleDto.EmployeeId <= 0)
			{
				return BadRequest(new ExecuteAndReponse
				{
					Status = false,
					Message = "Employee ID is required and must be greater than 0",
					Code = HttpStatusCode.BadRequest
				});
			}

			if (employeeRoleDto.RoleId <= 0)
			{
				return BadRequest(new ExecuteAndReponse
				{
					Status = false,
					Message = "Role ID is required and must be greater than 0",
					Code = HttpStatusCode.BadRequest
				});
			}

			var result = await _rbacService.UpsertEmployeeRoleAsync(employeeRoleDto);
			return StatusCode((int)result.Code, result);
		}

		[HttpGet("employee-roles/{employeeId:long}")]
		public async Task<ActionResult<FetchAndResponse>> GetEmployeeRoles(long employeeId)
		{
			if (employeeId <= 0)
			{
				return BadRequest(new FetchAndResponse
				{
					Status = false,
					Message = "Employee ID must be greater than 0",
					Code = HttpStatusCode.BadRequest
				});
			}

			var result = await _rbacService.GetEmployeeRolesAsync(employeeId);
			return StatusCode((int)result.Code, result);
		}

		[HttpPost("delete-employee-role"), Authorize, RequirePageAccess("/employee-role_list")]
		public async Task<ActionResult<ExecuteAndReponse>> DeleteEmployeeRole([FromBody] DeleteEmployeeRoleDto deleteRequest)
		{
			if (deleteRequest == null)
			{
				return BadRequest(new ExecuteAndReponse
				{
					Status = false,
					Message = "Delete request data cannot be empty",
					Code = HttpStatusCode.BadRequest
				});
			}

			if (deleteRequest.EmployeeId <= 0)
			{
				return BadRequest(new ExecuteAndReponse
				{
					Status = false,
					Message = "Employee ID must be greater than 0",
					Code = HttpStatusCode.BadRequest
				});
			}

			if (deleteRequest.RoleId <= 0)
			{
				return BadRequest(new ExecuteAndReponse
				{
					Status = false,
					Message = "Role ID must be greater than 0",
					Code = HttpStatusCode.BadRequest
				});
			}

			var result = await _rbacService.DeleteEmployeeRoleAsync(deleteRequest.EmployeeId, deleteRequest.RoleId);
			return StatusCode((int)result.Code, result);
		}
    }
}
