using HRMSAPI.Data;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace HRMSAPI.DTO
{
    public class StoreLocationUpsertDto
    {
        public string? NameOfLocation { get; set; }
        public string? LocationIncharge { get; set; }
        public string? Address { get; set; }
        public string? SAPCode { get; set; }
        public string? Zone { get; set; }
        public bool? BillingOver50Lac { get; set; }
        public string? PFCode { get; set; }
        public string? ESICode { get; set; }
        public string? Type { get; set; }
        public int? StateId { get; set; }
        public string? WeeklyOff { get; set; }
        public string? EmailID { get; set; }
        public string? ERPSiteNameCode { get; set; }
        public string? Udf1 { get; set; }
        public string? Udf2 { get; set; }
        public string? Udf3 { get; set; }
    }

    public class StoreBudgetUpsertDto
    {
        public int? StoreBudgetId { get; set; }
        public int? StoreLocationsId { get; set; }
        public int? DesignationId { get; set; }
        public string? DesignationName { get; set; }
        public int? BudgetManpowerCount { get; set; }
        public decimal? BudgetAmount { get; set; }
        public string? Udf1 { get; set; }
        public string? Udf2 { get; set; }
        public string? Udf3 { get; set; }
    }

    public class StoreDetailDto
    {
        public int StoreLocationsId { get; set; }
        public string NameOfLocation { get; set; }
        public string LocationIncharge { get; set; }
        public string Address { get; set; }
        public string SAPCode { get; set; }
        public string Zone { get; set; }
        public bool BillingOver50Lac { get; set; }
        public string PFCode { get; set; }
        public string ESICode { get; set; }
        public string Type { get; set; }
        public int StateId { get; set; }
        public string? StateName { get; set; }

        public string WeeklyOff { get; set; }
        public string EmailID { get; set; }
        public string ERPSiteNameCode { get; set; }
    
        public string CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; }
        public string LastupdatedBy { get; set; }
        public List<StoreBudgetUpsertDto> StoreBudgets { get; set; }
    }
}

