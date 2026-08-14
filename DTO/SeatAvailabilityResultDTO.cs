namespace HRMSAPI.DTO
{
    public class SeatAvailabilityResultDTO
    {
        public int SeatBudget { get; set; }
        public int FilledByEmployees { get; set; }
        public int FilledByCandidates { get; set; }
        public int Occupied { get; set; }
        public int Vacancy { get; set; }
        public decimal? MaxBudgetedSalary { get; set; }
        public bool IsAvailable { get; set; }
        public string? LocationName { get; set; }
        public string? DepartmentName { get; set; }
        public string? SubDepartmentName1 { get; set; }
        public string? SubDepartmentName2 { get; set; }
        public string? SubDepartmentName3 { get; set; }
        public string? DesignationName { get; set; }
        public string? Message { get; set; }
    }
}
