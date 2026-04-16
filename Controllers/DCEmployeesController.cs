using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Threading.Tasks;
using HRMSAPI.DTO;
using HRMSAPI.Interfaces; // Import the correct namespace for DCEmployeeDTO

[Route("api/[controller]")]
[ApiController]
public class DCEmployeeController : ControllerBase
{
    private readonly IDCEmployeeService _employeeService;

    public DCEmployeeController(IDCEmployeeService employeeService)
    {
        _employeeService = employeeService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] DCLoginRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return StatusCode((int)HttpStatusCode.BadRequest, new ApiResponse<object>(
                    HttpStatusCode.BadRequest,
                    false,
                    "Invalid request data.",
                    ModelState
                ));
            }

            var employees = await _employeeService.DCLoginAsync(request);
            if (employees.Count == 0)
            {
                return StatusCode((int)HttpStatusCode.Unauthorized, new ApiResponse<object>(
                    HttpStatusCode.Unauthorized,
                    false,
                    "Invalid credentials",
                    null
                ));
            }

            return StatusCode((int)HttpStatusCode.OK, new ApiResponse<List<DCEmployeeDTO>>(
                HttpStatusCode.OK,
                true,
                "Login successful",
                employees
            ));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during login: {ex.Message}");
            return StatusCode((int)HttpStatusCode.InternalServerError, new ApiResponse<object>(
                HttpStatusCode.InternalServerError,
                true,
                "An error occurred while processing the login request.",
                null
            ));
        }
    }
}