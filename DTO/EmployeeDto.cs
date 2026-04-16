public class DCEmployeeDto
{
    public long? EmployeeId { get; set; }
    public long? CandidateId { get; set; }
    public string? Title { get; set; }
    public string? FirstName { get; set; }
    public string? MiddleName { get; set; }
    public string? LastName { get; set; }
    public string? FullName { get; set; }
    public string? FatherName { get; set; }
    public string? MotherName { get; set; }
    public DateTime? DOB { get; set; }
    public string? Gender { get; set; }
    public string? PanNo { get; set; }
    public string? AadharNo { get; set; }
    public string? NameOnAadhar { get; set; }
    public string? PlaceOfBirth { get; set; }
    public string? PresentAddress { get; set; }
    public string? PresentAddressPinCode { get; set; }
    public string? PermanentAddress { get; set; }
    public string? MaritalStatus { get; set; }
    public string? Mobile { get; set; }
    public string? EmailAddress { get; set; }
    public string? Nationality { get; set; }
    public string? Religion { get; set; }
    public string? BankName { get; set; }
    public string? AccountNo { get; set; }
    public string? BankIfscCode { get; set; }
    public string? HighestQualification { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? CreatedOn { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedOn { get; set; }
    public string? DeletedBy { get; set; }
    public DateTime? DeletedOn { get; set; }
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
    public string? Ecode { get; set; }
    public long? DesignationId { get; set; }
    public long? DepartmentId { get; set; }
    public long? LocationId { get; set; }
    public DateTime? DOJ { get; set; }
    public DateTime? DateOfResignation { get; set; }
    public DateTime? DateOfLeft { get; set; }
    public bool IsResigned { get; set; }
    public List<EmployeeDocDto> Documents { get; set; } = new List<EmployeeDocDto>(); // Added for document metadata
}

public class EmployeeDocDto
{
    public string FilePath { get; set; } = string.Empty;
    public string DocType { get; set; } = string.Empty;
    public string FileSize { get; set; } = string.Empty;
    public string? UpdatedBy { get; set; }
    public DateTime? CreatedOn { get; set; }
}


public class ForgetPasswordDto
{
    public string ECode { get; set; }
    public DateTime? DOB { get; set; }
}

public class PasswordResetToken
{

    public long EmployeeId { get; set; }
    public string Token { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
}

public class ResetPasswordDto
{
    public string Token { get; set; }
    public string NewPassword { get; set; }
}

public class AdminResetPasswordDto
{
    public long? EmployeeId { get; set; }
    public string Ecode { get; set; }
}


