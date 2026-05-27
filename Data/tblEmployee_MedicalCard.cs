// Manually authored entities for the medical-card feature added 2026-05-26.
// Two artifacts live here:
//   1) Partial extension of EF-scaffolded tblEmployee adding the MedicalCardUrl
//      column from DatabaseScripts/Add_tblEmployee_MedicalCardUrl_20260526.sql.
//   2) New tblEmployee_MedicalCard entity holding one row per parsed PDF page
//      (DatabaseScripts/Add_tblEmployee_MedicalCard_20260526.sql).
// DbSet registration lives in Data/HRMSContext_MedicalCard.cs (partial).
#nullable disable
using System;

namespace HRMSAPI.Data;

public partial class tblEmployee
{
    public string MedicalCardUrl { get; set; }
}

public partial class tblEmployee_MedicalCard
{
    public int Id { get; set; }
    public long EmployeeId { get; set; }
    public string Ecode { get; set; }
    public int CardOrder { get; set; }
    public string UhidNo { get; set; }
    public string HolderName { get; set; }
    public int? Age { get; set; }
    public string Gender { get; set; }
    public DateOnly? PlanValidFrom { get; set; }
    public DateOnly? PlanValidTo { get; set; }
    public string PolicyNo { get; set; }
    public string Organisation { get; set; }
    public string Insurer { get; set; }
    public string Tpa { get; set; }
    public decimal? SumAssured { get; set; }
    public string SourcePdfUrl { get; set; }
    public string RawText { get; set; }
    public DateTime CreatedOn { get; set; }
    public string CreatedBy { get; set; }
    public DateTime? UpdatedOn { get; set; }
    public string UpdatedBy { get; set; }
}
