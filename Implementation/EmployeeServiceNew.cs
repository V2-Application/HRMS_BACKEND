using ClosedXML.Excel;
using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using DocumentFormat.OpenXml.Office2016.Drawing.Charts;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Vml.Office;
using DocumentFormat.OpenXml.Wordprocessing;
using Emgu.CV.Face;
using HRMSAPI.Data;
using HRMSAPI.DTO;
using HRMSAPI.Interfaces;
using HRMSAPI.Models.Candidate;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OfficeOpenXml;
using Roomsy.DTOS.GenericsResponses;
using System;
using System.Data;
using System.Net;
using System.Net.Mail;
using System.Reflection;
using GetEmployeeDetailsResult = HRMSAPI.DTO.GetEmployeeDetailsResultNew;

namespace HRMSAPI.Implementation
{
    public class EmployeeServiceNew : BaseService,IEmployeeServiceNew
    {
        private readonly string savePath = Path.Combine("wwwroot");
        private readonly IConfiguration _configuration;
        private readonly HRMSContext _context;
        private readonly ILogger<EmployeeServiceNew> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        public EmployeeServiceNew(HRMSContext context, IConfiguration configuration, ILogger<EmployeeServiceNew> logger, IHttpContextAccessor httpContextAccessor) : base(context)
        {
            _configuration = configuration;
            _context = context;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
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
                        command.CommandText = "GetEmployeeDetails_New"; // Use procedure name only
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
                        var employees = new List<GetEmployeeDetailsResult>();
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var employee = new GetEmployeeDetailsResult
                                {
                                    EmployeeId = (int)reader.GetInt64(reader.GetOrdinal("EmployeeId")),  // Fix Data Type
                                    CandidateId = reader.IsDBNull(reader.GetOrdinal("CandidateId"))
    ? 0
    : (int)reader.GetInt64(reader.GetOrdinal("CandidateId")),
                                    // Fix Data Type
                                    FullName = reader.IsDBNull(reader.GetOrdinal("FullName")) ? string.Empty : reader.GetString(reader.GetOrdinal("FullName")),
                                    DepartmentName = reader.IsDBNull(reader.GetOrdinal("DepartmentName")) ? string.Empty : reader.GetString(reader.GetOrdinal("DepartmentName")),
                                    DesignationName = reader.IsDBNull(reader.GetOrdinal("DesignationName")) ? string.Empty : reader.GetString(reader.GetOrdinal("DesignationName")),
                                    LocationName = reader.IsDBNull(reader.GetOrdinal("LocationName")) ? string.Empty : reader.GetString(reader.GetOrdinal("LocationName")),
                                    StoreCode = reader.IsDBNull(reader.GetOrdinal("STCode")) ? string.Empty : reader.GetString(reader.GetOrdinal("STCode")),
                                    //Ecode = reader.IsDBNull(reader.GetOrdinal("Ecode")) ? string.Empty : reader.GetString(reader.GetOrdinal("Ecode")),
                                    Ecode = reader.IsDBNull(reader.GetOrdinal("LocBasedECode")) ? string.Empty : reader.GetString(reader.GetOrdinal("LocBasedECode")),
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

        public async Task<FetchAndResponse> GetEmployeeOrCandidateById(int Id,bool isCandidate = true)
        {
            try {
                if (isCandidate) {
                    // Fetch candidate details
                    var candidateEntity = await FindOneWithNoTrackingAsync<Data.Candidate>(row => row.Id == Id);
                    if (candidateEntity == null)
                    {
                        return BuildFetchErrorResponse("Candidate not found", HttpStatusCode.NotFound);
                    }

                    // Map to Candidate Model
                    var candidate = new HRMSAPI.Models.Candidate.Candidate
                    {
                        cid = candidateEntity.Id,
                        title = candidateEntity.TITLE ?? "",
                        fullName = $"{candidateEntity.FIRST_NAME ?? ""} {candidateEntity.MIDDLE_NAME ?? ""} {candidateEntity.LAST_NAME ?? ""}".Trim(),
                        firstName = candidateEntity.FIRST_NAME ?? "",
                        middleName = candidateEntity.MIDDLE_NAME ?? "",
                        lastName = candidateEntity.LAST_NAME ?? "",
                        husbandName = candidateEntity.HUSBAND_NAME ?? "",
                        joiningDate = candidateEntity.JOINING_DATE,
                        department = candidateEntity.DEPARTMENT ?? "",
                        location = candidateEntity.LOCATION ?? "",
                        grossSalary = candidateEntity.GROSS_SALARY?.ToString() ?? "0",
                        uanNo = candidateEntity.UAN_NO ?? "",
                        fathersName = candidateEntity.FATHER_NAME ?? "",
                        mothersName = candidateEntity.MOTHER_NAME ?? "",
                        designation = candidateEntity.DESIGNATION ?? "",
                        dob = candidateEntity.DOB,
                        gender = candidateEntity.GENDER ?? "",
                        panNo = candidateEntity.PAN_NO ?? "",
                        aadharNo = candidateEntity.AADHAR_NO ?? "",
                        nameOnAadhar = candidateEntity.NAME_ON_AADHAR ?? "",
                        placeOfBirth = candidateEntity.PLACE_OF_BIRTH ?? "",
                        presentAddress = candidateEntity.PRESENT_ADDRESS ?? "",
                        presentAddressPinCode = candidateEntity.PRESENT_ADDRESS_PIN_CODE ?? "",
                        permanentAddress = candidateEntity.PERMANENT_ADDRESS ?? "",
                        permanentAddressPinCode = candidateEntity._PERMANENT_ADDRESS_PIN_CODE ?? "",
                        maritalStatus = candidateEntity.MARITIAL_STATUS ?? "",
                        mobile = candidateEntity.MOBILE ?? "",
                        emailAddress = candidateEntity.EMAIL_ADDRESS ?? "",
                        nationality = candidateEntity.NATIONALITY ?? "",
                        religion = candidateEntity.RELIGION ?? "",
                        bankName = candidateEntity.BANK_NAME ?? "",
                        accountNo = candidateEntity.A_C_NO ?? "",
                        bankIfscCode = candidateEntity.BANK_IFSC_CODE ?? "",
                        statusId = candidateEntity.StatusId,
                        applicantCode = candidateEntity.ApplicantId,
                        beneficiaryAddress = candidateEntity.BENEFICIARY_ADDRESS ?? "",
                        lastCtcAnnual = candidateEntity.LAST_CTC_ANNUAL_?.ToString() ?? "0",
                        contact1LastCompany = candidateEntity.CONTACT1_OF_LAST_3_COMPANY ?? "",
                        contact2LastCompany = candidateEntity.CONTACT2_OF_LAST_3_COMPANY ?? "",
                        contact3LastCompany = candidateEntity.CONTACT3_OF_LAST_3_COMPANY ?? "",
                        contact4LastCompany = candidateEntity.CONTACT4_OF_LAST_3_COMPANY ?? "",
                        contact5LastCompany = candidateEntity.CONTACT5_OF_LAST_3_COMPANY ?? "",
                        company1 = candidateEntity.COMPANY_1 ?? "",
                        company2 = candidateEntity.COMPANY_2 ?? "",
                        company3 = candidateEntity.COMPANY_3 ?? "",
                        empCode = candidateEntity.EMP_CODE ?? "",
                        reference = candidateEntity.REFERENCE ?? "",
                        reference1LastCompany = candidateEntity.REFERENCE1__OF_LAST_3_COMPANY ?? "",
                        reference2LastCompany = candidateEntity.REFERENCE2__OF_LAST_3_COMPANY ?? "",
                        reference3LastCompany = candidateEntity.REFERENCE3__OF_LAST_3_COMPANY ?? "",
                        reference4LastCompany = candidateEntity.REFERENCE4__OF_LAST_3_COMPANY ?? "",
                        reference5LastCompany = candidateEntity.REFERENCE5__OF_LAST_3_COMPANY ?? "",
                        isRelativeInCompany = candidateEntity.ISRELATIVEINCOMPANY ?? false,
                        workLocation = candidateEntity.WORK_LOCATION ?? "",
                        weeklyOff = candidateEntity.WEEKLY_OFF ?? "",
                        esicno = candidateEntity.PREV__EST_NO_ ?? "",
                        positionHeldInPreviousCompany = candidateEntity.POSITION_HELD_IN_PREVIOUS_COMPANY ?? "",
                        // Document flags
                        isPassportPhotoUploaded = candidateEntity.IsPassportPhotoUploaded ?? false,
                        isSalarySlipUploaded = candidateEntity.IsSalarySlipUploaded ?? false,
                        isBankStatementUploaded = candidateEntity.IsBankStatementUploaded ?? false,
                        isPrevOfferLetterUploaded = candidateEntity.IsPrevOfferLetterUploaded ?? false,
                        isPanAttachmentUploaded = candidateEntity.IsPanAttachmentUploaded ?? false,
                        isAadharAttachmentUploaded = candidateEntity.IsAadharAttachmentUploaded ?? false,
                        isAadharBackAttachmentUploaded = candidateEntity.IsAadharBackAttachmentUploaded ?? false,
                        isBankPassbookAttachmentUploaded = candidateEntity.IsBankPassbookAttachmentUpoaded ?? false,
                        isEducationAttachmentUploaded = candidateEntity.IsEducationAttachmentUploaded ?? false,
                        isResumeAttachmentUploaded = candidateEntity.IsResumeUploaded ?? false,
                        isOtherAttachmentUploaded = candidateEntity.IsOtherAttachmentUploaded ?? false,
                        prevEstNo = candidateEntity.PREV__EST_NO_ ?? "",
                        // Audit fields
                        createdBy = candidateEntity.CreatedBy ?? "",
                        createdOn = candidateEntity.CreatedOn,
                        updatedBy = candidateEntity.UpdatedBy ?? "",
                        updatedOn = candidateEntity.UpdatedOn,
                        //prevEstNo = candidateEntity.PREV__EST_NO_ ?? "";
                        // Newly added fields on 30 Apr 25
                        BasicSalary = candidateEntity.BasicSalary ?? 0,
                        HRA = candidateEntity.HRA ?? 0,
                        CCA = candidateEntity.CCA ?? 0,  // Added CCA mapping
                        SpecialAllowance = candidateEntity.SpecialAllowance ?? 0,
                        DA = candidateEntity.DA ?? 0,
                        ExtraAllowance = candidateEntity.ExtraAllowance ?? 0,
                        monthlyGrossCTC = candidateEntity.monthlyGrossCTC ?? 0,
                        annuallyNetCTC = candidateEntity.annuallyNetCTC ?? 0,
                        PFApplicable = candidateEntity.PFApplicable ?? false,
                        bonusApplicable = candidateEntity.BonusApplicable ?? "No",
                        ESICApplicable = candidateEntity.ESICApplicable ?? false,
                        differentlyAbledReason = candidateEntity.DifferentlyAbledReason ?? "",
                        differentlyAbledRemarks = candidateEntity.DifferentlyAbledRemarks ?? "",
                        differentlyAbled = candidateEntity.DifferentlyAbled ?? false,
                        skillType = candidateEntity.SkillType ?? "",
                        ShiftID = candidateEntity.ShiftID ?? 0,
                        Source = candidateEntity.Source ?? "",
                        ReferenceEmployee = candidateEntity.ReferenceEmployee ?? "",
                        IsUANRegistered = candidateEntity.IsUANRegistered ?? false,
                        PreferredLocation = candidateEntity.PreferredLocation ?? "",
                        AoCode = candidateEntity.AOCode ?? ""
                    };
                    // Fetch Family Details
                    var familyData = await _context.tblFamilies.AsNoTracking()
                        .Where(f => f.CID == Id)
                        .Select(f => new CandidateUpdateFamilyMemberNew
                        {
                            familyMemberName = f.Family_Member_Name ?? "",
                            relation = f.Relation ?? "",
                            dob = f.DOB.GetValueOrDefault()
                        })
                        .ToListAsync() ?? new List<CandidateUpdateFamilyMemberNew>();

                    // Fetch Experience Details
                    var experienceData = await _context.tblExperiences.AsNoTracking()
                        .Where(e => e.CID == Id)
                        .Select(e => new CandidateUpdateExperienceNew
                        {
                            nameOfCompany = e.Name_of_Company ?? "",
                            workLocation = e.Work_Location ?? "",
                            positionHeld = e.Position_Held ?? "",
                            from = e.From.GetValueOrDefault(),
                            to = e.To.GetValueOrDefault(),
                            inHand = e.InHand,
                            lastCtc = e.Last_CTC
                        })
                        .ToListAsync() ?? new List<CandidateUpdateExperienceNew>();

                    // Fetch Qualification Details
                    var qualificationData = await _context.tblQualifications.AsNoTracking()
                        .Where(q => q.CID == Id)
                        .Select(q => new CandidateUpdateQualificationNew
                        {
                            education = q.Education ?? "",
                            yop = q.YOP ?? "",
                            grade = q.Grade ?? "",
                            type = q.Type ?? ""
                        })
                        .ToListAsync() ?? new List<CandidateUpdateQualificationNew>();

                    // Fetch Document Details
                    var documentData = await _context.CanidateDocs.AsNoTracking()
                        .Where(d => d.CId == Id && d.IsDeleted == false)
                        .OrderByDescending(d => d.CreatedOn)
                        .Select(d => new CandidateDocumentDto
                        {
                            Id = d.Id,
                            CandidateId = d.CId,
                            FilePath = d.FilePath ?? "",
                            DocumentType = d.FileType ?? "",
                            FileSize = d.FileSize ?? ""
                        })
                        .ToListAsync() ?? new List<CandidateDocumentDto>();


                    var assignLocationData = await _context.AssignLocationHistories
                        .AsNoTracking()
                        .Where(alh =>
                     (alh.EmployeeId == Id || alh.CandidateId == Id))
    .OrderByDescending(alh => alh.AssignedOnDate)
    .Select(alh => new
    {
        Id = alh.AssignLocationHistoryId,
        AssignedLocation = alh.AssignedLocation,
        AssignedLocationName = _context.tblLocations
            .Where(l => l.LocationId == alh.AssignedLocation)
            .Select(l => l.LocationName)
            .FirstOrDefault() ?? "",
        AssignedLocationSTCode = _context.tblLocations
            .Where(l => l.LocationId == alh.AssignedLocation)
            .Select(l => l.STCode)
            .FirstOrDefault() ?? "",
        AssignedReason = alh.AssignedReason,
        IsActive = alh.IsActive,
        AssignedOnDate = alh.AssignedOnDate,
        ReleasedOnDate = alh.ReleasedOnDate,
        DesignationId = alh.designationid,
        DesignationName = _context.tblDesignations
            .Where(d => d.DesignationId == alh.designationid)
            .Select(d => d.DesignationName)
            .FirstOrDefault() ?? "",
        DepartmentId = alh.departmentid,
        DepartmentName = _context.tblDepartments
            .Where(d => d.DepartmentId == alh.departmentid)
            .Select(d => d.DepartmentName)
            .FirstOrDefault() ?? "",
        TemporaryTransfer = alh.TemporaryTransfer,
        PermanentTransfer = alh.PermanentTransfer,
        TransferApprovalStatus = alh.TransferApprovalStatus,
        IsReportingHeadApproval = alh.IsReportingHeadApproval
    })
    .ToListAsync();

                    return BuildFetchSuccessResponse("Data Fetched Successfully", new
                    {
                        CandidateInfo = candidate,
                        FamilyMembersList = familyData,
                        ExperienceList = experienceData,
                        QualificationList = qualificationData,
                        Documents = documentData,
                        AssignLocations = assignLocationData
                    });
                }
                else {
                    var employeeEntity = await FindOneWithNoTrackingAsync<Data.tblEmployee>(row =>row.EmployeeId == Id);
                    if (employeeEntity == null)
                        return BuildFetchErrorResponse("No Employee Found",HttpStatusCode.NotFound);
                    var candidateEntity = await FindOneWithNoTrackingAsync<Data.Candidate>(row => row.Id == employeeEntity.CandidateId);
                    if (employeeEntity == null)
                    {
                        return BuildFetchErrorResponse("Employee not found", HttpStatusCode.NotFound);
                    }
                    var company = await FindOneWithNoTrackingAsync<Data.tblCompany>(row=>row.CompanyId==employeeEntity.CompanyId);
                    // Map to Candidate Model
                    var candidate = new HRMSAPI.Models.Candidate.Candidate();
                    //{
                        candidate.id = employeeEntity.EmployeeId;
                        candidate.cid = employeeEntity.CandidateId ?? 0;
                        candidate.title = employeeEntity.TITLE ?? "";
                        //candidate.fullName = $"{employeeEntity.FirstName ?? ""} {employeeEntity.MiddleName ?? ""} {employeeEntity.LastName ?? ""}".Trim();
                        candidate.fullName = $"{employeeEntity.FULL_NAME}".Trim();
                        candidate.firstName = employeeEntity.FirstName ?? "";
                        candidate.middleName = employeeEntity.MiddleName ?? "";
                        candidate.lastName = employeeEntity.LastName ?? "";
                        candidate.husbandName = candidateEntity?.HUSBAND_NAME ?? "";
                        candidate.joiningDate = employeeEntity.DOJ;
                        candidate.department = employeeEntity?.DepartmentId.ToString() ?? "";
                        //location = candidateEntity.LOCATION ?? "";
                        candidate.location = employeeEntity.LocationId.ToString() ?? "";
                        candidate.grossSalary = employeeEntity.GROSS_SALARY?.ToString() ?? "0";
                        candidate.uanNo = employeeEntity.UAN_NO ?? "";
                        candidate.fathersName = employeeEntity.FATHER_S_NAME ?? "";
                        candidate.mothersName = employeeEntity.MOTHER_S_NAME ?? "";
                        candidate.designation = employeeEntity.DesignationId.ToString() ?? "";
                        candidate.dob = employeeEntity.DOB;
                        candidate.gender = employeeEntity.GENDER ?? "";
                        candidate.panNo = employeeEntity.PAN_NO ?? "";
                        candidate.aadharNo = employeeEntity.AADHAR_NO ?? "";
                        candidate.nameOnAadhar = employeeEntity.NAME_ON_ADHAR ?? "";
                        candidate.placeOfBirth = employeeEntity.PLACE_OF_BIRTH ?? "";
                        candidate.presentAddress = employeeEntity.PRESENT_ADDRESS ?? "";
                        candidate.presentAddressPinCode = employeeEntity.PRESENT_ADDRESS_PIN_CODE ?? "";
                        candidate.permanentAddress = employeeEntity.PERMANENT_ADDRESS ?? "";
                        candidate.permanentAddressPinCode = employeeEntity.PERMANENT_ADDRESS_PIN_CODE ?? "";
                        candidate.maritalStatus = employeeEntity.MARITIAL_STATUS ?? "";
                        candidate.mobile = employeeEntity.MOBILE ?? "";
                        candidate.emailAddress = employeeEntity.EMAIL_ADDRESS ?? "";
                        candidate.nationality = employeeEntity.NATIONALITY ?? "";
                        candidate.religion = employeeEntity.RELIGION ?? "";
                        candidate.bankName = employeeEntity.BANK_NAME ?? "";
                        candidate.accountNo = employeeEntity.A_C_NO ?? "";
                        candidate.bankIfscCode = employeeEntity.BANK_IFSC_CODE ?? "";
                        candidate.statusId = employeeEntity.StatusId;
                        candidate.applicantCode = employeeEntity.ApplicantId;
                        candidate.beneficiaryAddress = employeeEntity.BENEFICIARY_ADDRESS ?? "";
                        candidate.lastCtcAnnual = employeeEntity.LAST_CTC_ANNUAL_?.ToString() ?? "0";
                        candidate.contact1LastCompany = employeeEntity.CONTACT1_OF_LAST_3_COMPANY ?? "";
                        candidate.contact2LastCompany = employeeEntity.CONTACT2_OF_LAST_3_COMPANY1 ?? "";
                        candidate.contact3LastCompany = employeeEntity.CONTACT3_OF_LAST_3_COMPANY11 ?? "";
                        candidate.contact4LastCompany = employeeEntity.CONTACT4_OF_LAST_3_COMPANY11 ?? "";
                        candidate.contact5LastCompany = employeeEntity.CONTACT5_OF_LAST_3_COMPANY111 ?? "";
                        candidate.company1 = employeeEntity.COMPANY_1 ?? "";
                        candidate.company2 = employeeEntity.COMPANY_2 ?? "";
                        candidate.company3 = employeeEntity.COMPANY_3 ?? "";
                        candidate.empCode = employeeEntity.Ecode ?? "";
                        candidate.reference = employeeEntity.REFERENCE ?? "";
                        candidate.reference1LastCompany = employeeEntity.REFERENCE1__OF_LAST_3_COMPANY ?? "";
                        candidate.reference2LastCompany = employeeEntity.REFERENCE2__OF_LAST_3_COMPANY1 ?? "";
                        candidate.reference3LastCompany = employeeEntity.REFERENCE3__OF_LAST_3_COMPANY11 ?? "";
                        candidate.reference4LastCompany = employeeEntity.REFERENCE4__OF_LAST_3_COMPANY11 ?? "";
                        candidate.reference5LastCompany = employeeEntity.REFERENCE5__OF_LAST_3_COMPANY111 ?? "";
                        candidate.isRelativeInCompany = employeeEntity.ISRELATIVEINCOMPANY ?? false;
                        candidate.workLocation = employeeEntity.WORK_LOCATION ?? "";
                        candidate.weeklyOff = employeeEntity.WEEKLY_OFF ?? "";
                        candidate.positionHeldInPreviousCompany = employeeEntity.POSITION_HELD_IN_PREVIOUS_COMPANY ?? "";
                        // Document flags
                        candidate.isPassportPhotoUploaded = employeeEntity.IsPassportPhotoUploaded ?? false;
                        candidate.isSalarySlipUploaded = employeeEntity.IsSalarySlipUploaded ?? false;
                        candidate.isBankStatementUploaded = employeeEntity.IsBankStatementUploaded ?? false;
                        candidate.isPrevOfferLetterUploaded = employeeEntity.IsPrevOfferLetterUploaded ?? false;
                        candidate.isPanAttachmentUploaded = employeeEntity.IsPanAttachmentUploaded ?? false;
                        candidate.isAadharAttachmentUploaded = employeeEntity.IsAadharAttachmentUploaded ?? false;
                    candidate.isAadharBackAttachmentUploaded = employeeEntity.IsAadharBackAttachmentUploaded ?? false;
                        candidate.isBankPassbookAttachmentUploaded = employeeEntity.IsBankPassbookAttachmentUpoaded ?? false;
                        candidate.isEducationAttachmentUploaded = employeeEntity.IsEducationAttachmentUploaded ?? false;
                        candidate.isResumeAttachmentUploaded = employeeEntity.IsResumeUploaded ?? false;
                        candidate.isOtherAttachmentUploaded = candidateEntity?.IsOtherAttachmentUploaded ?? false;
                    candidate.prevEstNo = candidateEntity?.PREV__EST_NO_ ?? "";
                        // Audit fields
                        candidate.createdBy = employeeEntity.CreatedBy ?? "";
                        candidate.createdOn = employeeEntity.CreatedOn;
                        candidate.updatedBy = employeeEntity.UpdatedBy ?? "";
                        candidate.updatedOn = employeeEntity.UpdatedOn;
                    candidate.prevEstNo = employeeEntity.ESICNO ?? "";
                        // Newly added fields on 30 Apr 25
                        candidate.BasicSalary = employeeEntity.BasicSalary ?? 0;
                        candidate.HRA = employeeEntity.HRA ?? 0;
                        candidate.CCA = employeeEntity.CCA ?? 0;  // Added CCA mapping
                        candidate.SpecialAllowance = employeeEntity.SpecialAllowance ?? 0;
                        candidate.DA = employeeEntity.DA ?? 0;
                        candidate.ExtraAllowance = employeeEntity.ExtraAllowance ?? 0;
                        candidate.monthlyGrossCTC = employeeEntity.monthlyGrossCTC ?? 0;
                        candidate.annuallyNetCTC = employeeEntity.annuallyNetCTC ?? 0;
                        candidate.PFApplicable = employeeEntity.PFApplicable ?? false;
                        candidate.bonusApplicable = employeeEntity.BonusApplicable ?? "No";
                        candidate.ESICApplicable = employeeEntity.ESICApplicable ?? false;
                        candidate.companyId = employeeEntity.CompanyId ?? 0;
                    candidate.companyName = company?.CompanyName ?? "";
                    candidate.esicno = employeeEntity.ESICNO ?? "";
                    candidate.IsUANRegistered = employeeEntity.IsUANRegistered ?? false;
                    candidate.PreferredLocation = employeeEntity.PreferredLocation ?? "";
                    candidate.AoCode = employeeEntity.AOCode ?? "";
                    var reportingHeadId = _context.tblEmployees
                   .Where(e => e.Ecode == employeeEntity.ReportHeadEcode)
                   .Select(a => (int?)a.EmployeeId)
                    .FirstOrDefault();
                    candidate.reportingHeadId = reportingHeadId ?? 0; // or null if reportingHeadId is nullable
                    var reportingheadname = _context.tblEmployees
                                          .Where(e => e.EmployeeId == reportingHeadId)
                                          .Select(a => a.FirstName ?? a.FULL_NAME)
                                          .FirstOrDefault();
                    candidate.reportingHeadName = reportingheadname ?? string.Empty;
                    var reportingheadecode = _context.tblEmployees
                                          .Where(e => e.EmployeeId == reportingHeadId)
                                          .Select(a =>a.Ecode)
                                          .FirstOrDefault();
                    candidate.reportinHeadEcode = reportingheadecode ?? "";
                    candidate.isActive = employeeEntity.IsActive ?? false;
                    candidate.LastWorkingDay = _context.tblEmployeeSeprations
                                                .Where(a => a.EmployeeId == Id && a.IsApprovedByHR==true && a.IsApprovedByManager==true)
                                                .Select(a => (DateTime?)a.LastDay)
                                                .FirstOrDefault() ?? _context.tblEmployees
                                                .Where(e => e.EmployeeId == Id)
                                                .Select(e => (DateTime?)e.DateOfLeft)
                                                .FirstOrDefault(); ;
                    candidate.fingerprintRegistered = employeeEntity.fingerprintRegistered ?? false;
                    // Fetch Family Details

                    //other by nick
                    candidate.differentlyAbled = candidateEntity?.DifferentlyAbled ?? false;
                    candidate.differentlyAbledReason = candidateEntity?.DifferentlyAbledReason ?? "";
                    candidate.differentlyAbledRemarks = candidateEntity?.DifferentlyAbledRemarks ?? "";
                    candidate.skillType = candidateEntity?.SkillType ?? "";
                    candidate.ShiftID = employeeEntity?.ShiftID ?? candidateEntity?.ShiftID ?? 0;

                    var idToPass = employeeEntity.CandidateId>0 ?employeeEntity.CandidateId:employeeEntity.EmployeeId;
                    var familyData = await _context.tblFamilies.AsNoTracking()
                        .Where(f => f.CID == idToPass && f.IsActive==true && f.IsDeleted==false)
                        .Select(f => new CandidateUpdateFamilyMemberNew
                        {
                            Id = f.ID,
                            familyMemberName = f.Family_Member_Name ?? "",
                            relation = f.Relation ?? "",
                            dob = f.DOB.GetValueOrDefault()
                        })
                        .ToListAsync() ?? new List<CandidateUpdateFamilyMemberNew>();

                    // Fetch Experience Details
                    var experienceData = await _context.tblExperiences.AsNoTracking()
                        .Where(e => e.CID == idToPass && e.IsActive == true && e.IsDeleted == false)
                        .Select(e => new CandidateUpdateExperienceNew
                        {
                            Id = e.ID,
                            nameOfCompany = e.Name_of_Company ?? "",
                            workLocation = e.Work_Location ?? "",
                            positionHeld = e.Position_Held ?? "",
                            from = e.From.GetValueOrDefault(),
                            to = e.To.GetValueOrDefault(),
                            inHand = e.InHand,
                            lastCtc = e.Last_CTC
                        })
                        .ToListAsync() ?? new List<CandidateUpdateExperienceNew>();

                    // Fetch Qualification Details
                    var qualificationData = await _context.tblQualifications.AsNoTracking()
                        .Where(q => q.CID == idToPass && q.IsActive == true && q.IsDeleted == false)
                        .Select(q => new CandidateUpdateQualificationNew
                        {
                            education = q.Education ?? "",
                            yop = q.YOP ?? "",
                            grade = q.Grade ?? "",
                            type = q.Type ?? ""
                        })
                        .ToListAsync() ?? new List<CandidateUpdateQualificationNew>();

                    // Fetch Document Details
                    var documentData = await _context.CanidateDocs.AsNoTracking()
                        .Where(d => d.CId == idToPass && d.IsDeleted == false && d.IsActive==true)
                        .OrderByDescending(d => d.CreatedOn)
                        .Select(d => new CandidateDocumentDto
                        {
                            Id = d.Id,
                            CandidateId = d.CId,
                            FilePath = d.FilePath ?? "",
                            DocumentType = d.FileType ?? "",
                            FileSize = d.FileSize ?? ""
                        })
                        .ToListAsync() ?? new List<CandidateDocumentDto>();

                    // Fetch Assign Location History
                    var assignLocationData = await _context.AssignLocationHistories.AsNoTracking()
                        .Where(alh => alh.EmployeeId == idToPass && alh.IsActive==true 
                        && alh.TransferApprovalStatus != 4)
                        .OrderByDescending(alh => alh.AssignedOnDate)
                        .Select(alh => new
                        {
                            Id = alh.AssignLocationHistoryId,
                            AssignedLocation = alh.AssignedLocation,
                            AssignedLocationName = _context.tblLocations
            .Where(l => l.LocationId == alh.AssignedLocation)
            .Select(l => l.LocationName)
            .FirstOrDefault() ?? "",
                            AssignedLocationSTCode = _context.tblLocations
            .Where(l => l.LocationId == alh.AssignedLocation)
            .Select(l => l.STCode)
            .FirstOrDefault() ?? "",
                            AssignedReason = alh.AssignedReason,
                            IsActive = alh.IsActive,
                            AssignedOnDate = alh.AssignedOnDate,
                            ReleasedOnDate = alh.ReleasedOnDate,
                            DesignationId = alh.designationid,
                            DesignationName = _context.tblDesignations
            .Where(d => d.DesignationId == alh.designationid)
            .Select(d => d.DesignationName)
            .FirstOrDefault() ?? "",
                            DepartmentId = alh.departmentid,
                            DepartmentName = _context.tblDepartments
            .Where(d => d.DepartmentId == alh.departmentid)
            .Select(d => d.DepartmentName)
            .FirstOrDefault() ?? "",
                            TemporaryTransfer = alh.TemporaryTransfer,
                            PermanentTransfer = alh.PermanentTransfer,
                            TransferApprovalStatus = alh.TransferApprovalStatus,
                            IsReportingHeadApproval = alh.IsReportingHeadApproval,
                            ISHRApproval = alh.IsHRApproval


                        })
                        .ToListAsync();
                    return BuildFetchSuccessResponse("Data Fetched Successfully", new
                    {
                        CandidateInfo = candidate,
                        FamilyMembersList = familyData,
                        ExperienceList = experienceData,
                        QualificationList = qualificationData,
                        Documents = documentData,
                        AssignLocations = assignLocationData
                    });
                }
            }
            catch (Exception ex) {
                return BuildFetchErrorResponse(ex.Message,HttpStatusCode.BadRequest);
            }
        }
            
        public async Task<ExecuteAndReponse> UpdateEmployee(CandidateUpdate details, CandidateDocs files,string updatedBy)
        {
            try {
                var rreportHeadEcode = _context.tblEmployees
                                    .Where(a => a.EmployeeId == details.reportingHeadId)
                                    .Select(r => r.Ecode)
                                    .FirstOrDefault() ?? "";
                AssignedLocationDTO assignLocations;
                try
                {
                    //assignLocations = JsonConvert.DeserializeObject<List<AssignedLocationDTO>>(details.AssignLocationsListJson ?? "")
                    //                  ?? new List<AssignedLocationDTO>();
                    assignLocations = JsonConvert.DeserializeObject<AssignedLocationDTO>(details.AssignLocationsListJson ?? "")
                                      ?? new AssignedLocationDTO();
                }
                catch
                {
                    assignLocations = new AssignedLocationDTO();
                }
                 var employee = new UpdateEmployee
                {
                    candidateInfo = new CandidateInfo
                    {
                        husbandName = details.husbandName ?? "",
                        location = details.location ?? "",
                        department = details.department ?? "",
                        joiningDate = Convert.ToDateTime(details.joiningDate),
                        grossSalary = details.grossSalary ?? "",
                        uanNo = details.uanNo ?? "",
                        statusId = details.statusId ?? 0,
                        permanentAddressPinCode = details.permanentAddressPinCode ?? "",
                        empCode = details.empCode ?? "",
                        applicantCode = details.applicantCode ?? "",
                        weeklyOff = details.weeklyOff ?? "",
                        isRelativeInCompany = details.isRelativeInCompany ?? false,
                        beneficiaryAddress = details.beneficiaryAddress ?? "",
                        prevEstNo = details.prevEstNo ?? "",
                        reference = details.reference ?? "",
                        basicSalary = Convert.ToDecimal(details.BasicSalary),
                        hra = Convert.ToDecimal(details.HRA),
                        cca = Convert.ToDecimal(details.CCA),
                        specialAllowance = Convert.ToDecimal(details.SpecialAllowance),
                        da = Convert.ToDecimal(details.DA),
                        extraAllowance = Convert.ToDecimal(details.ExtraAllowance),
                        monthlyGrossCtc = Convert.ToDecimal(details.monthlyGrossCTC),
                        annuallyNetCtc = Convert.ToDecimal(details.annuallyNetCTC),
                        isApplicant = details.IsApplicant ?? false,
                        id = details.id,
                        cid = details.cid,
                        firstName = details.firstName ?? "",
                        middleName = details.middleName ?? "",
                        lastName = details.lastName ?? "",
                        fathersName = details.fathersName ?? "",
                        mothersName = details.mothersName ?? "",
                        designation = details.designation ??"",
                        dob = Convert.ToDateTime(details.dob),
                        gender = details.gender ?? "",
                        panNo = details.panNo ?? "",
                        aadharNo = details.aadharNo ??"",
                        nameOnAadhar = details.nameOnAadhar ?? "",
                        placeOfBirth = details.placeOfBirth ?? "",
                        presentAddress = details.presentAddress ?? "",
                        presentAddressPinCode = details.presentAddressPinCode ?? "",
                        permanentAddress = details.permanentAddress ?? "",
                        esicno = details.prevEstNo ?? "",
                        maritalStatus = details.maritalStatus ?? "",
                        mobile = details.mobile ?? "",
                        emailAddress = details.emailAddress ?? "",
                        nationality = details.nationality ?? "",
                        religion = details.religion ?? "",
                        bankName = details.bankName ??"",
                        accountNo = details.accountNo ?? "",
                        bankIfscCode = details.bankIfscCode ?? "",
                        reference1LastCompany = details.reference1LastCompany ?? "",
                        reference2LastCompany = details.reference2LastCompany ?? "",
                        reference3LastCompany = details.reference3LastCompany ?? "",
                        reference4LastCompany = details.reference4LastCompany ?? "",
                        reference5LastCompany = details.reference5LastCompany ?? "",
                        contact1LastCompany = details.contact1LastCompany ?? "",
                        contact2LastCompany = details.contact2LastCompany ??"",
                        contact3LastCompany = details.contact3LastCompany ?? "",
                        contact4LastCompany = details.contact4LastCompany ?? "",
                        contact5LastCompany = details.contact5LastCompany ?? "",

                        familyMemberName = details.familyMemberName ?? "",
                        familyMemberDob = Convert.ToDateTime(details.familyMemberDob),
                        familyMemberRelation = details.familyMemberRelation ?? "",

                        company1 = details.company1 ?? "",
                        company2 = details.company2 ?? "",
                        company3 = details.company3 ?? "",
                        PreferredLocation = details.PreferredLocation ?? "",
                        workLocation = details.workLocation ?? "",
                        positionHeldInPreviousCompany = details.positionHeldInPreviousCompany ?? "",
                        from = details.from,
                        to = details.to,
                        inHandSalary = Convert.ToDecimal
                        (details.inHandSalary),
                        lastCtcAnnual = details.lastCtcAnnual??"",
                        highestQualification = details.highestQualification ?? "",
                        updatedBy = updatedBy,
                        updatedOn = DateTime.UtcNow,
                        isSalarySlipUploaded = details.isSalarySlipUploaded ?? false,
                        isBankStatementUploaded = details.isBankStatementUploaded ?? false,
                        isPrevOfferLetterUploaded = details.isPrevOfferLetterUploaded ?? false,
                        isOfferLetterAttachmentUploaded = details.isOfferLetterAttachmentUploaded ?? false,
                        isPassportPhotoUploaded = details.isPassportPhotoUploaded ?? false,
                        isPanAttachmentUploaded = details.isPanAttachmentUploaded ?? false,
                        isAadharAttachmentUploaded = details.isAadharAttachmentUploaded ?? false,
                        isAadharBackAttachmentUploaded = details.isAadharBackAttachmentUploaded ?? false,
                        isBankPassbookAttachmentUploaded = details.isBankPassbookAttachmentUploaded ?? false,
                        isEducationAttachmentUploaded = details.isEducationAttachmentUploaded ?? false,
                        isResumeAttachmentUploaded = details.isResumeAttachmentUploaded ?? false,
                        IsOtherAttachmentUploaded = details.isOtherAttachmentUploaded ?? false,
                        pfApplicable = details.PFApplicable ?? false,
                        esicApplicable = details.ESICApplicable ?? false,
                        bonusApplicable = details.bonusApplicable ?? "No",
                        fingerprintRegistered = details.fingerprintRegistered,
                        SkillType = details.skillType ?? "",
                        DifferentlyAbled = details.differentlyAbled ?? false,
                        DifferentlyAbledReason = details.differentlyAbledReason ?? "",
                        DifferentlyAbledRemarks = details.differentlyAbledRemarks ?? "",
                        ShiftID = details.ShiftID ?? 1,
                        Source = details.Source ?? "",
                        ReferenceEmployee = details.ReferenceEmployee ?? "",
                        title = details.title ?? "",
                        fullName = details.fullName ?? "",
                        AoCode = details.AoCode ?? "",
                        reportHeadEcode = _context.tblEmployees
                                    .Where(a => a.EmployeeId == details.reportingHeadId)
                                    .Select(r => r.Ecode)
                                    .FirstOrDefault() ?? "",
                        isUanRegistered = details.IsUANRegistered,                          
                    },
                    familyMembersList = JsonConvert.DeserializeObject<List<FamilyMember>>(details.FamilyMembersListJson??"") ?? new List<FamilyMember>(),
                    experienceList = JsonConvert.DeserializeObject<List<Experience>>(details.ExperienceListJson??"") ?? new List<Experience>(),
                    qualificationList = JsonConvert.DeserializeObject<List<Qualification>>(details.QualificationListJson ?? "") ?? new List<Qualification>(),
                    
                    assignLocations = assignLocations,
                   



                 };
                #region BasicInfo
                var employeeData = await GetOneRecordWithTrackingAsync<tblEmployee>(row => row.EmployeeId == employee.candidateInfo.id);

                ////By Gautam for checking if the employee chnage to designation, location, department where there is no seat
                
                //int oldLocationId = employeeData.LocationId ?? 0;
                //int oldDepartmentId = employeeData.DepartmentId ?? 0;
                //int oldDesignationId = employeeData.DesignationId ?? 0;

                //int newLocationId = Convert.ToInt32(employee.candidateInfo.location);
                //int newDepartmentId = Convert.ToInt32(employee.candidateInfo.department);
                //int newDesignationId = Convert.ToInt32(employee.candidateInfo.designation);

                //bool isSeatChange = oldLocationId != newLocationId || oldDepartmentId != newDepartmentId || oldDesignationId != newDesignationId;

                //if (isSeatChange)
                //{
                //    var canCreate = await _context.Database
                //      .SqlQueryRaw<int>
                //      (@"SELECT [Value]FROM (SELECT CAST(dbo.fn_CanCreateEmployee({0},{1},{2}) AS INT) AS [Value]) AS s",
                //            newLocationId,
                //            newDepartmentId,
                //            newDesignationId
                //          ).FirstOrDefaultAsync();
                //    if (canCreate == 0)
                //    {
                //        return BuildExecuteErrorResponse(
                //            "No vacant seat available for the selected Location, Department and Designation.",
                //            HttpStatusCode.BadRequest
                //        );
                //    }
                //}

                ////var candidateData = await GetOneRecordWithTrackingAsync<Data.Candidate>(row => row.Id == employee.CandidateInfo.Cid);
                employeeData.PreferredLocation = employee.candidateInfo.PreferredLocation ?? "";
                employeeData.fingerprintRegistered = employee.candidateInfo.fingerprintRegistered;
                employeeData.TITLE = employee.candidateInfo.title ?? "";
                employeeData.FirstName = employee.candidateInfo.firstName ?? "";
                employeeData.MiddleName = employee.candidateInfo.middleName ?? "";
                employeeData.LastName = employee.candidateInfo.lastName ?? "";
                employeeData.FULL_NAME = employee.candidateInfo.fullName ?? "";
                // candidateData.HUSBAND_NAME = employee.candidateInfo.husbandName ?? "";
                employeeData.DOJ = employee.candidateInfo.joiningDate;
                employeeData.DepartmentId = Convert.ToInt32(employee.candidateInfo.department);
                // candidateData.LOCATION = employee.candidateInfo.location ?? "";
                employeeData.LocationId = Convert.ToInt32(employee.candidateInfo.location);
                employeeData.GROSS_SALARY = decimal.TryParse(employee.candidateInfo.grossSalary, out var grossSalary)
    ? grossSalary
    : 0;
                employeeData.UAN_NO = employee.candidateInfo.uanNo ?? "";
                employeeData.FATHER_S_NAME = employee.candidateInfo.fathersName ?? "";
                employeeData.MOTHER_S_NAME = employee.candidateInfo.mothersName ?? "";
                employeeData.DesignationId = Convert.ToInt32(employee.candidateInfo.designation);
                employeeData.DOB = employee.candidateInfo.dob;
                employeeData.GENDER = employee.candidateInfo.gender ?? "";
                employeeData.PAN_NO = employee.candidateInfo.panNo ?? "";
                employeeData.AADHAR_NO = employee.candidateInfo.aadharNo ?? "";
                employeeData.NAME_ON_ADHAR = employee.candidateInfo.nameOnAadhar ?? "";
                employeeData.PLACE_OF_BIRTH = employee.candidateInfo.placeOfBirth ?? "";
                employeeData.PRESENT_ADDRESS = employee.candidateInfo.presentAddress ?? "";
                employeeData.PRESENT_ADDRESS_PIN_CODE = employee.candidateInfo.presentAddressPinCode ?? "";
                employeeData.PERMANENT_ADDRESS = employee.candidateInfo.permanentAddress ?? "";
                employeeData.PERMANENT_ADDRESS_PIN_CODE = employee.candidateInfo.permanentAddressPinCode ?? "";
                employeeData.MARITIAL_STATUS = employee.candidateInfo.maritalStatus ?? "";
                employeeData.MOBILE = employee.candidateInfo.mobile ?? "";
                employeeData.EMAIL_ADDRESS = employee.candidateInfo.emailAddress ?? "";
                employeeData.NATIONALITY = employee.candidateInfo.nationality ?? "";
                employeeData.RELIGION = employee.candidateInfo.religion ?? "";
                employeeData.BANK_NAME = employee.candidateInfo.bankName ?? "";
                employeeData.A_C_NO = employee.candidateInfo.accountNo ?? "";
                employeeData.BANK_IFSC_CODE = employee.candidateInfo.bankIfscCode ?? "";
                employeeData.StatusId = employee.candidateInfo.statusId;
                employeeData.ApplicantId = employee.candidateInfo.applicandId.ToString();
                employeeData.BENEFICIARY_ADDRESS = employee.candidateInfo.beneficiaryAddress ?? "";
                employeeData.LAST_CTC_ANNUAL_ = decimal.TryParse(employee.candidateInfo.lastCtcAnnual, out var lastCtcAnnual)
    ? lastCtcAnnual
    : 0;
                employeeData.ESICNO = employee.candidateInfo.esicno ?? "";
                employeeData.CONTACT1_OF_LAST_3_COMPANY = employee.candidateInfo.contact1LastCompany ?? "";
                employeeData.CONTACT2_OF_LAST_3_COMPANY1 = employee.candidateInfo.contact2LastCompany ?? "";
                employeeData.CONTACT3_OF_LAST_3_COMPANY11 = employee.candidateInfo.contact3LastCompany ?? "";
                employeeData.CONTACT4_OF_LAST_3_COMPANY11 = employee.candidateInfo.contact4LastCompany ?? "";
                employeeData.CONTACT5_OF_LAST_3_COMPANY111 = employee.candidateInfo.contact5LastCompany ?? "";
                employeeData.COMPANY_1 = employee.candidateInfo.company1 ?? "";
                employeeData.COMPANY_2 = employee.candidateInfo.company2 ?? "";
                employeeData.COMPANY_3 = employee.candidateInfo.company3 ?? "";
                employeeData.Ecode = employee.candidateInfo.empCode ?? "";
                employeeData.REFERENCE = employee.candidateInfo.reference ?? "";
                employeeData.REFERENCE1__OF_LAST_3_COMPANY = employee.candidateInfo.reference1LastCompany ?? "";
                employeeData.REFERENCE2__OF_LAST_3_COMPANY1 = employee.candidateInfo.reference2LastCompany ?? "";
                employeeData.REFERENCE3__OF_LAST_3_COMPANY11 = employee.candidateInfo.reference3LastCompany ?? "";
                employeeData.REFERENCE4__OF_LAST_3_COMPANY11 = employee.candidateInfo.reference4LastCompany ?? "";
                employeeData.REFERENCE5__OF_LAST_3_COMPANY111 = employee.candidateInfo.reference5LastCompany ?? "";
                employeeData.ISRELATIVEINCOMPANY = employee.candidateInfo.isRelativeInCompany;
                employeeData.WORK_LOCATION = employee.candidateInfo.workLocation ?? "";
                employeeData.WEEKLY_OFF = employee.candidateInfo.weeklyOff ?? "";
                employeeData.POSITION_HELD_IN_PREVIOUS_COMPANY = employee.candidateInfo.positionHeldInPreviousCompany ?? "";

                // Audit fields
                employeeData.UpdatedBy = updatedBy;
                employeeData.UpdatedOn = DateTime.UtcNow;

                // Newly added fields
                employeeData.BasicSalary = Convert.ToDecimal(employee.candidateInfo.basicSalary);
                employeeData.HRA = employee.candidateInfo.hra;
                employeeData.CCA = employee.candidateInfo.cca;
                employeeData.SpecialAllowance = employee.candidateInfo.specialAllowance;
                employeeData.DA = employee.candidateInfo.da;
                employeeData.ExtraAllowance = employee.candidateInfo.extraAllowance;
                employeeData.monthlyGrossCTC = employee.candidateInfo.monthlyGrossCtc;
                employeeData.annuallyNetCTC = employee.candidateInfo.annuallyNetCtc;
                employeeData.PFApplicable = employee.candidateInfo.pfApplicable;
                employeeData.BonusApplicable = employee.candidateInfo.bonusApplicable;
                employeeData.ESICApplicable = employee.candidateInfo.esicApplicable;
                employeeData.ReportHeadEcode = employee.candidateInfo.reportHeadEcode;
                employeeData.ShiftID = employee.candidateInfo.ShiftID ?? 1;
                employeeData.IsUANRegistered = employee.candidateInfo.isUanRegistered ?? false;
                employeeData.AOCode = employee.candidateInfo.AoCode ?? "";
                int ra = await _context.SaveChangesAsync();

                //if (ra < 1)
                //    return BuildExecuteErrorResponse("Unable to update Data",HttpStatusCode.BadRequest);
                #endregion BasicInfo
                //  var idPass = employeeData.CandidateId > 0 ? employeeData.CandidateId : employeeData.EmployeeId;


                // by nikhil
                var idPass = employeeData.CandidateId > 0 ? employeeData.CandidateId : employeeData.EmployeeId;

                //var documents = _context.CanidateDocs.AsQueryable().Where(row => row.CId == employeeData.CandidateId).ToList();

                //var documents = _context.CanidateDocs.AsQueryable().Where(row => row.CId == documentQueryId).ToList();
                //var idPass = employeeData.EmployeeId;
                #region Family
                var familyData = _context.tblFamilies.AsQueryable().Where(row => row.CID == idPass).ToList();

                var newFamilyMembers = new List<tblFamily>();
                foreach (var family in familyData) {
                    var match = employee.familyMembersList.AsEnumerable().FirstOrDefault(row => row.id == family.ID);
                    if (match != null) {
                        family.Family_Member_Name = match.familyMemberName;
                        family.Relation = match.relation;
                        family.DOB = match.dob;
                        family.UpdatedBy = updatedBy;
                        family.UpdatedOn = DateTime.UtcNow;
                    }
                    else {

                            family.IsDeleted = true;
                            family.IsActive = false;
                            family.UpdatedOn = DateTime.UtcNow;
                            family.UpdatedBy = updatedBy;
                    }
                }
                var newMembers = employee.familyMembersList.Where(x => x.id == 0 || x.id==null).ToList();
                foreach (var newMember in newMembers)
                {
                    var newEntry = new tblFamily
                    {
                        CID = idPass,
                        Family_Member_Name = newMember.familyMemberName,
                        Relation = newMember.relation,
                        DOB = newMember.dob,
                        CreatedBy = updatedBy,
                        CreatedOn = DateTime.UtcNow,
                        // Set other fields
                    };
                    _context.tblFamilies.Add(newEntry);
                }
                ra = await _context.SaveChangesAsync();
                //if (ra < 1 && (familyData.Count>0 || newMembers.Count>0))
                //    return BuildExecuteErrorResponse("Unable to Save Family Details",HttpStatusCode.BadRequest);
                #endregion Family

                #region Experience
                var experienceData = _context.tblExperiences.AsQueryable().Where(row => row.CID == idPass).ToList();

                var newExperienceData = new List<tblExperience>();
                foreach (var experience in experienceData)
                {
                    var match = employee.experienceList.AsEnumerable().FirstOrDefault(row => row.id == experience.ID);
                    if (match != null)
                    {
                        experience.Name_of_Company = match.nameOfCompany;
                        experience.Work_Location = match.workLocation;
                        experience.Position_Held = match.positionHeld;
                        experience.From = match.from;
                        experience.To = match.to;
                        experience.Last_CTC = match.lastCtc;
                        //experience.InHand = match.inHan;
                        experience.UpdatedBy = updatedBy;
                        experience.UpdatedOn = DateTime.UtcNow;
                    }
                    else
                    {

                        experience.IsDeleted = true;
                        experience.IsActive = false;
                        experience.UpdatedOn = DateTime.UtcNow;
                        experience.UpdatedBy = updatedBy;
                    }
                }
                var newExperiences = employee.experienceList.Where(x => x.id == 0 || x.id == null).ToList();
                foreach (var newExperience in newExperiences)
                {
                    var newEntry = new tblExperience
                    {
                        CID = idPass,
                        Name_of_Company = newExperience.nameOfCompany,
                        Work_Location = newExperience.workLocation,
                        Position_Held = newExperience.positionHeld,
                        From = newExperience.from,
                        To = newExperience.to,
                        Last_CTC = newExperience.lastCtc,   
                        //InHand = newExperience.inHand,
                        CreatedBy = updatedBy,
                        CreatedOn = DateTime.UtcNow,
                        // Set other fields
                    };
                    _context.tblExperiences.Add(newEntry);
                }
                ra = await _context.SaveChangesAsync();
                //if (ra < 1)
                //    return BuildExecuteErrorResponse("Unable to Save Experience Details", HttpStatusCode.BadRequest);

                #endregion Experience

                #region Qualification
                var qualificationData = _context.tblQualifications.AsQueryable().Where(row => row.CID == idPass).ToList();

                var newQualificationData = new List<tblQualification>();
                foreach (var qualification in qualificationData)
                {
                    var match = employee.qualificationList.AsEnumerable().FirstOrDefault(row => row.id == qualification.ID);
                    if (match != null)
                    {
                        qualification.Education = match.education;
                        qualification.YOP = match.yop;
                        qualification.Grade = match.grade;
                        qualification.Type = match.type;
                        qualification.UpdatedBy = updatedBy;
                        qualification.UpdatedOn = DateTime.UtcNow;
                    }
                    else
                    {

                        qualification.IsDeleted = true;
                        qualification.IsActive = false;
                        qualification.UpdatedOn = DateTime.UtcNow;
                        qualification.UpdatedBy = updatedBy;
                    }
                }
                var newQualifications = employee.qualificationList.Where(x => x.id == 0 || x.id == null).ToList();
                foreach (var newQualification in newQualifications)
                {
                    var newEntry = new tblQualification
                    {
                        CID = idPass,
                        Education = newQualification.education,
                        YOP = newQualification.yop,
                        Grade = newQualification.grade,
                        Type = newQualification.type,
                        CreatedBy = updatedBy,
                        CreatedOn = DateTime.UtcNow,
                        // Set other fields
                    };
                    _context.tblQualifications.Add(newEntry);
                }
                ra = await _context.SaveChangesAsync();
                //if (ra < 1)
                //    return BuildExecuteErrorResponse("Unable to Save Experience Details", HttpStatusCode.BadRequest);

                #endregion Qualification

                #region AssignedLocation
                //var assignedLocations = _context.AssignLocationHistories.AsQueryable().Where(row => row.EmployeeId == employeeData.EmployeeId).ToList();
                var assignedLocations = _context.AssignLocationHistories.AsQueryable().Where(row => row.EmployeeId == idPass).ToList();

                //var newAssignedLocations = new List<AssignLocationHistory>();
                //foreach (var assignLocation in assignedLocations)
                //{
                //    var match = employee.assignLocations.id==assignLocation.AssignLocationHistoryId?employee.assignLocations:null;
                //    if (match != null)
                //    {
                //        assignLocation.AssignedLocation = match.assignedLocation;
                //        assignLocation.AssignedReason = match.assignedReason;
                //        assignLocation.AssignedOnDate = DateTime.UtcNow;
                //        assignLocation.ReleasedOnDate = match.releasedOnDate;
                //    }
                //    //else
                //    //{
                //    //    _context.AssignLocationHistories.Remove(assignLocation);
                //    //}
                //}
                var newAssignLocation = employee.assignLocations;
                //foreach (var newAssignLocation in newAssignLocations)
                //{
                try
                {
                    var newEntry1 = new AssignLocationHistory
                    {
                        EmployeeId = idPass,
                        AssignedLocation = newAssignLocation.assignedLocation,
                        AssignedReason = newAssignLocation.assignedReason,
                        IsActive = true,
                        AssignedOnDate = newAssignLocation.assignedOnDate,
                        ReleasedOnDate = newAssignLocation.releasedOnDate,
                        TemporaryTransfer = newAssignLocation.TemporaryTransfer,
                        PermanentTransfer = newAssignLocation.PermanentTransfer,
                        TransferApprovalStatus = newAssignLocation.TransferApprovalStatus,
                        IsReportingHeadApproval = newAssignLocation.IsReportingHeadApproval,
                        IsHRApproval = newAssignLocation.IsHRApproval,
                        departmentid = newAssignLocation.DepartmentId,
                        designationid = newAssignLocation.DesignationId,
                        
                        // Set other fields
                    };
                    _context.AssignLocationHistories.Add(newEntry1);
                    //}
                    ra = await _context.SaveChangesAsync();
                }
                catch (Exception ex) { }
                //if (ra < 1)
                //    return BuildExecuteErrorResponse("Unable to Save AssignLocation Details", HttpStatusCode.BadRequest);

                #endregion AssignedLocation

                #region Documents
                var documentQueryId = employeeData.CandidateId > 0 ? employeeData.CandidateId : employeeData.EmployeeId;
               
                //var documents = _context.CanidateDocs.AsQueryable().Where(row => row.CId == employeeData.CandidateId).ToList();

                var documents = _context.CanidateDocs.AsQueryable().Where(row => row.CId == documentQueryId).ToList();


                //foreach (var document in documents)
                //{
                //    var match = employee.documents.AsEnumerable().FirstOrDefault(row => row.id == document.Id);
                //    if (match == null)
                //    {
                //        _context.CanidateDocs.Remove(document);
                //    }
                //}

                var candidateDocs = new CandidateDocs
                {
                    PassportPhoto = employee.NewDocs?.PassportPhoto,
                    Last3SalarySlip = employee.NewDocs?.Last3SalarySlip ?? new List<IFormFile>(),
                    Last3BankStatement = employee.NewDocs?.Last3BankStatement,
                    PrevOfferLetter = employee.NewDocs?.PrevOfferLetter,
                    PanAttachment = employee.NewDocs?.PanAttachment ?? new List<IFormFile>(),
                    AadharAttachment = employee.NewDocs?.AadharAttachment ?? new List<IFormFile>(),
                    AadharBackAttachment = files?.AadharBackAttachment ?? new List<IFormFile>(),
                    BankPassbookAttachment = employee.NewDocs?.BankPassbookAttachment ?? new List<IFormFile>(),
                    EducationAttachment = employee.NewDocs?.EducationAttachment ?? new List<IFormFile>(),
                    ResumeAttachment = employee.NewDocs?.ResumeAttachment ?? new List<IFormFile>(),
                    OtherAttachment = employee.NewDocs?.OtherAttachment ?? new List<IFormFile>(),
                    OfferLetterAttachment = employee.NewDocs?.OfferLetterAttachment ?? new List<IFormFile>(),
                    BankStatementVideo = employee.NewDocs?.BankStatementVideo ?? new List<IFormFile>(),
                };
                employeeData.IsPassportPhotoUploaded = details.isPassportPhotoUploaded ?? false;
                employeeData.IsSalarySlipUploaded = details.isSalarySlipUploaded ?? false;
                employeeData.IsBankPassbookAttachmentUpoaded = details.isBankStatementUploaded ?? false;
                employeeData.IsPrevOfferLetterUploaded = details.isPrevOfferLetterUploaded ?? false;
                employeeData.IsPanAttachmentUploaded = details.isPanAttachmentUploaded ?? false;
                employeeData.IsAadharAttachmentUploaded = details.isAadharAttachmentUploaded ?? false;
                employeeData.IsAadharBackAttachmentUploaded = details.isAadharBackAttachmentUploaded ?? false;
                employeeData.IsBankPassbookAttachmentUpoaded = details.isBankPassbookAttachmentUploaded ?? false;
                employeeData.IsEducationAttachmentUploaded = details.isEducationAttachmentUploaded ?? false;
                employeeData.IsResumeUploaded = details.isResumeAttachmentUploaded ?? false;
                employeeData.IsOtherAttachmentUploaded = details.isOtherAttachmentUploaded ?? false;
                employeeData.IsPrevOfferLetterUploaded = details.isOfferLetterAttachmentUploaded ?? false;
                //employeeData.Off
                await SaveNewAttachments(Convert.ToInt64(idPass), employeeData.EMAIL_ADDRESS, files, updatedBy);
                try
                {
                    ra = await _context.SaveChangesAsync();
                }
                catch (Exception ex) { }
                //if (ra < 1)
                //    return BuildExecuteErrorResponse("Unable to Save AssignLocation Details", HttpStatusCode.BadRequest);
                #endregion Documents

                return BuildExecuteSuccessResponse("Updated Successfully");


            }
            catch (Exception ex) { 
                return BuildExecuteErrorResponse(ex.Message,HttpStatusCode.BadRequest);
            }
        }
        public async Task<ExecuteAndReponse> UpdateEmployeeWithExcel(IFormFile file, string updatedBy)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return BuildExecuteErrorResponse("No file uploaded", HttpStatusCode.BadRequest);

                var now = DateTime.Now;
                var year = now.Year.ToString();
                var month = now.ToString("MMM");
                var dateString = now.ToString("ddMMyyyyHHmmssfff");
                var folderPath = Path.Combine("wwwroot", year, month, dateString, updatedBy,"EmpMaster");

                // Ensure the directory exists
                Directory.CreateDirectory(folderPath);

                // Save the uploaded file
                var fileName = Path.GetFileName(file.FileName);
                var savePath = Path.Combine(folderPath, fileName);

                using (var fileStream = new FileStream(savePath, FileMode.Create))
                {
                    await file.CopyToAsync(fileStream);
                }

                using (var stream = file.OpenReadStream())
                using (var workbook = new XLWorkbook(stream))
                {
                    var worksheet = workbook.Worksheet(1); // 1-based index

                    // Header validation
                    string[] expectedHeaders = new string[]
                    {
                        "Employee Code", "First Name", "Middle Name", "Last Name", "Full Name", "Gender", "Husband Name", "Aadhar Number", "Father's Name", "Place of Birth",
                        "Name on Aadhar", "Mother's Name", "PAN Number", "Date of Birth", "Date of Joining", "Basic Salary", "CCA", "Special Allowance", "Location", "Department",
                        "Present Address", "Present Address Pin Code", "Gross Salary", "DA", "Permanent Address", "Permanent Address Pin Code", "HRA", "Extra Allowance", "Designation",
                        "Monthly Gross CTC", "Annually Net CTC", "UAN Number", "PF Applicable", "Bonus Applicable", "ESIC Applicable", "ESIC Number", "Company", "Reporting Manager Ecode",
                        "Marital Status", "Mobile", "Email Address", "Beneficiary Address", "Nationality", "Religion", "Bank Name", "Account Number", "Bank IFSC Code", "Is Relative in Company",
                        "Reference", "Store Code",
                        "Reimbersment", "Fuel_and_Maintainence", "Books_and_Periodicals", "Professional Attire", "Driver Wages", "Meal Voucher", "Mobile Bill","IsExtraDayApplicable","AO Code"
                    };
                    var headerRow = worksheet.Row(1);
                    int cellCount = headerRow.CellsUsed().Count();
                    if (expectedHeaders.Length != cellCount)
                    {
                        return BuildExecuteErrorResponse($"Column count mismatch: Expected {expectedHeaders.Length} columns, found {cellCount}. Please follow the correct format.", HttpStatusCode.BadRequest);
                    }
                    for (int i = 0; i < expectedHeaders.Length; i++)
                    {
                        var cellValue = headerRow.Cell(i + 1).GetValue<string>().Trim();
                        if (!string.Equals(cellValue, expectedHeaders[i], StringComparison.OrdinalIgnoreCase))
                        {
                            return BuildExecuteErrorResponse($"Header mismatch at column {i + 1}: Expected '{expectedHeaders[i]}', found '{cellValue}'", HttpStatusCode.BadRequest);
                        }
                    }
                    var rows = worksheet.RowsUsed().Skip(1); // Skip header row

                    // Duplicate ECODE check
                    var ecodeList = rows
                        .Select(r => r.Cell(1).GetValue<string>()?.Trim())
                        .Where(e => !string.IsNullOrWhiteSpace(e))
                        .ToList();

                    var duplicateEcodes = ecodeList
                        .GroupBy(e => e, StringComparer.OrdinalIgnoreCase)
                        .Where(g => g.Count() > 1)
                        .Select(g => g.Key)
                        .ToList();

                    if (duplicateEcodes.Any())
                    {
                        return BuildExecuteErrorResponse(
                            $"Duplicate ECODE(s) found in Excel: {string.Join(", ", duplicateEcodes)}. Each ECODE must be unique.",
                            HttpStatusCode.BadRequest
                        );
                    }

                    foreach (var row in rows)
                    {
                        // Column 1: Employee Code (Ecode)
                        var empCode = row.Cell(1).GetValue<string>();
                        if (string.IsNullOrWhiteSpace(empCode)) continue;

                        var employee = await _context.tblEmployees.FirstOrDefaultAsync(e => e.Ecode.Trim().ToLower() == empCode.Trim().ToLower());
                        if (employee == null) continue;

                        // Column 2: First Name
                        var firstName = row.Cell(2).GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(firstName)) employee.FirstName = firstName;

                        // Column 3: Middle Name
                        var middleName = row.Cell(3).GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(middleName)) employee.MiddleName = middleName;

                        // Column 4: Last Name
                        var lastName = row.Cell(4).GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(lastName)) employee.LastName = lastName;

                        // Column 5: Full Name
                        var fullName = row.Cell(5).GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(fullName)) employee.FULL_NAME = fullName;

