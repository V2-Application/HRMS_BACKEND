namespace HRMSAPI.Controllers
{
    public partial class EmpAttendanceController
    {
        // Response DTO for consistent response format
        public class ResponseDto
        {
            public bool Status { get; set; }
            public string Message { get; set; }
            public object? Data { get; set; }
        }
    }
}

