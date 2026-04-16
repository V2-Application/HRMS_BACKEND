using HRMSAPI.DTO;
using HRMSAPI.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Roomsy.DTOS.GenericsResponses;
using System.Threading.Tasks;

namespace HRMSAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    //[Authorize]
    public class EmployeeRoleController : ControllerBase
    {
        private readonly IEmployeeRoleService _employeeRoleService;
        private readonly ILogger<EmployeeRoleController> _logger;

        public EmployeeRoleController(IEmployeeRoleService employeeRoleService, ILogger<EmployeeRoleController> logger)
        {
            _employeeRoleService = employeeRoleService ?? throw new System.ArgumentNullException(nameof(employeeRoleService));
            _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
        }

        [HttpPost("bulk-upsert")]
        public async Task<IActionResult> BulkUpsertEmployeeRoles([FromBody] EmployeeRoleBulkUpsertDto request)
        {
            if (request == null)
            {
                return BadRequest("Request data is required");
            }

            var result = await _employeeRoleService.BulkUpsertEmployeeRolesAsync(request);
            return StatusCode((int)result.Code, result);
        }

        [HttpGet("get-all")]
        public async Task<IActionResult> GetAllEmployeeRoles()
        {
            var result = await _employeeRoleService.GetAllEmployeeRolesAsync();
            return StatusCode((int)result.Code, result);
        }
    }
}
