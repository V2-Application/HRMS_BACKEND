
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

public class UpdateVendorEmployeeRequestDTO : IValidatableObject
{
    [Required]
    public string Ecode { get; set; }

    [Required]
    public string ContractorCode { get; set; }

    [Required]
    [StringLength(100)]
    public string FirstName { get; set; }

    [StringLength(100)]
    public string? MiddleName { get; set; }

    [StringLength(100)]
    public string? LastName { get; set; }

    [StringLength(50)]
    public string? FatherName { get; set; }

    [EmailAddress(ErrorMessage = "Invalid email format")]
    public string? Email { get; set; }

    [RegularExpression(@"^\d{10}$", ErrorMessage = "Mobile must be 10 digits")]
    public string? Mobile { get; set; }

    public int? DepartmentId { get; set; }
    public int? DesignationId { get; set; }
    public int? LocationId { get; set; }

    public DateTime? DOJ { get; set; }
    public DateTime? DOB { get; set; }

    [Required]
    [RegularExpression("Male|Female", ErrorMessage = "Gender must be Male or Female")]
    public string Gender { get; set; }

    [RegularExpression(@"^\d{12}$", ErrorMessage = "Aadhar must be 12 digits")]
    public string? AadharNo { get; set; }

    [RegularExpression(@"^[A-Z]{5}[0-9]{4}[A-Z]{1}$", ErrorMessage = "Invalid PAN format")]
    public string? PANNo { get; set; }
    public string? PermanentAddress { get; set; }
    public string? PermanentAddressPinCode { get; set; }
    public bool? IsActive { get; set; }
    public string? HusbandName { get; set; }
    public int? ShiftId { get; set; }
    public DateTime? ContractStartDate { get; set; }
    public DateTime? ContractEndDate { get; set; }

    // Salary Fields
    public decimal? BasicSalary { get; set; }
    public decimal? CCA { get; set; }
    public decimal? DA { get; set; }
    public decimal? ExtraAllowance { get; set; }
    public decimal? SpecialAllowance { get; set; }
    public decimal? HRA { get; set; }
    public decimal? GROSS_SALARY { get; set; }
    public decimal? monthlyGrossCTC { get; set; }
    public decimal? annuallyNetCTC { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // PF/UAN validation
        //if (PFApplicable == true)
        //{
        //    if (string.IsNullOrWhiteSpace(UANNo))
        //        yield return new ValidationResult("UAN is required when PF is applicable", new[] { nameof(UANNo) });
        //    else if (!Regex.IsMatch(UANNo, @"^\d{12}$"))
        //        yield return new ValidationResult("UAN must be 12 digits", new[] { nameof(UANNo) });
        //}

        // ESIC validation
        //if (ESICApplicable == true)
        //{
        //    if (string.IsNullOrWhiteSpace(ESICNo))
        //        yield return new ValidationResult("ESIC number is required when ESIC is applicable", new[] { nameof(ESICNo) });
        //    else if (!Regex.IsMatch(ESICNo, @"^\d{10}$"))
        //        yield return new ValidationResult("ESIC number must be 10 digits", new[] { nameof(ESICNo) });
        //}

        // Mobile validation
        if (!string.IsNullOrWhiteSpace(Mobile) && !Regex.IsMatch(Mobile, @"^[6-9]\d{9}$"))
        {
            yield return new ValidationResult("Mobile number must be 10 digits starting with 6-9", new[] { nameof(Mobile) });
        }

        // Email validation (already decorated, but extra check if needed)
        if (!string.IsNullOrWhiteSpace(Email))
        {
            var emailAttribute = new EmailAddressAttribute();
            if (!emailAttribute.IsValid(Email))
                yield return new ValidationResult("Invalid Email format", new[] { nameof(Email) });
        }

        // PAN validation
        if (!string.IsNullOrWhiteSpace(PANNo) && !Regex.IsMatch(PANNo, @"^[A-Z]{5}[0-9]{4}[A-Z]{1}$"))
        {
            yield return new ValidationResult("Invalid PAN format", new[] { nameof(PANNo) });
        }

        // Aadhaar validation
        if (!string.IsNullOrWhiteSpace(AadharNo) && !Regex.IsMatch(AadharNo, @"^\d{12}$"))
        {
            yield return new ValidationResult("Aadhaar must be 12 digits", new[] { nameof(AadharNo) });
        }

        // Contract date validation
        if (ContractStartDate.HasValue && ContractEndDate.HasValue && ContractEndDate < ContractStartDate)
        {
            yield return new ValidationResult(
                "Contract End Date cannot be earlier than Contract Start Date",
                new[] { nameof(ContractStartDate), nameof(ContractEndDate) });
        }
    }
}
