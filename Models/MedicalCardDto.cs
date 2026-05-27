using System;

namespace HRMSAPI.Models;

public class MedicalCardDto
{
    public int id { get; set; }
    public long employeeId { get; set; }
    public string ecode { get; set; }
    public int cardOrder { get; set; }
    public string uhidNo { get; set; }
    public string holderName { get; set; }
    public int? age { get; set; }
    public string gender { get; set; }
    public DateOnly? planValidFrom { get; set; }
    public DateOnly? planValidTo { get; set; }
    public string policyNo { get; set; }
    public string organisation { get; set; }
    public string insurer { get; set; }
    public string tpa { get; set; }
    public decimal? sumAssured { get; set; }
    public string sourcePdfUrl { get; set; }
}
