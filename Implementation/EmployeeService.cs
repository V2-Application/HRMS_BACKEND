using DocumentFormat.OpenXml.Wordprocessing;
using HRMSAPI.Data;
using HRMSAPI.DTO;
using HRMSAPI.Interfaces;
using HRMSAPI.Models.Candidate;
using HRMSAPI.Utility;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Roomsy.DTOS.GenericsResponses;
using System.Data;
using System.Diagnostics;
using System.Net;
using System.Net.Mail;
using GetEmployeeDetailsResult = HRMSAPI.DTO.GetEmployeeDetailsResult;

namespace HRMSAPI.Implementation
{
    public class EmployeeService : IEmployeeService
    {
        private readonly string savePath = Path.Combine("wwwroot");
        private readonly IConfiguration _configuration;
        private readonly HRMSContext _context;
        private readonly ILogger<EmployeeService> _logger;
        private readonly IWebHostEnvironment _env;
        private readonly IEmailService _emailService;

        public EmployeeService(HRMSContext context, IConfiguration configuration, ILogger<EmployeeService> logger, IWebHostEnvironment env, IEmailService emailService)
        {
            _configuration = configuration;
            _context = context;
            _logger = logger;
            _env = env;
            _emailService = emailService;


        }

        public async Task<(List<GetEmployeeDetailsResult> Employees, long TotalCount, int CurrentPageNumber)> EmployeeList(int pageNumber, int pageSize, string searchTerm = "")
        {
            try
            {
                using (var connection = _context.Database.GetDbConnection())
                {
                    await connection.OpenAsync();
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "GetEmployeeDetails"; // Use procedure name only
                        command.CommandType = CommandType.StoredProcedure;

                        // Input parameters
                        command.Parameters.Add(new SqlParameter("@PageNumber", SqlDbType.Int) { Value = pageNumber });
                        command.Parameters.Add(new SqlParameter("@PageSize", SqlDbType.Int) { Value = pageSize });
                        command.Parameters.Add(new SqlParameter("@SearchTerm", SqlDbType.NVarChar, 100) { Value = searchTerm ?? string.Empty });
                        command.Parameters.Add(new SqlParameter("@Email", SqlDbType.NVarChar, 150) { Value = DBNull.Value });
                        command.Parameters.Add(new SqlParameter("@DesignationName", SqlDbType.NVarChar, 100) { Value = DBNull.Value });

                        // Output parameters
                        var totalEmployeesParam = new SqlParameter("@TotalEmployees", SqlDbType.Int)
                        {
                            Direction = ParameterDirection.Output
                        };
                        var currentPageNumberParam = new SqlParameter("@CurrentPageNumber", SqlDbType.Int)
                        {
                            Direction = ParameterDirection.Output
                        };
                        command.Parameters.Add(totalEmployeesParam);
                        command.Parameters.Add(currentPageNumberParam);

                        // Execute reader to get employee list
                        var employees = new List<GetEmployeeDetailsResult>();
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var employee = new GetEmployeeDetailsResult
                                {
                                    EmployeeId = (int)reader.GetInt64(reader.GetOrdinal("EmployeeId")),  // Fix Data Type
                                    FullName = reader.IsDBNull(reader.GetOrdinal("FullName")) ? string.Empty : reader.GetString(reader.GetOrdinal("FullName")),
                                    DepartmentName = reader.IsDBNull(reader.GetOrdinal("DepartmentName")) ? string.Empty : reader.GetString(reader.GetOrdinal("DepartmentName")),
                                    DesignationName = reader.IsDBNull(reader.GetOrdinal("DesignationName")) ? string.Empty : reader.GetString(reader.GetOrdinal("DesignationName")),
                                    LocationName = reader.IsDBNull(reader.GetOrdinal("LocationName")) ? string.Empty : reader.GetString(reader.GetOrdinal("LocationName")),
                                    StoreCode = reader.IsDBNull(reader.GetOrdinal("STCode")) ? string.Empty : reader.GetString(reader.GetOrdinal("STCode")),
                                    Ecode = reader.IsDBNull(reader.GetOrdinal("Ecode")) ? string.Empty : reader.GetString(reader.GetOrdinal("Ecode")),
                                    EmailAddress = reader.IsDBNull(reader.GetOrdinal("EmailAddress")) ? string.Empty : reader.GetString(reader.GetOrdinal("EmailAddress")),
                                    ReportHeadEcode = reader.IsDBNull(reader.GetOrdinal("ReportHeadEcode")) ? string.Empty : reader.GetString(reader.GetOrdinal("ReportHeadEcode")),
                                    IsActive = reader.IsDBNull(reader.GetOrdinal("IsActive")) ? false : reader.GetBoolean(reader.GetOrdinal("IsActive")),
                                    IsDeleted = reader.IsDBNull(reader.GetOrdinal("IsDeleted")) ? false : reader.GetBoolean(reader.GetOrdinal("IsDeleted"))
                                };
                                employees.Add(employee);
                            }
                        }

                        // Retrieve Output Parameters
                        long totalCount = Convert.ToInt64(totalEmployeesParam.Value);
                        int currentPageNumber = Convert.ToInt32(currentPageNumberParam.Value);

                        return (employees, totalCount, currentPageNumber);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in EmployeeList: {ex.Message}");
                throw new ApplicationException("An error occurred while fetching employee details.", ex);
            }
        }
        public async Task<(List<GetEmployeeDetailsResult> Employees, int TotalEmployees, int CurrentPageNumber)> EmployeeSearchList(string searchTerm, string? email = null, string? designationName = null)
        {
            try
            {
                using (var connection = _context.Database.GetDbConnection())
                {
                    await connection.OpenAsync();
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "GetEmployeeDetails";
                        command.CommandType = CommandType.StoredProcedure;

                        // Add input parameters

                        command.Parameters.Add(new SqlParameter("@PageNumber", SqlDbType.Int) { Value = 0 });
                        command.Parameters.Add(new SqlParameter("@PageSize", SqlDbType.Int) { Value = 0 });
                        command.Parameters.Add(new SqlParameter("@SearchTerm", SqlDbType.NVarChar, 100) { Value = searchTerm ?? string.Empty });
                        command.Parameters.Add(new SqlParameter("@Email", SqlDbType.NVarChar, 150) { Value = string.IsNullOrWhiteSpace(email) ? DBNull.Value : email });
                        command.Parameters.Add(new SqlParameter("@DesignationName", SqlDbType.NVarChar, 100) { Value = string.IsNullOrWhiteSpace(designationName) ? DBNull.Value : designationName });

                        // Add output parameters
                        var totalEmployeesParam = new SqlParameter("@TotalEmployees", SqlDbType.Int) { Direction = ParameterDirection.Output };
                        var currentPageNumberParam = new SqlParameter("@CurrentPageNumber", SqlDbType.Int) { Direction = ParameterDirection.Output };
                        command.Parameters.Add(totalEmployeesParam);
                        command.Parameters.Add(currentPageNumberParam);

                        var employees = new List<GetEmployeeDetailsResult>();
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            // Debug: Log column names
                            Console.WriteLine("Columns returned by SqlDataReader:");
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                Console.WriteLine($"Column {i}: {reader.GetName(i)}");
                            }

                            if (!reader.HasRows)
                            {
                                Console.WriteLine("No rows returned by the stored procedure.");
                            }

                            while (await reader.ReadAsync())
                            {
                                var employee = new GetEmployeeDetailsResult
                                {
                                    EmployeeId = (int)reader.GetInt64(reader.GetOrdinal("EmployeeId")),
                                    EmailAddress = reader.IsDBNull(reader.GetOrdinal("EMAIL ADDRESS")) ? string.Empty : reader.GetString(reader.GetOrdinal("EMAIL ADDRESS")).Trim(),
                                    FullName = reader.IsDBNull(reader.GetOrdinal("FullName")) ? string.Empty : reader.GetString(reader.GetOrdinal("FullName")).Trim(),
                                    DepartmentName = reader.IsDBNull(reader.GetOrdinal("DepartmentName")) ? string.Empty : reader.GetString(reader.GetOrdinal("DepartmentName")).Trim(),
                                    DesignationName = reader.IsDBNull(reader.GetOrdinal("DesignationName")) ? string.Empty : reader.GetString(reader.GetOrdinal("DesignationName")).Trim(),
                                    LocationName = reader.IsDBNull(reader.GetOrdinal("LocationName")) ? string.Empty : reader.GetString(reader.GetOrdinal("LocationName")).Trim(),
                                    STCode = reader.IsDBNull(reader.GetOrdinal("STCode")) ? string.Empty : reader.GetString(reader.GetOrdinal("STCode")).Trim(),
                                    Ecode = reader.IsDBNull(reader.GetOrdinal("Ecode")) ? string.Empty : reader.GetString(reader.GetOrdinal("Ecode")).Trim(),
                                    ReportHeadEcode = reader.IsDBNull(reader.GetOrdinal("ReportHeadEcode")) ? string.Empty : reader.GetString(reader.GetOrdinal("ReportHeadEcode")).Trim(),
                                    dateOfJoining = reader.IsDBNull(reader.GetOrdinal("JoiningDate")) ? string.Empty : reader.GetString(reader.GetOrdinal("JoiningDate")).Trim(),
                                    ReportHeadName = reader.IsDBNull(reader.GetOrdinal("ReportHeadName")) ? string.Empty : reader.GetString(reader.GetOrdinal("ReportHeadName")).Trim(),


                                };
                                employees.Add(employee);
                            }
                        }

                        // Retrieve output parameter values
                        int totalEmployees = totalEmployeesParam.Value != DBNull.Value ? (int)totalEmployeesParam.Value : 0;
                        int currentPageNumber = currentPageNumberParam.Value != DBNull.Value ? (int)currentPageNumberParam.Value : 0;

                        return (employees, totalEmployees, currentPageNumber);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in EmployeeSearchList: {ex.Message}");
                throw new ApplicationException("An error occurred while fetching employee details.", ex);
            }
        }
        public async Task<(bool Success, string Message)> DeleteEmployeeAsync(long id, string deletedBy)
        {
            try
            {
                var emp = await _context.tblEmployees.FindAsync(id);
                if (emp == null)
                    return (false, "Employee not found");

                emp.IsActive = false;
                emp.IsDeleted = true;
                emp.DeletedBy = deletedBy;
                emp.DeletedOn = DateTime.Now;

                await _context.SaveChangesAsync();
                return (true, "Employee deleted successfully!");
            }
            catch (Exception ex)
            {
                return (false, $"Failed to delete employee: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> UpsertEmployeeAsync(DCEmployeeDto employeeDto, EmployeeDocs files)
        {
            try
            {
                tblEmployee employee = await _context.tblEmployees.FindAsync(employeeDto.EmployeeId)
                    ?? new tblEmployee { CreatedOn = DateTime.Now, CreatedBy = employeeDto.CreatedBy ?? "system", IsActive = true };

                // Map DTO to entity
                MapEmployeeDtoToEntity(employeeDto, employee);

                if (!employeeDto.EmployeeId.HasValue)
                {
                    _context.tblEmployees.Add(employee);
                }

                // Save to get EmployeeId for new employees
                await _context.SaveChangesAsync();

                // Check document uploads with null-safe operations
                var documentFlags = new
                {
                    IsPassportUploaded = files.PassportPhoto?.Length > 0,
                    IsLast3Slips = files.Last3SalarySlip?.Any(f => f.Length > 0) == true,
                    IsBankStatement = files.Last3BankStatement?.Length > 0,
                    IsPrevOfferLetter = files.PrevOfferLetter?.Length > 0,
                    IsPanAttachment = files.PanAttachment?.Any(f => f.Length > 0) == true,
                    IsAadharAttachment = files.AadharAttachment?.Any(f => f.Length > 0) == true,
                    IsBankPassbook = files.BankPassbookAttachment?.Any(f => f.Length > 0) == true,
                    IsEducationAttachment = files.EducationAttachment?.Any(f => f.Length > 0) == true,
                    IsResumeAttachment = files.ResumeAttachment?.Any(f => f.Length > 0) == true
                };

                // Update document flags
                UpdateDocumentFlags(employee, documentFlags);

                // Save attachments using the confirmed employee ID
                await SaveEmployeeAttachmentsAsync(employee.EmployeeId, employee.EMAIL_ADDRESS, files, employeeDto.UpdatedBy ?? employee.CreatedBy);

                await _context.SaveChangesAsync();

                return (true, employeeDto.EmployeeId.HasValue ? "Employee updated successfully!" : "Employee added successfully!");
            }
            catch (Exception ex)
            {
                return (false, $"Failed to upsert employee: {ex.Message}");
            }
        }


        public async Task<string> SaveFileAsync(IFormFile file, string folderName, string identifier)
        {
            var directoryPath = Path.Combine(savePath, identifier, folderName);
            Directory.CreateDirectory(directoryPath);

            var fileName = $"{DateTime.Now:yyyyMMddHHmmssffff}_{file.FileName}";
            var filePath = Path.Combine(directoryPath, fileName);
            var returnPath = Path.Combine(identifier, folderName, fileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            return returnPath;
        }

        public async Task SaveEmployeeAttachmentsAsync(long empId, string email, EmployeeDocs files, string updatedBy)
        {
            async Task SaveFileIfExists(IFormFile? file, string folder, string docType, int index = 0)
            {
                if (file?.Length > 0)
                {
                    string filePath = await SaveFileAsync(file, folder, $"{empId}_{email}");
                    int result =
                        await _context.GetProcedures().sp_InsertEmployeeDocsAsync(
                        empId, filePath, docType, file.Length.ToString(), updatedBy);

                    if (result < 1)
                        throw new Exception($"Unable to save {docType}{(index > 0 ? $" - {index}" : "")}");
                }
            }

            async Task SaveFileListIfExists(List<IFormFile>? fileList, string folder, string docType)
            {
                if (fileList?.Count > 0)
                {
                    for (int i = 0; i < fileList.Count; i++)
                    {
                        await SaveFileIfExists(fileList[i], folder, docType, i + 1);
                    }
                }
            }

            await Task.WhenAll(
                SaveFileIfExists(files.PassportPhoto, "PassportPhotos", "PassportPhoto"),
                SaveFileListIfExists(files.Last3SalarySlip, "SalarySlips", "SalarySlip"),
                SaveFileIfExists(files.Last3BankStatement, "BankStatements", "BankStatement"),
                SaveFileIfExists(files.PrevOfferLetter, "PrevOfferLetters", "PrevOfferLetter"),
                SaveFileListIfExists(files.PanAttachment, "Pan", "Pan"),
                SaveFileListIfExists(files.AadharAttachment, "Aadhar", "Aadhar"),
                SaveFileListIfExists(files.BankPassbookAttachment, "BankPassbook", "BankPassbook"),
                SaveFileListIfExists(files.EducationAttachment, "Education", "Education"),
                SaveFileListIfExists(files.ResumeAttachment, "Resume", "Resume")
            );
        }

        private void MapEmployeeDtoToEntity(DCEmployeeDto dto, tblEmployee employee)
        {
            employee.CandidateId = dto.CandidateId;
            employee.TITLE = dto.Title;
            employee.FirstName = dto.FirstName;
            employee.MiddleName = dto.MiddleName;
            employee.LastName = dto.LastName;
            employee.FULL_NAME = dto.FullName;
            employee.FATHER_S_NAME = dto.FatherName;
            employee.MOTHER_S_NAME = dto.MotherName;
            employee.DOB = dto.DOB;
            employee.GENDER = dto.Gender;
            employee.PAN_NO = dto.PanNo;
            employee.AADHAR_NO = dto.AadharNo;
            employee.NAME_ON_ADHAR = dto.NameOnAadhar;
            employee.PLACE_OF_BIRTH = dto.PlaceOfBirth;
            employee.PRESENT_ADDRESS = dto.PresentAddress;
            employee.PRESENT_ADDRESS_PIN_CODE = dto.PresentAddressPinCode;
            employee.PERMANENT_ADDRESS = dto.PermanentAddress;
            employee.MARITIAL_STATUS = dto.MaritalStatus;
            employee.MOBILE = dto.Mobile;
            employee.EMAIL_ADDRESS = dto.EmailAddress;
            employee.NATIONALITY = dto.Nationality;
            employee.RELIGION = dto.Religion;
            employee.BANK_NAME = dto.BankName;
            employee.A_C_NO = dto.AccountNo;
            employee.BANK_IFSC_CODE = dto.BankIfscCode;
            employee.HIGHEST_QUALIFICATION = dto.HighestQualification;
            employee.Ecode = dto.Ecode;
            employee.DesignationId = (int?)dto.DesignationId;
            employee.DepartmentId = (int?)dto.DepartmentId;
            employee.LocationId = (int?)dto.LocationId;
            employee.DOJ = dto.DOJ;
            employee.DateOfResignation = dto.DateOfResignation;
            employee.DateOfLeft = dto.DateOfLeft;
            employee.isresgined = dto.IsResigned;
            employee.IsActive = dto.IsActive;
            employee.IsDeleted = dto.IsDeleted;

            if (dto.EmployeeId.HasValue)
            {
                employee.UpdatedBy = dto.UpdatedBy ?? "system";
                employee.UpdatedOn = DateTime.Now;
            }
        }

        private void UpdateDocumentFlags(tblEmployee employee, dynamic flags)
        {
            // Assuming tblEmployee has these properties; adjust if they're in a different entity
            employee.IsPassportPhotoUploaded = flags.IsPassportUploaded ? true : employee.IsPassportPhotoUploaded;
            employee.IsSalarySlipUploaded = flags.IsLast3Slips ? true : employee.IsSalarySlipUploaded;
            employee.IsBankStatementUploaded = flags.IsBankStatement ? true : employee.IsBankStatementUploaded;
            employee.IsPrevOfferLetterUploaded = flags.IsPrevOfferLetter ? true : employee.IsPrevOfferLetterUploaded;
            employee.IsPanAttachmentUploaded = flags.IsPanAttachment ? true : employee.IsPanAttachmentUploaded;
            employee.IsAadharAttachmentUploaded = flags.IsAadharAttachment ? true : employee.IsAadharAttachmentUploaded;
            employee.IsBankPassbookAttachmentUpoaded = flags.IsBankPassbook ? true : employee.IsBankPassbookAttachmentUpoaded;
            employee.IsEducationAttachmentUploaded = flags.IsEducationAttachment ? true : employee.IsEducationAttachmentUploaded;

        }

        public async Task<(bool Success, DCEmployeeDto? Employee, string Message)> GetEmployeeByIdAsync(long employeeId)
        {
            try
            {
                var employee = await _context.tblEmployees
                    .FirstOrDefaultAsync(e => e.EmployeeId == employeeId);

                if (employee == null)
                {
                    return (false, null, "Employee not found or has been deleted.");
                }

                var documents = await _context.EmployeeDocs
                    .Where(d => d.EmployeeId == employeeId)
                    .Select(d => new EmployeeDocDto
                    {
                        FilePath = d.FilePath ?? string.Empty,
                        DocType = d.FileType ?? string.Empty,
                        FileSize = d.FileSize ?? string.Empty,
                        UpdatedBy = d.UpdatedBy ?? string.Empty,
                        CreatedOn = d.CreatedOn ?? DateTime.MinValue
                    })
                    .ToListAsync();

                var employeeDto = new DCEmployeeDto
                {
                    EmployeeId = employee.EmployeeId,
                    CandidateId = employee.CandidateId ?? 0,
                    Title = employee.TITLE ?? string.Empty,
                    FirstName = employee.FirstName ?? string.Empty,
                    MiddleName = employee.MiddleName ?? string.Empty,
                    LastName = employee.LastName ?? string.Empty,
                    FullName = employee.FULL_NAME ?? string.Empty,
                    FatherName = employee.FATHER_S_NAME ?? string.Empty,
                    MotherName = employee.MOTHER_S_NAME ?? string.Empty,
                    DOB = employee.DOB ?? DateTime.MinValue,
                    Gender = employee.GENDER ?? string.Empty,
                    PanNo = employee.PAN_NO ?? string.Empty,
                    AadharNo = employee.AADHAR_NO ?? string.Empty,
                    NameOnAadhar = employee.NAME_ON_ADHAR ?? string.Empty,
                    PlaceOfBirth = employee.PLACE_OF_BIRTH ?? string.Empty,
                    PresentAddress = employee.PRESENT_ADDRESS ?? string.Empty,
                    PresentAddressPinCode = employee.PRESENT_ADDRESS_PIN_CODE ?? string.Empty,
                    PermanentAddress = employee.PERMANENT_ADDRESS ?? string.Empty,
                    MaritalStatus = employee.MARITIAL_STATUS ?? string.Empty,
                    Mobile = employee.MOBILE ?? string.Empty,
                    EmailAddress = employee.EMAIL_ADDRESS ?? string.Empty,
                    Nationality = employee.NATIONALITY ?? string.Empty,
                    Religion = employee.RELIGION ?? string.Empty,
                    BankName = employee.BANK_NAME ?? string.Empty,
                    AccountNo = employee.A_C_NO ?? string.Empty,
                    BankIfscCode = employee.BANK_IFSC_CODE ?? string.Empty,
                    HighestQualification = employee.HIGHEST_QUALIFICATION ?? string.Empty,
                    CreatedBy = employee.CreatedBy ?? string.Empty,
                    CreatedOn = employee.CreatedOn ?? DateTime.MinValue,
                    UpdatedBy = employee.UpdatedBy ?? string.Empty,
                    UpdatedOn = employee.UpdatedOn ?? DateTime.MinValue,
                    DeletedBy = employee.DeletedBy ?? string.Empty,
                    DeletedOn = employee.DeletedOn ?? DateTime.MinValue,
                    IsActive = employee.IsActive ?? false,
                    IsDeleted = employee.IsDeleted ?? false,
                    Ecode = employee.Ecode ?? string.Empty,
                    DesignationId = employee.DesignationId ?? 0,
                    DepartmentId = employee.DepartmentId ?? 0,
                    LocationId = employee.LocationId ?? 0,
                    DOJ = employee.DOJ ?? DateTime.MinValue,
                    DateOfResignation = employee.DateOfResignation ?? DateTime.MinValue,
                    DateOfLeft = employee.DateOfLeft ?? DateTime.MinValue,
                    IsResigned = employee.isresgined ?? false,
                    Documents = documents
                };

                return (true, employeeDto, "Employee and documents retrieved successfully!");
            }
            catch (Exception ex)
            {
                return (false, null, $"Failed to retrieve employee: {ex.Message}");
            }
        }
        #region EMPLOYEE UPSERT

        #endregion




        #region facial recognition
        public async Task<tblEmployee> GetEmployeeByEcodeAsync(string ecode)
        {
            return await _context.tblEmployees
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Ecode == ecode && e.IsActive == true && e.IsDeleted == false);
        }

        public async Task<List<tblEmployee>> GetActiveEmployeesWithFaceDataAsync()
        {
            return await _context.tblEmployees
                .AsNoTracking()
                .Where(e => e.IsActive == true && e.IsDeleted == false && e.FaceData != null)
                .ToListAsync();
        }

        public async Task UpdateEmployeeAsync(tblEmployee employee)
        {
            _context.tblEmployees.Update(employee);
            await _context.SaveChangesAsync();
        }
        #endregion


        //public async Task<EmployeeSalarySlip?> GetSalaryDetailsByEcode(string ecode, string month)
        //{
        //    try
        //    {
        //        await using var conn = _context.Database.GetDbConnection();
        //        await conn.OpenAsync();

        //        await using var cmd = conn.CreateCommand();
        //        cmd.CommandText = "dbo.sp_GetEmployeeSalarySlipDetails";
        //        cmd.CommandType = CommandType.StoredProcedure;

        //        var ecodeParam = cmd.CreateParameter();
        //        ecodeParam.ParameterName = "@Ecode";
        //        ecodeParam.Value = ecode;
        //        ecodeParam.DbType = DbType.String;
        //        cmd.Parameters.Add(ecodeParam);

        //        var monthParam = cmd.CreateParameter();
        //        monthParam.ParameterName = "@Month";
        //        monthParam.Value = month;
        //        monthParam.DbType = DbType.String;
        //        cmd.Parameters.Add(monthParam);

        //        using var reader = await cmd.ExecuteReaderAsync();

        //        if (await reader.ReadAsync())
        //        {
        //            return new EmployeeSalarySlip
        //            {
        //                ECode = reader.GetString(reader.GetOrdinal("ECode")),
        //                EmployeeId = reader.GetInt64(reader.GetOrdinal("EmployeeId")),
        //                EmployeeName = reader.GetString(reader.GetOrdinal("EmployeeName")),
        //                Designation = reader.GetString(reader.GetOrdinal("Designation")),
        //                DateofJoining = reader.IsDBNull(reader.GetOrdinal("DateofJoining")) ? null : reader.GetDateTime(reader.GetOrdinal("DateofJoining")),
        //                LocationName = reader.GetString(reader.GetOrdinal("LocationName")),
        //                Department = reader.GetString(reader.GetOrdinal("Department")),
        //                BankAccountNo = reader.GetString(reader.GetOrdinal("BankAccountNo")),
        //                PAN_NO = reader.GetString(reader.GetOrdinal("PAN_NO")),
        //                BankName = reader.GetString(reader.GetOrdinal("BankName")),
        //                NoofDays = reader.GetString(reader.GetOrdinal("NoofDays")),
        //                IfscCode = reader.GetString(reader.GetOrdinal("IfscCode")),
        //                UniversalAccountNumber = reader.GetString(reader.GetOrdinal("UniversalAccountNumber")),
        //                EsicNo = reader.GetString(reader.GetOrdinal("EsicNo")),
        //                BasicSalary = reader.IsDBNull(reader.GetOrdinal("BasicSalary")) ? null : reader.GetDecimal(reader.GetOrdinal("BasicSalary")),
        //                CCA = reader.IsDBNull(reader.GetOrdinal("CCA")) ? null : reader.GetDecimal(reader.GetOrdinal("CCA")),
        //                DA = reader.IsDBNull(reader.GetOrdinal("DA")) ? null : reader.GetDecimal(reader.GetOrdinal("DA")),
        //                HRA = reader.IsDBNull(reader.GetOrdinal("HRA")) ? null : reader.GetDecimal(reader.GetOrdinal("HRA")),
        //                Incentive = reader.GetString(reader.GetOrdinal("Incentive")),
        //                SpecialAllowance = reader.IsDBNull(reader.GetOrdinal("SpecialAllowance")) ? null : reader.GetDecimal(reader.GetOrdinal("SpecialAllowance")),
        //                ExtraAllowance = reader.IsDBNull(reader.GetOrdinal("ExtraAllowance")) ? null : reader.GetDecimal(reader.GetOrdinal("ExtraAllowance")),
        //                EPF = reader.IsDBNull(reader.GetOrdinal("EPF")) ? null : reader.GetDecimal(reader.GetOrdinal("EPF")),
        //                ESIC = reader.IsDBNull(reader.GetOrdinal("ESIC")) ? null : reader.GetDecimal(reader.GetOrdinal("ESIC")),
        //                TDS = reader.GetString(reader.GetOrdinal("TDS")),
        //                PTax = reader.GetString(reader.GetOrdinal("PTax")),
        //                Loan = reader.GetString(reader.GetOrdinal("Loan")),
        //                CashShort = reader.GetString(reader.GetOrdinal("CashShort")),
        //                DieselDeduction = reader.GetString(reader.GetOrdinal("DieselDeduction")),
        //                Penality = reader.GetString(reader.GetOrdinal("Penality")),
        //                Lwf = reader.GetString(reader.GetOrdinal("Lwf")),
        //                Fuel_and_Maintenance = reader.IsDBNull(reader.GetOrdinal("Fuel_and_Maintenance")) ? null : reader.GetDecimal(reader.GetOrdinal("Fuel_and_Maintenance")),
        //                Books_and_Periodicals = reader.IsDBNull(reader.GetOrdinal("Books_and_Periodicals")) ? null : reader.GetDecimal(reader.GetOrdinal("Books_and_Periodicals")),
        //                Professional_Attire = reader.IsDBNull(reader.GetOrdinal("Professional_Attire")) ? null : reader.GetDecimal(reader.GetOrdinal("Professional_Attire")),
        //                Driver_Wages = reader.IsDBNull(reader.GetOrdinal("Driver_Wages")) ? null : reader.GetDecimal(reader.GetOrdinal("Driver_Wages")),
        //                Mobile_Bill = reader.IsDBNull(reader.GetOrdinal("Mobile_Bill")) ? null : reader.GetDecimal(reader.GetOrdinal("Mobile_Bill")),
        //                Meal_Voucher = reader.IsDBNull(reader.GetOrdinal("Meal_Voucher")) ? null : reader.GetDecimal(reader.GetOrdinal("Meal_Voucher")),
        //                GrossEarnings = reader.IsDBNull(reader.GetOrdinal("GrossEarnings")) ? null : reader.GetDecimal(reader.GetOrdinal("GrossEarnings")),
        //                GrossDeduction = reader.IsDBNull(reader.GetOrdinal("GrossDeduction")) ? null : reader.GetDecimal(reader.GetOrdinal("GrossDeduction")),
        //                FinalGrossEarnings_Netpay = reader.IsDBNull(reader.GetOrdinal("FinalGrossEarnings_Netpay")) ? null : reader.GetDecimal(reader.GetOrdinal("FinalGrossEarnings_Netpay")),
        //                FinalGrossEarnings_Netpay_rem = reader.IsDBNull(reader.GetOrdinal("FinalGrossEarnings_Netpay_rem")) ? null : reader.GetDecimal(reader.GetOrdinal("FinalGrossEarnings_Netpay_rem")),
        //                EC_PF = reader.IsDBNull(reader.GetOrdinal("EC_PF")) ? null : reader.GetDecimal(reader.GetOrdinal("EC_PF")),
        //                EC_EPS = reader.IsDBNull(reader.GetOrdinal("EC_EPS")) ? null : reader.GetDecimal(reader.GetOrdinal("EC_EPS")),
        //                ERC_PF = reader.IsDBNull(reader.GetOrdinal("ERC_PF")) ? null : reader.GetDecimal(reader.GetOrdinal("ERC_PF")),
        //                E_VPF = reader.GetString(reader.GetOrdinal("E_VPF")),
        //                Payble_Days = reader.IsDBNull(reader.GetOrdinal("Payble_Days")) ? null : reader.GetDecimal(reader.GetOrdinal("Payble_Days")),
        //                EarnedLeaveBalance = reader.IsDBNull(reader.GetOrdinal("EarnedLeaveBalance")) ? null : reader.GetDecimal(reader.GetOrdinal("EarnedLeaveBalance")),
        //                EarnedLeaveUsed = reader.IsDBNull(reader.GetOrdinal("EarnedLeaveUsed")) ? null : reader.GetDecimal(reader.GetOrdinal("EarnedLeaveUsed")),
        //                CasualLeaveBalance = reader.IsDBNull(reader.GetOrdinal("CasualLeaveBalance")) ? null : reader.GetDecimal(reader.GetOrdinal("CasualLeaveBalance")),
        //                CasualLeaveUsed = reader.IsDBNull(reader.GetOrdinal("CasualLeaveUsed")) ? null : reader.GetDecimal(reader.GetOrdinal("CasualLeaveUsed")),
        //                CompoOffBalance = reader.IsDBNull(reader.GetOrdinal("CompoOffBalance")) ? null : reader.GetDecimal(reader.GetOrdinal("CompoOffBalance")),
        //                CompoOffUsed = reader.IsDBNull(reader.GetOrdinal("CompoOffUsed")) ? null : reader.GetDecimal(reader.GetOrdinal("CompoOffUsed")),

        //                MONTH = reader.GetString(reader.GetOrdinal("MONTH")),
        //                PF_Number = reader.GetString(reader.GetOrdinal("PF_Number")),


        //            };
        //        }

        //        return null;
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"[GetEmployeeSalarySlipAsync] Error: {ex.Message}");
        //        throw;
        //    }
        //}
        public async Task<EmployeeSalarySlip?> GetSalaryDetailsByEcode(string ecode, string month)
        {
            try
            {
                await using var conn = _context.Database.GetDbConnection();
                await conn.OpenAsync();

                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "dbo.sp_GetEmployeeSalarySlipDetailsnew";
                cmd.CommandType = CommandType.StoredProcedure;

                var ecodeParam = cmd.CreateParameter();
                ecodeParam.ParameterName = "@Ecode";
                ecodeParam.Value = ecode;
                ecodeParam.DbType = DbType.String;
                cmd.Parameters.Add(ecodeParam);

                var monthParam = cmd.CreateParameter();
                monthParam.ParameterName = "@Month";
                monthParam.Value = month;
                monthParam.DbType = DbType.String;
                cmd.Parameters.Add(monthParam);

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return new EmployeeSalarySlip
                    {
                        // Identity / Org
                        ECode = reader.GetStr("ECode", "E.CODE", "E Code"),
                        EmployeeId = reader.GetInt64("EmployeeId"),
                        EmployeeName = reader.GetStr("EmployeeName", "NAME", "FULL NAME"),
                        Designation = reader.GetStr("Designation", "DESIGNATION"),
                        DateofJoining = reader.GetDt("DateofJoining", "JOINING DATE", "DOJ"),
                        LocationName = reader.GetStr("LocationName", "LOCATION", "Location Name"),
                        Department = reader.GetStr("Department", "DEPARTMENT"),

                        // Banking / IDs
                        BankAccountNo = reader.GetStr("BankAccountNo", "A/C NO", "AC NO"),
                        PAN_NO = reader.GetStr("PAN_NO", "PAN NO", "PAN NO."),
                        BankName = reader.GetStr("BankName", "BANK NAME"),
                        NoofDays = reader.GetStr("NoofDays", "PAYABLE DAYS", "Payble_Days", "PAYABLE DAYS."),
                        IfscCode = reader.GetStr("IfscCode", "BANK IFSC CODE", "IFSC"),
                        UniversalAccountNumber = reader.GetStr("UniversalAccountNumber", "UAN NO"),
                        EsicNo = reader.GetStr("EsicNo", "ESICNO", "ESIC NO"),

                        // Earnings (decimals) - Actual values
                        BasicSalary = reader.GetDec("BasicSalary", "BASIC SALARY", "BasicSalary(Actual)"),
                        CCA = reader.GetDec("CCA", "C.C.A.", "CCA(Actual)"),
                        DA = reader.GetDec("DA", "D.A", "DA(Actual)"),
                        HRA = reader.GetDec("HRA", "H.R.A.", "HRA(Actual)"),

                        // Budget values
                        BasicSalaryBud = reader.GetDec("BasicSalaryBud", "BasicSalary(Bud.)"),
                        CCABud = reader.GetDec("CCABud", "CCA(Bud.)"),
                        DABud = reader.GetDec("DABud", "DA(Bud.)"),
                        HRABud = reader.GetDec("HRABud", "HRA(Bud.)"),

                        // String heads in your DTO
                        Incentive = reader.GetStr("Incentive", "INCENTIVE AMT", "Incentive"),

                        // More earnings (decimals)
                        SpecialAllowance = reader.GetDec("SpecialAllowance", "SPECIAL ALLOWANCE", "SpecialAllowance(Actual)"),
                        SpecialAllowanceBud = reader.GetDec("SpecialAllowanceBud", "SpecialAllowance(Bud.)"),
                        ExtraAllowance = reader.GetDec("ExtraAllowance", "EXTRA DAYS ALLOWANCE", "ExtraDayAllowance"),

                        // New calculated fields
                        GrossEarningsCTCRef = reader.GetDec("GrossEarningsCTCRef"),
                        GrossEarningsAmount = reader.GetDec("GrossEarningsAmount"),
                        GrossNetPay = reader.GetDec("GrossNetPay"),

                        // Employee deductions (decimals)
                        EPF = reader.GetDec("EPF", "PF", "PF(Employee)"),
                        ESIC = reader.GetDec("ESIC", "ESI", "ESIC(Employee)"),

                        // Deductions (string in your DTO)
                        TDS = reader.GetStr("TDS"),
                        PTax = reader.GetStr("PTax", "P-TAX", "PTax"),
                        Loan = reader.GetStr("Loan"),
                        CashShort = reader.GetStr("CashShort", "CASH SHORT"),
                        DieselDeduction = reader.GetStr("DieselDeduction", "DIESEL"),
                        Penality = reader.GetStr("Penality", "PENALTY"),
                        Lwf = reader.GetStr("Lwf"),

                        // Reimb (decimals)
                        Fuel_and_Maintenance = reader.GetDec("Fuel_and_Maintenance", "Fuel and Maintenance (REIMB)"),
                        Books_and_Periodicals = reader.GetDec("Books_and_Periodicals", "Books and Periodicals (REIMB)"),
                        Professional_Attire = reader.GetDec("Professional_Attire", "Professional Attire (REIMB)"),
                        Driver_Wages = reader.GetDec("Driver_Wages", "Driver Wages (REIMB)"),
                        Mobile_Bill = reader.GetDec("Mobile_Bill", "Mobile Bill (REIMB)"),
                        Meal_Voucher = reader.GetDec("Meal_Voucher", "Meal Voucher (REIMB)"),

                        // Totals (decimals)
                        GrossEarnings = reader.GetDec("GrossEarnings", "GROSS EARNING", "Monthly Gross CTC(Actual)"),
                        GrossDeduction = reader.GetDec("GrossDeduction", "TOTAL DEDUCTION", "TotalDeductions"),
                        FinalGrossEarnings_Netpay =
                                                 reader.GetDec("FinalGrossEarnings_Netpay",
                                                               "FINAL Gross EARNING (Actual After Deduction AND AddONS)",
                                                               "Monthly Gross CTC(Actual After Deduction AND AddONS)"),
                        FinalGrossEarnings_Netpay_rem = reader.GetDec("FinalGrossEarnings_Netpay_rem"),

                        // Employer contrib (decimals)
                        EC_PF = reader.GetDec("EC_PF", "PF (EMPLOYER)", "PF(Employeer)"),
                        EC_EPS = reader.GetDec("EC_EPS"),
                        ERC_PF = reader.GetDec("ERC_PF", "ESIC (EMPLOYER)", "ESIC(Employeer)"),

                        // Misc strings
                        E_VPF = reader.GetStr("E_VPF"),

                        // Leave / days (decimals)
                        Payble_Days = reader.GetDec("Payble_Days", "PAYABLE DAYS", "PAYABLE DAYS."),
                        EarnedLeaveBalance = reader.GetDec("EarnedLeaveBalance", "EL LEAVE CLS_BAL", "EarnedLeaveBalance"),
                        EarnedLeaveUsed = reader.GetDec("EarnedLeaveUsed", "EL LEAVE AVAILED", "EL LEAVE AVAILED.", "EarnedLeaveUsed"),
                        CasualLeaveBalance = reader.GetDec("CasualLeaveBalance", "CL LEAVE CLS_BAL", "CasualLeaveBalance"),
                        CasualLeaveUsed = reader.GetDec("CasualLeaveUsed", "CL LEAVE AVAILED", "CL LEAVE AVAILED.", "CasualLeaveUsed"),
                        CompoOffBalance = reader.GetDec("CompoOffBalance", "CO LEAVE CLS_BAL", "CompoOffBalance"),
                        CompoOffUsed = reader.GetDec("CompoOffUsed", "CO LEAVE AVAILED", "CO LEAVE AVAILED.", "CompoOffUsed"),

                        // Month
                        MONTH = reader.GetStr("MONTH", "Month"),
                        PF_Number = reader.GetStr("PF_Number", "P.F. No.")
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetEmployeeSalarySlipAsync] Error: {ex.Message}");
                throw;
            }
        }



        public async Task<List<EmployeeSalarySlipDto>> GetAllSalarySlipsDetails(string month, int pageNumber, int pageSize, string? searchTerm = "")
        {
            var result = new List<EmployeeSalarySlipDto>();


            try
            {
                await using var conn = _context.Database.GetDbConnection();
                await conn.OpenAsync();

                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "dbo.sp_GetAllEmployeeSalarySlipDetails";
                cmd.CommandType = CommandType.StoredProcedure;

                var monthParam = cmd.CreateParameter();
                monthParam.ParameterName = "@Month";
                monthParam.Value = month;
                cmd.Parameters.Add(monthParam);

                var searchParam = cmd.CreateParameter();
                searchParam.ParameterName = "@SearchTerm";
                searchParam.Value = (object?)searchTerm ?? DBNull.Value;
                cmd.Parameters.Add(searchParam);

                var pageParam = cmd.CreateParameter();
                pageParam.ParameterName = "@PageNumber";
                pageParam.Value = pageNumber;
                cmd.Parameters.Add(pageParam);

                var sizeParam = cmd.CreateParameter();
                sizeParam.ParameterName = "@PageSize";
                sizeParam.Value = pageSize;
                cmd.Parameters.Add(sizeParam);

                using var reader = await cmd.ExecuteReaderAsync();
                while (reader.Read())
                {
                    var dto = new EmployeeSalarySlipDto
                    {
                        Ecode = reader["Ecode"].ToString(),
                        Location_Code = reader["Location_Code"].ToString(),
                        LocationName = reader["LocationName"].ToString(),
                        EmployeeName = reader["EmployeeName"].ToString(),
                        Designation = reader["Designation"].ToString(),
                        Department = reader["Department"].ToString(),
                        MonthYear = reader["MonthYear"].ToString(),
                        TtlBgtDays = reader.GetDecimal(reader.GetOrdinal("TtlBgtDays")),
                        ActualTtlDays = reader.GetDecimal(reader.GetOrdinal("ActualTtlDays")),
                        ActualWeekly = reader.GetDecimal(reader.GetOrdinal("ActualWeekly")),
                        PresentWeeklyOff = reader.GetDecimal(reader.GetOrdinal("PresentWeeklyOff")),
                        PaybleDays = reader.GetDecimal(reader.GetOrdinal("PaybleDays")),
                        ExtraDays = reader.GetDecimal(reader.GetOrdinal("ExtraDays")),
                        Absent = reader.GetDecimal(reader.GetOrdinal("Absent")),
                        BasicSalaryBud = reader.GetDecimal(reader.GetOrdinal("BasicSalaryBud")),
                        HRABud = reader.GetDecimal(reader.GetOrdinal("HRABud")),
                        CCABud = reader.GetDecimal(reader.GetOrdinal("CCABud")),
                        SpecialAllowanceBud = reader.GetDecimal(reader.GetOrdinal("SpecialAllowanceBud")),
                        DABud = reader.GetDecimal(reader.GetOrdinal("DABud")),
                        ReimbersmentBud = reader.GetDecimal(reader.GetOrdinal("ReimbersmentBud")),
                        FuelAndMaintenanceBud = reader.GetDecimal(reader.GetOrdinal("FuelAndMaintenanceBud")),
                        BooksAndPeriodicalsBud = reader.GetDecimal(reader.GetOrdinal("BooksAndPeriodicalsBud")),
                        ProfessionalAttireBud = reader.GetDecimal(reader.GetOrdinal("ProfessionalAttireBud")),
                        DriverWagesBud = reader.GetDecimal(reader.GetOrdinal("DriverWagesBud")),
                        MobileBillBud = reader.GetDecimal(reader.GetOrdinal("MobileBillBud")),
                        MealVoucherBud = reader.GetDecimal(reader.GetOrdinal("MealVoucherBud")),
                        MonthlyGrossCTCBud = reader.GetDecimal(reader.GetOrdinal("MonthlyGrossCTCBud")),
                        BasicSalaryActual = reader.GetDecimal(reader.GetOrdinal("BasicSalaryActual")),
                        HRAActual = reader.GetDecimal(reader.GetOrdinal("HRAActual")),
                        CCAActual = reader.GetDecimal(reader.GetOrdinal("CCAActual")),
                        SpecialAllowanceActual = reader.GetDecimal(reader.GetOrdinal("SpecialAllowanceActual")),
                        DAActual = reader.GetDecimal(reader.GetOrdinal("DAActual")),
                        ExtraDayAllowance = reader["ExtraDayAllowance"].ToString(),
                        ReimbersmentActual = reader.GetDecimal(reader.GetOrdinal("ReimbersmentActual")),
                        FuelAndMaintenanceActual = reader.GetDecimal(reader.GetOrdinal("FuelAndMaintenanceActual")),
                        BooksAndPeriodicalsActual = reader.GetDecimal(reader.GetOrdinal("BooksAndPeriodicalsActual")),
                        ProfessionalAttireActual = reader.GetDecimal(reader.GetOrdinal("ProfessionalAttireActual")),
                        DriverWagesActual = reader.GetDecimal(reader.GetOrdinal("DriverWagesActual")),
                        MobileBillActual = reader.GetDecimal(reader.GetOrdinal("MobileBillActual")),
                        MealVoucherActual = reader.GetDecimal(reader.GetOrdinal("MealVoucherActual")),
                        PFEmployee = reader.GetDecimal(reader.GetOrdinal("PFEmployee")),
                        PFEmployeer = reader.GetDecimal(reader.GetOrdinal("PFEmployeer")),
                        PFTotal = reader["PFTotal"].ToString(),
                        ESICEmployee = reader.GetDecimal(reader.GetOrdinal("ESICEmployee")),
                        ESICEmployeer = reader.GetDecimal(reader.GetOrdinal("ESICEmployeer")),
                        ESICTotal = reader["ESICTotal"].ToString(),
                        TDS = reader["TDS"].ToString(),
                        PTax = reader["PTax"].ToString(),
                        Loan = reader["Loan"].ToString(),
                        CashShort = reader["CashShort"].ToString(),
                        DieselDeduction = reader["DieselDeduction"].ToString(),
                        Penality = reader["Penality"].ToString(),
                        Lwf = reader["Lwf"].ToString(),
                        MonthlyGrossCTCActual = reader.GetDecimal(reader.GetOrdinal("MonthlyGrossCTCActual")),
                        MonthlyGrossCTCActualAfterDeduction = reader.GetDecimal(reader.GetOrdinal("MonthlyGrossCTCActualAfterDeduction")),
                        Payble_Days = reader.GetDecimal(reader.GetOrdinal("Payble_Days")),
                        LeaveUsed = reader.GetDecimal(reader.GetOrdinal("LeaveUsed")),
                        OpeningEL = reader.GetDecimal(reader.GetOrdinal("OpeningEL")),
                        EarnedLeaveAcquired = reader.GetDecimal(reader.GetOrdinal("EarnedLeaveAcquired")),
                        EarnedLeaveUsed = reader.GetDecimal(reader.GetOrdinal("EarnedLeaveUsed")),
                        EarnedLeaveBalance = reader.GetDecimal(reader.GetOrdinal("EarnedLeaveBalance")),
                        OpeningCL = reader.GetDecimal(reader.GetOrdinal("OpeningCL")),
                        CasualLeaveAcquired = reader.GetDecimal(reader.GetOrdinal("CasualLeaveAcquired")),
                        CasualLeaveUsed = reader.GetDecimal(reader.GetOrdinal("CasualLeaveUsed")),
                        CasualLeaveBalance = reader.GetDecimal(reader.GetOrdinal("CasualLeaveBalance")),
                        OpeningCompoOff = reader.GetDecimal(reader.GetOrdinal("OpeningCompoOff")),
                        CompoOffAcquired = reader.GetDecimal(reader.GetOrdinal("CompoOffAcquired")),
                        CompoOffUsed = reader.GetDecimal(reader.GetOrdinal("CompoOffUsed")),
                        CompoOffBalance = reader.GetDecimal(reader.GetOrdinal("CompoOffBalance")),
                    };

                    result.Add(dto);
                }

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                throw;
            }
        }

        public async Task<(List<GetEmployeeDetailsResultNew_Hold> EmployeesHold, long TotalCount, int CurrentPageNumber)> GetEmployee_HoldList(int pageNumber, int pageSize, string searchTerm = "")
        {
            try
            {
                using (var connection = _context.Database.GetDbConnection())
                {
                    await connection.OpenAsync();
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "sp_GetEmployeeDetails_HoldList"; // Use procedure name only
                        command.CommandType = CommandType.StoredProcedure;

                        // Input parameters
                        command.Parameters.Add(new SqlParameter("@PageNumber", SqlDbType.Int) { Value = pageNumber });
                        command.Parameters.Add(new SqlParameter("@PageSize", SqlDbType.Int) { Value = pageSize });
                        command.Parameters.Add(new SqlParameter("@SearchTerm", SqlDbType.NVarChar, 100) { Value = searchTerm ?? string.Empty });

                        // Output parameters
                        var totalEmployeesParam = new SqlParameter("@TotalEmployees", SqlDbType.Int)
                        {
                            Direction = ParameterDirection.Output
                        };
                        var currentPageNumberParam = new SqlParameter("@CurrentPageNumber", SqlDbType.Int)
                        {
                            Direction = ParameterDirection.Output
                        };
                        command.Parameters.Add(totalEmployeesParam);
                        command.Parameters.Add(currentPageNumberParam);

                        // Execute reader to get employee list
                        var employeesHold = new List<GetEmployeeDetailsResultNew_Hold>();
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var employee = new GetEmployeeDetailsResultNew_Hold
                                {
                                    EmployeeId = (int)reader.GetInt64(reader.GetOrdinal("EmployeeId")),
                                    CandidateId = reader.IsDBNull(reader.GetOrdinal("CandidateId")) ? 0 : (int)reader.GetInt64(reader.GetOrdinal("CandidateId")),
                                    FullName = reader.IsDBNull(reader.GetOrdinal("FullName")) ? string.Empty : reader.GetString(reader.GetOrdinal("FullName")),
                                    DepartmentName = reader.IsDBNull(reader.GetOrdinal("DepartmentName")) ? string.Empty : reader.GetString(reader.GetOrdinal("DepartmentName")),
                                    DesignationName = reader.IsDBNull(reader.GetOrdinal("DesignationName")) ? string.Empty : reader.GetString(reader.GetOrdinal("DesignationName")),
                                    LocationName = reader.IsDBNull(reader.GetOrdinal("LocationName")) ? string.Empty : reader.GetString(reader.GetOrdinal("LocationName")),
                                    StoreCode = reader.IsDBNull(reader.GetOrdinal("STCode")) ? string.Empty : reader.GetString(reader.GetOrdinal("STCode")),
                                    //Ecode = reader.IsDBNull(reader.GetOrdinal("Ecode")) ? string.Empty : reader.GetString(reader.GetOrdinal("Ecode")),
                                    Ecode = reader.IsDBNull(reader.GetOrdinal("Ecode")) ? string.Empty : reader.GetString(reader.GetOrdinal("Ecode")),
                                    ReportHeadEcode = reader.IsDBNull(reader.GetOrdinal("ReportHeadEcode")) ? string.Empty : reader.GetString(reader.GetOrdinal("ReportHeadEcode")),
                                    IsActive = reader.IsDBNull(reader.GetOrdinal("IsActive")) ? false : reader.GetBoolean(reader.GetOrdinal("IsActive")),
                                    IsDeleted = reader.IsDBNull(reader.GetOrdinal("IsDeleted")) ? false : reader.GetBoolean(reader.GetOrdinal("IsDeleted")),
                                    IsResigned = reader.IsDBNull(reader.GetOrdinal("isResgined")) ? false : reader.GetBoolean(reader.GetOrdinal("isResgined")),
                                    DateOfLeft = reader.IsDBNull(reader.GetOrdinal("DateOfLeft")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("DateOfLeft")),
                                    DateOfResignation = reader.IsDBNull(reader.GetOrdinal("DateOfResignation")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("DateOfResignation")),
                                    Payble_Days = reader.IsDBNull(reader.GetOrdinal("Payble_Days")) ? 0 : reader.GetDecimal(reader.GetOrdinal("Payble_Days")),
                                    Final_Amount = reader.IsDBNull(reader.GetOrdinal("Final_Amount")) ? 0 : reader.GetDecimal(reader.GetOrdinal("Final_Amount")),

                                };
                                employeesHold.Add(employee);
                            }
                        }

                        // Retrieve Output Parameters
                        long totalCount = Convert.ToInt64(totalEmployeesParam.Value);
                        int currentPageNumber = Convert.ToInt32(currentPageNumberParam.Value);

                        return (employeesHold, totalCount, currentPageNumber);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in EmployeeList: {ex.Message}");
                throw new ApplicationException("An error occurred while fetching employee details.", ex);
            }
        }




        public async Task<List<OfferLetterDto>> GetOfferLettersOnMail(string employeeIds)
        {
            var offerLetters = new List<OfferLetterDto>();

            try
            {
                using (var connection = _context.Database.GetDbConnection())
                {
                    await connection.OpenAsync();

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "sp_GetOfferletterOnMail";
                        command.CommandType = CommandType.StoredProcedure;


                        command.Parameters.Add(new SqlParameter("@EmployeeIds", SqlDbType.VarChar)
                        {
                            Value = employeeIds
                        });

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var filePath = reader["FilePath"].ToString();
                                var email = reader["email"].ToString();
                                var pdfName = reader["pdfName"].ToString();
                                var FullName = reader["Full_Name"].ToString();
                                var DesignationName = reader["DesignationName"].ToString();

                                offerLetters.Add(new OfferLetterDto
                                {
                                    ApplicantId = Convert.ToInt32(reader["CId"]),
                                    FilePath = filePath,
                                    EmailId = email,
                                    PdfFileName = pdfName,
                                    FullName = FullName,
                                    DesignationName = DesignationName,
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {

                throw new ApplicationException("Failed to fetch offer letter data", ex);
            }

            return offerLetters;
        }

        public async Task SendOfferLetters(string employeeIds)
        {
            var offerLetters = await GetOfferLettersOnMail(employeeIds);

            foreach (var letter in offerLetters)
            {
                try
                {
                    string fullPath = Path.Combine(_env.ContentRootPath, "wwwroot", letter.FilePath.Replace("\\", Path.DirectorySeparatorChar.ToString()));

                    if (!File.Exists(fullPath))
                    {

                        await SaveEmailStatus(letter.ApplicantId, letter.EmailId, false, "File not found");
                        continue;
                        throw new ApplicationException("File Not Found");
                    }

                    string message = $"Dear Mr/Ms {letter.FullName},<br><br>We are excited at the prospect of you joining us as {letter.DesignationName} with V2 Retail. Through great appointments such as this, we want to make V2 Retail a continuously great place to work—especially one that fosters ownership, accountability, and drive in all of us. We believe you too will find it equally fulfilling to work with our organization.<br><br>I am excited to share our offer with you, as discussed on our recent call.<br><br>We would appreciate it if you could acknowledge this email and provide your acceptance today. In parallel, I will begin outlining an engaging and meaningful plan for your onboarding at V2 Retail while you serve your notice period.<br><br>Congratulations once again! We look forward to a long and enriching partnership with you at V2 Retail.<br><br>Warm Regards,<br>V2 HR Department";

                    var result = await _emailService.SendOfferLetterEmail(new List<string> { letter.EmailId }, new List<string>(), "Offer of Employement:Welcome to V2 Parivar", message, fullPath);


                    if (result)
                    {

                        await SaveEmailStatus(letter.ApplicantId, letter.EmailId, true);

                    }
                    else
                    {

                        await SaveEmailStatus(letter.ApplicantId, letter.EmailId, false, "SMTP error");
                        throw new ApplicationException("SMTP error");
                    }
                }
                catch (Exception ex)
                {

                    await SaveEmailStatus(letter.ApplicantId, letter.EmailId, false, ex.Message);
                    throw new ApplicationException(ex.Message);
                }
            }
        }

        public async Task SaveEmailStatus(int applicantId, string email, bool isSent, string errorMessage = "")
        {
            try
            {
                using (var connection = _context.Database.GetDbConnection())
                {
                    await connection.OpenAsync();

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "sp_InsertEmailData";
                        command.CommandType = CommandType.StoredProcedure; // Use only StoredProcedure


                        var param1 = command.CreateParameter();
                        param1.ParameterName = "@ApplicantId";
                        param1.DbType = DbType.Int32;
                        param1.Value = applicantId;
                        command.Parameters.Add(param1);

                        var param2 = command.CreateParameter();
                        param2.ParameterName = "@Email";
                        param2.DbType = DbType.String;
                        param2.Size = 200;
                        param2.Value = email;
                        command.Parameters.Add(param2);

                        var param3 = command.CreateParameter();
                        param3.ParameterName = "@IsSent";
                        param3.DbType = DbType.Boolean;
                        param3.Value = isSent;
                        command.Parameters.Add(param3);

                        var param4 = command.CreateParameter();
                        param4.ParameterName = "@ErrorMessage";
                        param4.DbType = DbType.String;
                        param4.Size = 1000;
                        param4.Value = string.IsNullOrEmpty(errorMessage) ? (object)DBNull.Value : errorMessage;
                        command.Parameters.Add(param4);

                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not save email status: {ex.Message}");
            }
        }
        //public async Task<ExecuteAndReponse> upsertMarketingEmpChecklistAsync(MarketingEmpChecklistDto EmpDto)
        //{
        //    try
        //    {
        //        if (EmpDto == null)
        //            throw new ArgumentNullException(nameof(EmpDto));

        //        if (string.IsNullOrWhiteSpace(EmpDto.E_Code))
        //            throw new ArgumentException("E_Code is required.");

        //        // Check if employee exists
        //        var employeeExists = await _context.tblEmployees.AsNoTracking().AnyAsync(e => e.Ecode == EmpDto.E_Code);
        //        var existingPF = await _context.MarketingEmpChecklists.FirstOrDefaultAsync(m => m.E_Code == EmpDto.E_Code);

        //        if (existingPF != null)
        //        {
        //            // UPDATE
        //            existingPF.E_Code = EmpDto.E_Code;
        //            existingPF.Resignation_V2parivar = EmpDto.Resignation_V2parivar;
        //            existingPF.No_Dues_form_submitted = EmpDto.No_Dues_form_submitted;
        //            existingPF.Finger_registration_removed = EmpDto.Finger_registration_removed;
        //            existingPF.Email_Inactive = EmpDto.Email_Inactive;
        //            existingPF.Assets_Received = EmpDto.Assets_Received;
        //            existingPF.Attendance_updated = EmpDto.Attendance_updated;

        //            _context.MarketingEmpChecklists.Update(existingPF);
        //        }
        //        else
        //        {
        //            // INSERT
        //            if (employeeExists == true)
        //            {
        //                var newPF = new MarketingEmpChecklist
        //                {
        //                    E_Code = EmpDto.E_Code,
        //                    Resignation_V2parivar = EmpDto.Resignation_V2parivar,
        //                    No_Dues_form_submitted = EmpDto.No_Dues_form_submitted,
        //                    Finger_registration_removed = EmpDto.Finger_registration_removed,
        //                    Email_Inactive = EmpDto.Email_Inactive,
        //                    Assets_Received = EmpDto.Assets_Received,
        //                    Attendance_updated = EmpDto.Attendance_updated
        //                };
        //                await _context.MarketingEmpChecklists.AddAsync(newPF);
        //            }
        //            else
        //            {
        //                throw new ArgumentException($"ECode {EmpDto.E_Code} does not exist.");
        //            }

        //        }
        //        await _context.SaveChangesAsync();
        //        return new ExecuteAndReponse
        //        {
        //            Status = true,
        //            Message = "Employee checklist record upserted successfully.",
        //            Code = HttpStatusCode.OK
        //        };
        //    }
        //    catch (Exception ex)
        //    {
        //        return new ExecuteAndReponse
        //        {
        //            Status = false,
        //            Message = ex.Message,
        //            Code = HttpStatusCode.BadRequest
        //        };
        //    }
        //}
        //public async Task<List<EmployeeResignationChecklistMasterDTO>> GetEmployeeResignationChecklistMasterAsync()
        //{
        //    var data = await _context.GetProcedures().GetEmployeeResignationChecklistMasterAsync();

        //    if (data == null)
        //        return new List<EmployeeResignationChecklistMasterDTO>();

        //    return data.Select(x => new EmployeeResignationChecklistMasterDTO
        //    {
        //        EmployeeResignationChecklistMasterId = x.EmployeeResignationChecklistMasterId,
        //        ResignationChecklist = x.ResignationChecklist,
        //        IsActive = x.IsActive
        //    }).ToList();
        //}
        //public async Task<List<EmployeeResignationChecklistByECodeDTO>> GetEmployeeResignationChecklistByECodeAsync(string ECode)
        //{
        //    var data = await _context.GetProcedures().EmployeeResignationChecklistByECodeAsync(ECode);

        //    return data.Select(x => new EmployeeResignationChecklistByECodeDTO
        //    {
        //        CheckListName = x.CheckListName,
        //        CheckListId = x.EmployeeResignationChecklistMasterId, 
        //        IsChecked = x.IsActive,
        //        Attachment = x.CheckListName == "No Dues form submitted"
        //    ? (x.IsActive == true ? x.NoDuesAttechment : "Null")
        //    : null
        //    }).ToList();
        //}


        //public async Task<List<GetEmployeeResignationChecklist>>GetEmployeeResignationChecklistByECodeAsync(string ECode)
        //{
        //    try
        //    {
        //        if (string.IsNullOrWhiteSpace(ECode))
        //            throw new ArgumentException("Employee code is required.");

        //        bool employeeExists = await _context.tblEmployees
        //            .AnyAsync(e => e.Ecode == ECode);

        //        if (!employeeExists)
        //        {
        //            throw new KeyNotFoundException(
        //                $"Employee with ECode '{ECode}' does not exist.");
        //        }

        //        var data = await _context.GetProcedures().EmployeeResignationChecklistByECodeAsync(ECode);

        //        return data.Select(x => new GetEmployeeResignationChecklist
        //        {
        //            CheckListId = x.EmployeeResignationChecklistMasterId,
        //            CheckListName = x.CheckListName,
        //            IsAttachmentRequired = x.IsAttachment ?? false,
        //            IsChecked = x.ResignationChecklistResponse ?? false,
        //            Attachment = x.Attachment
        //        }).ToList();
        //    }

        //    catch (Exception ex)
        //    {
        //        throw new ApplicationException("Failed to fetch checklist data",ex);
        //    }
        //}

        public async Task<List<GetEmployeeResignationChecklist>>GetEmployeeResignationChecklistByECodeAsync(string ECode)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ECode))
                    throw new ArgumentException("Employee code is required.");

                var employeeId = await _context.tblEmployees
                    .Where(e => e.Ecode == ECode)
                    .Select(e => e.EmployeeId)
                    .FirstOrDefaultAsync();

                bool employeeExists = await _context.tblEmployees
                    .AnyAsync(e => e.EmployeeId == employeeId);

                if (!employeeExists)
                    throw new KeyNotFoundException($"Employee with ECode '{ECode}' does not exist.");
                
                var data = await _context.GetProcedures()
                    .EmployeeResignationChecklistByECodeAsync(employeeId.ToString());

                return data.Select(x => new GetEmployeeResignationChecklist
                {
                    CheckListId = x.EmployeeResignationChecklistMasterId,
                    CheckListName = x.CheckListName,
                    IsAttachmentRequired = x.IsAttachment ?? false,
                    IsChecked = x.ResignationChecklistResponse ?? false,
                    Attachment = x.Attachment
                }).ToList();
            }
            catch (Exception ex)
            {
                throw new ApplicationException("Failed to fetch checklist data",ex);
            }
        }


        public async Task<bool> SaveChecklistAsync(ResignationChecklistResponseDto dto, string EmployeeId)
        {

            try
            {
                var master = await _context.EmployeeResignationChecklistMasters.FirstOrDefaultAsync(x => x.EmployeeResignationChecklistMasterId
                                  == dto.EmployeeResignationChecklistMasterId
                                  && x.IsDeleted != true);
                if (master == null)
                    throw new Exception("Checklist master not found");

                string Ecode = await _context.tblEmployees.Where(e => e.EmployeeId.ToString() == EmployeeId).Select(e => e.Ecode).FirstOrDefaultAsync();

                string attachmentPath = null;
                var existing = await _context.EmployeeResignationChecklistResponses.FirstOrDefaultAsync(x =>x.EmployeeResignationChecklistMasterId == dto.EmployeeResignationChecklistMasterId &&x.EmployeeId == dto.EmployeeId);

                if (existing != null)
                {
                    existing.Attachment = attachmentPath;
                    existing.LastUpdatedOn = DateTime.Now;
                }
                
                bool resignationChecklistResponse;

                if (master.IsAttachment == true)
                {
                    if (dto.Attachment != null && dto.Attachment.Length > 0)
                    {
                        
                        attachmentPath = await SaveAttachmentAsync(dto.Attachment,dto.EmployeeId);

                        resignationChecklistResponse = true;
                    }
                    else
                    {
                        resignationChecklistResponse = false;
                        attachmentPath = null;
                    }
                }
                else
                {
                    resignationChecklistResponse = false;
                }

                if (existing != null)
                {
                    existing.ResignationChecklistResponse = resignationChecklistResponse;
                    existing.Attachment = attachmentPath;
                    existing.LastUpdatedOn = DateTime.Now;
                    existing.LastUpdatedBy = Ecode;
                }
                else
                {
                    var response = new EmployeeResignationChecklistResponse
                    {
                        EmployeeResignationChecklistMasterId = dto.EmployeeResignationChecklistMasterId,
                        EmployeeId = dto.EmployeeId,
                        ResignationChecklistResponse = resignationChecklistResponse,
                        Attachment = attachmentPath,
                        CreatedOn = DateTime.Now,
                        LastUpdatedOn = DateTime.Now,
                        CreatedBy = Ecode,
                        LastUpdatedBy = Ecode
                    };

                    _context.EmployeeResignationChecklistResponses.Add(response);
                }

                
                await _context.SaveChangesAsync();

                return true;
            }       
            catch(Exception ex)
            {
                throw new ApplicationException("Failed to fetch check list data", ex);
            }
        }
        public async Task<bool> SaveChecklistListAsync(List<ResignationChecklistItemDto> items, List<IFormFile> files, string EmployeeId)
        {
            try
            {
                if (items == null || items.Count == 0)
                    throw new Exception("No checklist items provided");

                string Ecode = await _context.tblEmployees.Where(e => e.EmployeeId.ToString() == EmployeeId).Select(e => e.Ecode).FirstOrDefaultAsync();

                int fileIndex = 0; // Track which file to use for items requiring attachments

                foreach (var item in items)
                {
                    var master = await _context.EmployeeResignationChecklistMasters.FirstOrDefaultAsync(x => 
                        x.EmployeeResignationChecklistMasterId == item.EmployeeResignationChecklistMasterId
                        && x.IsDeleted != true);
                    
                    if (master == null)
                        throw new Exception($"Checklist master not found for ID: {item.EmployeeResignationChecklistMasterId}");

                    string attachmentPath = null;
                    var existing = await _context.EmployeeResignationChecklistResponses.FirstOrDefaultAsync(x =>
                        x.EmployeeResignationChecklistMasterId == item.EmployeeResignationChecklistMasterId 
                        && x.EmployeeId == item.EmployeeId);

                    bool resignationChecklistResponse = false;

                    if (master.IsAttachment == true)
                    {
                        // Get the file for this item if available
                        IFormFile? attachmentFile = null;
                        if (fileIndex < files.Count)
                        {
                            attachmentFile = files[fileIndex];
                            fileIndex++; // Move to next file for next item requiring attachment
                        }

                        if (attachmentFile != null && attachmentFile.Length > 0)
                        {
                            attachmentPath = await SaveAttachmentAsync(attachmentFile, item.EmployeeId);
                            resignationChecklistResponse = true;
                        }
                        else
                        {
                            resignationChecklistResponse = false;
                            attachmentPath = null;
                        }
                    }
                    else
                    {
                        resignationChecklistResponse = false;
                    }

                    if (existing != null)
                    {
                        existing.ResignationChecklistResponse = resignationChecklistResponse;
                        existing.Attachment = attachmentPath;
                        existing.LastUpdatedOn = DateTime.Now;
                        existing.LastUpdatedBy = Ecode;
                    }
                    else
                    {
                        var response = new EmployeeResignationChecklistResponse
                        {
                            EmployeeResignationChecklistMasterId = item.EmployeeResignationChecklistMasterId,
                            EmployeeId = item.EmployeeId,
                            ResignationChecklistResponse = resignationChecklistResponse,
                            Attachment = attachmentPath,
                            CreatedOn = DateTime.Now,
                            LastUpdatedOn = DateTime.Now,
                            CreatedBy = Ecode,
                            LastUpdatedBy = Ecode
                        };

                        _context.EmployeeResignationChecklistResponses.Add(response);
                    }
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                throw new ApplicationException("Failed to save checklist data", ex);
            }
        }

        private async Task<string> SaveAttachmentAsync(IFormFile file,string employeeId)
        { 
            var rootPath = Path.Combine(Directory.GetCurrentDirectory(),"wwwroot","resignation-attachments",employeeId);

            if (!Directory.Exists(rootPath))
                Directory.CreateDirectory(rootPath);

            var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
            var filePath = Path.Combine(rootPath, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return Path.Combine("resignation-attachments", employeeId, fileName).Replace("\\", "/");
        }       

    }
}