                        // Column 6: Gender
                        var gender = row.Cell(6).GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(gender)) employee.GENDER = gender;

                        // Column 7: Husband Name
                        var husbandName = row.Cell(7).GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(husbandName)) employee.Husband_Name = husbandName;

                        // Column 8: Aadhar Number
                        var aadharNo = row.Cell(8).GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(aadharNo)) employee.AADHAR_NO = aadharNo;

                        // Column 9: Father's Name
                        var fatherName = row.Cell(9).GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(fatherName)) employee.FATHER_S_NAME = fatherName;

                        // Column 10: Place of Birth
                        var placeOfBirth = row.Cell(10).GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(placeOfBirth)) employee.PLACE_OF_BIRTH = placeOfBirth;

                        // Column 11: Name on Aadhar
                        var nameOnAadhar = row.Cell(11).GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(nameOnAadhar)) employee.NAME_ON_ADHAR = nameOnAadhar;

                        // Column 12: Mother's Name
                        var motherName = row.Cell(12).GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(motherName)) employee.MOTHER_S_NAME = motherName;

                        // Column 13: PAN Number
                        var panNo = row.Cell(13).GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(panNo)) employee.PAN_NO = panNo;

                        // Column 14: Date of Birth
                        if (DateTime.TryParse(row.Cell(14).GetValue<string>(), out DateTime dob))
                            employee.DOB = dob;

                        // Column 15: Date of Joining
                        if (DateTime.TryParse(row.Cell(15).GetValue<string>(), out DateTime joiningDate))
                            employee.DOJ = joiningDate;

                        // Column 16: Basic Salary
                        if (decimal.TryParse(row.Cell(16).GetValue<string>(), out decimal basicSalary))
                            employee.BasicSalary = basicSalary;

                        // Column 17: CCA
                        if (decimal.TryParse(row.Cell(17).GetValue<string>(), out decimal cca))
                            employee.CCA = cca;

                        // Column 18: Special Allowance
                        if (decimal.TryParse(row.Cell(18).GetValue<string>(), out decimal specialAllowance))
                            employee.SpecialAllowance = specialAllowance;

                        // Column 19: Location
                        var locationName = row.Cell(19).GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(locationName))
                        {
                            var location = await _context.tblLocations.FirstOrDefaultAsync(l => l.LocationName == locationName);
                            if (location != null)
                                employee.LocationId = location.LocationId;
                        }

                        // Column 20: Department
                        var deptName = row.Cell(20).GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(deptName))
                        {
                            var dept = await _context.tblDepartments.FirstOrDefaultAsync(d => d.DepartmentName == deptName);
                            if (dept != null)
                                employee.DepartmentId = dept.DepartmentId;
                        }

                        // Column 21: Present Address
                        var presentAddress = row.Cell(21).GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(presentAddress)) employee.PRESENT_ADDRESS = presentAddress;

                        // Column 22: Present Address Pin Code
                        var presentAddressPin = row.Cell(22).GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(presentAddressPin)) employee.PRESENT_ADDRESS_PIN_CODE = presentAddressPin;

                        // Column 23: Gross Salary
                        if (decimal.TryParse(row.Cell(23).GetValue<string>(), out decimal grossSalary))
                            employee.GROSS_SALARY = grossSalary;

                        // Column 24: DA
                        if (decimal.TryParse(row.Cell(24).GetValue<string>(), out decimal da))
                            employee.DA = da;

                        // Column 25: Permanent Address
                        var permanentAddress = row.Cell(25).GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(permanentAddress)) employee.PERMANENT_ADDRESS = permanentAddress;

                        // Column 26: Permanent Address Pin Code
                        var permanentAddressPin = row.Cell(26).GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(permanentAddressPin)) employee.PERMANENT_ADDRESS_PIN_CODE = permanentAddressPin;

                        // Column 27: HRA
                        if (decimal.TryParse(row.Cell(27).GetValue<string>(), out decimal hra))
                            employee.HRA = hra;

                        // Column 28: Extra Allowance
                        if (decimal.TryParse(row.Cell(28).GetValue<string>(), out decimal extraAllowance))
                            employee.ExtraAllowance = extraAllowance;

                        // Column 29: Designation
                        var designationName = row.Cell(29).GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(designationName))
                        {
                            var designation = await _context.tblDesignations.FirstOrDefaultAsync(d => d.DesignationName == designationName);
                            if (designation != null)
                                employee.DesignationId = designation.DesignationId;
                        }

                        // Column 30: Monthly Gross CTC
                        if (decimal.TryParse(row.Cell(30).GetValue<string>(), out decimal monthlyGrossCTC))
                            employee.monthlyGrossCTC = monthlyGrossCTC;

                        // Column 31: Annually Net CTC
                        if (decimal.TryParse(row.Cell(31).GetValue<string>(), out decimal annuallyNetCTC))
                            employee.annuallyNetCTC = annuallyNetCTC;

                        // Column 32: UAN Number
                        var uanNo = row.Cell(32).GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(uanNo)) employee.UAN_NO = uanNo;

                        // Column 33: PF Applicable
                        var pfApplicable = row.Cell(33).GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(pfApplicable))
                            employee.PFApplicable = pfApplicable.Trim().ToLower() == "yes";

                        // Column 34: Bonus Applicable
                        var bonusApplicable = row.Cell(34).GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(bonusApplicable))
                        {
                            var b = bonusApplicable.Trim().ToLower();
                            employee.BonusApplicable = b switch { "stat" => "Stat", "ctc" => "Ctc", "yes" => "Ctc", _ => "No" };
                        }

                        // Column 35: ESIC Applicable
                        var esicApplicable = row.Cell(35).GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(esicApplicable))
                            employee.ESICApplicable = esicApplicable.Trim().ToLower() == "yes";

                        // Column 36: ESIC Number
                        var esicNo = row.Cell(36).GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(esicNo)) employee.ESICNO = esicNo;

                        // Column 37: Company
                        var companyName = row.Cell(37).GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(companyName))
                        {
                            var company = await _context.tblCompanies.FirstOrDefaultAsync(c => c.CompanyName == companyName);
                            if (company != null)
                                employee.CompanyId = company.CompanyId;
                        }

                        // Column 38: Reporting Manager Ecode
                        var reportingManagerEcode = row.Cell(38).GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(reportingManagerEcode))
                            employee.ReportHeadEcode = reportingManagerEcode;

                        // Column 39: Marital Status
                        var maritalStatus = row.Cell(39).GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(maritalStatus)) employee.MARITIAL_STATUS = maritalStatus;

                        // Column 40: Mobile
                        var mobile = row.Cell(40).GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(mobile)) employee.MOBILE = mobile;

                        // Column 41: Email Address
                        var email = row.Cell(41).GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(email)) employee.EMAIL_ADDRESS = email;

                        // Column 42: Beneficiary Address
                        var beneficiaryAddress = row.Cell(42).GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(beneficiaryAddress)) employee.BENEFICIARY_ADDRESS = beneficiaryAddress;

                        // Column 43: Nationality
                        var nationality = row.Cell(43).GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(nationality)) employee.NATIONALITY = nationality;

                        // Column 44: Religion
                        var religion = row.Cell(44).GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(religion)) employee.RELIGION = religion;

                        // Column 45: Bank Name
                        var bankName = row.Cell(45).GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(bankName)) employee.BANK_NAME = bankName;

                        // Column 46: Account Number
                        var accountNo = row.Cell(46).GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(accountNo)) employee.A_C_NO = accountNo;

                        // Column 47: Bank IFSC Code
                        var bankIfsc = row.Cell(47).GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(bankIfsc)) employee.BANK_IFSC_CODE = bankIfsc;

                        // Column 48: Is Relative in Company
                        var isRelative = row.Cell(48).GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(isRelative))
                            employee.ISRELATIVEINCOMPANY = isRelative.Trim().ToLower() == "yes";

                        // Column 49: Reference
                        var reference = row.Cell(49).GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(reference)) employee.REFERENCE = reference;
                        var storeCode = row.Cell(50).GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(storeCode))
                        {
                            var location = await _context.tblLocations.FirstOrDefaultAsync(l => l.STCode.Trim().ToLower() == storeCode.Trim().ToLower());
                            if (location != null)
                                employee.LocationId = location.LocationId;
                        }
                        // New columns: 51-57
                        if (decimal.TryParse(row.Cell(51).GetValue<string>(), out decimal reimbersment))
                            employee.Reimbersment = reimbersment;
                        if (decimal.TryParse(row.Cell(52).GetValue<string>(), out decimal fuelAndMaintainence))
                            employee.Fuel_and_Maintainence = fuelAndMaintainence;
                        if (decimal.TryParse(row.Cell(53).GetValue<string>(), out decimal booksAndPeriodicals))
                            employee.Books_and_Periodicals = booksAndPeriodicals;
                        if (decimal.TryParse(row.Cell(54).GetValue<string>(), out decimal professionalAttire))
                            employee.Professional_Attire = professionalAttire;
                        if (decimal.TryParse(row.Cell(55).GetValue<string>(), out decimal driverWages))
                            employee.Driver_Wages = driverWages;
                        if (decimal.TryParse(row.Cell(56).GetValue<string>(), out decimal mealVoucher))
                            employee.Meal_Voucher = mealVoucher;
                        if (decimal.TryParse(row.Cell(57).GetValue<string>(), out decimal mobileBill))
                            employee.Mobile_Bill = mobileBill;
                        var extradaysApplicable = row.Cell(58).GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(extradaysApplicable))
                            employee.IsExtraDayApplicable = extradaysApplicable.Trim().ToLower() == "yes";
                        var aoCode = row.Cell(59).GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(aoCode)) employee.AOCode = aoCode;
                        // Update audit fields
                        employee.UpdatedBy = updatedBy;
                        employee.UpdatedOn = DateTime.UtcNow;
                    }

                    int ra = await _context.SaveChangesAsync();
                    if (ra < 1) return BuildExecuteErrorResponse("Unable to update data.", HttpStatusCode.BadRequest);
                    return BuildExecuteSuccessResponse("Employee records updated successfully");
                }
            }
            catch (Exception ex)
            {
                return BuildExecuteErrorResponse($"Error updating employee records: {ex.Message}", HttpStatusCode.BadRequest);
            }
        }

        public async Task<ExecuteAndReponse> BulkInsertEmployeesWithExcel(IFormFile file, string createdBy)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return BuildExecuteErrorResponse("No file uploaded", HttpStatusCode.BadRequest);

                var now = DateTime.Now;
                var folderPath = Path.Combine("wwwroot", now.Year.ToString(), now.ToString("MMM"),
                    now.ToString("ddMMyyyyHHmmssfff"), createdBy, "EmpBulkInsert");
                Directory.CreateDirectory(folderPath);

                var fileName = Path.GetFileName(file.FileName);
                var savePath = Path.Combine(folderPath, fileName);
                using (var fileStream = new FileStream(savePath, FileMode.Create))
                {
                    await file.CopyToAsync(fileStream);
                }

                using var stream = file.OpenReadStream();
                using var workbook = new XLWorkbook(stream);
                var worksheet = workbook.Worksheet(1);

                // Header validation — same 59-column format
                string[] expectedHeaders = new string[]
                {
                    "Employee Code", "First Name", "Middle Name", "Last Name", "Full Name", "Gender", "Husband Name", "Aadhar Number", "Father's Name", "Place of Birth",
                    "Name on Aadhar", "Mother's Name", "PAN Number", "Date of Birth", "Date of Joining", "Basic Salary", "CCA", "Special Allowance", "Location", "Department",
                    "Present Address", "Present Address Pin Code", "Gross Salary", "DA", "Permanent Address", "Permanent Address Pin Code", "HRA", "Extra Allowance", "Designation",
                    "Monthly Gross CTC", "Annually Net CTC", "UAN Number", "PF Applicable", "Bonus Applicable", "ESIC Applicable", "ESIC Number", "Company", "Reporting Manager Ecode",
                    "Marital Status", "Mobile", "Email Address", "Beneficiary Address", "Nationality", "Religion", "Bank Name", "Account Number", "Bank IFSC Code", "Is Relative in Company",
                    "Reference", "Store Code",
                    "Reimbersment", "Fuel_and_Maintainence", "Books_and_Periodicals", "Professional Attire", "Driver Wages", "Meal Voucher", "Mobile Bill", "IsExtraDayApplicable", "AO Code"
                };

                var headerRow = worksheet.Row(1);
                int cellCount = headerRow.CellsUsed().Count();
                if (expectedHeaders.Length != cellCount)
                    return BuildExecuteErrorResponse($"Column count mismatch: Expected {expectedHeaders.Length} columns, found {cellCount}. Please follow the correct format.", HttpStatusCode.BadRequest);

                for (int i = 0; i < expectedHeaders.Length; i++)
                {
                    var cellValue = headerRow.Cell(i + 1).GetValue<string>().Trim();
                    if (!string.Equals(cellValue, expectedHeaders[i], StringComparison.OrdinalIgnoreCase))
                        return BuildExecuteErrorResponse($"Header mismatch at column {i + 1}: Expected '{expectedHeaders[i]}', found '{cellValue}'", HttpStatusCode.BadRequest);
                }

                var rows = worksheet.RowsUsed().Skip(1).ToList();
                if (!rows.Any())
                    return BuildExecuteErrorResponse("No data rows found in the Excel file.", HttpStatusCode.BadRequest);

                // Validate: Company column (37) is required for every row
                for (int idx = 0; idx < rows.Count; idx++)
                {
                    var companyVal = rows[idx].Cell(37).GetValue<string>()?.Trim();
                    if (string.IsNullOrWhiteSpace(companyVal))
                        return BuildExecuteErrorResponse($"Company is required at row {idx + 2}.", HttpStatusCode.BadRequest);
                }

                // Validate: Mobile (40) required
                for (int idx = 0; idx < rows.Count; idx++)
                {
                    var mobile = rows[idx].Cell(40).GetValue<string>()?.Trim();
                    if (string.IsNullOrWhiteSpace(mobile))
                        return BuildExecuteErrorResponse($"Mobile is required at row {idx + 2}.", HttpStatusCode.BadRequest);
                }

                // Check duplicate mobiles within the file
                var mobileList = rows.Select(r => r.Cell(40).GetValue<string>()?.Trim()).Where(m => !string.IsNullOrWhiteSpace(m)).ToList();
                var dupMobiles = mobileList.GroupBy(m => m).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
                if (dupMobiles.Any())
                    return BuildExecuteErrorResponse($"Duplicate Mobile(s) found: {string.Join(", ", dupMobiles)}.", HttpStatusCode.BadRequest);

                // Default password
                string defaultPassword = "V2@123";
                string hashedPassword = BCrypt.Net.BCrypt.HashPassword(defaultPassword);

                int insertedCount = 0;
                var errors = new List<string>();

                foreach (var row in rows)
                {
                    var rowNum = row.RowNumber();
                    try
                    {
                        // Resolve Company
                        var companyName = row.Cell(37).GetValue<string>()?.Trim();
                        var company = await _context.tblCompanies.FirstOrDefaultAsync(c => c.CompanyName.Trim().ToLower() == companyName.Trim().ToLower());
                        if (company == null)
                        {
                            errors.Add($"Row {rowNum}: Company '{companyName}' not found.");
                            continue;
                        }

                        // Determine prefix and pad length
                        string prefix;
                        int padLength;
                        switch (company.CompanyId)
                        {
                            case 1: prefix = "V"; padLength = 5; break;
                            case 2: prefix = "V2S"; padLength = 4; break;
                            case 3: prefix = "PT"; padLength = 5; break;
                            case 4: prefix = "CT"; padLength = 5; break;
                            case 6: prefix = "E"; padLength = 4; break;
                            default: errors.Add($"Row {rowNum}: Unknown CompanyId {company.CompanyId}."); continue;
                        }

                        // Generate next ecode
                        var lastEmployee = await _context.tblEmployees
                            .Where(e => e.Ecode.StartsWith(prefix) && e.CompanyId == company.CompanyId)
                            .OrderByDescending(e => e.EmployeeId)
                            .FirstOrDefaultAsync();

                        int nextNumber;
                        if (lastEmployee != null && lastEmployee.Ecode.Length > prefix.Length &&
                            int.TryParse(lastEmployee.Ecode.Substring(prefix.Length), out int lastNum))
                        {
                            nextNumber = lastNum + 1;
                        }
                        else
                        {
                            nextNumber = company.CompanyId == 2 ? 2701 : 1;
                        }

                        string newEcode = prefix + nextNumber.ToString().PadLeft(padLength, '0');

                        // Check duplicate
                        if (await _context.tblEmployees.AnyAsync(e => e.Ecode == newEcode))
                        {
                            errors.Add($"Row {rowNum}: Generated Ecode '{newEcode}' already exists.");
                            continue;
                        }

                        // Read fields from Excel
                        var firstName = row.Cell(2).GetValue<string>()?.Trim();
                        var middleName = row.Cell(3).GetValue<string>()?.Trim();
                        var lastName = row.Cell(4).GetValue<string>()?.Trim();
                        var fullName = row.Cell(5).GetValue<string>()?.Trim();
                        if (string.IsNullOrWhiteSpace(fullName))
                            fullName = string.Join(" ", new[] { firstName, middleName, lastName }.Where(n => !string.IsNullOrWhiteSpace(n)));

                        var emp = new tblEmployee
                        {
                            Ecode = newEcode,
                            FirstName = Truncate(firstName, 100),
                            MiddleName = Truncate(middleName, 100),
                            LastName = Truncate(lastName, 100),
                            FULL_NAME = Truncate(fullName, 255),
                            GENDER = Truncate(row.Cell(6).GetValue<string>()?.Trim(), 10),
                            Husband_Name = Truncate(row.Cell(7).GetValue<string>()?.Trim(), 100),
                            AADHAR_NO = Truncate(row.Cell(8).GetValue<string>()?.Trim(), 20),
                            FATHER_S_NAME = Truncate(row.Cell(9).GetValue<string>()?.Trim(), 100),
                            PLACE_OF_BIRTH = Truncate(row.Cell(10).GetValue<string>()?.Trim(), 100),
                            NAME_ON_ADHAR = Truncate(row.Cell(11).GetValue<string>()?.Trim(), 100),
                            MOTHER_S_NAME = Truncate(row.Cell(12).GetValue<string>()?.Trim(), 100),
                            PAN_NO = Truncate(row.Cell(13).GetValue<string>()?.Trim(), 50),
                            PRESENT_ADDRESS = Truncate(row.Cell(21).GetValue<string>()?.Trim(), 255),
                            PRESENT_ADDRESS_PIN_CODE = Truncate(row.Cell(22).GetValue<string>()?.Trim(), 10),
                            PERMANENT_ADDRESS = Truncate(row.Cell(25).GetValue<string>()?.Trim(), 255),
                            PERMANENT_ADDRESS_PIN_CODE = Truncate(row.Cell(26).GetValue<string>()?.Trim(), 10),
                            UAN_NO = Truncate(row.Cell(32).GetValue<string>()?.Trim(), 50),
                            ESICNO = Truncate(row.Cell(36).GetValue<string>()?.Trim(), 100),
                            ReportHeadEcode = Truncate(row.Cell(38).GetValue<string>()?.Trim(), 50),
                            MARITIAL_STATUS = Truncate(row.Cell(39).GetValue<string>()?.Trim(), 20),
                            MOBILE = Truncate(row.Cell(40).GetValue<string>()?.Trim(), 20),
                            EMAIL_ADDRESS = Truncate(row.Cell(41).GetValue<string>()?.Trim(), 100),
                            BENEFICIARY_ADDRESS = Truncate(row.Cell(42).GetValue<string>()?.Trim(), 500),
                            NATIONALITY = Truncate(row.Cell(43).GetValue<string>()?.Trim(), 50),
                            RELIGION = Truncate(row.Cell(44).GetValue<string>()?.Trim(), 50),
                            BANK_NAME = Truncate(row.Cell(45).GetValue<string>()?.Trim(), 100),
                            A_C_NO = Truncate(row.Cell(46).GetValue<string>()?.Trim(), 30),
                            BANK_IFSC_CODE = Truncate(row.Cell(47).GetValue<string>()?.Trim(), 15),
                            REFERENCE = Truncate(row.Cell(49).GetValue<string>()?.Trim(), 255),
                            AOCode = Truncate(row.Cell(59).GetValue<string>()?.Trim(), 255),
                            CompanyId = company.CompanyId,
                            PasswordHash = hashedPassword,
                            Password = defaultPassword,
                            IsActive = true,
                            IsDeleted = false,
                            CreatedOn = DateTime.UtcNow,
                            CreatedBy = createdBy,
                            ShiftID = 1,
                        };

                        // Date fields
                        if (DateTime.TryParse(row.Cell(14).GetValue<string>(), out DateTime dob))
                            emp.DOB = dob;
                        if (DateTime.TryParse(row.Cell(15).GetValue<string>(), out DateTime doj))
                            emp.DOJ = doj;
                        else
                            emp.DOJ = DateTime.Now;

                        // Decimal fields
                        if (decimal.TryParse(row.Cell(16).GetValue<string>(), out decimal basicSalary)) emp.BasicSalary = basicSalary;
                        if (decimal.TryParse(row.Cell(17).GetValue<string>(), out decimal cca)) emp.CCA = cca;
                        if (decimal.TryParse(row.Cell(18).GetValue<string>(), out decimal specialAllowance)) emp.SpecialAllowance = specialAllowance;
                        if (decimal.TryParse(row.Cell(23).GetValue<string>(), out decimal grossSalary)) emp.GROSS_SALARY = grossSalary;
                        if (decimal.TryParse(row.Cell(24).GetValue<string>(), out decimal da)) emp.DA = da;
                        if (decimal.TryParse(row.Cell(27).GetValue<string>(), out decimal hra)) emp.HRA = hra;
                        if (decimal.TryParse(row.Cell(28).GetValue<string>(), out decimal extraAllowance)) emp.ExtraAllowance = extraAllowance;
                        if (decimal.TryParse(row.Cell(30).GetValue<string>(), out decimal monthlyGrossCTC)) emp.monthlyGrossCTC = monthlyGrossCTC;
                        if (decimal.TryParse(row.Cell(31).GetValue<string>(), out decimal annuallyNetCTC)) emp.annuallyNetCTC = annuallyNetCTC;
                        if (decimal.TryParse(row.Cell(51).GetValue<string>(), out decimal reimbersment)) emp.Reimbersment = reimbersment;
                        if (decimal.TryParse(row.Cell(52).GetValue<string>(), out decimal fuel)) emp.Fuel_and_Maintainence = fuel;
                        if (decimal.TryParse(row.Cell(53).GetValue<string>(), out decimal books)) emp.Books_and_Periodicals = books;
                        if (decimal.TryParse(row.Cell(54).GetValue<string>(), out decimal attire)) emp.Professional_Attire = attire;
                        if (decimal.TryParse(row.Cell(55).GetValue<string>(), out decimal driverWages)) emp.Driver_Wages = driverWages;
                        if (decimal.TryParse(row.Cell(56).GetValue<string>(), out decimal mealVoucher)) emp.Meal_Voucher = mealVoucher;
                        if (decimal.TryParse(row.Cell(57).GetValue<string>(), out decimal mobileBill)) emp.Mobile_Bill = mobileBill;

                        // Boolean fields
                        var pfVal = row.Cell(33).GetValue<string>()?.Trim();
                        if (!string.IsNullOrWhiteSpace(pfVal)) emp.PFApplicable = pfVal.ToLower() == "yes";
                        var bonusVal = row.Cell(34).GetValue<string>()?.Trim();
                        if (!string.IsNullOrWhiteSpace(bonusVal)) emp.BonusApplicable = bonusVal.ToLower() == "yes" ? "Ctc" : "No";
                        var esicVal = row.Cell(35).GetValue<string>()?.Trim();
                        if (!string.IsNullOrWhiteSpace(esicVal)) emp.ESICApplicable = esicVal.ToLower() == "yes";
                        var relativeVal = row.Cell(48).GetValue<string>()?.Trim();
                        if (!string.IsNullOrWhiteSpace(relativeVal)) emp.ISRELATIVEINCOMPANY = relativeVal.ToLower() == "yes";
                        var extraDayVal = row.Cell(58).GetValue<string>()?.Trim();
                        if (!string.IsNullOrWhiteSpace(extraDayVal)) emp.IsExtraDayApplicable = extraDayVal.ToLower() == "yes";

                        // Lookup: Location (col 19)
                        var locationName = row.Cell(19).GetValue<string>()?.Trim();
                        if (!string.IsNullOrWhiteSpace(locationName))
                        {
                            var loc = await _context.tblLocations.FirstOrDefaultAsync(l => l.LocationName == locationName);
                            if (loc != null) emp.LocationId = loc.LocationId;
                        }
                        // Store Code (col 50) overrides location
                        var storeCode = row.Cell(50).GetValue<string>()?.Trim();
                        if (!string.IsNullOrWhiteSpace(storeCode))
                        {
                            var loc = await _context.tblLocations.FirstOrDefaultAsync(l => l.STCode.Trim().ToLower() == storeCode.Trim().ToLower());
                            if (loc != null) emp.LocationId = loc.LocationId;
                        }

                        // Lookup: Department (col 20)
                        var deptName = row.Cell(20).GetValue<string>()?.Trim();
                        if (!string.IsNullOrWhiteSpace(deptName))
                        {
                            var dept = await _context.tblDepartments.FirstOrDefaultAsync(d => d.DepartmentName == deptName);
                            if (dept != null) emp.DepartmentId = dept.DepartmentId;
                        }

                        // Lookup: Designation (col 29)
                        var desgName = row.Cell(29).GetValue<string>()?.Trim();
                        if (!string.IsNullOrWhiteSpace(desgName))
                        {
                            var desg = await _context.tblDesignations.FirstOrDefaultAsync(d => d.DesignationName == desgName);
                            if (desg != null) emp.DesignationId = desg.DesignationId;
                        }

                        await _context.tblEmployees.AddAsync(emp);
                        await _context.SaveChangesAsync();

                        // Assign default Employee role (RoleId = 3)
                        var empRole = new tblEmployeeRole
                        {
                            EmployeeId = emp.EmployeeId,
                            RoleId = 3,
                            AssignedOn = DateTime.UtcNow,
                            AssignedBy = "System",
                            LastUpdatedBy = "System",
                            LastUpdatedOn = DateTime.UtcNow
                        };
                        await _context.tblEmployeeRoles.AddAsync(empRole);
                        await _context.SaveChangesAsync();

                        insertedCount++;
                    }
                    catch (Exception ex)
                    {
                        var innerMsg = ex.InnerException?.Message ?? ex.Message;
                        errors.Add($"Row {rowNum}: {innerMsg}");
                    }
                }

                if (insertedCount == 0 && errors.Any())
                    return BuildExecuteErrorResponse($"No employees inserted. Errors: {string.Join("; ", errors)}", HttpStatusCode.BadRequest);

                var msg = $"{insertedCount} employee(s) created successfully.";
                if (errors.Any())
                    msg += $" Errors in {errors.Count} row(s): {string.Join("; ", errors)}";

                return BuildExecuteSuccessResponse(msg);
            }
            catch (Exception ex)
            {
                return BuildExecuteErrorResponse($"Error inserting employees: {ex.Message}", HttpStatusCode.BadRequest);
            }
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }

        private async Task SaveNewAttachments(long candidateId, string email, CandidateDocs files, string updatedBy)
        {
            async Task SaveFileIfExists(IFormFile? file, string folder, string docType, int index = 0)
            {
                if (file?.Length > 0)
                {
                    var filePath = await SaveFile(file, folder, $"{candidateId}_{email}");
                    int result = await _context.GetProcedures().sp_InsertCandidateDocsAsync(
                        candidateId, filePath, docType, file.Length.ToString(), updatedBy);
                    if (result < 1)
                        throw new Exception($"Unable to save {docType}{(index > 0 ? $" - {index}" : "")}");
                }
            }

            async Task SaveFileListIfExists(List<IFormFile>? files, string folder, string docType)
            {
                if (files?.Count > 0)
                {
                    for (int i = 0; i < files.Count; i++)
                        await SaveFileIfExists(files[i], folder, docType, i + 1);
                }
            }

            await SaveFileIfExists(files.PassportPhoto, "PassportPhotos", "PassportPhoto");
            await SaveFileListIfExists(files.Last3SalarySlip, "SalarySlips", "SalarySlip");
            await SaveFileIfExists(files.Last3BankStatement, "BankStatements", "BankStatement");
            await SaveFileIfExists(files.PrevOfferLetter, "PrevOfferLetters", "PrevOfferLetter");
            await SaveFileListIfExists(files.OfferLetterAttachment, "OfferLetter", "OfferLetter");
            await SaveFileListIfExists(files.PanAttachment, "Pan", "Pan");
            await SaveFileListIfExists(files.AadharAttachment, "Aadhar", "Aadhar");
            await SaveFileListIfExists(files.AadharBackAttachment, "AadharBack", "AadharBack");
            await SaveFileListIfExists(files.BankPassbookAttachment, "BankPassbook", "BankPassbook");
            await SaveFileListIfExists(files.EducationAttachment, "Education", "Education");
            await SaveFileListIfExists(files.ResumeAttachment, "Resume", "Resume");
            await SaveFileListIfExists(files.BankStatementVideo, "BankStatementVideo", "BankStatementVideo");
        }

        public async Task<string> SaveFile(IFormFile file, string folderName, string candidateId)
        {
            // Create a directory for the candidate if not exists
            //var directoryPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "CandidateFiles", candidateId.ToString(),folderName);
            var directoryPath = Path.Combine(savePath, candidateId.ToString(), folderName);
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            // Create the file path
            var fileName = $"{DateTime.Now.ToString("ddMMyyyyHHmmssffff")}_{file.FileName}";
            var filePath = Path.Combine(directoryPath, fileName);
            //var returnPath = Path.Combine("CandidateFiles", candidateId.ToString(), folderName,fileName);
            var returnPath = Path.Combine(candidateId.ToString(), folderName, fileName);

            // Save the file
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            // Return the file path to be stored in the database
            return returnPath;
        }



        private (bool,string) ValidateUpdateEmployeeRequest(EmployeeStatusUpdateRequest request) {
            if (request == null) return (false,"No Request Found");
            if (request.id == null || request.id < 1) return (false, "Incorrect Emp Id ");

            if (request.isactive == null) return (false, "No Status to update was found");

            if (String.IsNullOrEmpty(request.remarks)) return (false, "No Remarks/Reason was found.");

            if (!request.isactive && request.leavingDate == null) {
                return (false, "No Leaving Date Found.");       
            }

            if (request.lastUpdatedBy == null) return (false,"Id from which updation is occuring, not provided.");
            return (true,"Correct");

        }

        //public async Task<ExecuteAndReponse> UpdateEmployeeStatus(EmployeeStatusUpdateRequest request)
        //{
        //    using (var trans = await _context.Database.BeginTransactionAsync())
        //    {
        //        try
        //        {
        //            var requestValidate = ValidateUpdateEmployeeRequest(request);

        //            if (!requestValidate.Item1) return BuildExecuteErrorResponse(requestValidate.Item2, HttpStatusCode.BadRequest);
        //            var emp = await GetOneRecordWithTrackingAsync<tblEmployee>(row => row.EmployeeId == request.id);
        //            if (emp == null)
        //                return BuildExecuteErrorResponse("No Employee Data Found", HttpStatusCode.NotFound);

        //            emp.IsActive = request.isactive;
        //            emp.IsDeleted = !request.isactive;
        //            emp.LastUpdatedBy = string.IsNullOrWhiteSpace(request.lastUpdatedBy) ? "System" : request.lastUpdatedBy;
        //            emp.DeletedOn = DateTime.UtcNow;
        //            emp.ActiveInActiveRemarks = request.remarks;
        //            emp.DateOfLeft = request.leavingDate;

        //            //if (request.isactive) { 
        //            //}
        //            int ra = await _context.SaveChangesAsync();
        //            if (ra < 1) {
        //                await trans.RollbackAsync();
        //                return BuildExecuteErrorResponse("Unable to Update Status of Employee", HttpStatusCode.BadRequest);
        //            }

        //            // make history too
        //            var history = new tblEmployeeActiveInActiveHistory
        //            {
        //                EmpId = request.id.ToString(),
        //                ActionPerformed = request.isactive.ToString(),
        //                LeavingDate = request.leavingDate,
        //                Remarks = request.remarks,
        //                CreatedBy = request.lastUpdatedBy,
        //                UpdatedBy = request.lastUpdatedBy,
        //                UpdatedOn = DateTime.UtcNow,
        //            };


        //            if (!await SaveOneAsync<tblEmployeeActiveInActiveHistory>(history))
        //            {
        //                await trans.RollbackAsync();
        //                return BuildExecuteErrorResponse("Unable to Update Status of Employee",HttpStatusCode.BadRequest);
        //            }


        //            await trans.CommitAsync();
        //            return BuildExecuteSuccessResponse("Employee Status Updated Successfully");
        //        }
        //        catch (Exception ex)
        //        {
        //            return BuildExecuteErrorResponse(ex.Message, HttpStatusCode.BadRequest);
        //        }
        //    }
        //}
        //nikhil new 19-09-2025
        public async Task<ExecuteAndReponse> UpdateEmployeeStatus(EmployeeStatusUpdateRequest request)
        {
            using (var trans = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    var requestValidate = ValidateUpdateEmployeeRequest(request);

                    if (!requestValidate.Item1)
                        return BuildExecuteErrorResponse(requestValidate.Item2, HttpStatusCode.BadRequest);

                    var emp = await GetOneRecordWithTrackingAsync<tblEmployee>(row => row.EmployeeId == request.id);
                    if (emp == null)
                        return BuildExecuteErrorResponse("No Employee Data Found", HttpStatusCode.NotFound);

                    emp.IsActive = request.isactive;
                    emp.IsDeleted = !request.isactive;
                    emp.LastUpdatedBy = string.IsNullOrWhiteSpace(request.lastUpdatedBy) ? "System" : request.lastUpdatedBy;
                    emp.DeletedOn = DateTime.UtcNow;
                    emp.ActiveInActiveRemarks = request.remarks;

                    // ✅ Clear DateOfLeft if employee is active again
                    if (request.isactive)
                    {
                        emp.DateOfLeft = null;
                    }
                    else
                    {
                        emp.DateOfLeft = request.leavingDate;
                    }

                    int ra = await _context.SaveChangesAsync();
                    if (ra < 1)
                    {
                        await trans.RollbackAsync();
                        return BuildExecuteErrorResponse("Unable to Update Status of Employee", HttpStatusCode.BadRequest);
                    }

                    // Make history too
                    var history = new tblEmployeeActiveInActiveHistory
                    {
                        EmpId = request.id.ToString(),
                        ActionPerformed = request.isactive ? "Activated" : "Deactivated",
                        LeavingDate = request.isactive ? null : request.leavingDate,
                        Remarks = request.remarks,
                        CreatedBy = request.lastUpdatedBy,
                        UpdatedBy = request.lastUpdatedBy,
                        UpdatedOn = DateTime.UtcNow,
                    };

                    if (!await SaveOneAsync<tblEmployeeActiveInActiveHistory>(history))
                    {
                        await trans.RollbackAsync();
                        return BuildExecuteErrorResponse("Unable to Update Status of Employee", HttpStatusCode.BadRequest);
                    }

                    await trans.CommitAsync();
                    return BuildExecuteSuccessResponse("Employee Status Updated Successfully");
                }
                catch (Exception ex)
                {
                    return BuildExecuteErrorResponse(ex.Message, HttpStatusCode.BadRequest);
                }
            }
        }
       //public async Task<ExecuteAndReponse> UpdateEmployeeStatusWithReasonAndAttachment(EmployeeStatusUpdateWithReasonAndAttachmentRequest request)
        //{
        //    using (var trans = await _context.Database.BeginTransactionAsync())
        //    {
        //        try
        //        {
        //            var requestValidate = ValidateUpdateEmployeeRequest(request);

        //            if (!requestValidate.Item1) return BuildExecuteErrorResponse(requestValidate.Item2, HttpStatusCode.BadRequest);
        //            var emp = await GetOneRecordWithTrackingAsync<tblEmployee>(row => row.EmployeeId == request.id);
        //            if (emp == null)
        //                return BuildExecuteErrorResponse("No Employee Data Found", HttpStatusCode.NotFound);

        //            emp.IsActive = request.isactive;
        //            emp.IsDeleted = !request.isactive;
        //            emp.LastUpdatedBy = string.IsNullOrWhiteSpace(request.lastUpdatedBy) ? "System" : request.lastUpdatedBy;
        //            emp.DeletedOn = DateTime.UtcNow;
        //            emp.ActiveInActiveRemarks = request.remarks;
        //            emp.DateOfLeft = request.leavingDate;
        //            emp.DOL_Reason = request.reasonid;

        //            //if (request.isactive) { 
        //            //}
        //            int ra = await _context.SaveChangesAsync();
        //            if (ra < 1)
        //            {
        //                await trans.RollbackAsync();
        //                return BuildExecuteErrorResponse("Unable to Update Status of Employee", HttpStatusCode.BadRequest);
        //            }

        //            // save Files
        //            string rootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Uploads", "InactiveAttachments");
        //            if (!Directory.Exists(rootPath))
        //            {
        //                Directory.CreateDirectory(rootPath);
        //            }
        //            List<string> savedFilePaths = new();

        //            if (request.inactiveattachment != null && request.inactiveattachment.Count > 0)
        //            {
        //                foreach (var file in request.inactiveattachment)
        //                {
        //                    if (file != null && file.Length > 0)
        //                    {
        //                        //var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
        //                        var fileName = $"{DateTime.Now.ToString("ddMMyyyyHHmmssfff")}{Path.GetExtension(file.FileName)}";
        //                        var fullPath = Path.Combine(rootPath, fileName);
        //                        var relativePath = Path.Combine("Uploads", "InactiveAttachments", fileName); // Save this to DB

        //                        using (var stream = new FileStream(fullPath, FileMode.Create))
        //                        {
        //                            await file.CopyToAsync(stream);
        //                        }

        //                        savedFilePaths.Add(relativePath);
        //                    }
        //                }
        //            }
        //            if (savedFilePaths.Any())
        //            {
        //                var attachments = savedFilePaths.Select(path => new tblEmployeeInActiveFile
        //                {
        //                    EmpId = (int)request.id,
        //                    FilePath = path,
        //                    CreatedOn = DateTime.UtcNow,
        //                    CreatedBy = request.lastUpdatedBy ?? "System"
        //                }).ToList();

        //                await _context.tblEmployeeInActiveFiles.AddRangeAsync(attachments);
        //                ra = await _context.SaveChangesAsync();
        //                if (ra < 1)
        //                {
        //                    await trans.RollbackAsync();
        //                    return BuildExecuteErrorResponse("Unable to Update Status of Employee", HttpStatusCode.BadRequest);
        //                }
        //            }



        //            // make history too
        //            var history = new tblEmployeeActiveInActiveHistory
        //            {
        //                EmpId = request.id.ToString(),
        //                ActionPerformed = request.isactive.ToString(),
        //                LeavingDate = request.leavingDate,
        //                Remarks = request.remarks,
        //                CreatedBy = request.lastUpdatedBy,
        //                UpdatedBy = request.lastUpdatedBy,
        //                UpdatedOn = DateTime.UtcNow,
        //            };


        //            if (!await SaveOneAsync<tblEmployeeActiveInActiveHistory>(history))
        //            {
        //                await trans.RollbackAsync();
        //                return BuildExecuteErrorResponse("Unable to Update Status of Employee", HttpStatusCode.BadRequest);
        //            }


        //            await trans.CommitAsync();
        //            return BuildExecuteSuccessResponse("Employee Status Updated Successfully");
        //        }
        //        catch (Exception ex)
        //        {
        //            return BuildExecuteErrorResponse(ex.Message, HttpStatusCode.BadRequest);
        //        }
        //    }
        //}
        //public async Task<ExecuteAndReponse> UpdateEmployeeStatusWithReasonAndAttachment(EmployeeStatusUpdateWithReasonAndAttachmentRequest request)
        //{
        //    using (var trans = await _context.Database.BeginTransactionAsync())
        //    {
        //        try
        //        {
        //            var requestValidate = ValidateUpdateEmployeeRequest(request);
        //            if (!requestValidate.Item1)
        //                return BuildExecuteErrorResponse(requestValidate.Item2, HttpStatusCode.BadRequest);

        //            // Validate ResignationTypeId and AbscondingReasonId
        //            if (request.ResignationTypeId.HasValue)
        //            {
        //                var resignationType = await _context.tblResignationTypes
        //                    .AnyAsync(rt => rt.ResignationTypeId == request.ResignationTypeId);
        //                if (!resignationType)
        //                    return BuildExecuteErrorResponse("Invalid ResignationTypeId", HttpStatusCode.BadRequest);


        //                if (request.AbscondingReasonId.HasValue)
        //                {
        //                    if (request.ResignationTypeId == 10 && !request.AbscondingReasonId.HasValue)
        //                        return BuildExecuteErrorResponse("AbscondingReasonId is required when ResignationTypeId is Absconding", HttpStatusCode.BadRequest);
        //                }

        //                if (request.BlackListReasonId.HasValue)
        //                {
        //                    if (request.ResignationTypeId == 10 && !request.BlackListReasonId.HasValue)
        //                        return BuildExecuteErrorResponse("BlackListReasonId is required when ResignationTypeId is BlackList", HttpStatusCode.BadRequest);

        //                }

        //                if (request.AbscondingReasonId.HasValue)
        //                {
        //                    var abscondingReason = await _context.tblAbscondingReasons
        //                        .AnyAsync(ar => ar.AbscondingReasonId == request.AbscondingReasonId && ar.ResignationTypeId == request.ResignationTypeId);
        //                    if (!abscondingReason)
        //                        return BuildExecuteErrorResponse("Invalid AbscondingReasonId or mismatch with ResignationTypeId", HttpStatusCode.BadRequest);
        //                }

        //                if (request.BlackListReasonId.HasValue)
        //                {
        //                    var abscondingReason = await _context.tblBlacklistReasons
        //                        .AnyAsync(ar => ar.BlacklistReasonId == request.BlackListReasonId && ar.ResignationTypeId == request.ResignationTypeId);
        //                    if (!abscondingReason)
        //                        return BuildExecuteErrorResponse("Invalid BlacklistReasonId or mismatch with ResignationTypeId", HttpStatusCode.BadRequest);
        //                }
        //            }
        //            else if (request.AbscondingReasonId.HasValue)
        //            {
        //                return BuildExecuteErrorResponse("AbscondingReasonId cannot be provided without ResignationTypeId", HttpStatusCode.BadRequest);
        //            }

        //            else if (request.BlackListReasonId.HasValue)
        //            {
        //                return BuildExecuteErrorResponse("BlackListReasonId cannot be provided without ResignationTypeId", HttpStatusCode.BadRequest);
        //            }

        //            var emp = await GetOneRecordWithTrackingAsync<tblEmployee>(row => row.EmployeeId == request.id);
        //            if (emp == null)
        //                return BuildExecuteErrorResponse("No Employee Data Found", HttpStatusCode.NotFound);

        //            emp.IsActive = request.isactive;
        //            emp.IsDeleted = !request.isactive;
        //            emp.LastUpdatedBy = string.IsNullOrWhiteSpace(request.lastUpdatedBy) ? "System" : request.lastUpdatedBy;
        //            emp.DeletedOn = request.isactive ? null : DateTime.UtcNow;
        //            emp.ActiveInActiveRemarks = request.remarks;
        //            emp.DateOfLeft = request.leavingDate;
        //            emp.DOL_Reason = request.reasonid;

        //            int ra = await _context.SaveChangesAsync();
        //            if (ra < 1)
        //            {
        //                await trans.RollbackAsync();
        //                return BuildExecuteErrorResponse("Unable to Update Status of Employee", HttpStatusCode.BadRequest);
        //            }

        //            // Save Files
        //            string rootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Uploads", "InactiveAttachments");
        //            if (!Directory.Exists(rootPath))
        //            {
        //                Directory.CreateDirectory(rootPath);
        //            }
        //            List<string> savedFilePaths = new();

        //            if (request.inactiveattachment != null && request.inactiveattachment.Count > 0)
        //            {
        //                foreach (var file in request.inactiveattachment)
        //                {
        //                    if (file != null && file.Length > 0)
        //                    {
        //                        var fileName = $"{DateTime.Now.ToString("ddMMyyyyHHmmssfff")}{Path.GetExtension(file.FileName)}";
        //                        var fullPath = Path.Combine(rootPath, fileName);
        //                        var relativePath = Path.Combine("Uploads", "InactiveAttachments", fileName);

        //                        using (var stream = new FileStream(fullPath, FileMode.Create))
        //                        {
        //                            await file.CopyToAsync(stream);
        //                        }

        //                        savedFilePaths.Add(relativePath);
        //                    }
        //                }
        //            }
        //            if (savedFilePaths.Any())
        //            {
        //                var attachments = savedFilePaths.Select(path => new tblEmployeeInActiveFile
        //                {
        //                    EmpId = (int)request.id,
        //                    FilePath = path,
        //                    CreatedOn = DateTime.UtcNow,
        //                    CreatedBy = request.lastUpdatedBy ?? "System"
        //                }).ToList();

        //                await _context.tblEmployeeInActiveFiles.AddRangeAsync(attachments);
        //                ra = await _context.SaveChangesAsync();
        //                if (ra < 1)
        //                {
        //                    await trans.RollbackAsync();
        //                    return BuildExecuteErrorResponse("Unable to Save Attachments", HttpStatusCode.BadRequest);
        //                }
        //            }

        //            // Make history
        //            var history = new tblEmployeeActiveInActiveHistory
        //            {
        //                EmpId = request.id.ToString(),
        //                ActionPerformed = request.isactive.ToString(),
        //                LeavingDate = request.leavingDate,
        //                Remarks = request.remarks,
        //                CreatedBy = request.lastUpdatedBy ?? "System",
        //                UpdatedBy = request.lastUpdatedBy ?? "System",
        //                UpdatedOn = DateTime.UtcNow,
        //                ResignationTypeId = request.ResignationTypeId, // Store in history
        //                AbscondingReasonId = request.AbscondingReasonId, // Store in history
        //                BlackListReasonId = request.BlackListReasonId // Store in history
        //            };

        //            if (!await SaveOneAsync<tblEmployeeActiveInActiveHistory>(history))
        //            {
        //                await trans.RollbackAsync();
        //                return BuildExecuteErrorResponse("Unable to Save History", HttpStatusCode.BadRequest);
        //            }

        //            await trans.CommitAsync();
        //            return BuildExecuteSuccessResponse("Employee Status Updated Successfully");
        //        }
        //        catch (Exception ex)
        //        {
        //            await trans.RollbackAsync();
        //            return BuildExecuteErrorResponse($"Error updating employee status: {ex.Message}", HttpStatusCode.BadRequest);
        //        }
        //    }
        //}
        public async Task<ExecuteAndReponse> UpdateEmployeeStatusWithReasonAndAttachment(EmployeeStatusUpdateWithReasonAndAttachmentRequest request)
        {
            using (var trans = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    var requestValidate = ValidateUpdateEmployeeRequest(request);
                    if (!requestValidate.Item1)
                        return BuildExecuteErrorResponse(requestValidate.Item2, HttpStatusCode.BadRequest);

                    // Run additional validation ONLY if setting employee to inactive
                    if (!request.isactive)
                    {
                        if (request.ResignationTypeId.HasValue)
                        {
                            var resignationType = await _context.tblResignationTypes
                                .AnyAsync(rt => rt.ResignationTypeId == request.ResignationTypeId);
                            if (!resignationType)
                                return BuildExecuteErrorResponse("Invalid ResignationTypeId", HttpStatusCode.BadRequest);


                            //bool isChecked = false;
                            //if (request.ResignationTypeId == 10 && !request.AbscondingReasonId.HasValue)
                            //    return BuildExecuteErrorResponse("AbscondingReasonId is required when ResignationTypeId is Absconding", HttpStatusCode.BadRequest);
                            //isChecked = true;


                            //if (request.ResignationTypeId == 10 && !request.BlackListReasonId.HasValue && !isChecked )
                            //    return BuildExecuteErrorResponse("BlackListReasonId is required when ResignationTypeId is BlackList", HttpStatusCode.BadRequest);

                            if (request.AbscondingReasonId.HasValue)
                            {
                                var abscondingReason = await _context.tblAbscondingReasons
                                    .AnyAsync(ar => ar.AbscondingReasonId == request.AbscondingReasonId && ar.ResignationTypeId == request.ResignationTypeId);
                                if (!abscondingReason)
                                    return BuildExecuteErrorResponse("Invalid AbscondingReasonId or mismatch with ResignationTypeId", HttpStatusCode.BadRequest);
                            }

                            if (request.BlackListReasonId.HasValue)
                            {
                                var blacklistReason = await _context.tblBlacklistReasons
                                    .AnyAsync(ar => ar.BlacklistReasonId == request.BlackListReasonId && ar.ResignationTypeId == request.ResignationTypeId);
                                if (!blacklistReason)
                                    return BuildExecuteErrorResponse("Invalid BlacklistReasonId or mismatch with ResignationTypeId", HttpStatusCode.BadRequest);
                            }
                        }
                        else
                        {
                            if (request.AbscondingReasonId.HasValue)
                                return BuildExecuteErrorResponse("AbscondingReasonId cannot be provided without ResignationTypeId", HttpStatusCode.BadRequest);

                            if (request.BlackListReasonId.HasValue)
                                return BuildExecuteErrorResponse("BlackListReasonId cannot be provided without ResignationTypeId", HttpStatusCode.BadRequest);
                        }
                    }

                    var emp = await GetOneRecordWithTrackingAsync<tblEmployee>(row => row.EmployeeId == request.id);
                    if (emp == null)
                        return BuildExecuteErrorResponse("No Employee Data Found", HttpStatusCode.NotFound);

                    emp.IsActive = request.isactive;
                    emp.IsDeleted = !request.isactive;
                    emp.LastUpdatedBy = string.IsNullOrWhiteSpace(request.lastUpdatedBy) ? "System" : request.lastUpdatedBy;
                    emp.ActiveInActiveRemarks = request.remarks;

                    if (!request.isactive)
                    {
                        var leavingData = _context
     .Database
     .SqlQueryRaw<DateTime?>(
         "EXEC dbo.sp_GetEmployeeEffectiveLeavingDate @EmployeeId",
         new SqlParameter("@EmployeeId", request.id))
     .AsEnumerable()
     .FirstOrDefault();

                        if (leavingData != null)
                        {
                            emp.DateOfLeft = leavingData;
                        }
                        emp.DeletedOn = DateTime.UtcNow;
                        //emp.DateOfLeft = request.leavingDate;
                        emp.DOL_Reason = request.reasonid;
                    }
                    else
                    {
                        // Reset these fields on activation
                        emp.DeletedOn = null;
                        emp.DateOfLeft = null;
                        emp.DOL_Reason = null;
                    }

                    int ra = await _context.SaveChangesAsync();
                    if (ra < 1)
                    {
                        await trans.RollbackAsync();
                        return BuildExecuteErrorResponse("Unable to Update Status of Employee", HttpStatusCode.BadRequest);
                    }

                    // Save Files Only If Inactivating
                    List<string> savedFilePaths = new();
                    if (!request.isactive && request.inactiveattachment != null && request.inactiveattachment.Count > 0)
                    {
                        string rootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Uploads", "InactiveAttachments");
                        if (!Directory.Exists(rootPath))
                            Directory.CreateDirectory(rootPath);

                        foreach (var file in request.inactiveattachment)
                        {
                            if (file != null && file.Length > 0)
                            {
                                var fileName = $"{DateTime.Now:ddMMyyyyHHmmssfff}{Path.GetExtension(file.FileName)}";
                                var fullPath = Path.Combine(rootPath, fileName);
                                var relativePath = Path.Combine("Uploads", "InactiveAttachments", fileName);

                                using (var stream = new FileStream(fullPath, FileMode.Create))
                                {
                                    await file.CopyToAsync(stream);
                                }

                                savedFilePaths.Add(relativePath);
                            }
                        }

                        if (savedFilePaths.Any())
                        {
                            var attachments = savedFilePaths.Select(path => new tblEmployeeInActiveFile
                            {
                                EmpId = (int)request.id,
                                FilePath = path,
                                CreatedOn = DateTime.UtcNow,
                                CreatedBy = request.lastUpdatedBy ?? "System"
                            }).ToList();

                            await _context.tblEmployeeInActiveFiles.AddRangeAsync(attachments);
                            ra = await _context.SaveChangesAsync();
                            if (ra < 1)
                            {
                                await trans.RollbackAsync();
                                return BuildExecuteErrorResponse("Unable to Save Attachments", HttpStatusCode.BadRequest);
                            }
                        }
                    }

                    // Insert History Entry
                    var history = new tblEmployeeActiveInActiveHistory
                    {
                        EmpId = request.id.ToString(),
                        ActionPerformed = request.isactive.ToString(),
                        //LeavingDate = request.leavingDate,
                        LeavingDate = emp.DateOfLeft,
                        Remarks = request.remarks,
                        CreatedBy = request.lastUpdatedBy ?? "System",
                        UpdatedBy = request.lastUpdatedBy ?? "System",
                        UpdatedOn = DateTime.UtcNow,
                        ResignationTypeId = request.ResignationTypeId,
                        AbscondingReasonId = request.AbscondingReasonId,
                        BlackListReasonId = request.BlackListReasonId
                    };

                    if (!await SaveOneAsync<tblEmployeeActiveInActiveHistory>(history))
                    {
                        await trans.RollbackAsync();
                        return BuildExecuteErrorResponse("Unable to Save History", HttpStatusCode.BadRequest);
                    }

                    await trans.CommitAsync();
                    return BuildExecuteSuccessResponse("Employee Status Updated Successfully");
                }
                catch (Exception ex)
                {
                    await trans.RollbackAsync();
                    return BuildExecuteErrorResponse($"Error updating employee status: {ex.Message}", HttpStatusCode.BadRequest);
                }
            }
        }

        public async Task<FetchAndResponse> GetInActiveStatusList()
        {
            try {
                var inactiveList = await GetMultipleRecordAsync<tblInActiveStatus>(row => row.IsActive == true && !row.IsDeleted == true);
                if (inactiveList != null && inactiveList.Count > 0)
                    return BuildFetchSuccessResponse("Fetched Successfully",inactiveList);
                return BuildFetchErrorResponse("No Data Found",HttpStatusCode.NotFound);
            }
            catch (Exception ex ) {
                return BuildFetchErrorResponse(ex.Message,HttpStatusCode.BadRequest);
            }
        }
        public async Task<(List<GetEmployeeDetailsResult> Employees, long TotalCount, int CurrentPageNumber)> GetEmployeeDetailsByManagerIdAsync(
       long managerId, int pageNumber = 1, int pageSize = 10, string searchTerm = null)
        {
            try
            {
                // Get the manager's Ecode
                var managerEmployee = await _context.tblEmployees
                    .FirstOrDefaultAsync(e => e.EmployeeId == managerId);

                if (managerEmployee == null)
                {
                    return (new List<GetEmployeeDetailsResult>(), 0, pageNumber);
                }

                var query = (from e in _context.tblEmployees.AsNoTracking()
                             join d in _context.tblDepartments on e.DepartmentId equals d.DepartmentId into dept
                             from d in dept.DefaultIfEmpty()
                             join dg in _context.tblDesignations on e.DesignationId equals dg.DesignationId into desig
                             from dg in desig.DefaultIfEmpty()
                             join l in _context.tblLocations on e.LocationId equals l.LocationId into loc
                             from l in loc.DefaultIfEmpty()
                             where ((e.ReportHeadEcode == managerEmployee.Ecode && managerEmployee.IsStore == false)
                                 || (l.STCode == managerEmployee.Ecode && managerEmployee.IsStore == true))
                                && e.IsActive == true
                                && e.IsDeleted == false
                             select new GetEmployeeDetailsResult
                             {
                                 EmployeeId = (int)e.EmployeeId,
                                 CandidateId = (int)(e.CandidateId ?? 0),
                                 LocBasedECode = "E-" + (l != null ? l.STCode : "") + "-" + (d != null ? d.DepartmentId.ToString() : "") + "-" +
                                                 (dg != null ? dg.DesignationId.ToString() : "") + "-" +
                                                 (e.CompanyId == 1 ? e.Ecode.Substring(1) :
                                                  e.CompanyId == 2 ? e.Ecode.Substring(3) :
                                                  e.CompanyId == 3 ? e.Ecode.Substring(2) : e.Ecode),
                                 FullName = e.FULL_NAME ?? "",
                                 DepartmentName = d != null ? d.DepartmentName : "",
                                 DesignationName = dg != null ? dg.DesignationName : "",
                                 LocationName = l != null ? l.LocationName : "",
                                 StoreCode = l != null ? l.STCode : "",
                                 Ecode = e.Ecode ?? "",
                                 ReportHeadEcode = e.ReportHeadEcode ?? "",
                                 IsActive = (bool)e.IsActive,
                                 IsDeleted = (bool)e.IsDeleted,
                                 DateOfJoining = e.JOINING_DATE,
                             });


                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    searchTerm = searchTerm.ToLower();
                    query = query.Where(e =>
                        (e.FullName != null && e.FullName.ToLower().Contains(searchTerm)) ||
                        (e.Ecode != null && e.Ecode.ToLower().Contains(searchTerm)) ||
                        (e.DepartmentName != null && e.DepartmentName.ToLower().Contains(searchTerm)) ||
                        (e.DesignationName != null && e.DesignationName.ToLower().Contains(searchTerm)) ||
                        (e.LocationName != null && e.LocationName.ToLower().Contains(searchTerm)) ||
                        (e.StoreCode != null && e.StoreCode.ToLower().Contains(searchTerm)) 
                        );
                }

                long totalCount = await query.CountAsync();

                query = query
                    .OrderByDescending(e => e.EmployeeId)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize);

                var employees = await query.ToListAsync();
                return (employees, totalCount, pageNumber);
            }
            catch (Exception ex)
            {
                throw new ApplicationException("An error occurred while fetching employee details by manager ID.", ex);
            }
        }

      
        public async Task<(List<GetEmployeeDetailsResultNew> Employees, long TotalCount, int CurrentPageNumber, long ActiveCount, long InactiveCount,long abscondCnt, long locCountt)> EmployeeListWithCards(int pageNumber, int pageSize, string searchTerm = "", string mode = "all")
        {
            try
            {
                using (var connection = _context.Database.GetDbConnection())
                {
                    await connection.OpenAsync();
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "GetEmployeeDetailsWithCards";
                        command.CommandType = CommandType.StoredProcedure;

                        // Input parameters
                        command.Parameters.Add(new SqlParameter("@PageNumber", SqlDbType.Int) { Value = pageNumber });
                        command.Parameters.Add(new SqlParameter("@PageSize", SqlDbType.Int) { Value = pageSize });
                        command.Parameters.Add(new SqlParameter("@SearchTerm", SqlDbType.NVarChar, 100) { Value = searchTerm ?? string.Empty });
                        command.Parameters.Add(new SqlParameter("@Mode", SqlDbType.NVarChar, 10) { Value = mode });

                        // Output parameters
                        var totalEmployeesParam = new SqlParameter("@TotalEmployees", SqlDbType.Int) { Direction = ParameterDirection.Output };
                        var currentPageNumberParam = new SqlParameter("@CurrentPageNumber", SqlDbType.Int) { Direction = ParameterDirection.Output };
                        var activeCountParam = new SqlParameter("@ActiveCount", SqlDbType.Int) { Direction = ParameterDirection.Output };
                        var inactiveCountParam = new SqlParameter("@InactiveCount", SqlDbType.Int) { Direction = ParameterDirection.Output };
                        var abscondCount = new SqlParameter("@AbscondCount", SqlDbType.Int) { Direction = ParameterDirection.Output };
                        var locCount = new SqlParameter("@LocCount", SqlDbType.Int) { Direction = ParameterDirection.Output };
                        command.Parameters.Add(totalEmployeesParam);
                        command.Parameters.Add(currentPageNumberParam);
                        command.Parameters.Add(activeCountParam);
                        command.Parameters.Add(inactiveCountParam);
                        command.Parameters.Add(abscondCount);
                        command.Parameters.Add(locCount);

                        // Execute reader to get employee list
                        var employees = new List<GetEmployeeDetailsResultNew>();
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var employee = new GetEmployeeDetailsResultNew
                                {
                                    TotalEmployees = null, // Output parameter, not part of result set
                                    EmployeeId = reader.GetInt64(reader.GetOrdinal("EmployeeId")),
                                    CandidateId = reader.IsDBNull(reader.GetOrdinal("CandidateId")) ? 0 : reader.GetInt64(reader.GetOrdinal("CandidateId")),
                                    FullName = reader.IsDBNull(reader.GetOrdinal("FullName")) ? string.Empty : reader.GetString(reader.GetOrdinal("FullName")),
                                    DepartmentName = reader.IsDBNull(reader.GetOrdinal("DepartmentName")) ? string.Empty : reader.GetString(reader.GetOrdinal("DepartmentName")),
                                    DesignationName = reader.IsDBNull(reader.GetOrdinal("DesignationName")) ? string.Empty : reader.GetString(reader.GetOrdinal("DesignationName")),
                                    LocationName = reader.IsDBNull(reader.GetOrdinal("LocationName")) ? string.Empty : reader.GetString(reader.GetOrdinal("LocationName")),
                                    StoreCode = reader.IsDBNull(reader.GetOrdinal("STCode")) ? string.Empty : reader.GetString(reader.GetOrdinal("STCode")),
                                    STCode = reader.IsDBNull(reader.GetOrdinal("STCode")) ? string.Empty : reader.GetString(reader.GetOrdinal("STCode")),
                                    RegionName = reader.IsDBNull(reader.GetOrdinal("RegionName")) ? string.Empty : reader.GetString(reader.GetOrdinal("RegionName")),
                                    ZoneName = reader.IsDBNull(reader.GetOrdinal("ZoneName")) ? string.Empty : reader.GetString(reader.GetOrdinal("ZoneName")),
                                    ClusterName = reader.IsDBNull(reader.GetOrdinal("ClusterName")) ? string.Empty : reader.GetString(reader.GetOrdinal("ClusterName")),
                                    Ecode = reader.IsDBNull(reader.GetOrdinal("Ecode")) ? string.Empty : reader.GetString(reader.GetOrdinal("Ecode")),
                                    ReportHeadEcode = reader.IsDBNull(reader.GetOrdinal("ReportHeadEcode")) ? string.Empty : reader.GetString(reader.GetOrdinal("ReportHeadEcode")),
                                    IsActive = reader.IsDBNull(reader.GetOrdinal("IsActive")) ? false : reader.GetBoolean(reader.GetOrdinal("IsActive")),
                                    IsDeleted = reader.IsDBNull(reader.GetOrdinal("IsDeleted")) ? false : reader.GetBoolean(reader.GetOrdinal("IsDeleted")),
                                    LocBasedECode = reader.IsDBNull(reader.GetOrdinal("LocBasedECode")) ? string.Empty : reader.GetString(reader.GetOrdinal("LocBasedECode")),
                                    DateOfJoining = reader.IsDBNull(reader.GetOrdinal("DateOfJoining")) ? null : reader.GetDateTime(reader.GetOrdinal("DateOfJoining")),
                                    IsStore = reader.IsDBNull(reader.GetOrdinal("IsStore")) ? false : reader.GetBoolean(reader.GetOrdinal("IsStore")),
                                    CreatedOn = reader.IsDBNull(reader.GetOrdinal("CreatedOn")) ? null : reader.GetDateTime(reader.GetOrdinal("CreatedOn")),
                                    UpdatedOn = reader.IsDBNull(reader.GetOrdinal("UpdatedOn")) ? null : reader.GetDateTime(reader.GetOrdinal("UpdatedOn")),
                                    CreatedBy = reader.IsDBNull(reader.GetOrdinal("CreatedBy")) ? null : reader.GetString(reader.GetOrdinal("CreatedBy")),
                                    UpdatedBy = reader.IsDBNull(reader.GetOrdinal("UpdatedBy")) ? null : reader.GetString(reader.GetOrdinal("UpdatedBy")),
                                    //Gender = reader.IsDBNull(reader.GetOrdinal("Gender")) ? string.Empty : reader.GetString(reader.GetOrdinal("Gender")),

                                };
                                employees.Add(employee);
                            }
                        }

                        // Retrieve Output Parameters
                        long totalCount = Convert.ToInt64(totalEmployeesParam.Value);
                        int currentPageNumber = Convert.ToInt32(currentPageNumberParam.Value);
                        long activeCount = Convert.ToInt64(activeCountParam.Value);
                        long inactiveCount = Convert.ToInt64(inactiveCountParam.Value);
                        long abscondCnt = Convert.ToInt64(abscondCount.Value);
                        long locCountt = Convert.ToInt64(locCount.Value);

                        return (employees, totalCount, currentPageNumber, activeCount, inactiveCount,abscondCnt,locCountt);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in EmployeeListWithCards: {ex.Message}");
                throw new ApplicationException("An error occurred while fetching employee details with cards.", ex);
            }
        }
        public async Task<(List<GetEmployeeDetailsResultNew_Test> Employees, long TotalCount, int CurrentPageNumber, long ActiveCount, long InactiveCount, long abscondCnt, long locCountt)> EmployeeListWithCards_Test(string? managerId,int pageNumber, int pageSize, string searchTerm = "", string mode = "all")
        {
            try
            {
                using (var connection = _context.Database.GetDbConnection())
                {
                    await connection.OpenAsync();
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "GetEmployeeDetailsWithCards_Test";
                        command.CommandType = CommandType.StoredProcedure;
                        command.CommandTimeout = 0;

                        // Input parameters
                        command.Parameters.Add(new SqlParameter("@PageNumber", SqlDbType.Int) { Value = pageNumber });
                        command.Parameters.Add(new SqlParameter("@PageSize", SqlDbType.Int) { Value = pageSize });
                        command.Parameters.Add(new SqlParameter("@SearchTerm", SqlDbType.NVarChar, 100) { Value = searchTerm ?? string.Empty });
                        command.Parameters.Add(new SqlParameter("@Mode", SqlDbType.NVarChar, 10) { Value = mode });
                        command.Parameters.Add(new SqlParameter("@ManagerId", SqlDbType.NVarChar, 10) { Value = managerId ?? string.Empty });

                        // Output parameters
                        var totalEmployeesParam = new SqlParameter("@TotalEmployees", SqlDbType.Int) { Direction = ParameterDirection.Output };
                        var currentPageNumberParam = new SqlParameter("@CurrentPageNumber", SqlDbType.Int) { Direction = ParameterDirection.Output };
                        var activeCountParam = new SqlParameter("@ActiveCount", SqlDbType.Int) { Direction = ParameterDirection.Output };
                        var inactiveCountParam = new SqlParameter("@InactiveCount", SqlDbType.Int) { Direction = ParameterDirection.Output };
                        var abscondCount = new SqlParameter("@AbscondCount", SqlDbType.Int) { Direction = ParameterDirection.Output };
                        var locCount = new SqlParameter("@LocCount", SqlDbType.Int) { Direction = ParameterDirection.Output };
                        command.Parameters.Add(totalEmployeesParam);
                        command.Parameters.Add(currentPageNumberParam);
                        command.Parameters.Add(activeCountParam);
                        command.Parameters.Add(inactiveCountParam);
                        command.Parameters.Add(abscondCount);
                        command.Parameters.Add(locCount);

                        // Execute reader to get employee list
                        var employees = new List<GetEmployeeDetailsResultNew_Test>();
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            // Log available columns for debugging
                            var columnCount = reader.FieldCount;
                            var columnNames = new List<string>();
                            for (int i = 0; i < columnCount; i++)
                            {
                                columnNames.Add(reader.GetName(i));
                            }
                            _logger.LogInformation("Available columns in result set: {Columns}", string.Join(", ", columnNames));

                            while (await reader.ReadAsync())
                            {
                                // Helper method to safely get column value
                                string GetStringValue(string columnName)
                                {
                                    try
                                    {
                                        var ordinal = reader.GetOrdinal(columnName);
                                        return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
                                    }
                                    catch
                                    {
                                        return string.Empty;
                                    }
                                }

                                DateTime? GetDateTimeValue(string columnName)
                                {
                                    try
                                    {
                                        var ordinal = reader.GetOrdinal(columnName);
                                        return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
                                    }
                                    catch
                                    {
                                        return null;
                                    }
                                }

                                decimal? GetDecimalValue(string columnName)
                                {
                                    try
                                    {
                                        var ordinal = reader.GetOrdinal(columnName);
                                        return reader.IsDBNull(ordinal) ? null : reader.GetDecimal(ordinal);
                                    }
                                    catch
                                    {
                                        return null;
                                    }
                                }

                                bool? GetBooleanValue(string columnName)
                                {
                                    try
                                    {
                                        var ordinal = reader.GetOrdinal(columnName);
                                        return reader.IsDBNull(ordinal) ? null : reader.GetBoolean(ordinal);
                                    }
                                    catch
                                    {
                                        return null;
                                    }
                                }

                                long GetLongValue(string columnName)
                                {
                                    try
                                    {
                                        var ordinal = reader.GetOrdinal(columnName);
                                        return reader.IsDBNull(ordinal) ? 0 : reader.GetInt64(ordinal);
                                    }
                                    catch
                                    {
                                        return 0;
                                    }
                                }

                                long GetIntValue(string columnName)
                                {
                                    try
                                    {
                                        var ordinal = reader.GetOrdinal(columnName);
                                        return reader.IsDBNull(ordinal) ? 0 : reader.GetInt32(ordinal);
                                    }
                                    catch
                                    {
                                        return 0;
                                    }
                                }
                                bool GetBoolValue(string columnName)
                                {
                                    try
                                    {
                                        var ordinal = reader.GetOrdinal(columnName);
                                        return reader.IsDBNull(ordinal) ? false : reader.GetBoolean(ordinal);
                                    }
                                    catch
                                    {
                                        return false;
                                    }
                                }

                                var employee = new GetEmployeeDetailsResultNew_Test
                                {
                                    ZoneName = GetStringValue("ZoneName"),
                                    RegionName = GetStringValue("RegionName"),
                                    ClusterName = GetStringValue("ClusterName"),
                                    STCode = GetStringValue("STCode"),
                                    LocationName = GetStringValue("LocationName"),
                                    Ecode = GetStringValue("Ecode"),
                                    FullName = GetStringValue("FullName"),

                                    // Personal/HR fields
                                    Gender = GetStringValue("Gender"),
                                    DOB = GetDateTimeValue("DOB"),
                                    AgeInYears = GetDecimalValue("AgeInYears"),

                                    DepartmentId = GetIntValue("DepartmentId").ToString(),
                                    DesignationId = GetIntValue("DesignationId").ToString(),
                                    DepartmentName = GetStringValue("DepartmentName"),
                                    DesignationName = GetStringValue("DesignationName"),

                                    DOJ = GetDateTimeValue("DOJ"),
                                    ResignationTypeName = GetStringValue("ResignationTypeName"),
                                    DateOfLeft = GetDateTimeValue("DateOfLeft"),

                                    // Bank details
                                    BankName = GetStringValue("BANK NAME"),
                                    AccountNumber = GetStringValue("A/C NO"),
                                    BankIfscCode = GetStringValue("BANK IFSC CODE"),

                                    // Address details
                                    PermanentAddress = GetStringValue("PERMANENT ADDRESS"),
                                    PermanentAddressPinCode = GetStringValue("PERMANENT ADDRESS PIN CODE"),
                                    PresentAddress = GetStringValue("PRESENT ADDRESS"),
                                    PresentAddressPinCode = GetStringValue("PRESENT ADDRESS PIN CODE"),

                                    // Contact and personal details
                                    Mobile = GetStringValue("MOBILE"),
                                    EmailAddress = GetStringValue("EMAIL ADDRESS"),
                                    AadharNo = GetStringValue("AADHAR NO"),
                                    PanNo = GetStringValue("PAN NO"),
                                    HighestQualification = GetStringValue("HIGHEST QUALIFICATION"),
                                    FatherName = GetStringValue("FATHER'S NAME"),
                                    MotherName = GetStringValue("MOTHER'S NAME"),
                                    MaritalStatus = GetStringValue("MARITIAL STATUS"),

                                    // Reporting details
                                    ReportHeadEcode = GetStringValue("ReportHeadEcode"),
                                    ReportHeadFullName = GetStringValue("ReportHeadFullName"),
                                    ReportHeadDesignation = GetStringValue("ReportHeadDesignation"),

                                    // Experience details
                                    CompanyName1 = GetStringValue("COMPANY NAME-1"),
                                    From1 = GetStringValue("From-I"),
                                    To1 = GetStringValue("To-I"),
                                    Years1 = GetDecimalValue("YEARS-1"),
                                    CompanyName2 = GetStringValue("COMPANY NAME-2"),
                                    From2 = GetStringValue("From-II"),
                                    To2 = GetStringValue("To-II"),
                                    Years2 = GetDecimalValue("YEARS-2"),
                                    CompanyName3 = GetStringValue("COMPANY NAME-3"),
                                    From3 = GetStringValue("From-III"),
                                    To3 = GetStringValue("To-III"),
                                    Years3 = GetDecimalValue("YEARS-3"),
                                    TotalExperience = GetDecimalValue("TTL EXPERIENCE"),
                                    LocStatus = GetBooleanValue("LocStatus"),
                                    EmployeeStatus = GetStringValue("EmployeeStatus"),

                                    // Audit fields
                                    EmployeeId = GetLongValue("EmployeeId"),
                                    CandidateId = GetLongValue("CandidateId"),
                                    LocBasedECode = GetStringValue("LocBasedECode"),
                                    IsActive = GetBoolValue("IsActive"),
                                    IsDeleted = GetBoolValue("IsDeleted"),
                                    DateOfJoining = GetDateTimeValue("DateOfJoining"),
                                    IsStore = GetBoolValue("IsStore"),
                                    CreatedOn = GetDateTimeValue("CreatedOn"),
                                    UpdatedOn = GetDateTimeValue("UpdatedOn"),
                                    CreatedBy = GetStringValue("CreatedBy"),
                                    UpdatedBy = GetStringValue("UpdatedBy"),
                                };
                                employees.Add(employee);
                            }
                        }

                        // Retrieve Output Parameters
                        long totalCount = Convert.ToInt64(totalEmployeesParam.Value);
                        int currentPageNumber = Convert.ToInt32(currentPageNumberParam.Value);
                        long activeCount = Convert.ToInt64(activeCountParam.Value);
                        long inactiveCount = Convert.ToInt64(inactiveCountParam.Value);
                        long abscondCnt = Convert.ToInt64(abscondCount.Value);
                        long locCountt = Convert.ToInt64(locCount.Value);

                        return (employees, totalCount, currentPageNumber, activeCount, inactiveCount, abscondCnt, locCountt);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in EmployeeListWithCards_Test: {ex.Message}");
                throw new ApplicationException("An error occurred while fetching employee details with cards (test version).", ex);
            }
        }

        public async Task<ExecuteAndReponse> RefreshEmpDetails(string eCode)
        {
            try {
                if (String.IsNullOrEmpty(eCode)) return BuildExecuteErrorResponse("ECode is mandatory to Serve, serve it accordingly",HttpStatusCode.BadRequest);
                 await _context.GetProcedures().prc_UpdateEmployeeFromCandidateByEcodeAsync(eCode);
                return BuildExecuteSuccessResponse("Executed Successfully");
            }
            catch (Exception ex) {
                return BuildExecuteErrorResponse(ex.Message,HttpStatusCode.BadRequest);
            }
        }

        public async Task<ExecuteAndReponse> UpdateEmployeeDetails(CandidateRequest empUpdateDetails, string updatedBy)
        {
            try
            {
                #region BasicInfo

                // Try to fetch an existing record from tempTblEmployee  // Have to change from tblEmployee to tempTblEmployee 
                //var employeeData = await GetOneRecordWithTrackingAsync<tblEmployee>(row => row.EmployeeId == employee.candidateInfo.id);
                var employeeInf = await GetOneRecordWithTrackingAsync<tblEmployee>(row => row.EmployeeId == Convert.ToInt64(updatedBy));
                long EmployeeId = employeeInf.EmployeeId;
                long? CandidateId = employeeInf.CandidateId;

                var employeeData = await _context.tempTblEmployees.FirstOrDefaultAsync(row => row.EmployeeId == EmployeeId && row.Is_Approved == false && row.Is_Rejected == false);
                tempTblEmployee addEmployee;
                if (employeeData == null)
                {
                    addEmployee = new tempTblEmployee
                    {
                        EmployeeId = EmployeeId,
                        CandidateId = CandidateId,
                        TITLE = empUpdateDetails.Title,
                        FirstName = empUpdateDetails.FirstName,
                        MiddleName = empUpdateDetails.MiddleName,
                        LastName = empUpdateDetails.LastName,
                        FULL_NAME = empUpdateDetails.FullName,
                        FATHER_S_NAME = empUpdateDetails.FathersName,
                        MOTHER_S_NAME = empUpdateDetails.MothersName,
                        Husband_Name = empUpdateDetails.HusbandName,
                        PLACE_OF_BIRTH = empUpdateDetails.PlaceOfBirth,
                        PAN_NO = empUpdateDetails.PanNo,
                        AADHAR_NO = empUpdateDetails.AadharNo,
                        NAME_ON_ADHAR = empUpdateDetails.NameOnAadhar,
                        DOB = empUpdateDetails.Dob,
                        PRESENT_ADDRESS = empUpdateDetails.PresentAddress,
                        PERMANENT_ADDRESS = empUpdateDetails.PermanentAddress,
                        PRESENT_ADDRESS_PIN_CODE = empUpdateDetails.PresentAddressPinCode,
                        PERMANENT_ADDRESS_PIN_CODE = empUpdateDetails.PermanentAddressPinCode,
                        MARITIAL_STATUS = empUpdateDetails.MaritalStatus,
                        MOBILE = empUpdateDetails.Mobile,
                        EMAIL_ADDRESS = empUpdateDetails.EmailAddress,
                        BENEFICIARY_ADDRESS = empUpdateDetails.BeneficiaryAddress,
                        NATIONALITY = empUpdateDetails.Nationality,
                        RELIGION = empUpdateDetails.Religion,
                        BANK_NAME = empUpdateDetails.BankName,
                        A_C_NO = empUpdateDetails.AccountNo,
                        BANK_IFSC_CODE = empUpdateDetails.BankIfscCode,
                        ISRELATIVEINCOMPANY = empUpdateDetails.IsRelativeInCompany,
                        AppliedBy = Convert.ToString(EmployeeId),
                        AppliedOn = DateTime.Now,
                        Is_TITLE_Approved = false,
                        Is_FirstName_Approved = false,
                        Is_MiddleName_Approved = false,
                        Is_LastName_Approved = false,
                        Is_FULLNAME_Approved = false,
                        Is_FATHERSNAME_Approved = false,
                        Is_MOTHERSNAME_Approved = false,
                        Is_HusbandName_Approved = false,
                        Is_PlaceOfBirth_Approved = false,
                        Is_PANNO_Approved = false,
                        Is_AADHARNO_Approved = false,
                        Is_NAMEONADHAR_Approved = false,
                        Is_DOB_Approved = false,
                        Is_PRESENTADDRESS_Approved = false,
                        Is_PERMANENTADDRESS_Approved = false,
                        Is_PRESENTPIN_Approved = false,
                        Is_PERMANENTPIN_Approved = false,
                        Is_MARITIALSTATUS_Approved = false,
                        Is_MOBILE_Approved = false,
                        Is_EMAILADDRESS_Approved = false,
                        Is_BENEFICIARYADDRESS_Approved = false,
                        Is_NATIONALITY_Approved = false,
                        Is_RELIGION_Approved = false,
                        Is_BANKNAME_Approved = false,
                        Is_ACNO_Approved = false,
                        Is_IFSC_Approved = false,
                        Is_ISRELATIVEINCOMPANY_Approved = false,
                        Is_Approved = false,
                        Is_Rejected = false
                    };
                    await _context.AddAsync(addEmployee);
                }
                else
                {
                    employeeData.EmployeeId = EmployeeId;
                    employeeData.CandidateId = CandidateId;
                    employeeData.TITLE = empUpdateDetails.Title;
                    employeeData.FirstName = empUpdateDetails.FirstName;
                    employeeData.MiddleName = empUpdateDetails.MiddleName;
                    employeeData.LastName = empUpdateDetails.LastName;
                    employeeData.FULL_NAME = empUpdateDetails.FullName;
                    employeeData.FATHER_S_NAME = empUpdateDetails.FathersName;
                    employeeData.MOTHER_S_NAME = empUpdateDetails.MothersName;
                    employeeData.Husband_Name = empUpdateDetails.HusbandName;
                    employeeData.PLACE_OF_BIRTH = empUpdateDetails.PlaceOfBirth;
                    employeeData.PAN_NO = empUpdateDetails.PanNo;
                    employeeData.AADHAR_NO = empUpdateDetails.AadharNo;
                    employeeData.NAME_ON_ADHAR = empUpdateDetails.NameOnAadhar;
                    employeeData.DOB = empUpdateDetails.Dob;
                    employeeData.PRESENT_ADDRESS = empUpdateDetails.PresentAddress;
                    employeeData.PERMANENT_ADDRESS = empUpdateDetails.PermanentAddress;
                    employeeData.PRESENT_ADDRESS_PIN_CODE = empUpdateDetails.PresentAddressPinCode;
                    employeeData.PERMANENT_ADDRESS_PIN_CODE = empUpdateDetails.PermanentAddressPinCode;
                    employeeData.MARITIAL_STATUS = empUpdateDetails.MaritalStatus;
                    employeeData.MOBILE = empUpdateDetails.Mobile;
                    employeeData.EMAIL_ADDRESS = empUpdateDetails.EmailAddress;
                    employeeData.BENEFICIARY_ADDRESS = empUpdateDetails.BeneficiaryAddress;
                    employeeData.NATIONALITY = empUpdateDetails.Nationality;
                    employeeData.RELIGION = empUpdateDetails.Religion;
                    employeeData.BANK_NAME = empUpdateDetails.BankName;
                    employeeData.A_C_NO = empUpdateDetails.AccountNo;
                    employeeData.BANK_IFSC_CODE = empUpdateDetails.BankIfscCode;
                    employeeData.ISRELATIVEINCOMPANY = empUpdateDetails.IsRelativeInCompany;
                    employeeData.UpdatedBy = Convert.ToString(EmployeeId);
                    employeeData.UpdatedOn = DateTime.Now;
                    employeeData.Is_TITLE_Approved = false;
                    employeeData.Is_FirstName_Approved = false;
                    employeeData.Is_MiddleName_Approved = false;
                    employeeData.Is_LastName_Approved = false;
                    employeeData.Is_FULLNAME_Approved = false;
                    employeeData.Is_FATHERSNAME_Approved = false;
                    employeeData.Is_MOTHERSNAME_Approved = false;
                    employeeData.Is_HusbandName_Approved = false;
                    employeeData.Is_PlaceOfBirth_Approved = false;
                    employeeData.Is_PANNO_Approved = false;
                    employeeData.Is_AADHARNO_Approved = false;
                    employeeData.Is_NAMEONADHAR_Approved = false;
                    employeeData.Is_DOB_Approved = false;
                    employeeData.Is_PRESENTADDRESS_Approved = false;
                    employeeData.Is_PERMANENTADDRESS_Approved= false;
                    employeeData.Is_PRESENTPIN_Approved = false;
                    employeeData.Is_PERMANENTPIN_Approved = false;
                    employeeData.Is_MARITIALSTATUS_Approved = false;
                    employeeData.Is_MOBILE_Approved = false;
                    employeeData.Is_EMAILADDRESS_Approved = false;
                    employeeData.Is_BENEFICIARYADDRESS_Approved = false;
                    employeeData.Is_NATIONALITY_Approved = false;
                    employeeData.Is_RELIGION_Approved = false;
                    employeeData.Is_BANKNAME_Approved = false;
                    employeeData.Is_ACNO_Approved = false;
                    employeeData.Is_IFSC_Approved = false;
                    employeeData.Is_ISRELATIVEINCOMPANY_Approved = false;
                    employeeData.Is_Approved = false;
                    employeeData.Is_Rejected = false;
                }
                int ra = await _context.SaveChangesAsync();
                if (ra < 1)
                    throw new Exception("Unable to Save employee data.");

                #endregion BasicInfo

                var idPass = (CandidateId != null && CandidateId > 0) ? CandidateId : EmployeeId;

                #region Family

                var familyData =_context.tempTblFamilies.Where(row => row.EmpId == EmployeeId && row.Is_Approved == false && row.IsRejected == false && row.IsDeleted == false && row.IsActive == true).ToList();

                if (empUpdateDetails.FamilyMemberDetails != null && empUpdateDetails.FamilyMemberDetails.Count>0)
                {
                    var newFamilyMembers = new List<tempTblFamily>();
                    if (familyData != null && familyData.Count > 0)
                    {
                        foreach (var family in familyData)
                        {
                            var match = empUpdateDetails.FamilyMemberDetails
                                .FirstOrDefault(row => row.ID == family.FID && row.ID !=0  && row.ID !=null);

                            if (match != null)
                            {
                                family.Family_Member_Name = match.FamilyMemberName;
                                family.Relation = match.Relation;
                                family.DOB = match.Dob;
                                family.UpdatedBy = updatedBy;
                                family.UpdatedOn = DateTime.Now;
                                family.IsDeleted = false;  
                                family.IsActive = true;
                                family.Is_DOB_Approved = false;
                                family.Is_FamilyMemberName_Approved = false;
                                family.Is_Relation_Approved = false;
                                family.Is_Approved = false;
                                family.IsRejected = false;
                            }
                            else
                            {
                                family.IsDeleted = true;    // soft deleted  
                                family.IsActive = false;       
                                family.UpdatedOn = DateTime.Now;
                                family.UpdatedBy = updatedBy;
                            }
                        }

                        var newMembers = empUpdateDetails.FamilyMemberDetails
                            .Where(row => row.ID == 0 || row.ID == null)
                            .ToList();

                        foreach (var newMember in newMembers)
                        {
                            var newEntry = new tempTblFamily    
                            {
                                FID = newMember.ID,
                                EmpId = EmployeeId,
                                CID = CandidateId,
                                Family_Member_Name = newMember.FamilyMemberName,
                                Relation = newMember.Relation,
                                DOB = newMember.Dob,
                                AppliedBy = updatedBy,
                                AppliedOn = DateTime.Now,
                                IsDeleted = false, 
                                IsActive = true,
                                Is_DOB_Approved = false,
                                Is_FamilyMemberName_Approved = false,
                                Is_Relation_Approved = false,
                                Is_Approved = false
                            };

                            _context.tempTblFamilies.Add(newEntry);
                        }
                    }
                    else
                    {
                        var newMembers = empUpdateDetails.FamilyMemberDetails
                            .ToList();

                        foreach (var newMember in newMembers)
                        {
                            var newEntry = new tempTblFamily         
                            {
                                FID = newMember.ID,
                                EmpId = EmployeeId,
                                CID = CandidateId,
                                Family_Member_Name = newMember.FamilyMemberName,
                                Relation = newMember.Relation,
                                DOB = newMember.Dob,
                                AppliedBy = updatedBy,
                                AppliedOn = DateTime.Now,
                                IsDeleted = false,   
                                IsActive = true, 
                                Is_DOB_Approved = false,
                                Is_FamilyMemberName_Approved = false,
                                Is_Relation_Approved = false,
                                Is_Approved = false,
                                IsRejected = false
                            };

                            _context.tempTblFamilies.Add(newEntry);
                        }
                    }

                }
                else
                {
                    // If list is null, soft-delete all existing records
                    foreach (var family in familyData)
                    {
                        family.IsDeleted = true; 
                        family.IsActive = false;   
                        family.UpdatedOn = DateTime.UtcNow;
                        family.UpdatedBy = updatedBy;
                    }
                }

                ra = await _context.SaveChangesAsync();

                #endregion Family

                #region Experience

                var experienceData = _context.tempTblExperiences.Where(row => row.EmpId == EmployeeId && row.Is_Approved == false && row.IsRejected == false && row.IsDeleted == false && row.IsActive == true).ToList(); //&& row.IsDeleted == false && row.IsActive == true

                if (empUpdateDetails.ExperienceDetails != null && empUpdateDetails.ExperienceDetails.Count > 0)
                {
                    var newExperienceData = new List<tempTblExperience>();

                    if (experienceData != null && experienceData.Count > 0)
                    {
                        foreach (var experience in experienceData)
                        {
                            var match = empUpdateDetails.ExperienceDetails
                                .FirstOrDefault(row => row.ID == experience.EID && row.ID != 0 && row.ID != null);

                            if (match != null)
                            {
                                experience.Name_of_Company = match.NameOfCompany;
                                experience.Work_Location = match.WorkLocation;
                                experience.Position_Held = match.PositionHeld;
                                experience.From = match.From;
                                experience.To = match.To;
                                experience.Last_CTC = match.LastCtc;
                                experience.UpdatedBy = updatedBy;
                                experience.UpdatedOn = DateTime.Now;
                                experience.IsDeleted = false; 
                                experience.IsActive = true;
                                experience.Is_NameOfCompany_Approved = false;
                                experience.Is_WorkLocation_Approved = false;
                                experience.Is_PositionHeld_Approved = false;
                                experience.Is_FromDate_Approved = false;
                                experience.Is_ToDate_Approved = false;
                                experience.Is_LastCTC_Approved = false;
                                experience.Is_Approved = false;
                                experience.IsRejected = false;
                            }
                            else
                            {
                                experience.IsDeleted = true;  // soft delete 
                                experience.IsActive = false;
                                experience.UpdatedBy = updatedBy;
                                experience.UpdatedOn = DateTime.Now;
                            }
                        }

                        var newExperiences = empUpdateDetails.ExperienceDetails
                            .Where(x => x.ID == 0 || x.ID == null)
                            .ToList();

                        foreach (var newExperience in newExperiences)
                        {
                            var newEntry = new tempTblExperience 
                            {
                                EID = newExperience.ID,
                                EmpId = EmployeeId,
                                CID = CandidateId,
                                Name_of_Company = newExperience.NameOfCompany,
                                Work_Location = newExperience.WorkLocation,
                                Position_Held = newExperience.PositionHeld,
                                From = newExperience.From,
                                To = newExperience.To,
                                Last_CTC = newExperience.LastCtc,
                                AppliedBy = updatedBy,
                                AppliedOn = DateTime.Now,
                                IsDeleted = false, 
                                IsActive = true,
                                Is_NameOfCompany_Approved = false,
                                Is_WorkLocation_Approved = false,
                                Is_PositionHeld_Approved = false,
                                Is_FromDate_Approved = false,
                                Is_ToDate_Approved = false,
                                Is_LastCTC_Approved = false,
                                Is_Approved = false,
                                IsRejected = false
                            };

                            _context.tempTblExperiences.Add(newEntry);
                        }
                    }
                    else
                    {
                        var newExperiences = empUpdateDetails.ExperienceDetails.ToList();

                        foreach (var newExperience in newExperiences)
                        {
                            var newEntry = new tempTblExperience 
                            {
                                EID = newExperience.ID,
                                EmpId = EmployeeId,
                                CID = CandidateId,
                                Name_of_Company = newExperience.NameOfCompany,
                                Work_Location = newExperience.WorkLocation,
                                Position_Held = newExperience.PositionHeld,
                                From = newExperience.From,
                                To = newExperience.To,
                                Last_CTC = newExperience.LastCtc,
                                AppliedBy = updatedBy,
                                AppliedOn = DateTime.Now,
                                IsDeleted = false,
                                IsActive = true,
                                Is_NameOfCompany_Approved = false,
                                Is_WorkLocation_Approved = false,
                                Is_PositionHeld_Approved = false,
                                Is_FromDate_Approved = false,
                                Is_ToDate_Approved = false,
                                Is_LastCTC_Approved = false,
                                Is_Approved = false,
                                IsRejected = false
                            };

                            _context.tempTblExperiences.Add(newEntry);
                        }

                    }
                }
                else
                {
                    // If experience list is null, soft-delete all existing records
                    foreach (var experience in experienceData)
                    {
                        experience.IsDeleted = true;             // have to add these fields  
                        experience.IsActive = false;             // have to add these fields  
                        experience.UpdatedBy = updatedBy;
                        experience.UpdatedOn = DateTime.Now;
                    }
                }

                ra = await _context.SaveChangesAsync();
                #endregion Experience

                #region Qualification

                var qualificationData = _context.tempTblQualifications.Where(row => row.EmpId == EmployeeId && row.Is_Approved == false && row.IsRejected == false && row.IsDeleted == false && row.IsActive == true) // && row.IsDeleted == false && row.IsActive == true
                    .ToList();

                if (empUpdateDetails.QualificationDetails != null && empUpdateDetails.QualificationDetails.Count > 0)
                {
                    var newQualificationData = new List<tempTblQualification>();    //  have to change from tblQualification to tempTblQualification

                    if (qualificationData != null && qualificationData.Count > 0)
                    {
                        foreach (var qualification in qualificationData)
                        {
                            var match = empUpdateDetails.QualificationDetails
                                .FirstOrDefault(row => row.ID == qualification.QID && row.ID != 0 && row.ID != null);

                            if (match != null)
                            {
                                qualification.Education = match.Education;
                                qualification.YOP = match.Yop;
                                qualification.Grade = match.Grade;
                                qualification.Type = match.Type;
                                qualification.UpdatedBy = updatedBy;
                                qualification.UpdatedOn = DateTime.Now;
                                qualification.IsDeleted = false;
                                qualification.IsActive = true;
                                qualification.Is_Education_Approved = false;
                                qualification.Is_YOP_Approved = false;
                                qualification.Is_Grade_Approved = false;
                                qualification.Is_Type_Approved = false;
                                qualification.Is_Approved = false;
                                qualification.IsRejected = false;
                            }
                            else
                            {
                                qualification.IsDeleted = true; // soft delete
                                qualification.IsActive = false;
                                qualification.UpdatedBy = updatedBy;
                                qualification.UpdatedOn = DateTime.Now;
                            }
                        }

                        var newQualifications = empUpdateDetails.QualificationDetails
                            .Where(x => x.ID == 0 || x.ID == null)
                            .ToList();

                        foreach (var newQualification in newQualifications)
                        {
                            var newEntry = new tempTblQualification
                            {
                                QID = newQualification.ID,
                                EmpId = EmployeeId,
                                CID = CandidateId,
                                Education = newQualification.Education,
                                YOP = newQualification.Yop,
                                Grade = newQualification.Grade,
                                Type = newQualification.Type,
                                AppliedBy = updatedBy,
                                AppliedOn = DateTime.Now,
                                IsDeleted = false,
                                IsActive = true,
                                Is_Education_Approved = false,
                                Is_YOP_Approved = false,
                                Is_Grade_Approved = false,
                                Is_Type_Approved = false,
                                Is_Approved = false,
                                IsRejected = false

                            };

                            _context.tempTblQualifications.Add(newEntry);
                        }
                    }
                    else
                    {
                        var newQualifications = empUpdateDetails.QualificationDetails
                            .ToList();

                        foreach (var newQualification in newQualifications)
                        {
                            var newEntry = new tempTblQualification
                            {
                                QID = newQualification.ID,
                                EmpId = EmployeeId,
                                CID = CandidateId,
                                Education = newQualification.Education,
                                YOP = newQualification.Yop,
                                Grade = newQualification.Grade,
                                Type = newQualification.Type,
                                AppliedBy = updatedBy,
                                AppliedOn = DateTime.Now,
                                IsDeleted = false,
                                IsActive = true,
                                Is_Education_Approved = false,
                                Is_YOP_Approved = false,
                                Is_Grade_Approved = false,
                                Is_Type_Approved = false,
                                Is_Approved = false,
                                IsRejected = false

                            };

                            _context.tempTblQualifications.Add(newEntry);
                        }

                    }
                }
                else
                {
                    // Soft-delete all existing qualifications if no list is provided
                    foreach (var qualification in qualificationData)
                    {
                        qualification.IsDeleted = true;
                        qualification.IsActive = false;
                        qualification.UpdatedBy = updatedBy;
                        qualification.UpdatedOn = DateTime.Now;
                    }
                }

                ra = await _context.SaveChangesAsync();

                //if (ra < 1)
                //    return BuildExecuteErrorResponse("Unable to Save Qualification Details", HttpStatusCode.BadRequest);

                #endregion Qualification

                #region Attatchments

                var existingDocs = _context.tempCandidateDocs.Where(doc => doc.EmpId == EmployeeId && doc.Is_Approved == false && doc.IsRejected == false && doc.IsDeleted == false && doc.IsActive == true).ToList();
                var attachments = empUpdateDetails.Attachments;
                if (attachments != null)
                {
                    var newDocs = new List<tempCandidateDoc>();

                    void ProcessDocs(List<string> files, string docType)
                    {
                        if (files != null && files.Count > 0)
                        {
                            foreach (var file in files)
                            {
                                string baseUrl = GetBaseUrl(); // from above method
                                //string relativePath = file.Replace(baseUrl + "/", "").Replace("\\", "/");
                                string relativePath = file.Replace(baseUrl + "/", "");
                                //string relativePath = file.IndexOf("Documents") >= 0 ? file.Substring(file.IndexOf("Documents")) : file;
                                string filePath = Path.Combine("wwwroot", relativePath);
                                long fileSizeInBytes = new FileInfo(filePath).Length;
                                var exists = existingDocs.FirstOrDefault(doc => doc.FileType == docType && doc.FilePath == relativePath);
                                if (exists == null)
                                {
                                    newDocs.Add(new tempCandidateDoc
                                    {
                                        EmpId = EmployeeId,
                                        CID = CandidateId,
                                        FileType = docType,
                                        FilePath = relativePath,
                                        FileSize = Convert.ToString(fileSizeInBytes),
                                        CreatedBy = updatedBy,
                                        CreatedOn = DateTime.Now,
                                        IsDeleted = false,  // Add these fields if your schema supports soft-delete
                                        IsActive = true,
                                        Is_Approved = false,
                                        IsRejected = false
                                    });
                                }
                            }
                        }
                    }

                    ProcessDocs(attachments.PassportPhoto, "PassportPhoto");
                    ProcessDocs(attachments.Pan, "Pan");
                    ProcessDocs(attachments.Aadhar, "Aadhar");
                    ProcessDocs(attachments.SalarySlip, "SalarySlip");
                    ProcessDocs(attachments.BankPassbook, "BankPassbook");
                    ProcessDocs(attachments.BankStatement, "BankStatement");
                    ProcessDocs(attachments.PrevOfferLetter, "PrevOfferLetter");
                    ProcessDocs(attachments.Education, "Education");
                    ProcessDocs(attachments.Resume, "Resume");
                    ProcessDocs(attachments.OfferLetter, "OfferLetter");
                    ProcessDocs(attachments.Others, "Others");

                    

                    if (newDocs.Count > 0)
                        _context.tempCandidateDocs.AddRange(newDocs);   //  if it will not work then try existingDocs.AddRange(newDocs)

                    // Soft-delete existing records that are not in the current list
                    //var allNewFilePaths = newDocs.Select(doc => doc.FilePath).ToList();
       
                    List<string> CurrentFilePaths = new List<string>();
                    if(attachments.PassportPhoto != null && attachments.PassportPhoto.Count >0)
                        CurrentFilePaths.AddRange(attachments.PassportPhoto);

                    if (attachments.Pan != null && attachments.Pan.Count > 0)
                        CurrentFilePaths.AddRange(attachments.Pan);

                    if (attachments.Aadhar != null && attachments.Aadhar.Count > 0)
                        CurrentFilePaths.AddRange(attachments.Aadhar);

                    if (attachments.SalarySlip != null && attachments.SalarySlip.Count > 0)
                        CurrentFilePaths.AddRange(attachments.SalarySlip);

                    if (attachments.BankPassbook != null && attachments.BankPassbook.Count > 0)
                        CurrentFilePaths.AddRange(attachments.BankPassbook);

                    if (attachments.BankStatement != null && attachments.BankStatement.Count > 0)
                        CurrentFilePaths.AddRange(attachments.BankStatement);

                    if (attachments.PrevOfferLetter != null && attachments.PrevOfferLetter.Count > 0)
                        CurrentFilePaths.AddRange(attachments.PrevOfferLetter);

                    if (attachments.Education != null && attachments.Education.Count > 0)
                        CurrentFilePaths.AddRange(attachments.Education);

                    if (attachments.Resume != null && attachments.Resume.Count > 0)
                        CurrentFilePaths.AddRange(attachments.Resume);

                    if (attachments.OfferLetter != null && attachments.OfferLetter.Count > 0)
                        CurrentFilePaths.AddRange(attachments.OfferLetter);

                    if (attachments.Others != null && attachments.Others.Count > 0)
                        CurrentFilePaths.AddRange(attachments.Others);

                    string baseUrl = GetBaseUrl();
                    var cleanedNewPaths = CurrentFilePaths
                        .Select(file => file.Replace(baseUrl + "/", ""))
                        .ToList();

                    foreach (var doc in existingDocs)
                    {
                        if (!cleanedNewPaths.Contains(doc.FilePath))
                        {
                            doc.IsDeleted = true;
                            doc.IsActive = false;
                            doc.UpdatedOn = DateTime.Now;
                            doc.UpdatedBy = updatedBy;
                        }
                    }
                }
                else
                {
                    // If attachment list is null, soft-delete all existing records
                    foreach (var doc in existingDocs)
                    {
                        doc.IsDeleted = true;
                        doc.IsActive = false;
                        doc.UpdatedOn = DateTime.Now;
                        doc.UpdatedBy = updatedBy;
                    }
                }

                ra = await _context.SaveChangesAsync();

                #endregion Attatchments
                
                return BuildExecuteSuccessResponse("Updated Successfully");
            }
            catch (Exception ex)
            {
                return BuildExecuteErrorResponse(ex.Message, HttpStatusCode.BadRequest);
            }
        }

        public string GetBaseUrl()
        {
            var request = _httpContextAccessor.HttpContext?.Request;
            if (request == null) return null;

            return $"{request.Scheme}://{request.Host}";
        }

        public async Task<List<EmployeeUpdateInfo>> GetPendingEmployeeUpdateListAsync()
        {
            var result = await (from temp in _context.tempTblEmployees
                                join emp in _context.tblEmployees
                                    on temp.EmployeeId equals emp.EmployeeId
                                where temp.Is_Approved == false && temp.Is_Rejected == false
                                select new EmployeeUpdateInfo
                                {
                                    EmployeeId = Convert.ToString(temp.EmployeeId),
                                    CandidateId = Convert.ToString(temp.CandidateId),
                                    FirstName = emp.FirstName ?? "",
                                    MiddleName = emp.MiddleName ?? "",
                                    LastName = emp.LastName ?? "",
                                    Department = emp.DepartmentId.ToString() ?? "",
                                    ReportingHeadName = _context.tblEmployees
                                                        .Where(e => e.EmployeeId == (_context.tblEmployees.Where(e => e.Ecode == emp.ReportHeadEcode)
                                                        .Select(a => (int?)a.EmployeeId)
                                                        .FirstOrDefault()))
                                                        .Select(a => a.FirstName ?? a.FULL_NAME)
                                                        .FirstOrDefault(),
                                    ReportingHeadECode = _context.tblEmployees
                                                         .Where(e => e.EmployeeId == (_context.tblEmployees.Where(e => e.Ecode == emp.ReportHeadEcode)
                                                        .Select(a => (int?)a.EmployeeId)
                                                        .FirstOrDefault()))
                                                         .Select(a => a.Ecode)
                                                         .FirstOrDefault(),
                                    EmailAddress = emp.EMAIL_ADDRESS ?? "",                                   
                                    Mobile = emp.MOBILE ?? ""                                    
                                }).ToListAsync();

           

            return result;
        }
        /*
        public async Task<List<EmployeeCombinedDto>> GetAllEmployeeUpdateComparisonsAsync()
        {
            var employees = await _context.tempTblEmployees
                .Select(e => new EmployeeCombinedDto
                {
                    
                    Updated = new TempEmployeeDataDto
                    {
                        Employee = _context.tempTblEmployees.FirstOrDefault(te => te.EmployeeId == e.EmployeeId && te.Is_Approved == false && te.Is_Rejected == false),
                        Family = _context.tempTblFamilies.Where(tf => tf.EmpId == e.EmployeeId).ToList(),
                        Experience = _context.tempTblExperiences.Where(tex => tex.EmpId == e.EmployeeId).ToList(),
                        Qualification = _context.tempTblQualifications.Where(tq => tq.EmpId == e.EmployeeId).ToList(),
                        Documents = _context.tempCandidateDocs.Where(td => td.EmpId == e.EmployeeId).ToList()
                    },
                    Original = new EmployeeDataDto
                    {
                        Employee = _context.tblEmployees.FirstOrDefault(row => row.EmployeeId == e.EmployeeId),
                        Family = _context.tblFamilies.Where(f => f.CID == (e.CandidateId == null ? e.EmployeeId : e.CandidateId)).ToList(),
                        Experience = _context.tblExperiences.Where(ex => ex.CID == (e.CandidateId == null ? e.EmployeeId : e.CandidateId)).ToList(),
                        Qualification = _context.tblQualifications.Where(q => q.CID == (e.CandidateId == null ? e.EmployeeId : e.CandidateId)).ToList(),
                        Documents = _context.CanidateDocs.Where(d => d.CId == (e.CandidateId == null ? e.EmployeeId : e.CandidateId)).ToList()
                    }
                })
                .ToListAsync();

            return employees;
        }
        */
        
        public async Task<EmployeeDetailsUpdateView> GetChangedFieldsForEmployeeAsync(long employeeId)
        {
            EmployeeDetailsUpdateView result = new EmployeeDetailsUpdateView();

            result.EmployeeId = employeeId;
            var tempEmp = await _context.tempTblEmployees.FirstOrDefaultAsync(e => e.EmployeeId == employeeId && e.Is_Approved == false && e.Is_Rejected == false);
            var permEmp = await _context.tblEmployees.FirstOrDefaultAsync(e => e.EmployeeId == employeeId);
            long? CandidateId = permEmp.CandidateId;
            long? idPass = CandidateId == null ? employeeId : CandidateId;

            if (tempEmp != null && permEmp != null)
            {
                result.EmployeeDetailsForUpdate = GetEmployeeChanges(tempEmp, permEmp);
            }
            
            result.FamilyDetailsForUpdate = await GetFamilyChangesAsync(employeeId,CandidateId ,idPass);
            result.ExperienceDetailsForUpdate = await GetExperienceChangesAsync(employeeId, CandidateId, idPass);
            result.QualificationDetailsForUpdate = await GetQualificationChangesAsync(employeeId, CandidateId, idPass);
            result.DocumentsDetailsForUpdate = await GetDocumentChangesAsync(employeeId, CandidateId, idPass);
            

            return result;
        }

        public List<ChangedFieldDto> GetEmployeeChanges(tempTblEmployee temp, tblEmployee perm)
        {
            var changes = new List<ChangedFieldDto>();

            if (temp.TITLE != perm.TITLE)
                changes.Add(new ChangedFieldDto { FieldName = "TITLE", OldValue = perm.TITLE, NewValue = temp.TITLE, IsApprovedField = "Is_TITLE_Approved", IsApproved = temp.Is_TITLE_Approved });

            if (temp.FirstName != perm.FirstName)
                changes.Add(new ChangedFieldDto { FieldName = "FirstName", OldValue = perm.FirstName, NewValue = temp.FirstName, IsApprovedField = "Is_FirstName_Approved", IsApproved = temp.Is_FirstName_Approved });

            if (temp.MiddleName != perm.MiddleName)
                changes.Add(new ChangedFieldDto { FieldName = "MiddleName", OldValue = perm.MiddleName, NewValue = temp.MiddleName, IsApprovedField = "Is_MiddleName_Approved", IsApproved = temp.Is_MiddleName_Approved });

            if (temp.LastName != perm.LastName)
                changes.Add(new ChangedFieldDto { FieldName = "LastName", OldValue = perm.LastName, NewValue = temp.LastName, IsApprovedField = "Is_LastName_Approved", IsApproved = temp.Is_LastName_Approved });

            if (temp.FULL_NAME != perm.FULL_NAME)
                changes.Add(new ChangedFieldDto { FieldName = "FULL_NAME", OldValue = perm.FULL_NAME, NewValue = temp.FULL_NAME, IsApprovedField = "Is_FULLNAME_Approved", IsApproved = temp.Is_FULLNAME_Approved });

            if (temp.FATHER_S_NAME != perm.FATHER_S_NAME)
                changes.Add(new ChangedFieldDto { FieldName = "FATHER_S_NAME", OldValue = perm.FATHER_S_NAME, NewValue = temp.FATHER_S_NAME, IsApprovedField = "Is_FATHERSNAME_Approved", IsApproved = temp.Is_FATHERSNAME_Approved });

            if (temp.MOTHER_S_NAME != perm.MOTHER_S_NAME)
                changes.Add(new ChangedFieldDto { FieldName = "MOTHER_S_NAME", OldValue = perm.MOTHER_S_NAME, NewValue = temp.MOTHER_S_NAME, IsApprovedField = "Is_MOTHERSNAME_Approved", IsApproved = temp.Is_MOTHERSNAME_Approved });

            if (temp.Husband_Name != perm.Husband_Name)
                changes.Add(new ChangedFieldDto { FieldName = "Husband_Name", OldValue = perm.Husband_Name, NewValue = temp.Husband_Name, IsApprovedField = "Is_HusbandName_Approved", IsApproved = temp.Is_HusbandName_Approved });

            if (temp.PLACE_OF_BIRTH != perm.PLACE_OF_BIRTH)
                changes.Add(new ChangedFieldDto { FieldName = "PLACE_OF_BIRTH", OldValue = perm.PLACE_OF_BIRTH, NewValue = temp.PLACE_OF_BIRTH, IsApprovedField = "Is_PlaceOfBirth_Approved", IsApproved = temp.Is_PlaceOfBirth_Approved });

            if (temp.PAN_NO != perm.PAN_NO)
                changes.Add(new ChangedFieldDto { FieldName = "PAN_NO", OldValue = perm.PAN_NO, NewValue = temp.PAN_NO, IsApprovedField = "Is_PANNO_Approved", IsApproved = temp.Is_PANNO_Approved });

            if (temp.AADHAR_NO != perm.AADHAR_NO)
                changes.Add(new ChangedFieldDto { FieldName = "AADHAR_NO", OldValue = perm.AADHAR_NO, NewValue = temp.AADHAR_NO, IsApprovedField = "Is_AADHARNO_Approved", IsApproved = temp.Is_AADHARNO_Approved });

            if (temp.NAME_ON_ADHAR != perm.NAME_ON_ADHAR)
                changes.Add(new ChangedFieldDto { FieldName = "NAME_ON_ADHAR", OldValue = perm.NAME_ON_ADHAR, NewValue = temp.NAME_ON_ADHAR, IsApprovedField = "Is_NAMEONADHAR_Approved", IsApproved = temp.Is_NAMEONADHAR_Approved });

            if (temp.DOB != perm.DOB)
                changes.Add(new ChangedFieldDto { FieldName = "DOB", OldValue = perm.DOB?.ToString("yyyy-MM-dd"), NewValue = temp.DOB?.ToString("yyyy-MM-dd"), IsApprovedField = "Is_DOB_Approved", IsApproved = temp.Is_DOB_Approved });

            if (temp.PRESENT_ADDRESS != perm.PRESENT_ADDRESS)
                changes.Add(new ChangedFieldDto { FieldName = "PRESENT_ADDRESS", OldValue = perm.PRESENT_ADDRESS, NewValue = temp.PRESENT_ADDRESS, IsApprovedField = "Is_PRESENTADDRESS_Approved", IsApproved = temp.Is_PRESENTADDRESS_Approved });

            if (temp.PERMANENT_ADDRESS != perm.PERMANENT_ADDRESS)
                changes.Add(new ChangedFieldDto { FieldName = "PERMANENT_ADDRESS", OldValue = perm.PERMANENT_ADDRESS, NewValue = temp.PERMANENT_ADDRESS, IsApprovedField = "Is_PERMANENTADDRESS_Approved", IsApproved = temp.Is_PERMANENTADDRESS_Approved });

            if (temp.PRESENT_ADDRESS_PIN_CODE != perm.PRESENT_ADDRESS_PIN_CODE)
                changes.Add(new ChangedFieldDto { FieldName = "PRESENT_ADDRESS_PIN_CODE", OldValue = perm.PRESENT_ADDRESS_PIN_CODE, NewValue = temp.PRESENT_ADDRESS_PIN_CODE, IsApprovedField = "Is_PRESENTPIN_Approved", IsApproved = temp.Is_PRESENTPIN_Approved });

            if (temp.PERMANENT_ADDRESS_PIN_CODE != perm.PERMANENT_ADDRESS_PIN_CODE)
                changes.Add(new ChangedFieldDto { FieldName = "PERMANENT_ADDRESS_PIN_CODE", OldValue = perm.PERMANENT_ADDRESS_PIN_CODE, NewValue = temp.PERMANENT_ADDRESS_PIN_CODE, IsApprovedField = "Is_PERMANENTPIN_Approved", IsApproved = temp.Is_PERMANENTPIN_Approved });

            if (temp.MARITIAL_STATUS != perm.MARITIAL_STATUS)
                changes.Add(new ChangedFieldDto { FieldName = "MARITIAL_STATUS", OldValue = perm.MARITIAL_STATUS, NewValue = temp.MARITIAL_STATUS, IsApprovedField = "Is_MARITIALSTATUS_Approved", IsApproved = temp.Is_MARITIALSTATUS_Approved });

            if (temp.MOBILE != perm.MOBILE)
                changes.Add(new ChangedFieldDto { FieldName = "MOBILE", OldValue = perm.MOBILE, NewValue = temp.MOBILE, IsApprovedField = "Is_MOBILE_Approved", IsApproved = temp.Is_MOBILE_Approved });

            if (temp.EMAIL_ADDRESS != perm.EMAIL_ADDRESS)
                changes.Add(new ChangedFieldDto { FieldName = "EMAIL_ADDRESS", OldValue = perm.EMAIL_ADDRESS, NewValue = temp.EMAIL_ADDRESS, IsApprovedField = "Is_EMAILADDRESS_Approved", IsApproved = temp.Is_EMAILADDRESS_Approved });

            if (temp.BENEFICIARY_ADDRESS != perm.BENEFICIARY_ADDRESS)
                changes.Add(new ChangedFieldDto { FieldName = "BENEFICIARY_ADDRESS", OldValue = perm.BENEFICIARY_ADDRESS, NewValue = temp.BENEFICIARY_ADDRESS, IsApprovedField = "Is_BENEFICIARYADDRESS_Approved", IsApproved = temp.Is_BENEFICIARYADDRESS_Approved });

            if (temp.NATIONALITY != perm.NATIONALITY)
                changes.Add(new ChangedFieldDto { FieldName = "NATIONALITY", OldValue = perm.NATIONALITY, NewValue = temp.NATIONALITY, IsApprovedField = "Is_NATIONALITY_Approved", IsApproved = temp.Is_NATIONALITY_Approved });

            if (temp.RELIGION != perm.RELIGION)
                changes.Add(new ChangedFieldDto { FieldName = "RELIGION", OldValue = perm.RELIGION, NewValue = temp.RELIGION, IsApprovedField = "Is_RELIGION_Approved", IsApproved = temp.Is_RELIGION_Approved });

            if (temp.BANK_NAME != perm.BANK_NAME)
                changes.Add(new ChangedFieldDto { FieldName = "BANK_NAME", OldValue = perm.BANK_NAME, NewValue = temp.BANK_NAME, IsApprovedField = "Is_BANKNAME_Approved", IsApproved = temp.Is_BANKNAME_Approved });

            if (temp.A_C_NO != perm.A_C_NO)
                changes.Add(new ChangedFieldDto { FieldName = "A_C_NO", OldValue = perm.A_C_NO, NewValue = temp.A_C_NO, IsApprovedField = "Is_ACNO_Approved", IsApproved = temp.Is_ACNO_Approved });

            if (temp.BANK_IFSC_CODE != perm.BANK_IFSC_CODE)
                changes.Add(new ChangedFieldDto { FieldName = "BANK_IFSC_CODE", OldValue = perm.BANK_IFSC_CODE, NewValue = temp.BANK_IFSC_CODE, IsApprovedField = "Is_IFSC_Approved", IsApproved = temp.Is_IFSC_Approved });

            if (temp.ISRELATIVEINCOMPANY != perm.ISRELATIVEINCOMPANY)
                changes.Add(new ChangedFieldDto
                {
                    FieldName = "ISRELATIVEINCOMPANY",
                    OldValue = perm.ISRELATIVEINCOMPANY?.ToString(),
                    NewValue = temp.ISRELATIVEINCOMPANY?.ToString(),
                    IsApprovedField = "Is_ISRELATIVEINCOMPANY_Approved",
                    IsApproved = temp.Is_ISRELATIVEINCOMPANY_Approved
                });

            return changes;
        }

        public async Task<List<FamilyChangeDto>> GetFamilyChangesAsync(long employeeId, long? CandidateId, long? idPass)
        {
            var tempFamilies = await _context.tempTblFamilies
                .Where(f => (f.EmpId == employeeId) && (f.IsDeleted == false) && (f.IsActive == true) && (f.IsRejected == false) && (f.Is_Approved == false))
                .ToListAsync();

            var permFamilies = await _context.tblFamilies
                .Where(f => (f.CID == idPass) && (f.IsDeleted == false) && (f.IsActive == true))
                .ToListAsync();

            var changes = new List<FamilyChangeDto>();

            var matched = new bool[tempFamilies.Count];

            foreach (var listOldItem in permFamilies)
            {
                bool found = false;
                for (int i = 0; i < (tempFamilies.Count); i++)
                {
                    if (!matched[i] && listOldItem.ID == tempFamilies[i].FID)
                    {
                        if (listOldItem.Family_Member_Name != (tempFamilies[i].Family_Member_Name) || listOldItem.DOB?.ToString("yyyy-MM-dd") != (tempFamilies[i].DOB?.ToString("yyyy-MM-dd")) || listOldItem.Relation != (tempFamilies[i].Relation))
                        {
                            changes.Add(new FamilyChangeDto {
                                FID = (listOldItem.ID),
                                ChangeType = "Updated",
                                Is_Approved = false,
                                OldData = new FamilyDDto 
                                {
                                    EmpId = employeeId,
                                    FID  = (listOldItem.ID),
                                    Family_Member_Name = listOldItem.Family_Member_Name,
                                    Relation = listOldItem.Relation,
                                    DOB = (DateTime)listOldItem.DOB,
                                    Is_FamilyMemberName_Approved = false,
                                    Is_Relation_Approved = false,
                                    Is_DOB_Approved = false
                                },
                                NewData = new FamilyDDto
                                {
                                    EmpId = employeeId,
                                    FID  = (long?)(tempFamilies[i].FID),
                                    Family_Member_Name = tempFamilies[i].Family_Member_Name,
                                    Relation = tempFamilies[i].Relation,
                                    DOB = (DateTime)tempFamilies[i].DOB,
                                    Is_FamilyMemberName_Approved = tempFamilies[i].Is_FamilyMemberName_Approved,
                                    Is_Relation_Approved = tempFamilies[i].Is_Relation_Approved,
                                    Is_DOB_Approved = tempFamilies[i].Is_DOB_Approved
                                }
                            });
                        }
                        matched[i] = true;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    changes.Add(new FamilyChangeDto
                    {
                        FID = (listOldItem.ID),
                        ChangeType = "Deleted",
                        Is_Approved = false,
                        OldData = new FamilyDDto
                        {
                            EmpId = employeeId,
                            FID = (listOldItem.ID),
                            Family_Member_Name = listOldItem.Family_Member_Name,
                            Relation = listOldItem.Relation,
                            DOB = (DateTime)listOldItem.DOB,
                            Is_FamilyMemberName_Approved = false,
                            Is_Relation_Approved = false,
                            Is_DOB_Approved = false
                        },
                        NewData = null,
                    });
                }
            }

            // Now find added records (those in B not matched to A)
            for (int i = 0; i < tempFamilies.Count; i++)
            {
                if (!matched[i])
                {
                    changes.Add(new FamilyChangeDto
                    {
                        FID = (long?)(tempFamilies[i].FID),
                        ChangeType = "Added",
                        Is_Approved = false,
                        OldData = null, 
                        NewData = new FamilyDDto
                                {
                            EmpId = employeeId,
                            FID = (long?)(tempFamilies[i].FID),
                            Family_Member_Name = tempFamilies[i].Family_Member_Name,
                            Relation = tempFamilies[i].Relation,
                            DOB = (DateTime)tempFamilies[i].DOB,
                            Is_FamilyMemberName_Approved = tempFamilies[i].Is_FamilyMemberName_Approved,
                            Is_Relation_Approved = tempFamilies[i].Is_Relation_Approved,
                            Is_DOB_Approved = tempFamilies[i].Is_DOB_Approved
                        }

                    });
                }
            }
            return changes;
        }

        public async Task<List<ExperienceChangeDto>> GetExperienceChangesAsync(long employeeId, long? CandidateId, long? idPass)
        {
            var tempTblExperiences = await _context.tempTblExperiences
                .Where(f => (f.EmpId == employeeId) && (f.IsDeleted == false) && (f.IsActive == true) && (f.IsRejected == false) && (f.Is_Approved == false))
                .ToListAsync();

            var tblExperiences = await _context.tblExperiences
                .Where(f => (f.CID == idPass) && (f.IsDeleted == false) && (f.IsActive == true))
                .ToListAsync();

            var changes = new List<ExperienceChangeDto>();

            var matched = new bool[tempTblExperiences.Count];

            foreach (var listOldItem in tblExperiences)
            {
                bool found = false;
                for (int i = 0; i < (tempTblExperiences.Count); i++)
                {
                    if (!matched[i] && listOldItem.ID == tempTblExperiences[i].EID)
                    {
                        if (listOldItem.Name_of_Company != (tempTblExperiences[i].Name_of_Company) || listOldItem.Work_Location != (tempTblExperiences[i].Work_Location) || listOldItem.Position_Held != (tempTblExperiences[i].Position_Held) || listOldItem.From != (tempTblExperiences[i].From) || listOldItem.To != (tempTblExperiences[i].To) || listOldItem.Last_CTC != (tempTblExperiences[i].Last_CTC))
                        {
                            changes.Add(new ExperienceChangeDto
                            {
                                EID = (listOldItem.ID),
                                ChangeType = "Updated",
                                Is_Approved = false,
                                OldData = new ExperienceDataDto
                                {
                                    EmpId = employeeId,
                                    EID = (listOldItem.ID),
                                    Name_of_Company = listOldItem.Name_of_Company,
                                    Work_Location = listOldItem.Work_Location,
                                    Position_Held = listOldItem.Position_Held,
                                    From = listOldItem.From,
                                    To = listOldItem.To,
                                    Last_CTC = listOldItem.Last_CTC,
                                    Is_NameOfCompany_Approved = false,
                                    Is_WorkLocation_Approved = false,
                                    Is_PositionHeld_Approved = false,
                                    Is_FromDate_Approved = false,
                                    Is_ToDate_Approved = false,
                                    Is_LastCTC_Approved = false
                                },
                                NewData =
                                {
                                    EmpId = employeeId,
                                    EID  = (long?)(tempTblExperiences[i].EID),
                                    Name_of_Company = tempTblExperiences[i].Name_of_Company,
                                    Work_Location = tempTblExperiences[i].Work_Location,
                                    Position_Held = tempTblExperiences[i].Position_Held,
                                    From = tempTblExperiences[i].From,
                                    To = tempTblExperiences[i].To,
                                    Last_CTC = tempTblExperiences[i].Last_CTC,
                                    Is_NameOfCompany_Approved = tempTblExperiences[i].Is_NameOfCompany_Approved,
                                    Is_WorkLocation_Approved = tempTblExperiences[i].Is_WorkLocation_Approved,
                                    Is_PositionHeld_Approved = tempTblExperiences[i].Is_PositionHeld_Approved,
                                    Is_FromDate_Approved = tempTblExperiences[i].Is_FromDate_Approved,
                                    Is_ToDate_Approved = tempTblExperiences[i].Is_ToDate_Approved,
                                    Is_LastCTC_Approved = tempTblExperiences[i].Is_LastCTC_Approved
                                }
                            });
                        }
                        matched[i] = true;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    changes.Add(new ExperienceChangeDto
                    {
                        EID = (listOldItem.ID),
                        ChangeType = "Deleted",
                        Is_Approved = false,
                        OldData = new ExperienceDataDto
                        {
                            EmpId = employeeId,
                            EID = (listOldItem.ID),
                            Name_of_Company = listOldItem.Name_of_Company,
                            Work_Location = listOldItem.Work_Location,
                            Position_Held = listOldItem.Position_Held,
                            From = listOldItem.From,
                            To = listOldItem.To,
                            Last_CTC = listOldItem.Last_CTC,
                            Is_NameOfCompany_Approved = false,
                            Is_WorkLocation_Approved = false,
                            Is_PositionHeld_Approved = false,
                            Is_FromDate_Approved = false,
                            Is_ToDate_Approved = false,
                            Is_LastCTC_Approved = false
                        },
                        NewData = null,
                    });
                }
            }

            // Now find added records (those in B not matched to A)
            for (int i = 0; i < tempTblExperiences.Count; i++)
            {
                if (!matched[i])
                {
                    changes.Add(new ExperienceChangeDto
                    {
                        EID = (long?)(tempTblExperiences[i].EID),
                        ChangeType = "Added",
                        Is_Approved = false,
                        OldData = null,
                        NewData = new ExperienceDataDto
                        {
                            EmpId = employeeId,
                            EID = (long?)(tempTblExperiences[i].EID),
                            Name_of_Company = tempTblExperiences[i].Name_of_Company,
                            Work_Location = tempTblExperiences[i].Work_Location,
                            Position_Held = tempTblExperiences[i].Position_Held,
                            From = tempTblExperiences[i].From,
                            To = tempTblExperiences[i].To,
                            Last_CTC = tempTblExperiences[i].Last_CTC,
                            Is_NameOfCompany_Approved = tempTblExperiences[i].Is_NameOfCompany_Approved,
                            Is_WorkLocation_Approved = tempTblExperiences[i].Is_WorkLocation_Approved,
                            Is_PositionHeld_Approved = tempTblExperiences[i].Is_PositionHeld_Approved,
                            Is_FromDate_Approved = tempTblExperiences[i].Is_FromDate_Approved,
                            Is_ToDate_Approved = tempTblExperiences[i].Is_ToDate_Approved,
                            Is_LastCTC_Approved = tempTblExperiences[i].Is_LastCTC_Approved
                        }

                    });
                }
            }
            return changes;
        }

        public async Task<List<QualificationChangeDto>> GetQualificationChangesAsync(long employeeId, long? CandidateId, long? idPass)
        {
            var tempTblQualifications = await _context.tempTblQualifications
                .Where(f => (f.EmpId == employeeId) && (f.IsDeleted == false) && (f.IsActive == true) && (f.IsRejected == false) && (f.Is_Approved == false))
                .ToListAsync();

            var tblQualifications = await _context.tblQualifications
                .Where(f => (f.CID == idPass) && (f.IsDeleted == false) && (f.IsActive == true))
                .ToListAsync();

            var changes = new List<QualificationChangeDto>();

            var matched = new bool[tempTblQualifications.Count];

            foreach (var listOldItem in tblQualifications )
            {
                bool found = false;
                for (int i = 0; i < (tempTblQualifications.Count); i++)
                {
                    if (!matched[i] && listOldItem.ID == tempTblQualifications[i].QID )
                    {
                        if (listOldItem.Education != (tempTblQualifications[i].Education) || listOldItem.YOP != (tempTblQualifications[i].YOP) || listOldItem.Grade != (tempTblQualifications[i].Grade) || listOldItem.Type != (tempTblQualifications[i].Type))
                        {
                            changes.Add(new QualificationChangeDto
                            {
                                QID = (listOldItem.ID),
                                ChangeType = "Updated",
                                Is_Approved = false,
                                OldData = new QualificationDataDto
                                {
                                    EmpId = employeeId,
                                    QID = (listOldItem.ID),
                                    Education = listOldItem.Education,
                                    YOP = listOldItem.YOP,
                                    Grade = listOldItem.Grade,
                                    Type = listOldItem.Type,
                                    Is_Education_Approved = false,
                                    Is_YOP_Approved = false,
                                    Is_Grade_Approved = false,
                                    Is_Type_Approved = false
                                },
                                NewData = new QualificationDataDto
                                {
                                    EmpId = employeeId,
                                    QID  = (long?)(tempTblQualifications[i].QID),
                                    Education = tempTblQualifications[i].Education,
                                    YOP = tempTblQualifications[i].YOP,
                                    Grade = tempTblQualifications[i].Grade,
                                    Type = tempTblQualifications[i].Type,
                                    Is_Education_Approved = tempTblQualifications[i].Is_Education_Approved,
                                    Is_YOP_Approved = tempTblQualifications[i].Is_YOP_Approved,
                                    Is_Grade_Approved = tempTblQualifications[i].Is_Grade_Approved,
                                    Is_Type_Approved = tempTblQualifications[i].Is_Type_Approved
                                }
                            });
                        }
                        matched[i] = true;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    changes.Add(new QualificationChangeDto
                    {
                        QID = (listOldItem.ID),
                        ChangeType = "Deleted",
                        Is_Approved = false,
                        OldData = new QualificationDataDto
                        {
                            EmpId = employeeId,
                            QID = (listOldItem.ID),
                            Education = listOldItem.Education,
                            YOP = listOldItem.YOP,
                            Grade = listOldItem.Grade,
                            Type = listOldItem.Type,
                            Is_Education_Approved = false,
                            Is_YOP_Approved = false,
                            Is_Grade_Approved = false,
                            Is_Type_Approved = false
                        },
                        NewData = null,
                    });
                }
            }

            // Now find added records (those in B not matched to A)
            for (int i = 0; i < tempTblQualifications.Count; i++)
            {
                if (!matched[i])
                {
                    changes.Add(new QualificationChangeDto
                    {
                        QID = (long?)(tempTblQualifications[i].QID), 
                        ChangeType = "Added",
                        Is_Approved = false,
                        OldData = null,
                        NewData = new QualificationDataDto
                        {
                            EmpId = employeeId,
                            QID = (long?)(tempTblQualifications[i].QID),
                            Education = tempTblQualifications[i].Education,
                            YOP = tempTblQualifications[i].YOP,
                            Grade = tempTblQualifications[i].Grade,
                            Type = tempTblQualifications[i].Type,
                            Is_Education_Approved = tempTblQualifications[i].Is_Education_Approved,
                            Is_YOP_Approved = tempTblQualifications[i].Is_YOP_Approved,
                            Is_Grade_Approved = tempTblQualifications[i].Is_Grade_Approved,
                            Is_Type_Approved = tempTblQualifications[i].Is_Type_Approved
                        }

                    });
                }
            }
            return changes;
        }

        public async Task<List<DocumentChangeDto>> GetDocumentChangesAsync(long employeeId, long? CandidateId, long? idPass)
        {
            var tempCandidateDocs = await _context.tempCandidateDocs
                .Where(f => (f.EmpId == employeeId) && (f.IsDeleted == false) && (f.IsActive == true) && (f.IsRejected == false) && (f.Is_Approved == false))
                .ToListAsync();

            var CanidateDocs = await _context.CanidateDocs
                .Where(f => (f.CId == idPass) && (f.IsDeleted == false) && (f.IsActive == true))
                .ToListAsync();

            var changes = new List<DocumentChangeDto>();

            var matched = new bool[tempCandidateDocs.Count];

            foreach (var listOldItem in CanidateDocs)
            {
                bool found = false;
                for (int i = 0; i < (tempCandidateDocs.Count); i++)
                {
                    if (!matched[i] && listOldItem.Id == tempCandidateDocs[i].DID)
                    {
                        if (listOldItem.FilePath != (tempCandidateDocs[i].FilePath))
                        {
                            changes.Add(new DocumentChangeDto
                            {
                                DID = (listOldItem.Id),
                                ChangeType = "Updated",
                                Is_Approved = false,
                                OldData = new DocumentDataDto
                                {
                                    EmpId = employeeId,
                                    DID = (listOldItem.Id),
                                    FilePath = listOldItem.FilePath,
                                    FileType = listOldItem.FileType,
                                    FileSize = listOldItem.FileSize,
                                    //Is_Approved = false,
                                },
                                NewData = new DocumentDataDto
                                {
                                    EmpId = employeeId,
                                    DID = (long?)(tempCandidateDocs[i].DID),
                                    FilePath = tempCandidateDocs[i].FilePath,
                                    FileType = tempCandidateDocs[i].FileType,
                                    FileSize = tempCandidateDocs[i].FileSize,
                                    //Is_Approved = tempCandidateDocs[i].Is_Approved,
                                }
                            });
                        }
                        matched[i] = true;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    changes.Add(new DocumentChangeDto
                    {
                        DID = (listOldItem.Id),
                        ChangeType = "Deleted",
                        Is_Approved = false,
                        OldData = new DocumentDataDto
                        {
                            EmpId = employeeId,
                            DID = (listOldItem.Id),
                            FilePath = listOldItem.FilePath,
                            FileType = listOldItem.FileType,
                            FileSize = listOldItem.FileSize,
                            //Is_Approved = false,
                        },
                        NewData = null,
                    });
                }
            }

            // Now find added records (those in B not matched to A)
            for (int i = 0; i < tempCandidateDocs.Count; i++)
            {
                if (!matched[i])
                {
                    changes.Add(new DocumentChangeDto
                    {
                        DID = (long?)(tempCandidateDocs[i].DID),
                        ChangeType = "Added",
                        Is_Approved = false,
                        OldData = null,
                        NewData = new DocumentDataDto
                        {
                            EmpId = employeeId,
                            DID = (long?)(tempCandidateDocs[i].DID),
                            FilePath = tempCandidateDocs[i].FilePath,
                            FileType = tempCandidateDocs[i].FileType,
                            FileSize = tempCandidateDocs[i].FileSize,
                            //Is_Approved = tempCandidateDocs[i].Is_Approved,
                        }

                    });
                }
            }
            return changes;
        }

        public async Task<ExecuteAndReponse> UpdateEmployeeApprovedDetails(EmployeeDetailsUpdateView employeeDetailsUpdateView, long EmployeeId, string updatedBy)
        {
            try
            {
                var employeeDataFlagUpdate = await _context.tempTblEmployees.FirstOrDefaultAsync(row => row.EmployeeId == EmployeeId && row.Is_Approved == false && row.Is_Rejected == false);
                // EMPLOYEE DETAILS
                if (employeeDetailsUpdateView.EmployeeDetailsForUpdate != null)
                {
                    foreach (var field in employeeDetailsUpdateView.EmployeeDetailsForUpdate)
                    {
                        if (field.IsApproved == true && !string.IsNullOrEmpty(field.IsApprovedField))
                        {
                            var property = employeeDataFlagUpdate.GetType().GetProperty(field.IsApprovedField);
                            if (property != null && property.CanWrite)
                            {
                                property.SetValue(employeeDataFlagUpdate, true);
                            }
                        }
                    }
                    await _context.SaveChangesAsync();
                    //   if (ra < 1)
                    //   throw new Exception("Unable to Save employee data.");
                }

                // FAMILY DETAILS
                List<long> listToDeleteFamilyData = new List<long>();
                var familyDataFlagUpdate = _context.tempTblFamilies.Where(row => row.EmpId == EmployeeId && row.Is_Approved == false && row.IsRejected == false && row.IsDeleted == false && row.IsActive == true).ToList();
                if (employeeDetailsUpdateView.FamilyDetailsForUpdate != null)
                {
                    foreach (var family in employeeDetailsUpdateView.FamilyDetailsForUpdate.Where(f => f.ChangeType == "Updated"))
                    {
                        var f = family.NewData;
                        if(family.Is_Approved == true)
                            familyDataFlagUpdate.FirstOrDefault(row => row.FID == f.FID).Is_Approved = true;
                        if (f.Is_FamilyMemberName_Approved == true)
                            familyDataFlagUpdate.FirstOrDefault(row => row.FID == f.FID).Is_FamilyMemberName_Approved = true;
                        if (f.Is_Relation_Approved == true)
                            familyDataFlagUpdate.FirstOrDefault(row => row.FID == f.FID).Is_Relation_Approved = true;
                        if (f.Is_DOB_Approved == true)
                            familyDataFlagUpdate.FirstOrDefault(row => row.FID == f.FID).Is_DOB_Approved = true;
                    }

                    foreach (var family in employeeDetailsUpdateView.FamilyDetailsForUpdate.Where(f => f.ChangeType == "Added"))
                    {
                            var f = family.NewData;
                        if (family.Is_Approved == true)
                            familyDataFlagUpdate.FirstOrDefault(row => (row.FID == 0 || row.FID == null) && row.Family_Member_Name == f.Family_Member_Name && row.DOB?.ToString("yyyy-MM-dd") == f.DOB.ToString("yyyy-MM-dd") && row.Relation == f.Relation).Is_Approved = true;
                        if (f.Is_FamilyMemberName_Approved == true)
                            familyDataFlagUpdate.FirstOrDefault(row => (row.FID == 0 || row.FID == null) && row.Family_Member_Name == f.Family_Member_Name && row.DOB?.ToString("yyyy-MM-dd") == f.DOB.ToString("yyyy-MM-dd") && row.Relation == f.Relation).Is_FamilyMemberName_Approved = true;
                        if (f.Is_Relation_Approved == true)
                            familyDataFlagUpdate.FirstOrDefault(row => (row.FID == 0 || row.FID == null) && row.Family_Member_Name == f.Family_Member_Name && row.DOB?.ToString("yyyy-MM-dd") == f.DOB.ToString("yyyy-MM-dd") && row.Relation == f.Relation).Is_Relation_Approved = true;
                        if (f.Is_DOB_Approved == true)
                            familyDataFlagUpdate.FirstOrDefault(row => (row.FID == 0 || row.FID == null) && row.Family_Member_Name == f.Family_Member_Name && row.DOB?.ToString("yyyy-MM-dd") == f.DOB.ToString("yyyy-MM-dd") && row.Relation == f.Relation).Is_DOB_Approved = true;
                    }

                    foreach (var family in employeeDetailsUpdateView.FamilyDetailsForUpdate.Where(f => f.ChangeType == "Deleted"))
                    {
                        //  have to handle
                        if (family.Is_Approved == true)
                            listToDeleteFamilyData.Add((long)family.OldData.FID);
                    }

                    await _context.SaveChangesAsync();
                }

                //  EXPERIENCE DETAILS
                List<long> listToDeleteExperienceData = new List<long>();
                var experienceDataFlagUpdate = _context.tempTblExperiences.Where(row => row.EmpId == EmployeeId && row.Is_Approved == false && row.IsRejected == false && row.IsDeleted == false && row.IsActive == true).ToList();
                if (employeeDetailsUpdateView.ExperienceDetailsForUpdate != null)
                {
                    foreach (var exp in employeeDetailsUpdateView.ExperienceDetailsForUpdate.Where(e => e.ChangeType == "Updated"))
                    {
                        var e = exp.NewData;
                        if (exp.Is_Approved == true)
                            experienceDataFlagUpdate.FirstOrDefault(row => row.EID == e.EID).Is_Approved = true;
                        if (e.Is_NameOfCompany_Approved == true)
                            experienceDataFlagUpdate.FirstOrDefault(row => row.EID == e.EID).Is_NameOfCompany_Approved = true;
                        if (e.Is_WorkLocation_Approved == true)
                            experienceDataFlagUpdate.FirstOrDefault(row => row.EID == e.EID).Is_WorkLocation_Approved = true;
                        if (e.Is_PositionHeld_Approved == true)
                            experienceDataFlagUpdate.FirstOrDefault(row => row.EID == e.EID).Is_PositionHeld_Approved = true;
                        if (e.Is_FromDate_Approved == true)
                            experienceDataFlagUpdate.FirstOrDefault(row => row.EID == e.EID).Is_FromDate_Approved = true;
                        if (e.Is_ToDate_Approved == true)
                            experienceDataFlagUpdate.FirstOrDefault(row => row.EID == e.EID).Is_ToDate_Approved = true;
                        if (e.Is_LastCTC_Approved == true)
                            experienceDataFlagUpdate.FirstOrDefault(row => row.EID == e.EID).Is_LastCTC_Approved = true;
                    }

                    foreach (var exp in employeeDetailsUpdateView.ExperienceDetailsForUpdate.Where(e => e.ChangeType == "Added"))
                    {
                            var e = exp.NewData;
                        if (exp.Is_Approved == true)
                            experienceDataFlagUpdate.FirstOrDefault(row => (row.EID == 0 || row.EID == null )&& row.Name_of_Company == e.Name_of_Company && row.Work_Location == e.Work_Location && row.Position_Held == e.Position_Held && row.From == e.From && row.To == e.To && row.Last_CTC == e.Last_CTC).Is_Approved = true;
                        if (e.Is_NameOfCompany_Approved == true)
                            experienceDataFlagUpdate.FirstOrDefault(row => (row.EID == 0 || row.EID == null) && row.Name_of_Company == e.Name_of_Company && row.Work_Location == e.Work_Location && row.Position_Held == e.Position_Held && row.From == e.From && row.To == e.To && row.Last_CTC == e.Last_CTC).Is_NameOfCompany_Approved = true;
                        if (e.Is_WorkLocation_Approved == true)
                            experienceDataFlagUpdate.FirstOrDefault(row => (row.EID == 0 || row.EID == null) && row.Name_of_Company == e.Name_of_Company && row.Work_Location == e.Work_Location && row.Position_Held == e.Position_Held && row.From == e.From && row.To == e.To && row.Last_CTC == e.Last_CTC).Is_WorkLocation_Approved = true;
                        if (e.Is_PositionHeld_Approved == true)
                            experienceDataFlagUpdate.FirstOrDefault(row => (row.EID == 0 || row.EID == null) && row.Name_of_Company == e.Name_of_Company && row.Work_Location == e.Work_Location && row.Position_Held == e.Position_Held && row.From == e.From && row.To == e.To && row.Last_CTC == e.Last_CTC).Is_PositionHeld_Approved = true;
                        if (e.Is_FromDate_Approved == true)
                            experienceDataFlagUpdate.FirstOrDefault(row => (row.EID == 0 || row.EID == null) && row.Name_of_Company == e.Name_of_Company && row.Work_Location == e.Work_Location && row.Position_Held == e.Position_Held && row.From == e.From && row.To == e.To && row.Last_CTC == e.Last_CTC).Is_FromDate_Approved = true;
                        if (e.Is_ToDate_Approved == true)
                            experienceDataFlagUpdate.FirstOrDefault(row => (row.EID == 0 || row.EID == null) && row.Name_of_Company == e.Name_of_Company && row.Work_Location == e.Work_Location && row.Position_Held == e.Position_Held && row.From == e.From && row.To == e.To && row.Last_CTC == e.Last_CTC).Is_ToDate_Approved = true;
                        if (e.Is_LastCTC_Approved == true)
                            experienceDataFlagUpdate.FirstOrDefault(row => (row.EID == 0 || row.EID == null) && row.Name_of_Company == e.Name_of_Company && row.Work_Location == e.Work_Location && row.Position_Held == e.Position_Held && row.From == e.From && row.To == e.To && row.Last_CTC == e.Last_CTC).Is_LastCTC_Approved = true;
                    }

                    foreach (var exp in employeeDetailsUpdateView.ExperienceDetailsForUpdate.Where(e => e.ChangeType == "Deleted"))
                    {
                        //  have to handle
                        if (exp.Is_Approved == true)
                            listToDeleteExperienceData.Add((long)exp.OldData.EID);
                        /*
                        var e = exp.NewData;
                        if (exp.Is_Approved == true)
                            experienceDataFlagUpdate.FirstOrDefault(row => (row.EID == 0 || row.EID == null) && row.Name_of_Company == e.Name_of_Company && row.Work_Location == e.Work_Location && row.Position_Held == e.Position_Held && row.From == e.From && row.To == e.To && row.Last_CTC == e.Last_CTC).Is_Approved = true;
                        if (e.Is_NameOfCompany_Approved == true)
                            experienceDataFlagUpdate.FirstOrDefault(row => (row.EID == 0 || row.EID == null) && row.Name_of_Company == e.Name_of_Company && row.Work_Location == e.Work_Location && row.Position_Held == e.Position_Held && row.From == e.From && row.To == e.To && row.Last_CTC == e.Last_CTC).Is_NameOfCompany_Approved = true;
                        if (e.Is_WorkLocation_Approved == true)
                            experienceDataFlagUpdate.FirstOrDefault(row => (row.EID == 0 || row.EID == null) && row.Name_of_Company == e.Name_of_Company && row.Work_Location == e.Work_Location && row.Position_Held == e.Position_Held && row.From == e.From && row.To == e.To && row.Last_CTC == e.Last_CTC).Is_WorkLocation_Approved = true;
                        if (e.Is_PositionHeld_Approved == true)
                            experienceDataFlagUpdate.FirstOrDefault(row => (row.EID == 0 || row.EID == null) && row.Name_of_Company == e.Name_of_Company && row.Work_Location == e.Work_Location && row.Position_Held == e.Position_Held && row.From == e.From && row.To == e.To && row.Last_CTC == e.Last_CTC).Is_PositionHeld_Approved = true;
                        if (e.Is_FromDate_Approved == true)
                            experienceDataFlagUpdate.FirstOrDefault(row => (row.EID == 0 || row.EID == null) && row.Name_of_Company == e.Name_of_Company && row.Work_Location == e.Work_Location && row.Position_Held == e.Position_Held && row.From == e.From && row.To == e.To && row.Last_CTC == e.Last_CTC).Is_FromDate_Approved = true;
                        if (e.Is_ToDate_Approved == true)
                            experienceDataFlagUpdate.FirstOrDefault(row => (row.EID == 0 || row.EID == null) && row.Name_of_Company == e.Name_of_Company && row.Work_Location == e.Work_Location && row.Position_Held == e.Position_Held && row.From == e.From && row.To == e.To && row.Last_CTC == e.Last_CTC).Is_ToDate_Approved = true;
                        if (e.Is_LastCTC_Approved == true)
                            experienceDataFlagUpdate.FirstOrDefault(row => (row.EID == 0 || row.EID == null) && row.Name_of_Company == e.Name_of_Company && row.Work_Location == e.Work_Location && row.Position_Held == e.Position_Held && row.From == e.From && row.To == e.To && row.Last_CTC == e.Last_CTC).Is_LastCTC_Approved = true;
                        */
                    }

                    await _context.SaveChangesAsync();
                }

                // QUALIFICATION DETAILS
                List<long> listToDeleteQualificationData = new List<long>();
                var qualificationDataFlagUpdate = _context.tempTblQualifications.Where(row => row.EmpId == EmployeeId && row.Is_Approved == false && row.IsRejected == false && row.IsDeleted == false && row.IsActive == true) 
                   .ToList();
                if (employeeDetailsUpdateView.QualificationDetailsForUpdate != null)
                {
                    foreach (var qual in employeeDetailsUpdateView.QualificationDetailsForUpdate.Where(q => q.ChangeType == "Updated"))
                    {
                        var q = qual.NewData;
                        if (qual.Is_Approved == true)
                            qualificationDataFlagUpdate.FirstOrDefault(row => row.QID == q.QID).Is_Approved = true;
                        if (q.Is_Education_Approved == true)
                            qualificationDataFlagUpdate.FirstOrDefault(row => row.QID == q.QID).Is_Education_Approved = true;
                        if (q.Is_YOP_Approved == true)
                            qualificationDataFlagUpdate.FirstOrDefault(row => row.QID == q.QID).Is_YOP_Approved = true;
                        if (q.Is_Grade_Approved == true)
                            qualificationDataFlagUpdate.FirstOrDefault(row => row.QID == q.QID).Is_Grade_Approved = true;
                        if (q.Is_Type_Approved == true)
                            qualificationDataFlagUpdate.FirstOrDefault(row => row.QID == q.QID).Is_Type_Approved = true;
                    }

                    foreach (var qual in employeeDetailsUpdateView.QualificationDetailsForUpdate.Where(q => q.ChangeType == "Added"))
                    {
                            var q = qual.NewData;
                        if (qual.Is_Approved == true)
                            qualificationDataFlagUpdate.FirstOrDefault(row => (row.QID == 0 || row.QID == null ) && row.Education == q.Education && row.YOP == q.YOP && row.Grade == q.Grade && row.Type == q.Type).Is_Approved = true;
                        if (q.Is_Education_Approved == true)
                            qualificationDataFlagUpdate.FirstOrDefault(row => (row.QID == 0 || row.QID == null) && row.Education == q.Education && row.YOP == q.YOP && row.Grade == q.Grade && row.Type == q.Type).Is_Education_Approved = true;
                        if (q.Is_YOP_Approved == true)
                            qualificationDataFlagUpdate.FirstOrDefault(row => (row.QID == 0 || row.QID == null) && row.Education == q.Education && row.YOP == q.YOP && row.Grade == q.Grade && row.Type == q.Type).Is_YOP_Approved = true;
                        if (q.Is_Grade_Approved == true)
                            qualificationDataFlagUpdate.FirstOrDefault(row => (row.QID == 0 || row.QID == null) && row.Education == q.Education && row.YOP == q.YOP && row.Grade == q.Grade && row.Type == q.Type).Is_Grade_Approved = true;
                        if (q.Is_Type_Approved == true)
                            qualificationDataFlagUpdate.FirstOrDefault(row => (row.QID == 0 || row.QID == null) && row.Education == q.Education && row.YOP == q.YOP && row.Grade == q.Grade && row.Type == q.Type).Is_Type_Approved = true;
                    }

                    foreach (var qual in employeeDetailsUpdateView.QualificationDetailsForUpdate.Where(q => q.ChangeType == "Deleted"))
                    {
                        if (qual.Is_Approved == true)
                            listToDeleteQualificationData.Add((long)qual.OldData.QID);
                        /*
                        var q = qual.NewData;
                        if (qual.Is_Approved == true)
                            qualificationDataFlagUpdate.FirstOrDefault(row => (row.QID == 0 || row.QID == null) && row.Education == q.Education && row.YOP == q.YOP && row.Grade == q.Grade && row.Type == q.Type).Is_Approved = true;
                        if (q.Is_Education_Approved == true)
                            qualificationDataFlagUpdate.FirstOrDefault(row => (row.QID == 0 || row.QID == null) && row.Education == q.Education && row.YOP == q.YOP && row.Grade == q.Grade && row.Type == q.Type).Is_Education_Approved = true;
                        if (q.Is_YOP_Approved == true)
                            qualificationDataFlagUpdate.FirstOrDefault(row => (row.QID == 0 || row.QID == null) && row.Education == q.Education && row.YOP == q.YOP && row.Grade == q.Grade && row.Type == q.Type).Is_YOP_Approved = true;
                        if (q.Is_Grade_Approved == true)
                            qualificationDataFlagUpdate.FirstOrDefault(row => (row.QID == 0 || row.QID == null) && row.Education == q.Education && row.YOP == q.YOP && row.Grade == q.Grade && row.Type == q.Type).Is_Grade_Approved = true;
                        if (q.Is_Type_Approved == true)
                            qualificationDataFlagUpdate.FirstOrDefault(row => (row.QID == 0 || row.QID == null) && row.Education == q.Education && row.YOP == q.YOP && row.Grade == q.Grade && row.Type == q.Type).Is_Type_Approved = true;
                        */
                    }
                    await _context.SaveChangesAsync();
                }

                // DOCUMENTS
                List<long> listToDeleteDocumentData = new List<long>();
                var documentDataFlagUpdate = _context.tempCandidateDocs.Where(row => row.EmpId == EmployeeId && row.Is_Approved == false && row.IsRejected == false && row.IsDeleted == false && row.IsActive == true)
                   .ToList();

                if (employeeDetailsUpdateView.DocumentsDetailsForUpdate != null)
                {
                    foreach (var doc in employeeDetailsUpdateView.DocumentsDetailsForUpdate.Where(d => d.ChangeType == "Added"))
                    {
                        var d = doc.NewData;
                        if (doc.Is_Approved == true)
                            documentDataFlagUpdate.FirstOrDefault(row => (row.DID == 0 || row.DID == null) && row.FileType == d.FileType && row.FilePath == d.FilePath).Is_Approved = true;

                    }
                    foreach (var doc in employeeDetailsUpdateView.DocumentsDetailsForUpdate.Where(d => d.ChangeType == "Deleted"))
                    {
                        if (doc.Is_Approved == true)
                            listToDeleteDocumentData.Add((long)doc.OldData.DID);
                    }
                    await _context.SaveChangesAsync();
                }
          

                #region BasicInfo



                // Try to fetch an existing record from tempTblEmployee  // Have to change from tblEmployee to tempTblEmployee 
                //var employeeData = await GetOneRecordWithTrackingAsync<tblEmployee>(row => row.EmployeeId == employee.candidateInfo.id);
                //var employeeInf = await GetOneRecordWithTrackingAsync<tblEmployee>(row => row.EmployeeId == Convert.ToInt64(updatedBy));
                //long EmployeeId = employeeInf.EmployeeId;
                //long? CandidateId = employeeInf.CandidateId;

                var employeeDataNew = await _context.tempTblEmployees.FirstOrDefaultAsync(row => row.EmployeeId == EmployeeId && row.Is_Approved == false && row.Is_Rejected == false);
                var employeeDataOld = await _context.tblEmployees.FirstOrDefaultAsync(row => row.EmployeeId == EmployeeId);
                long? CandidateId = employeeDataOld.CandidateId;
                int ra;
                if (employeeDataNew != null)
                {
                    if((bool)employeeDataNew.Is_TITLE_Approved)
                        employeeDataOld.TITLE = employeeDataNew.TITLE;

                    if ((bool)employeeDataNew.Is_FirstName_Approved)
                        employeeDataOld.FirstName = employeeDataNew.FirstName;

                    if ((bool)employeeDataNew.Is_MiddleName_Approved)
                        employeeDataOld.MiddleName = employeeDataNew.MiddleName;

                    if ((bool)employeeDataNew.Is_LastName_Approved)
                        employeeDataOld.LastName = employeeDataNew.LastName;

                    if ((bool)employeeDataNew.Is_FULLNAME_Approved)
                        employeeDataOld.FULL_NAME = employeeDataNew.FULL_NAME;

                    if ((bool)employeeDataNew.Is_FATHERSNAME_Approved)
                        employeeDataOld.FATHER_S_NAME = employeeDataNew.FATHER_S_NAME;

                    if ((bool)employeeDataNew.Is_MOTHERSNAME_Approved)
                        employeeDataOld.MOTHER_S_NAME = employeeDataNew.MOTHER_S_NAME;

                    if ((bool)employeeDataNew.Is_HusbandName_Approved)
                        employeeDataOld.Husband_Name = employeeDataNew.Husband_Name;

                    if ((bool)employeeDataNew.Is_PlaceOfBirth_Approved)
                        employeeDataOld.PLACE_OF_BIRTH = employeeDataNew.PLACE_OF_BIRTH;

                    if ((bool)employeeDataNew.Is_PANNO_Approved)
                        employeeDataOld.PAN_NO = employeeDataNew.PAN_NO;

                    if ((bool)employeeDataNew.Is_AADHARNO_Approved)
                        employeeDataOld.AADHAR_NO = employeeDataNew.AADHAR_NO;

                    if ((bool)employeeDataNew.Is_NAMEONADHAR_Approved)
                        employeeDataOld.NAME_ON_ADHAR = employeeDataNew.NAME_ON_ADHAR;

                    if ((bool)employeeDataNew.Is_DOB_Approved)
                        employeeDataOld.DOB = employeeDataNew.DOB;

                    if ((bool)employeeDataNew.Is_PRESENTADDRESS_Approved)
                        employeeDataOld.PRESENT_ADDRESS = employeeDataNew.PRESENT_ADDRESS;

                    if ((bool)employeeDataNew.Is_PERMANENTADDRESS_Approved)
                        employeeDataOld.PERMANENT_ADDRESS = employeeDataNew.PERMANENT_ADDRESS;

                    if ((bool)employeeDataNew.Is_PRESENTPIN_Approved)
                        employeeDataOld.PRESENT_ADDRESS_PIN_CODE = employeeDataNew.PRESENT_ADDRESS_PIN_CODE;

                    if ((bool)employeeDataNew.Is_PERMANENTPIN_Approved)
                        employeeDataOld.PERMANENT_ADDRESS_PIN_CODE = employeeDataNew.PERMANENT_ADDRESS_PIN_CODE;

                    if ((bool)employeeDataNew.Is_MARITIALSTATUS_Approved)
                        employeeDataOld.MARITIAL_STATUS = employeeDataNew.MARITIAL_STATUS;

                    if ((bool)employeeDataNew.Is_MOBILE_Approved)
                        employeeDataOld.MOBILE = employeeDataNew.MOBILE;

                    if ((bool)employeeDataNew.Is_EMAILADDRESS_Approved)
                        employeeDataOld.EMAIL_ADDRESS = employeeDataNew.EMAIL_ADDRESS;

                    if ((bool)employeeDataNew.Is_BENEFICIARYADDRESS_Approved)
                        employeeDataOld.BENEFICIARY_ADDRESS = employeeDataNew.BENEFICIARY_ADDRESS;

                    if ((bool)employeeDataNew.Is_NATIONALITY_Approved)
                        employeeDataOld.NATIONALITY = employeeDataNew.NATIONALITY;

                    if ((bool)employeeDataNew.Is_RELIGION_Approved)
                        employeeDataOld.RELIGION = employeeDataNew.RELIGION;

                    if ((bool)employeeDataNew.Is_BANKNAME_Approved)
                        employeeDataOld.BANK_NAME = employeeDataNew.BANK_NAME;

                    if ((bool)employeeDataNew.Is_ACNO_Approved)
                        employeeDataOld.A_C_NO = employeeDataNew.A_C_NO;

                    if ((bool)employeeDataNew.Is_IFSC_Approved)
                        employeeDataOld.BANK_IFSC_CODE = employeeDataNew.BANK_IFSC_CODE;

                    if ((bool)employeeDataNew.Is_ISRELATIVEINCOMPANY_Approved)
                        employeeDataOld.ISRELATIVEINCOMPANY = employeeDataNew.ISRELATIVEINCOMPANY;

                    employeeDataOld.UpdatedBy = Convert.ToString(updatedBy);
                    employeeDataOld.UpdatedOn = DateTime.Now;

                          ra = await _context.SaveChangesAsync();
                     //   if (ra < 1)
                     //   throw new Exception("Unable to Save employee data.");
                }

                #endregion BasicInfo

                var idPass = (CandidateId != null && CandidateId > 0) ? CandidateId : EmployeeId;

                #region Family

                //var familyDataNew = _context.tempTblFamilies.Where(row => row.EmpId == EmployeeId && row.Is_Approved == false && row.IsRejected == false && row.IsDeleted == false && row.IsActive == true).ToList();
                var familyDataNew = _context.tempTblFamilies.Where(row => row.EmpId == EmployeeId && row.IsRejected == false && row.IsDeleted == false && row.IsActive == true).ToList();
                var familyDataOld = _context.tblFamilies.Where(row => row.CID == idPass && row.IsDeleted == false && row.IsActive == true).ToList();


                if (familyDataNew != null && familyDataNew.Count > 0)
                {
                    var newFamilyMembers = new List<tblFamily>();  //
                    if (familyDataOld != null && familyDataOld.Count > 0)
                    {
                        foreach (var family in familyDataOld)
                        {
                            var match = familyDataNew
                                .FirstOrDefault(row => row.FID == family.ID && row.FID != 0 && row.FID != null);

                            if (match != null)
                            {   
                                //  update
                                if((bool)match.Is_Approved || (bool)match.Is_FamilyMemberName_Approved)
                                    family.Family_Member_Name = match.Family_Member_Name;

                                if ((bool)match.Is_Approved || (bool)match.Is_Relation_Approved)
                                    family.Relation = match.Relation;

                                if ((bool)match.Is_Approved || (bool)match.Is_DOB_Approved)
                                    family.DOB = match.DOB;

                                family.UpdatedBy = updatedBy;
                                family.UpdatedOn = DateTime.Now;
                                family.IsDeleted = false;
                                family.IsActive = true;
                            }
                            else
                            {   //soft Delete
                                if (listToDeleteFamilyData.Contains(family.ID))  //  if Approved by Approver
                                {
                                    family.IsDeleted = true;
                                    family.IsActive = false;
                                    family.UpdatedOn = DateTime.Now;
                                    family.UpdatedBy = updatedBy;
                                }
                            }
                        }

                        var newMembers = familyDataNew
                            .Where(row => row.FID == 0 || row.FID == null)
                            .ToList();

                        foreach (var newMember in newMembers)
                        {
                            var newEntry = new tblFamily
                            {
                                // added
                                CID = CandidateId,
                                Family_Member_Name = ((bool)newMember.Is_Approved || (bool)newMember.Is_FamilyMemberName_Approved) ? newMember.Family_Member_Name : "",
                                //Family_Member_Name = newMember.Family_Member_Name,
                                Relation = ((bool)newMember.Is_Approved || (bool)newMember.Is_Relation_Approved) ? newMember.Relation : "",
                                //Relation = newMember.Relation,
                                DOB = ((bool)newMember.Is_Approved || (bool)newMember.Is_DOB_Approved) ? newMember.DOB : null,
                                //DOB = newMember.DOB,
                                CreatedBy = Convert.ToString(EmployeeId),
                                CreatedOn = DateTime.Now,
                                //UpdatedBy = updatedBy,
                                //UpdatedOn = DateTime.Now,
                                IsDeleted = false, 
                                IsActive = true
                            };

                            _context.tblFamilies.Add(newEntry);
                        }
                    }
                    else
                    {
                        var newMembers = familyDataNew;

                        foreach (var newMember in newMembers)
                        {
                            var newEntry = new tblFamily
                            {
                                //  Added first time
                                CID = CandidateId,
                                Family_Member_Name = ((bool)newMember.Is_Approved || (bool)newMember.Is_FamilyMemberName_Approved) ? newMember.Family_Member_Name : "",
                                //Family_Member_Name = newMember.Family_Member_Name,
                                Relation = ((bool)newMember.Is_Approved || (bool)newMember.Is_Relation_Approved) ? newMember.Relation : "",
                                //Relation = newMember.Relation,
                                DOB = ((bool)newMember.Is_Approved || (bool)newMember.Is_DOB_Approved) ? newMember.DOB : null,
                                //DOB = newMember.DOB,
                                CreatedBy = Convert.ToString(EmployeeId),
                                CreatedOn = DateTime.Now,
                                //UpdatedBy = updatedBy,
                                //UpdatedOn = DateTime.Now,
                                IsDeleted = false, 
                                IsActive = true,
                            };

                            _context.tblFamilies.Add(newEntry);
                        }
                    }
                }
                else
                {
                    // If list is null, soft-delete all existing records
                    foreach (var family in familyDataOld)
                    {
                        if (listToDeleteFamilyData.Contains(family.ID))  //  if Approved by Approver
                        {
                            family.IsDeleted = true;
                            family.IsActive = false;
                            family.UpdatedOn = DateTime.Now;
                            family.UpdatedBy = updatedBy;
                        }
                    }
                }

                 ra = await _context.SaveChangesAsync();

                #endregion Family

                #region Experience

                //var experienceDataNew = _context.tempTblExperiences.Where(row => row.EmpId == EmployeeId && row.Is_Approved == false && row.IsRejected == false && row.IsDeleted == false && row.IsActive == true).ToList();
                var experienceDataNew = _context.tempTblExperiences.Where(row => row.EmpId == EmployeeId && row.IsRejected == false && row.IsDeleted == false && row.IsActive == true).ToList();
                var experienceDataOld = _context.tblExperiences.Where(row => row.CID == idPass && row.IsDeleted == false && row.IsActive == true).ToList();

                if (experienceDataNew != null && experienceDataNew.Count > 0)
                {
                    var newExperienceData = new List<tblExperience>();

                    if (experienceDataOld != null && experienceDataOld.Count > 0)
                    {
                        foreach (var experience in experienceDataOld)
                        {
                            var match = experienceDataNew
                                .FirstOrDefault(row => row.EID == experience.ID && row.EID != 0 && row.EID != null);

                            if (match != null)
                            {   // updated
                                if ((bool)match.Is_Approved || (bool)match.Is_NameOfCompany_Approved)
                                    experience.Name_of_Company = match.Name_of_Company;

                                if ((bool)match.Is_Approved || (bool)match.Is_WorkLocation_Approved)
                                    experience.Work_Location = match.Work_Location;

                                if ((bool)match.Is_Approved || (bool)match.Is_PositionHeld_Approved)
                                    experience.Position_Held = match.Position_Held;

                                if ((bool)match.Is_Approved || (bool)match.Is_FromDate_Approved)
                                    experience.From = match.From;

                                if ((bool)match.Is_Approved || (bool)match.Is_ToDate_Approved)
                                    experience.To = match.To;

                                if ((bool)match.Is_Approved || (bool)match.Is_LastCTC_Approved)
                                    experience.Last_CTC = match.Last_CTC;

                                experience.UpdatedBy = updatedBy;
                                experience.UpdatedOn = DateTime.Now;
                                experience.IsDeleted = false; 
                                experience.IsActive = true; 
                            }
                            else
                            {
                                //  soft deleted
                                if (listToDeleteExperienceData.Contains(experience.ID))  //  if Approved by Approver
                                {
                                    experience.IsDeleted = true;
                                    experience.IsActive = false;
                                    experience.UpdatedBy = updatedBy;
                                    experience.UpdatedOn = DateTime.Now;
                                }
                            }
                        }
                        
                        // Added new experience record added  
                        var newExperiences = experienceDataNew
                            .Where(x => x.EID == 0 || x.EID == null)
                            .ToList();

                        foreach (var newExperience in newExperiences)
                        {
                            // added
                            var newEntry = new tblExperience
                            {
                                CID = CandidateId,

                                Name_of_Company = ((bool)newExperience.Is_Approved || (bool)newExperience.Is_NameOfCompany_Approved) ? newExperience.Name_of_Company : "",
                                Work_Location = ((bool)newExperience.Is_Approved || (bool)newExperience.Is_WorkLocation_Approved) ? newExperience.Work_Location : "",
                                Position_Held = ((bool)newExperience.Is_Approved || (bool)newExperience.Is_PositionHeld_Approved) ? newExperience.Position_Held : "",
                                From = ((bool)newExperience.Is_Approved || (bool)newExperience.Is_FromDate_Approved) ? newExperience.From : null,
                                To = ((bool)newExperience.Is_Approved || (bool)newExperience.Is_ToDate_Approved) ? newExperience.To : null,
                                Last_CTC = ((bool)newExperience.Is_Approved || (bool)newExperience.Is_LastCTC_Approved) ? newExperience.Last_CTC : null,

                                //Name_of_Company = newExperience.Name_of_Company,
                                //Work_Location = newExperience.Work_Location,
                                //Position_Held = newExperience.Position_Held,
                                //From = newExperience.From,
                                //To = newExperience.To,
                                //Last_CTC = newExperience.Last_CTC,

                                CreatedBy = Convert.ToString(EmployeeId),
                                CreatedOn = DateTime.UtcNow,
                                UpdatedBy = updatedBy,
                                UpdatedOn = DateTime.UtcNow,
                                IsDeleted = false,
                                IsActive = true,
                            };

                            _context.tblExperiences.Add(newEntry);
                        }
                    }
                    else
                    {
                        // new experience data record added
                        var newExperiences = experienceDataNew;

                        foreach (var newExperience in newExperiences)
                        {   // added first time
                            var newEntry = new tblExperience 
                            {
                                CID = CandidateId,
                                Name_of_Company = ((bool)newExperience.Is_Approved || (bool)newExperience.Is_NameOfCompany_Approved) ? newExperience.Name_of_Company : "",
                                Work_Location = ((bool)newExperience.Is_Approved || (bool)newExperience.Is_WorkLocation_Approved) ? newExperience.Work_Location : "",
                                Position_Held = ((bool)newExperience.Is_Approved || (bool)newExperience.Is_PositionHeld_Approved) ? newExperience.Position_Held : "",
                                From = ((bool)newExperience.Is_Approved || (bool)newExperience.Is_FromDate_Approved) ? newExperience.From : null,
                                To = ((bool)newExperience.Is_Approved || (bool)newExperience.Is_ToDate_Approved) ? newExperience.To : null,
                                Last_CTC = ((bool)newExperience.Is_Approved || (bool)newExperience.Is_LastCTC_Approved) ? newExperience.Last_CTC : null,

                                //Name_of_Company = newExperience.Name_of_Company,
                                //Work_Location = newExperience.Work_Location,
                                //Position_Held = newExperience.Position_Held,
                                //From = newExperience.From,
                                //To = newExperience.To,
                                //Last_CTC = newExperience.Last_CTC,
                                CreatedBy = Convert.ToString(EmployeeId),
                                CreatedOn = DateTime.Now,
                                //UpdatedBy = updatedBy,
                                //UpdatedOn = DateTime.UtcNow,
                                IsDeleted = false,
                                IsActive = true,
                            };

                            _context.tblExperiences.Add(newEntry);
                        }
                    }
                }
                else
                {
                    // If experience list is null, soft-delete all existing records
                    foreach (var experience in experienceDataOld)
                    {
                        if (listToDeleteExperienceData.Contains(experience.ID))  //  if Approved by Approver
                        {
                            experience.IsDeleted = true;
                            experience.IsActive = false;
                            experience.UpdatedBy = updatedBy;
                            experience.UpdatedOn = DateTime.Now;
                        }
                    }
                }

                ra = await _context.SaveChangesAsync();
                #endregion Experience

                #region Qualification

               // var qualificationDataNew = _context.tempTblQualifications.Where(row => row.EmpId == EmployeeId && row.Is_Approved == false && row.IsRejected == false && row.IsDeleted == false && row.IsActive == true) // && row.IsDeleted == false && row.IsActive == true
               //     .ToList();
                var qualificationDataNew = _context.tempTblQualifications.Where(row => row.EmpId == EmployeeId && row.IsRejected == false && row.IsDeleted == false && row.IsActive == true)
                    .ToList();
                var qualificationDataOld = _context.tblQualifications.Where(row => row.CID == idPass && row.IsDeleted == false && row.IsActive == true)
                    .ToList();
                if (qualificationDataNew != null && qualificationDataNew.Count > 0)
                {
                    var newQualificationData = new List<tblQualification>();

                    if (qualificationDataOld != null && qualificationDataOld.Count > 0)
                    {
                        foreach (var qualification in qualificationDataOld)
                        {
                            var match = qualificationDataNew
                                .FirstOrDefault(row => row.QID == qualification.ID && row.QID != 0 && row.QID != null);

                            if (match != null)
                            {   // update
                                if ((bool)match.Is_Approved || (bool)match.Is_Education_Approved)
                                    qualification.Education = match.Education;

                                if ((bool)match.Is_Approved || (bool)match.Is_YOP_Approved)
                                    qualification.YOP = match.YOP;

                                if ((bool)match.Is_Approved || (bool)match.Is_Grade_Approved)
                                    qualification.Grade = match.Grade;

                                if ((bool)match.Is_Approved || (bool)match.Is_Type_Approved)
                                    qualification.Type = match.Type;

                                qualification.UpdatedBy = updatedBy;
                                qualification.UpdatedOn = DateTime.Now;
                                qualification.IsDeleted = false;
                                qualification.IsActive = true;
                            }
                            else
                            {
                                // soft delete
                                if (listToDeleteQualificationData.Contains(qualification.ID))  //  if Approved by Approver
                                {
                                    qualification.IsDeleted = true;
                                    qualification.IsActive = false;
                                    qualification.UpdatedBy = updatedBy;
                                    qualification.UpdatedOn = DateTime.Now;
                                }
                            }
                        }

                        var newQualifications = qualificationDataNew
                            .Where(x => x.QID == 0 || x.QID == null)
                            .ToList();

                        foreach (var newQualification in newQualifications)
                        {
                            var newEntry = new tblQualification  
                            {
                                //  added
                                CID = CandidateId,
                                Education = ((bool)newQualification.Is_Approved || (bool)newQualification.Is_Education_Approved) ? newQualification.Education : "",
                                YOP = ((bool)newQualification.Is_Approved || (bool)newQualification.Is_YOP_Approved) ? newQualification.YOP : "",
                                Grade = ((bool)newQualification.Is_Approved || (bool)newQualification.Is_Grade_Approved) ? newQualification.Grade : "",
                                Type = ((bool)newQualification.Is_Approved || (bool)newQualification.Is_Type_Approved) ? newQualification.Type : "",

                                //Education = newQualification.Education,
                                //YOP = newQualification.YOP,
                                //Grade = newQualification.Grade,
                                //Type = newQualification.Type,

                                CreatedBy = Convert.ToString(EmployeeId),
                                CreatedOn = DateTime.Now,
                                //UpdatedBy = updatedBy,
                                //UpdatedOn = DateTime.Now,
                                IsDeleted = false,
                                IsActive = true,
                            };

                            _context.tblQualifications.Add(newEntry);
                        }
                    }
                    else
                    {
                        var newQualifications = qualificationDataNew;

                        foreach (var newQualification in newQualifications)
                        {
                            //  added first time
                            var newEntry = new tblQualification 
                            {
                                CID = CandidateId,
                                Education = ((bool)newQualification.Is_Approved || (bool)newQualification.Is_Education_Approved) ? newQualification.Education : "",
                                YOP = ((bool)newQualification.Is_Approved || (bool)newQualification.Is_YOP_Approved) ? newQualification.YOP : "",
                                Grade = ((bool)newQualification.Is_Approved || (bool)newQualification.Is_Grade_Approved) ? newQualification.Grade : "",
                                Type = ((bool)newQualification.Is_Approved || (bool)newQualification.Is_Type_Approved) ? newQualification.Type : "",

                                //Education = newQualification.Education,
                                //YOP = newQualification.YOP,
                                //Grade = newQualification.Grade,
                                //Type = newQualification.Type,
                                CreatedBy = Convert.ToString(EmployeeId),
                                CreatedOn = DateTime.Now,
                                //UpdatedBy = updatedBy,
                                //UpdatedOn = DateTime.UtcNow,
                                IsDeleted = false,
                                IsActive = true,
                            };

                            _context.tblQualifications.Add(newEntry);   //  have to change from tblQualifications to tempTblQualifications
                        }

                    }
                }
                else
                {
                    // Soft-delete all existing qualifications if no list is provided
                    foreach (var qualification in qualificationDataOld)
                    {
                        if (listToDeleteQualificationData.Contains(qualification.ID))  //  if Approved by Approver
                        {
                            qualification.IsDeleted = true;
                            qualification.IsActive = false;
                            qualification.UpdatedBy = updatedBy;
                            qualification.UpdatedOn = DateTime.Now;
                        }
                    }
                }

                ra = await _context.SaveChangesAsync();

                //if (ra < 1)
                //    return BuildExecuteErrorResponse("Unable to Save Qualification Details", HttpStatusCode.BadRequest);

                #endregion Qualification

                #region Attatchments
                
                //var existingDocsNew = _context.tempCandidateDocs.Where(doc => doc.EmpId == EmployeeId && doc.Is_Approved == false && doc.IsRejected == false && doc.IsDeleted == false && doc.IsActive == true).ToList();
                var existingDocsNew = _context.tempCandidateDocs.Where(doc => doc.EmpId == EmployeeId && doc.IsRejected == false && doc.IsDeleted == false && doc.IsActive == true).ToList();
                var existingDocsOld = _context.CanidateDocs.Where(doc => doc.CId == idPass && doc.IsDeleted == false && doc.IsActive == true).ToList();

                var attachments = existingDocsNew;
                if (attachments != null)
                {
                    var newDocs = new List<CanidateDoc>();
                    foreach (var file in attachments)
                    {
                        var exists = existingDocsOld.FirstOrDefault(doc => doc.FileType == file.FileType && doc.FilePath == file.FilePath);
                        if (exists == null)
                        {
                            //  added
                            if (file.Is_Approved == true)
                            {
                                newDocs.Add(new CanidateDoc
                                {
                                    //EmpId = EmployeeId,
                                    CId = (long)file.CID,
                                    FileType = file.FileType,
                                    FilePath = file.FilePath,
                                    FileSize = file.FileSize,
                                    CreatedBy = Convert.ToString(EmployeeId),
                                    CreatedOn = DateTime.Now,
                                    UpdatedBy = updatedBy,
                                    UpdatedOn = DateTime.Now,
                                    IsDeleted = false,
                                    IsActive = true,
                                });
                            }
                        }
                    }

                    if (newDocs.Count > 0)
                        _context.CanidateDocs.AddRange(newDocs);

                    // Soft-delete existing records that are not in the current list
                    var CurrentFilePaths = existingDocsNew.Select(doc => doc.FilePath).ToList();

                    foreach (var doc in existingDocsOld)
                    {
                        //  have to handle soft delete
                        if (!CurrentFilePaths.Contains(doc.FilePath))
                        {
                            if (listToDeleteDocumentData.Contains(doc.Id))  //  if Approved by Approver
                            {
                                doc.IsDeleted = true;
                                doc.IsActive = false;
                                doc.UpdatedOn = DateTime.Now;
                                doc.UpdatedBy = updatedBy;
                            }
                        }
                    }
                }
                else
                {
                    // If attachment list is null, soft-delete all existing records
                    foreach (var doc in existingDocsOld)
                    {
                        //  have to handle soft delete
                        if (listToDeleteDocumentData.Contains(doc.Id))  //  if Approved by Approver
                        {
                            doc.IsDeleted = true;
                            doc.IsActive = false;
                            doc.UpdatedOn = DateTime.Now;
                            doc.UpdatedBy = updatedBy;
                        }
                    }
                }

                ra = await _context.SaveChangesAsync();
                
                #endregion Attatchments


                
                return BuildExecuteSuccessResponse("Updated Successfully");
            }
            catch (Exception ex)
            {
                return BuildExecuteErrorResponse(ex.Message, HttpStatusCode.BadRequest);
            }
        }
    }
}
    