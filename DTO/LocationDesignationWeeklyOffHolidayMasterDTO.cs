namespace HRMSAPI.DTO
{
    public class LocationDesignationWeeklyOffHolidayMasterDTO
    {
        public int LocationDesignationWeeklyOffHolidayMasterId { get; set; }
        public string Month { get; set; }
        public int LocationCategoryId { get; set; }
        public string LocationCategoryName { get; set; }
        public int DesignationId { get; set; }
        public string DesignationName { get; set; }
        public decimal BudgetWeeklyOff { get; set; }
        public decimal BudgetHoliday { get; set; }
    }

}
