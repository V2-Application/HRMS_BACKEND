namespace HRMSAPI.DTO
{
    public class LocationDesignationPolicyDTO
    {
        public int LocationDesignationPolicyId { get; set; }
        public string LocationCategoryId { get; set; }
        public string LocationCategoryName { get; set; }
        public int DesignationId { get; set; }
        public string DesignationName { get; set; }
        public string TotalAttendance { get; set; }
        public decimal WeeklyOff { get; set; }
    }

}
