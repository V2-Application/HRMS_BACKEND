using Microsoft.AspNetCore.Mvc;
using HRMSAPI.Data;
using HRMSAPI.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using HRMSAPI.DTO;

namespace HRMSAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DropDownController : ControllerBase
    {
        private readonly IDropDownService _service;
        private readonly ILogger<DropDownController> _logger;
        // Used only by GetRoleMaster, which reads the V2 Parivar role list straight
        // from dbo.tblRole rather than going through a service.
        private readonly HRMSContext _context;

        public DropDownController(IDropDownService service, ILogger<DropDownController> logger, HRMSContext context)
        {
            _service = service;
            _logger = logger;
            _context = context;
        }

        [HttpGet("GetDesignation")]
        public async Task<IActionResult> GetDesignation()
        {
            try
            {
                var result = await _service.GetDesignation();
                return Ok(new
                {
                    Status = true,
                    Message = "Designations retrieved successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
               
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    Status = false,
                    Message = "An error occurred while fetching designations",
                    Error = ex.Message
                });
            }
        }

        [HttpGet("GetDesignationsByDepartment")]
        public async Task<IActionResult> GetDesignationsByDepartment([FromQuery] int? deptId = null)
        {
            try
            {
                var result = await _service.GetDesignationsByDepartment(deptId);
                return Ok(new
                {
                    Status = true,
                    Message = "Designations retrieved successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching designations by department");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    Status = false,
                    Message = "An error occurred while fetching designations",
                    Error = ex.Message
                });
            }
        }

        [HttpGet("GetDepartment")]
        public async Task<IActionResult> GetDepartment()
        {
            try
            {
                var result = await _service.GetDepartments();
                return Ok(new
                {
                    Status = true,
                    Message = "Departments retrieved successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching departments");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    Status = false,
                    Message = "An error occurred while fetching departments",
                    Error = ex.Message
                });
            }
        }

        [HttpGet("GetLocation")]
        public async Task<IActionResult> GetLocation()
        {
            try
            {
                var result = await _service.GetLocation();
                return Ok(new
                {
                    Status = true,
                    Message = "Locations retrieved successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching locations");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    Status = false,
                    Message = "An error occurred while fetching locations",
                    Error = ex.Message
                });
            }
        }

        [HttpGet("GetLeaveType")]
        public async Task<IActionResult> GetLeaveType()
        {
            try
            {
                var result = await _service.GetLeaveTypes();
                return Ok(new
                {
                    Status = true,
                    Message = "Leave types retrieved successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching leave types");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    Status = false,
                    Message = "An error occurred while fetching leave types",
                    Error = ex.Message
                });
            }
        }
        [HttpGet("countries")]
        public async Task<ActionResult<List<Country>>> GetCountries()
        {
            var countries = await _service.GetCountriesAsync();
            return Ok(countries);
        }

        // Endpoint to get states by country id
        [HttpGet("states/{countryId}")]
        public async Task<ActionResult<List<State>>> GetStatesByCountryId(int countryId)
        {
            var states = await _service.GetStatesByCountryIdAsync(countryId);
            if (states == null || !states.Any())
            {
                return NotFound($"No states found for country with ID {countryId}");
            }
            return Ok(states);
        }

        // Endpoint to get cities by state id
        [HttpGet("cities/{stateId}")]
        public async Task<ActionResult<List<City>>> GetCitiesByStateId(int stateId)
        {
            var cities = await _service.GetCitiesByStateIdAsync(stateId);
            if (cities == null || !cities.Any())
            {
                return NotFound($"No cities found for state with ID {stateId}");
            }
            return Ok(cities);
        }
        /// <summary>
        /// nick
        /// All dropdown in one 
        /// </summary>
        /// <param name="string"></param>
        /// <returns></returns>
        [HttpGet("GetDropdownData")]
        public async Task<IActionResult> GetDropdownData([FromQuery] string type)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(type))
                {
                    return BadRequest(new
                    {
                        Status = false,
                        Message = "Type parameter is required"
                    });
                }

                var requestedTypes = type.ToLower().Split(',').Select(t => t.Trim()).ToList();
                var response = new Dictionary<string, object>();

                if (requestedTypes.Contains("designation"))
                    response["Designation"] = await _service.GetDesignation();

                if (requestedTypes.Contains("department"))
                    response["Department"] = await _service.GetDepartments();

                if (requestedTypes.Contains("location"))
                    response["Location"] = await _service.GetLocation();

                if (requestedTypes.Contains("leavetype"))
                    response["LeaveType"] = await _service.GetLeaveTypes();

                if (response.Count == 0)
                {
                    return BadRequest(new
                    {
                        Status = false,
                        Message = "Invalid type parameter(s)"
                    });
                }

                return Ok(new
                {
                    Status = true,
                    Message = "Dropdown data retrieved successfully",
                    Data = response
                });
            }
            catch (Exception ex)
            {
                
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    Status = false,
                    Message = "An error occurred while fetching dropdown data",
                    Error = ex.Message
                });
            }
        }
        [HttpGet("GetCompany")]
        public async Task<IActionResult> GetCompany()
        {
            try
            {
                var result = await _service.GetCompany();
                return Ok(new
                {
                    Status = true,
                    Message = "Company retrieved successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching Company");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    Status = false,
                    Message = "An error occurred while fetching departments",
                    Error = ex.Message
                });
            }
        }
        [HttpGet("GetReasonForLeaving")]
        public async Task<IActionResult> GetReasonForLeaving()
        {
            try
            {
                var result = await _service.ReasonForSeparation();
                return Ok(new
                {
                    Status = true,
                    Message = "ReasonForLeaving List retrieved successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching Company");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    Status = false,
                    Message = "An error occurred while fetching departments",
                    Error = ex.Message
                });
            }
        }
        [HttpGet("GetResignationType")]
        public async Task<IActionResult> GetResignationType()
        {
            try
            {
                var result = await _service.GetResignationType();
                return Ok(new
                {
                    Status = true,
                    Message = "GetResignationType retrieved successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {

                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    Status = false,
                    Message = "An error occurred while fetching GetResignationType",
                    Error = ex.Message
                });
            }
        }
        [HttpGet("AbscondingReason/{resignationTypeId}")]
        public async Task<ActionResult<List<AbscondingReason>>> GetAbscondingReasonsByResignationTypeId(int resignationTypeId)
        {
            var reasons = await _service.GetAbscondingReasonsByResignationTypeIdAsync(resignationTypeId);
            if (reasons == null || !reasons.Any())
            {
                return NotFound($"No absconding reasons found for resignation type with ID {resignationTypeId}");
            }
            return Ok(reasons);
        }

        [HttpGet("BlackListReason")]
        public async Task<ActionResult<List<BlackListReason>>> GetBlackListReasonsByResignationTypeId(int resignationTypeId)
        {
            var reasons = await _service.GetBlackListReasonsByResignationTypeIdAsync(resignationTypeId);
            if (reasons == null || !reasons.Any())
            {
                return NotFound($"No BlackList reasons found for resignation type with ID {resignationTypeId}");
            }
            return Ok(reasons);
        }


        [HttpGet("GetStoreLocation")]
        public async Task<IActionResult> GetStoreLocation()
        {
            try
            {
                var result = await _service.GetStoreLocation();
                return Ok(new
                {
                    Status = true,
                    Message = "Locations retrieved successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching locations");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    Status = false,
                    Message = "An error occurred while fetching locations",
                    Error = ex.Message
                });
            }
        }
        [HttpGet("GetShiftMaster")]
        public async Task<IActionResult> GetShiftMaster()
        {
            try
            {
                var result = await _service.GetShiftMaster();
                return Ok(new
                {
                    Status = true,
                    Message = "ShiftMaster List retrieved successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    Status = false,
                    Message = "An error occurred",
                    Error = ex.Message
                });
            }
        }
        /// <summary>
        /// Active HR roles (dbo.tblRoleMaster) for the Role dropdown on the employee
        /// profile and the candidate page. Lives here rather than on
        /// RoleMasterController because that controller is gated on the Role Master
        /// page, which only IT Superadmin holds - HR still has to be able to PICK a
        /// role even though only IT Superadmin can maintain the list.
        ///
        /// Not the portal/RBAC roles: those come from /api/Auth/Roles.
        /// </summary>
        /// <summary>
        /// Feed for the "Role" dropdown on the employee profile and the candidate
        /// page: the HR-maintained job-role list from dbo.tblRoleMaster, managed on
        /// Masters -> Role Master.
        ///
        /// NOT the V2 Parivar portal roles (dbo.tblRole). Those are the live RBAC
        /// assignment and are managed under Settings -> Role Assignment; this field
        /// records a job role and grants no access.
        ///
        /// Active roles only, so a role HR has retired stops being offered without
        /// disturbing the employees already holding it.
        /// </summary>
        [HttpGet("GetRoleMaster")]
        public async Task<IActionResult> GetRoleMaster()
        {
            try
            {
                var roles = await _context.tblRoleMasters
                    .AsNoTracking()
                    .Where(r => r.IsActive == true)
                    .OrderBy(r => r.RoleName)
                    .Select(r => new { RoleId = r.RoleMasterId, RoleName = r.RoleName })
                    .ToListAsync();

                return Ok(new
                {
                    Status = true,
                    Message = "Role list retrieved successfully",
                    Data = roles
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching roles");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    Status = false,
                    Message = "An error occurred",
                    Error = ex.Message
                });
            }
        }

        [HttpGet("GetLocationDesignationPolicyCategory")]
        public async Task<IActionResult> GetLocationDesignationPolicyCategory()
        {
            try
            {
                var result = await _service.GetLocationDesignationPolicyCategory();
                return Ok(new
                {
                    Status = true,
                    Message = "Locations retrieved successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching locations");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    Status = false,
                    Message = "An error occurred while fetching locations",
                    Error = ex.Message
                });
            }
        }

    }
}