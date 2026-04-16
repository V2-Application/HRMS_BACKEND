public class JobOpeningDto
{
    public int? StoreBudgetId { get; set; }
    public string? DesignationName { get; set; }
    public int? LocationId { get; set; }          // Add this
    public string? LocationName { get; set; }
    public int? BudgetManpowerCount { get; set; }
    public decimal? BudgetAmount { get; set; }
    public string? KeyResponsibility { get; set; }
    public string? KeySkill { get; set; }
    public int? DesignationId { get; set; }
}

