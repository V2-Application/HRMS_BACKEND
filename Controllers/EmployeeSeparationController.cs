using HRMSAPI.DTO;
using HRMSAPI.Extension;
using HRMSAPI.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace HRMSAPI.Controllers
{

    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    [RequirePageAccess("/sepration/resignation_applications")]
    public class EmployeeSeparationController : ControllerBase
    {
        private readonly IEmployeeSeparationService _service;

        public EmployeeSeparationController(IEmployeeSeparationService service)
        {
            _service = service;
        }

        [HttpPost]
        public async Task<IActionResult> CreateEmployeeSeparation([FromBody] EmployeeSeparationDto model)
        {
            try
            {
                var result = await _service.CreateEmployeeSeparationAsync(model);
                return Ok(new
                {
                    Status = true,
                    Message = "Employee separation created successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    Status = false,
                    Message = "An error occurred while creating employee separation",
                    Error = ex.Message
                });
            }
        }
        [HttpGet]
        public async Task<IActionResult> GetEmployeeSeparations([FromQuery] long empId)
        {
            try
            {
                var result = await _service.GetEmployeeSeparationsAsync(empId);
                return Ok(new
                {
                    Status = true,
                    Message = "Employee separations retrieved successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    Status = false,
                    Message = "An error occurred while fetching employee separations",
                    Error = ex.Message
                });
            }
        }
        //[HttpGet("GetResignedEmployee")]

        //public async Task<ActionResult<(List<EmployeeSeparationResponseDto> Resignations, int TotalCount)>> GetResignations(
        //[FromQuery] long? managerId,
        //[FromQuery] int pageNumber = 1,
        //[FromQuery] int pageSize = 10,
        //[FromQuery] string searchTerm = null)
        //{
        //    try
        //    {
        //        if (pageNumber < 1 || pageSize < 1)
        //        {
        //            return BadRequest("Page number and page size must be greater than 0.");
        //        }

        //        var (resignations, totalCount) = await _service.GetResignationsByManagerAsync(
        //            managerId, pageNumber, pageSize, searchTerm);

        //        return Ok(new { Resignations = resignations, TotalCount = totalCount });
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, $"An error occurred: {ex.Message}");
        //    }
        //}
        [HttpGet("GetResignedEmployee")]
        public async Task<IActionResult> GetResignations(long? managerId, int pageNumber = 1, int pageSize = 10, string searchTerm = null, bool isExcel = false)
        {
            var result = await _service.GetResignationsByManagerAsync(
                managerId, pageNumber, pageSize, searchTerm, isExcel);

            // Excel download
            if (isExcel)
            {
                return File(
                    result.ExcelBytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "Resignations.xlsx");
            }

            // Normal list
            return Ok(new
            {
                Resignations = result.Data,
                TotalCount = result.TotalCount
            });
        }

        [HttpPost("action")]
        public async Task<ActionResult> ProcessSeparationAction([FromBody] ProcessSeparationActionDto model)
        {
            try
            {
                var identity = HttpContext.User.Identity as ClaimsIdentity;
                var loginDetail = AuthenticUserDetails.GetCurrentUserDetails(identity);

                var result = await _service.ProcessSeparationActionAsync(
                    model.EmployeeSeprationId,
                    model.UserId,
                    model.ActionType,
                    model.Remarks,                   
                    loginDetail.role,
                    model.LastDay,loginDetail.EmployeeId);

                return Ok(new
                {
                    Success = result,
                    Message = $"Separation request {model.ActionType.ToLower()}d successfully."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }
        [HttpGet("{id}")]
        public async Task<IActionResult> GetEmployeeSeparationById(int id)
        {
            try
            {
                var result = await _service.GetEmployeeSeparationByIdAsync(id);
                return Ok(new
                {
                    Status = true,
                    Message = "Employee separation retrieved successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    Status = false,
                    Message = "An error occurred while retrieving employee separation",
                    Error = ex.Message
                });
            }
        }
    }

}

