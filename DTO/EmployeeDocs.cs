public class EmployeeDocs
{
    public IFormFile? PassportPhoto { get; set; }
    public List<IFormFile>? Last3SalarySlip { get; set; }
    public IFormFile? Last3BankStatement { get; set; }
    public IFormFile? PrevOfferLetter { get; set; }
    public List<IFormFile>? PanAttachment { get; set; }
    public List<IFormFile>? AadharAttachment { get; set; }
    public List<IFormFile>? BankPassbookAttachment { get; set; }
    public List<IFormFile>? EducationAttachment { get; set; }
    public List<IFormFile>? ResumeAttachment { get; set; }
}

public class OfferLetterDto
{
    public int ApplicantId { get; set; }
    public string EmailId { get; set; }
    public string PdfFileName { get; set; }
    public string FilePath { get; set; }
    public string FullName { get; set; }
    public string DesignationName { get; set; }
    public string Title { get; set; }
}
public class DeleteCandidateDocRequest
{
    public long? Id { get; set; }
    public long? CId { get; set; }
    
    public string DeletedBy { get; set; } = default!;
}

public class DeleteCandidateDocResult
{
    public int RowsAffected { get; set; }
    public bool HardDelete { get; set; }
}