using HRMSAPI.Data;
using HRMSAPI.DTO;
using HRMSAPI.Extension;
using HRMSAPI.Implementation;
using HRMSAPI.Interfaces;
using HRMSAPI.Models.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Diagnostics.Contracts;
using System.Net;
using System.Security.Claims;

[ApiController]
[Authorize]
[Route("api/[controller]/[action]")]
[HRMSAPI.Extension.RequirePageAccess("/vendor/master-list")]
public class VendorController : ControllerBase
{
    private readonly IVendorService _service;

    public VendorController(IVendorService service)
    {
        _service = service;
    }
    [HttpGet]
    public async Task<IActionResult> GetAll(
      [FromQuery] int pageNumber = 1,
      [FromQuery] int pageSize = 10,
      [FromQuery] DateTime? contractStartDate = null,
      [FromQuery] DateTime? contractEndDate = null,
      [FromQuery] string searchTerm = "")
    {

        var userIdentity = User.Identity as ClaimsIdentity;
        if (userIdentity == null || !userIdentity.IsAuthenticated)
        {

            return Unauthorized(new
            {
                Status = false,
                Message = "User is not authenticated"
            });
        }
        // Call service
        var response = await _service.GetVendorListAsync(pageNumber, pageSize, contractStartDate, contractEndDate, searchTerm);

        // Return appropriate HTTP code
        if (response.Status)
        {
            return Ok(response);
        }
        else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return NotFound(response);
        }
        else
        {
            return StatusCode((int)response.StatusCode, response);
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetById(long id)
    {
        var userIdentity = User.Identity as ClaimsIdentity;
        if (userIdentity == null || !userIdentity.IsAuthenticated)
        {

            return Unauthorized(new
            {
                Status = false,
                Message = "User is not authenticated"
            });
        }
        var vendor = await _service.GetVendorByIdAsync(id);
        if (vendor.Status)
        {
            return Ok(vendor);

        }
        return BadRequest(vendor);

    }

    [HttpPost]

    public async Task<IActionResult> CreateVendor([FromBody] CreateVendorDTO vendorDTO)
    {

        var userIdentity = User.Identity as ClaimsIdentity;
        if (userIdentity == null || !userIdentity.IsAuthenticated)
        {

            return Unauthorized(new
            {
                Status = false,
                Message = "User is not authenticated"
            });
        }
        if (vendorDTO == null)
            return BadRequest(new Response { Status = false, Message = "Vendor data is required", StatusCode = System.Net.HttpStatusCode.BadRequest });
        var identity = HttpContext.User.Identity as ClaimsIdentity;
        var userClaims = AuthenticUserDetails.GetCurrentUserDetails(identity);
        var employeeId = Convert.ToInt64(userClaims.EmployeeId);
        var response = await _service.CreateVendor(vendorDTO, employeeId);

        if (response.Status)
            return Ok(response);
        else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            return BadRequest(response);
        else
            return StatusCode((int)response.StatusCode, response);
    }

    [HttpPut("{vendorId:long}")]
    public async Task<IActionResult> UpdateVendor(long vendorId, [FromBody] UpdateVendorDTO vendorDTO)
    {

        var userIdentity = User.Identity as ClaimsIdentity;
        if (userIdentity == null || !userIdentity.IsAuthenticated)
        {

            return Unauthorized(new
            {
                Status = false,
                Message = "User is not authenticated",
                StatusCode = System.Net.HttpStatusCode.Unauthorized
            });
        }

        var identity = HttpContext.User.Identity as ClaimsIdentity;
        var userClaims = AuthenticUserDetails.GetCurrentUserDetails(identity);
        var employeeId = Convert.ToInt64(userClaims.EmployeeId);

        var response = await _service.UpdateVendor(vendorId, vendorDTO, employeeId);

        return response.StatusCode switch
        {
            System.Net.HttpStatusCode.OK => Ok(response),
            System.Net.HttpStatusCode.NotFound => NotFound(response),
            System.Net.HttpStatusCode.BadRequest => BadRequest(response),
            System.Net.HttpStatusCode.InternalServerError => StatusCode(500, response),
            _ => StatusCode(500, response)
        };
    }

    [HttpPost("{id}")]
    public async Task<IActionResult> Delete(long id)
    {

        var userIdentity = User.Identity as ClaimsIdentity;
        if (userIdentity == null || !userIdentity.IsAuthenticated)
        {

            return Unauthorized(new
            {
                Status = false,
                Message = "User is not authenticated"
            });
        }
        var identity = HttpContext.User.Identity as ClaimsIdentity;
        var userClaims = AuthenticUserDetails.GetCurrentUserDetails(identity);
        var employeeId = Convert.ToInt64(userClaims.EmployeeId);
        var result = await _service.DeletevVendor(id, employeeId);
        if (result.Status)
        {
            return Ok(result);
        }
        return BadRequest(result);
    }
    [HttpGet]
    public async Task<IActionResult> GetSrviceCategory()
    {

        var userIdentity = User.Identity as ClaimsIdentity;
        if (userIdentity == null || !userIdentity.IsAuthenticated)
        {

            return Unauthorized(new
            {
                Status = false,
                Message = "User is not authenticated"
            });
        }
        var servicCategory = await _service.GetServiceCategory();
        if (servicCategory.Data.Count > 0)
        {
            return Ok(servicCategory);
        }
        else
        {
            return BadRequest(servicCategory);
        }
    }
    [HttpGet]
    public async Task<IActionResult> GetContractList()
    {

        var userIdentity = User.Identity as ClaimsIdentity;
        if (userIdentity == null || !userIdentity.IsAuthenticated)
        {

            return Unauthorized(new
            {
                Status = false,
                Message = "User is not authenticated"
            });
        }
        var contractStatus = await _service.GetContractStatus();
        if (contractStatus.Data.Count > 0)
        {
            return Ok(contractStatus);
        }
        else
        {
            return BadRequest(contractStatus);
        }
    }
    [HttpGet]
    public async Task<IActionResult> GetNatureOfWorkList()
    {

        var userIdentity = User.Identity as ClaimsIdentity;
        if (userIdentity == null || !userIdentity.IsAuthenticated)
        {

            return Unauthorized(new
            {
                Status = false,
                Message = "User is not authenticated"
            });
        }
        var workList = await _service.GetNatureOfWork();
        if (workList.Data.Count > 0)
        {
            return Ok(workList);
        }
        else
        {
            return BadRequest(workList);
        }
    }
    [HttpPost]
    public async Task<IActionResult> CreateService(RequestServiceDTO serviceDTO)
    {

        var userIdentity = User.Identity as ClaimsIdentity;
        if (userIdentity == null || !userIdentity.IsAuthenticated)
        {

            return Unauthorized(new
            {
                Status = false,
                Message = "User is not authenticated"
            });
        }
        var servicCategory = await _service.CreateServiceCategory(serviceDTO);
        if (servicCategory.Status)
        {
            return Ok(servicCategory);
        }
        else
        {
            return BadRequest(servicCategory);
        }
    }
    [HttpPost]
    public async Task<IActionResult> InsertVendorEmployee(VendorEmployeeRequestDTO request)
    {

        var userIdentity = User.Identity as ClaimsIdentity;
        if (userIdentity == null || !userIdentity.IsAuthenticated)
        {

            return Unauthorized(new
            {
                Status = false,
                Message = "User is not authenticated"
            });
        }

        var identity = HttpContext.User.Identity as ClaimsIdentity;
        var userClaims = AuthenticUserDetails.GetCurrentUserDetails(identity);
        var employeeId = Convert.ToInt64(userClaims.EmployeeId);
        var servicCategory = await _service.InsertVendorEmployee(request, employeeId.ToString());
        if (servicCategory.Status)
        {
            return Ok(servicCategory);
        }
        else
        {
            return BadRequest(servicCategory);
        }
    }
    [HttpPost]
    public async Task<IActionResult> UpdateVendorEmployee(string ecode, string contractorCode,
     [FromBody] UpdateVendorEmployeeRequestDTO request)
    {

        var userIdentity = User.Identity as ClaimsIdentity;
        if (userIdentity == null || !userIdentity.IsAuthenticated)
        {

            return Unauthorized(new
            {
                Status = false,
                Message = "User is not authenticated"
            });
        }
        if (string.IsNullOrWhiteSpace(ecode) && string.IsNullOrWhiteSpace(contractorCode))
        {
            return BadRequest(new Response
            {
                Status = false,
                StatusCode = System.Net.HttpStatusCode.BadRequest,
                Message = "Ecode and ContractorCode are required."
            });
        }
        if (request == null)
        {
            return BadRequest(new Response
            {
                Status = false,
                StatusCode = System.Net.HttpStatusCode.BadRequest,
                Message = "Invalid request data"
            });
        }
        var identity = HttpContext.User.Identity as ClaimsIdentity;
        var userClaims = AuthenticUserDetails.GetCurrentUserDetails(identity);
        var employeeId = Convert.ToInt64(userClaims.EmployeeId);
        var response = await _service.UpdateVendorEmployeeAsync(ecode, contractorCode, request, employeeId.ToString());

        // 🔹 Map Response.StatusCode to HTTP response
        return response.StatusCode switch
        {
            System.Net.HttpStatusCode.OK => Ok(response),

            System.Net.HttpStatusCode.NotFound => NotFound(response),

            System.Net.HttpStatusCode.BadRequest => BadRequest(response),

            System.Net.HttpStatusCode.InternalServerError =>
                StatusCode(StatusCodes.Status500InternalServerError, response),

            _ => StatusCode(StatusCodes.Status500InternalServerError, response)
        };
    }

    [HttpGet]
    public async Task<IActionResult> GetVendorEmployees(
        [FromQuery] string contractorCode,
        [FromQuery] string searchTerm = "",
        [FromQuery] int? isActiveFilter = null,
        [FromQuery] DateTime? contractStartDate = null,
        [FromQuery] DateTime? contractEndDate = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {

        var userIdentity = User.Identity as ClaimsIdentity;
        if (userIdentity == null || !userIdentity.IsAuthenticated)
        {

            return Unauthorized(new
            {
                Status = false,
                Message = "User is not authenticated"
            });
        }
        // Validate required parameter
        if (string.IsNullOrWhiteSpace(contractorCode))
        {
            return BadRequest(new Response
            {
                Status = false,
                StatusCode = System.Net.HttpStatusCode.BadRequest,
                Message = "ContractorCode is required.",
                Data = null
            });
        }

        // Call service
        var response = await _service.GetVendorEmployeesListAsync(contractorCode, searchTerm, isActiveFilter, contractStartDate, contractEndDate, pageNumber, pageSize);
        if (response.Status)
        {
            return Ok(response);
        }
        else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return NotFound(response);
        }
        else
        {
            return StatusCode((int)response.StatusCode, response);
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetEmployeesByEcode([FromQuery] string ecode, [FromQuery] string contractorCode)
    {
        var userIdentity = User.Identity as ClaimsIdentity;
        if (userIdentity == null || !userIdentity.IsAuthenticated)
        {

            return Unauthorized(new
            {
                Status = false,
                Message = "User is not authenticated"
            });
        }

        // Call the service
        var response = await _service.GetVendorEmployeesByIdAsync(ecode, contractorCode);

        // Map response.StatusCode to proper HTTP response
        return response.StatusCode switch
        {
            System.Net.HttpStatusCode.OK => Ok(response),
            System.Net.HttpStatusCode.BadRequest => BadRequest(response),
            System.Net.HttpStatusCode.NotFound => NotFound(response),
            System.Net.HttpStatusCode.InternalServerError => StatusCode(StatusCodes.Status500InternalServerError, response),
            _ => StatusCode(StatusCodes.Status500InternalServerError, response)
        };
    }

    [HttpGet]
    public async Task<IActionResult> GetContractors(
    [FromQuery] string contractorCode = null,
    [FromQuery] string contractorName = null,
     string searchTerm = "",
    [FromQuery] int pageNumber = 1,
    [FromQuery] int pageSize = 10)
    {
        var response = await _service.GetContractorDetailsAsync(contractorCode, contractorName, searchTerm, pageNumber, pageSize);
        return StatusCode((int)response.StatusCode, response);
    }



    [HttpGet]
    public async Task<IActionResult> GetContractorByCode(string contractorCode)
    {
        var userIdentity = User.Identity as ClaimsIdentity;
        if (userIdentity == null || !userIdentity.IsAuthenticated)
        {

            return Unauthorized(new
            {
                Status = false,
                Message = "User is not authenticated"
            });
        }
        // Validate required parameter
        if (string.IsNullOrWhiteSpace(contractorCode))
        {
            return BadRequest(new Response
            {
                Status = false,
                StatusCode = System.Net.HttpStatusCode.BadRequest,
                Message = "ContractorCode is required.",
                Data = null
            });
        }
        if (string.IsNullOrWhiteSpace(contractorCode))
        {
            return BadRequest(new Response
            {
                Status = false,
                StatusCode = HttpStatusCode.BadRequest,
                Message = "ContractorCode is required.",
                Data = null
            });
        }

        var response = await _service.GetContractorByCodeAsync(contractorCode);

        return StatusCode((int)response.StatusCode, response);
    }

    [HttpGet]
    public async Task<IActionResult> GetVendorEmployeesByContractorCode(
       [FromQuery] string contractorCode,
       [FromQuery] string searchTerm = "",
       [FromQuery] int? isActiveFilter = null,
       [FromQuery] DateTime? contractStartDate = null,
       [FromQuery] DateTime? contractEndDate = null,
       [FromQuery] int pageNumber = 1,
       [FromQuery] int pageSize = 10)
    {

        var userIdentity = User.Identity as ClaimsIdentity;
        if (userIdentity == null || !userIdentity.IsAuthenticated)
        {

            return Unauthorized(new
            {
                Status = false,
                Message = "User is not authenticated"
            });
        }
        // Validate required parameter
        if (string.IsNullOrWhiteSpace(contractorCode))
        {
            return BadRequest(new Response
            {
                Status = false,
                StatusCode = System.Net.HttpStatusCode.BadRequest,
                Message = "ContractorCode is required.",
                Data = null
            });
        }

        // Call service
        var response = await _service.GetVendorEmployeesListAsync1(contractorCode, searchTerm, isActiveFilter, contractStartDate, contractEndDate, pageNumber, pageSize);
        if (response.Status)
        {
            return Ok(response);
        }
        else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return NotFound(response);
        }
        else
        {
            return StatusCode((int)response.StatusCode, response);
        }
    }

    [HttpPost]
    public async Task<IActionResult> ImportVendorEmployeesBulk([FromForm] IFormFile file, [FromForm] string contractorCode)
    {
        try
        {
            if (string.IsNullOrEmpty(contractorCode))
            {
                return BadRequest("[Contractor Code] is required");
            }

            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            var userIdentity = User.Identity as ClaimsIdentity;
            if (userIdentity == null || !userIdentity.IsAuthenticated)
                return Unauthorized("User is not authenticated");

            var userClaims = AuthenticUserDetails.GetCurrentUserDetails(userIdentity);
            var employeeId = userClaims.EmployeeId.ToString();

            var response = await _service.ImportVendorEmployeesBulk(file, employeeId, contractorCode);

            if (!response.Status)
                return BadRequest(new { Status = false, Message = response.Message ?? "Unexpected Error." });

            return Ok(response.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Status = false, Message = "Error uploading vendor employees.", error = ex.Message });
        }
    }
}





