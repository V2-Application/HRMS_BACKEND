using HRMSAPI.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Net;
using System.Threading.Tasks;

[Route("api/[controller]")]
[ApiController]
public class DCAttendanceController : ControllerBase
{
    private readonly IDDCAttendanceService _attendanceRepository;

    public DCAttendanceController(IDDCAttendanceService attendanceRepository)
    {
        _attendanceRepository = attendanceRepository;
    }

    [HttpPost]
    public async Task<IActionResult> InsertAttendance([FromBody] List<DCAttendanceDTO> attendances)
    {
        try
        {
            if (attendances == null || !attendances.Any())
            {
                return StatusCode((int)HttpStatusCode.BadRequest, new ApiResponse<object>(
                    HttpStatusCode.BadRequest,
                    false,
                    "Attendance list is empty or null.",
                    null
                ));
            }

            if (!ModelState.IsValid)
            {
                return StatusCode((int)HttpStatusCode.BadRequest, new ApiResponse<object>(
                    HttpStatusCode.BadRequest,
                    false,
                    "Invalid request data.",
                    ModelState
                ));
            }

            var attendanceIds = await _attendanceRepository.InsertAttendanceAsync(attendances);
            var responseData = new { DCAttendanceIds = attendanceIds };

            return StatusCode((int)HttpStatusCode.OK, new ApiResponse<object>(
                HttpStatusCode.OK,
                true,
                "Attendance records created successfully.",
                responseData
            ));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error inserting attendance records: {ex.Message}");
            return StatusCode((int)HttpStatusCode.InternalServerError, new ApiResponse<object>(
                HttpStatusCode.InternalServerError,
                false,
                "An error occurred while inserting the attendance records.",
                null
            ));
        }
    }
}