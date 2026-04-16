namespace HRMSAPI.DTO
{
    public class FaceValidationResult
    {
        public bool IsValid { get; set; }
        public long? EmployeeId { get; set; }
        public string Message { get; set; }
        public string EmployeeCode { get; set; }
        public string Email { get; set; }
        public string FullName { get; set; }
    }
}
