using HRMSAPI.Data;
using HRMSAPI.DTO;
using HRMSAPI.Interfaces;
using HRMSAPI.Utility;
using Microsoft.AspNetCore.Mvc;
using SuzukiVidms.Infrastructure.Utilities;
using System.Text.Json;
using System.Text;

namespace HRMSAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FaceValidationController : ControllerBase
    {
        private readonly IEmployeeService _employeeService;
        private readonly FaceValidator _faceValidator;

        public FaceValidationController(IEmployeeService employeeService, FaceValidator faceValidator)
        {
            _employeeService = employeeService;
            _faceValidator = faceValidator;
        }

        [HttpPost("register-user-face")]
        public async Task<IActionResult> RegisterUserFace([FromBody] RegisterFaceDataRequest request)
        {
            try
            {
                // Validate request
                if (request == null || string.IsNullOrEmpty(request.Ecode) || string.IsNullOrEmpty(request.FaceDescriptorsJson))
                {
                    Console.WriteLine("Invalid request: Ecode or FaceDescriptorsJson is null or empty.");
                    return BadRequest(new FaceValidationResult
                    {
                        IsValid = false,
                        EmployeeId = null,
                        Message = "Ecode and face descriptors are required."
                    });
                }

                // Deserialize face descriptors
                float[] faceDescriptors;
                try
                {
                    faceDescriptors = FaceDataUtility.DeserializeFaceDescriptors(request.FaceDescriptorsJson);
                }
                catch (ArgumentException ex)
                {
                    Console.WriteLine($"Invalid face descriptors for Ecode {request.Ecode}: {ex.Message}");
                    return BadRequest(new FaceValidationResult
                    {
                        IsValid = false,
                        EmployeeId = null,
                        Message = ex.Message
                    });
                }

                // Find employee by Ecode
                var employee = await _employeeService.GetEmployeeByEcodeAsync(request.Ecode);
                if (employee == null)
                {
                    Console.WriteLine($"Employee not found for Ecode {request.Ecode}.");
                    return NotFound(new FaceValidationResult
                    {
                        IsValid = false,
                        EmployeeId = null,
                        Message = $"Employee with Ecode {request.Ecode} not found."
                    });
                }

                // Serialize face descriptors to comma-separated string
                string faceData;
                try
                {
                    StringBuilder sb = new StringBuilder();
                    for (int i = 0; i < faceDescriptors.Length; i++)
                    {
                        sb.Append(faceDescriptors[i].ToString(System.Globalization.CultureInfo.InvariantCulture));
                        if (i < faceDescriptors.Length - 1)
                            sb.Append(",");
                    }
                    faceData = sb.ToString();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to serialize face descriptors for Ecode {request.Ecode}: {ex.Message}");
                    return BadRequest(new FaceValidationResult
                    {
                        IsValid = false,
                        EmployeeId = employee.EmployeeId,
                        Message = "Failed to serialize face descriptors."
                    });
                }

                employee.FaceData = faceData;
                var employees = await _employeeService.GetActiveEmployeesWithFaceDataAsync();
                foreach (var emp in employees)
                {
                    if (emp.EmployeeId != employee.EmployeeId)
                    {
                        try
                        {
                            var storedDescriptors = FaceDataUtility.DeserializeFaceDescriptors(emp.FaceData);
                            if (_faceValidator.ValidateFaceDescriptors(faceDescriptors, storedDescriptors))
                            {
                                Console.WriteLine($"Duplicate face data detected for Ecode {request.Ecode}.");
                                return BadRequest(new FaceValidationResult
                                {
                                    IsValid = false,
                                    EmployeeId = employee.EmployeeId,
                                    Message = "Face data is too similar to an existing employee."
                                });
                            }
                        }
                        catch (ArgumentException)
                        {
                            continue;
                        }
                    }
                }

                try
                {
                    await _employeeService.UpdateEmployeeAsync(employee);
                    Console.WriteLine($"Face data registered successfully for Ecode {request.Ecode}.");
                    return Ok(new FaceValidationResult
                    {
                        IsValid = true,
                        EmployeeId = employee.EmployeeId,
                        Message = "Face data registered successfully.",
                        EmployeeCode = employee.Ecode,
                        Email = employee.EMAIL_ADDRESS,
                        FullName = employee.FULL_NAME
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error registering face data for Ecode {request.Ecode}: {ex.Message}");
                    return StatusCode(500, new FaceValidationResult
                    {
                        IsValid = false,
                        EmployeeId = employee.EmployeeId,
                        Message = "An error occurred while registering face data."
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error processing request: {ex.Message}");
                return StatusCode(500, new FaceValidationResult
                {
                    IsValid = false,
                    EmployeeId = null,
                    Message = "An unexpected error occurred."
                });
            }
        }
        [HttpPost("validate-face")]
        public async Task<IActionResult> ValidateFaceData([FromBody] ValidateFaceDataRequest request)
        {
            try
            {
                // Validate request
                if (request == null || string.IsNullOrEmpty(request.FaceDescriptorsJson))
                {
                    Console.WriteLine("Invalid request: FaceDescriptorsJson is null or empty.");
                    return BadRequest(new FaceValidationResult
                    {
                        IsValid = false,
                        EmployeeId = null,
                        Message = "Face descriptors are required."
                    });
                }

                // Deserialize face descriptors
                float[] faceDescriptors;
                try
                {
                    faceDescriptors = FaceDataUtility.DeserializeFaceDescriptors(request.FaceDescriptorsJson);
                }
                catch (ArgumentException ex)
                {
                    Console.WriteLine($"Invalid face descriptors: {ex.Message}");
                    return BadRequest(new FaceValidationResult
                    {
                        IsValid = false,
                        EmployeeId = null,
                        Message = ex.Message
                    });
                }

                // Retrieve active employees with face data
                var employees = await _employeeService.GetActiveEmployeesWithFaceDataAsync();
                if (!employees.Any())
                {
                    Console.WriteLine("No active employees with face data found.");
                    return Ok(new FaceValidationResult
                    {
                        IsValid = false,
                        EmployeeId = null,
                        Message = "No active employees with face data found."
                    });
                }

                // Compare face descriptors
                foreach (var employee in employees)
                {
                    float[] storedDescriptors;
                    try
                    {
                        storedDescriptors = FaceDataUtility.DeserializeFaceDescriptors(employee.FaceData);
                    }
                    catch (ArgumentException ex)
                    {
                        Console.WriteLine($"Error deserializing face data for EmployeeId {employee.EmployeeId}: {ex.Message}");
                        continue;
                    }

                    try
                    {
                        bool isMatch = _faceValidator.ValidateFaceDescriptors(faceDescriptors, storedDescriptors);
                        if (isMatch)
                        {
                            Console.WriteLine($"Face data matched for EmployeeId {employee.EmployeeId}.");
                            return Ok(new FaceValidationResult
                            {
                                IsValid = true,
                                EmployeeId = employee.EmployeeId,
                                Message = "Face data matched successfully.",
                                EmployeeCode = employee.Ecode,
                                Email = employee.EMAIL_ADDRESS,
                                FullName = employee.FULL_NAME
                            });
                        }
                    }
                    catch (ArgumentException ex)
                    {
                        Console.WriteLine($"Error validating face descriptors for EmployeeId {employee.EmployeeId}: {ex.Message}");
                        continue;
                    }
                }

                Console.WriteLine("No matching face data found.");
                return Ok(new FaceValidationResult
                {
                    IsValid = false,
                    EmployeeId = null,
                    Message = "No matching face data found."
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error processing request: {ex.Message}");
                return StatusCode(500, new FaceValidationResult
                {
                    IsValid = false,
                    EmployeeId = null,
                    Message = "An unexpected error occurred."
                });
            }
        }
    }
}