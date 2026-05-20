using HRMSAPI.DTO;
using HRMSAPI.Extension;
using HRMSAPI.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMSAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [RequirePageAccess("/salary/min-wages")]
    public class MinWageController : ControllerBase
    {
        private readonly IMinWageService _minWageService;

        public MinWageController(IMinWageService minWageService)
        {
            _minWageService = minWageService;
        }

        /// <summary>
        /// Validates if the provided salary is above the minimum wage for the given STCode
        /// </summary>
        /// <param name="request">Request containing STCode and Salary</param>
        /// <returns>Validation result with minimum wage information</returns>
        [HttpPost("validate")]
        public async Task<IActionResult> ValidateSalaryAgainstMinWage([FromBody] MinWageValidationRequestDto request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Invalid request data",
                        errors = ModelState
                    });
                }

                var result = await _minWageService.ValidateSalaryAgainstMinWageAsync(request.STCode, request.Salary);

                return Ok(new
                {
                    success = true,
                    data = result
                });
            }
            catch (ArgumentException ex)
            {
                // This includes the case when salary is below minimum wage
                return BadRequest(new
                {
                    success = false,
                    message = ex.Message
                });
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "An error occurred while validating salary against minimum wage",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Gets the list of all states with their minimum wages
        /// </summary>
        /// <returns>List of states with minimum wages</returns>
        [HttpGet("states")]
        public async Task<IActionResult> GetStateMinWagesList()
        {
            try
            {
                var result = await _minWageService.GetStateMinWagesListAsync();

                return Ok(new
                {
                    success = true,
                    data = result,
                    count = result.Count
                });
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "An error occurred while retrieving state minimum wages list",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Updates the minimum wage for a state by ID
        /// </summary>
        /// <param name="request">Request containing Id and MinWages</param>
        /// <returns>Updated state minimum wage information</returns>
        [HttpPost("update")]

        public async Task<IActionResult> UpdateMinWage([FromBody] UpdateMinWageRequestDto request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Invalid request data",
                        errors = ModelState
                    });
                }

                var result = await _minWageService.UpdateMinWageAsync(request.Id, request.MinWages);

                return Ok(new
                {
                    success = true,
                    message = "Minimum wage updated successfully",
                    data = result
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new
                {
                    success = false,
                    message = ex.Message
                });
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new
                {
                    success = false,
                    message = ex.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "An error occurred while updating minimum wage",
                    error = ex.Message
                });
            }
        }
    }
}

