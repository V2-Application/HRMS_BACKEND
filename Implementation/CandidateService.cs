using ASN.Controllers;
using BCrypt.Net;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.ExtendedProperties;
using DocumentFormat.OpenXml.InkML;
using DocumentFormat.OpenXml.Office.Word;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using HRMSAPI.Data;
using HRMSAPI.DTO;
using HRMSAPI.Interfaces;
using HRMSAPI.Models.Auth;
using HRMSAPI.Models.Candidate;
using HRMSAPI.Models.EvalutionForm;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using static Azure.Core.HttpHeader;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Candidate = HRMSAPI.Models.Candidate.Candidate;
using CandidateDocs = HRMSAPI.Models.Candidate.CandidateDocs;
using String = System.String;

namespace HRMSAPI.Implementation
{
    public class CandidateService : ICandidateService
    {
        public readonly HRMSContext _context;
        private readonly string savePath = Path.Combine("wwwroot");
        public readonly IEmailService _emailService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _configuration;
        public CandidateService(HRMSContext context, IEmailService emailService, IHttpContextAccessor httpContextAccessor, IConfiguration configuration)
        {
            _context = context;
            _emailService = emailService;
            _httpContextAccessor = httpContextAccessor;
            _configuration = configuration;
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
        public async Task<Response> InsertCandidateWithDocs(Candidate candidate, CandidateDocs files, string createdBy)
        {
            var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                bool isPassportUploaded = files.PassportPhoto != null && files.PassportPhoto?.Length > 0;
                bool isLast3Slips = files.Last3SalarySlip != null && files.Last3SalarySlip?.Count == 3;
                bool isBankStatement = files.Last3BankStatement != null && files.Last3BankStatement?.Length > 0;
                bool isPrevOfferLetter = files.PrevOfferLetter != null && files.PrevOfferLetter.Length > 0;
                bool isPanAttachment = files.PanAttachment != null && files.PanAttachment.Count > 0;
                bool isAadharAttachment = files.AadharAttachment != null && files.AadharAttachment.Count > 0;
                bool isBankPassbook = files.BankPassbookAttachment != null && files.BankPassbookAttachment.Count > 0;
                bool isEducationAttachment = files.EducationAttachment != null && files.EducationAttachment.Count > 0;
                var names = candidate.fullName.Split(' ').ToList();
                var FirstName = String.Empty;
                var MiddleName = String.Empty;
                var LastName = String.Empty;
                if (names.Count > 0)
                {
                    switch (names.Count)
                    {
                        case 1:
                            {
                                FirstName = names[0];
                                break;
                            }
                        case 2:
                            {
                                FirstName = names[0];
                                LastName = names[1];
                                break;
                            }
                        default:
                            {
                                FirstName = names[0];
                                LastName = names.LastOrDefault();
                                MiddleName = string.Join(" ", names.Skip(1).Take(names.Count - 2));
                                break;
                            }
                    }
                }
                int ra = 0;
                long cid = 0;

                if (candidate.id < 1)
                {
                    // Generate a new ApplicantId
                    var lastApplicant = await _context.Candidates
                                                      .OrderByDescending(c => c.Id)
                                                      .Select(c => c.ApplicantId)
                                                      .FirstOrDefaultAsync();

                    int lastNumber = lastApplicant != null ? int.Parse(lastApplicant.Substring(2)) : 0;
                    string newApplicantId = $"AV{(lastNumber + 1).ToString("D6")}";

                    var dataToBeInserted = new HRMSAPI.Data.Candidate
                    {
                        ApplicantId = newApplicantId,  // Assign the generated ID
                        TITLE = candidate.title,
                        FIRST_NAME = FirstName,
                        MIDDLE_NAME = MiddleName,
                        LAST_NAME = LastName,
                        FATHER_NAME = candidate.fathersName,
                        MOTHER_NAME = candidate.mothersName,
                        DESIGNATION = candidate.designation,
                        DOB = candidate.dob,
                        GENDER = candidate.gender,
                        PAN_NO = candidate.panNo,
                        AADHAR_NO = candidate.aadharNo,
                        NAME_ON_AADHAR = candidate.nameOnAadhar,
                        PLACE_OF_BIRTH = candidate.placeOfBirth,
                        PRESENT_ADDRESS = candidate.presentAddress,
                        PERMANENT_ADDRESS = candidate.permanentAddress,
                        _PERMANENT_ADDRESS_PIN_CODE = candidate.presentAddressPinCode,
                        MARITIAL_STATUS = candidate.maritalStatus,
                        MOBILE = candidate.mobile,
                        EMAIL_ADDRESS = candidate.emailAddress,
                        NATIONALITY = candidate.nationality,
                        RELIGION = candidate.religion,
                        BANK_NAME = candidate.bankName,
                        A_C_NO = candidate.accountNo,
                        BANK_IFSC_CODE = candidate.bankIfscCode,
                        CONTACT1_OF_LAST_3_COMPANY = candidate.contact1LastCompany,
                        CONTACT2_OF_LAST_3_COMPANY = candidate.contact2LastCompany,
                        CONTACT3_OF_LAST_3_COMPANY = candidate.contact3LastCompany,
                        CONTACT4_OF_LAST_3_COMPANY = candidate.contact4LastCompany,
                        CONTACT5_OF_LAST_3_COMPANY = candidate.contact5LastCompany,
                        REFERENCE1__OF_LAST_3_COMPANY = candidate.reference1LastCompany,
                        REFERENCE2__OF_LAST_3_COMPANY = candidate.reference2LastCompany,
                        REFERENCE3__OF_LAST_3_COMPANY = candidate.reference3LastCompany,
                        REFERENCE4__OF_LAST_3_COMPANY = candidate.reference4LastCompany,
                        REFERENCE5__OF_LAST_3_COMPANY = candidate.reference5LastCompany,
                        PreferredLocation = candidate.PreferredLocation,
                        IsPassportPhotoUploaded = isPassportUploaded,
                        IsSalarySlipUploaded = isLast3Slips,
                        IsBankStatementUploaded = isBankStatement,
                        IsPrevOfferLetterUploaded = isPrevOfferLetter,
                        IsPanAttachmentUploaded = isPanAttachment,
                        IsAadharAttachmentUploaded = isAadharAttachment,
                        IsBankPassbookAttachmentUpoaded = isBankPassbook,
                        IsEducationAttachmentUploaded = isEducationAttachment,
                        StatusId = 4,
                        PREV__EST_NO_ = candidate.prevEstNo
                    };

                    await _context.AddAsync(dataToBeInserted);
                    ra = await _context.SaveChangesAsync();
                    if (ra < 1) throw new Exception("Unable to Save Candidate Data, something technical went wrong");
                    cid = dataToBeInserted.Id;
                }

                //save family details 
                if (candidate.familyMemberDob != null || !String.IsNullOrEmpty(candidate.familyMemberName))
                {
                    var familyDetails = new tblFamily
                    {
                        CID = cid,
                        Family_Member_Name = candidate.familyMemberName,
                        DOB = candidate.familyMemberDob,
                        Relation = candidate.familyMemberRelation,
                    };
                    await _context.AddAsync(familyDetails);
                    ra = await _context.SaveChangesAsync();
                    if (ra < 1) throw new Exception("Unable to Save Candidate Family Data, something technical went wrong");
                }
                //save comapny details
                var companyDetails = new tblExperience
                {
                    CID = cid,
                    Name_of_Company = candidate.company1,
                    Work_Location = candidate.workLocation,
                    Position_Held = candidate.positionHeldInPreviousCompany,
                    From = candidate.from,
                    To = candidate.to,
                    InHand = Convert.ToInt64(candidate.inHandSalary),
                    Last_CTC = Convert.ToInt64(candidate.lastCtcAnnual),
                };
                await _context.AddAsync(companyDetails);
                ra = await _context.SaveChangesAsync();
                if (ra < 1) throw new Exception("Unable to Save Candidate Prev Company's Data, something technical went wrong");
                //save education
                var education = new tblQualification
                {
                    CID = cid,
                    Education = candidate.highestQualification,
                };
                await _context.AddAsync(education);
                ra = await _context.SaveChangesAsync();
                if (ra < 1) throw new Exception("Unable to Save Candidate Education's Data, something technical went wrong");

                //save passport photo
                if (files.PassportPhoto != null && files.PassportPhoto?.Length > 0)
                {
                    var filePath = await SaveFile(files.PassportPhoto, "PassportPhotos", cid.ToString() + "_" + candidate.emailAddress);
                    // Insert into the database using the stored procedure
                    int passphotora = await _context.GetProcedures().sp_InsertCandidateDocsAsync(cid, filePath, "PassportPhoto", files.PassportPhoto.Length.ToString(), createdBy);
                    if (passphotora < 1) throw new Exception("Unable to Save Passport Photo due to some technical issues.");
                }
                if (files.Last3SalarySlip != null && files.Last3SalarySlip.Count > 0)
                {
                    int index = 1;
                    foreach (var file in files.Last3SalarySlip)
                    {
                        var filePath = await SaveFile(file, "SalarySlips", cid.ToString() + "_" + candidate.emailAddress);
                        // Insert into the database using the stored procedure
                        int sslip = await _context.GetProcedures().sp_InsertCandidateDocsAsync(cid, filePath, "SalarySlip", file.Length.ToString(), createdBy);
                        if (sslip < 1) throw new Exception($"Unable to Save Salary Slip - {index} due to some technical issues.");
                        index++;
                    }
                }
                //else { 
                //        throw new Exception("Last 3 Salary Slip is Mandatory, so server it accordingly...");
                //}
                // Save Bank Statements
                if (files.Last3BankStatement != null && files.Last3BankStatement.Length > 0)
                {
                    //int index = 1;
                    //foreach (var file in files.Last3BankStatement)
                    //{
                    var filePath = await SaveFile(files.Last3BankStatement, "BankStatements", cid.ToString() + "_" + candidate.emailAddress);
                    // Insert into the database using the stored procedure
                    int bstatement = await _context.GetProcedures().sp_InsertCandidateDocsAsync(cid, filePath, "BankStatement", files.Last3BankStatement.Length.ToString(), createdBy);
                    if (bstatement < 1) throw new Exception($"Unable to Save BankStatements due to some technical issues.");

                }
                //else { 
                //        throw new Exception("Last 3 Bank Statements is Mandatory, so server it accordingly...");
                //}

                // Save Previous Offer Letter
                if (files.PrevOfferLetter != null && files.PrevOfferLetter.Length > 0)
                {
                    var filePath = await SaveFile(files.PrevOfferLetter, "PrevOfferLetters", cid.ToString() + "_" + candidate.emailAddress);
                    // Insert into the database using the stored procedure
                    int passphotora = await _context.GetProcedures().sp_InsertCandidateDocsAsync(cid, filePath, "PrevOfferLetter", files.PrevOfferLetter.Length.ToString(), createdBy);
                    if (passphotora < 1) throw new Exception("Unable to Save Prev. Offer Letter due to some technical issues.");
                }
                //else {
                //    throw new Exception("Prev Offer Letter is Mandatory, so server it accordingly...");
                //}

                //pan
                if (files.PanAttachment != null && files.PanAttachment.Count > 0)
                {
                    int index = 1;
                    foreach (var file in files.PanAttachment)
                    {
                        var filePath = await SaveFile(file, "Pan", cid.ToString() + "_" + candidate.emailAddress);
                        // Insert into the database using the stored procedure
                        int sslip = await _context.GetProcedures().sp_InsertCandidateDocsAsync(cid, filePath, "Pan", file.Length.ToString(), createdBy);
                        if (sslip < 1) throw new Exception($"Unable to Save Pan - {index} due to some technical issues.");
                        index++;
                    }
                }
                //else
                //{
                //    throw new Exception("Pan is Mandatory, so server it accordingly...");
                //}
                //aadhar
                if (files.AadharAttachment != null && files.AadharAttachment.Count > 0)
                {
                    int index = 1;
                    foreach (var file in files.AadharAttachment)
                    {
                        var filePath = await SaveFile(file, "Aadhar", cid.ToString() + "_" + candidate.emailAddress);
                        // Insert into the database using the stored procedure
                        int sslip = await _context.GetProcedures().sp_InsertCandidateDocsAsync(cid, filePath, "Aadhar", file.Length.ToString(), createdBy);
                        if (sslip < 1) throw new Exception($"Unable to Save Aadhar - {index} due to some technical issues.");
                        index++;
                    }
                }
                //else
                //{
                //    throw new Exception("Aadhar is Mandatory, so server it accordingly...");
                //}
                //bankPassbook
                if (files.BankPassbookAttachment != null && files.BankPassbookAttachment.Count > 0)
                {
                    int index = 1;
                    foreach (var file in files.BankPassbookAttachment)
                    {
                        var filePath = await SaveFile(file, "BankPassbook", cid.ToString() + "_" + candidate.emailAddress);
                        // Insert into the database using the stored procedure
                        int sslip = await _context.GetProcedures().sp_InsertCandidateDocsAsync(cid, filePath, "BankPassbook", file.Length.ToString(), createdBy);
                        if (sslip < 1) throw new Exception($"Unable to Save BankPassbook - {index} due to some technical issues.");
                        index++;
                    }
                }
                //else
                //{
                //    throw new Exception("BankPassbook is Mandatory, so server it accordingly...");
                //}
                //Education
                if (files.EducationAttachment != null && files.EducationAttachment.Count > 0)
                {
                    int index = 1;
                    foreach (var file in files.EducationAttachment)
                    {
                        var filePath = await SaveFile(file, "Education", cid.ToString() + "_" + candidate.emailAddress);
                        // Insert into the database using the stored procedure
                        int sslip = await _context.GetProcedures().sp_InsertCandidateDocsAsync(cid, filePath, "Education", file.Length.ToString(), createdBy);
                        if (sslip < 1) throw new Exception($"Unable to Save Education - {index} due to some technical issues.");
                        index++;
                    }
                }
                //else
                //{
                //    throw new Exception("Education is Mandatory, so server it accordingly...");
                //}
                var newCandiadte = new tblNewCandidateApproval
                {
                    CandidateId = cid,
                    HRApprovalStatus = 4,
                    AuditApprovalStatus = 4,
                };

                await _context.tblNewCandidateApprovals.AddAsync(newCandiadte);
                await _context.SaveChangesAsync();


                await transaction.CommitAsync();

                return new Response
                {
                    Status = true,
                    Message = "Inserted Successfully",
                    StatusCode = System.Net.HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return new Response
                {
                    Status = false,
                    Message = ex.Message,
                    StatusCode = System.Net.HttpStatusCode.BadRequest
                };
            }
        }
        //public async Task<Response> GetCandidateList(int pageNumber, int pageSize, string searchTerm = "", long employeeId = 0, string role = "")
        //{
        //    try
        //    {
        //        searchTerm = searchTerm?.Trim().ToLower();

        //        // Get employee location if role is StoreManager
        //        int? storeLocationId = null;
        //        if (role == "StoreManager")
        //        {
        //            storeLocationId = await _context.tblEmployees
        //                .Where(e => e.EmployeeId == employeeId)
        //                .Select(e => e.LocationId)
        //                .FirstOrDefaultAsync();
        //        }

        //        // Base query with joins
        //        var baseQuery = _context.Candidates
        //            .AsNoTracking()
        //            .Where(row => row.IsActive == true && row.IsDeleted != true)
        //            .GroupJoin(
        //                _context.tblNewCandidateApprovals,
        //                candidate => candidate.Id,
        //                approval => approval.CandidateId,
        //                (candidate, approvals) => new { candidate, approvals })
        //            .SelectMany(
        //                x => x.approvals.DefaultIfEmpty(),
        //                (c, a) => new
        //                {
        //                    Candidate = c.candidate,
        //                    Approval = a
        //                })
        //            // Filter out records where all approvals (Cluster, Audit, HR) are 1
        //            .Where(x => !(x.Approval != null &&
        //                          x.Approval.ClusterManagerApprovalStatus == 1 &&
        //                          x.Approval.AuditApprovalStatus == 1 &&
        //                          x.Approval.HRApprovalStatus == 1));

        //        // Apply location filter for StoreManager
        //        if (storeLocationId.HasValue)
        //        {
        //            baseQuery = baseQuery.Where(x => x.Candidate.LOCATION == storeLocationId.Value.ToString());
        //        }

        //        // Apply search filter if searchTerm is provided
        //        var filteredQuery = baseQuery;
        //        if (!string.IsNullOrEmpty(searchTerm))
        //        {
        //            filteredQuery = baseQuery.Where(x =>
        //                (x.Candidate.FIRST_NAME ?? "").ToLower().Contains(searchTerm) ||
        //                (x.Candidate.MIDDLE_NAME ?? "").ToLower().Contains(searchTerm) ||
        //                (x.Candidate.LAST_NAME ?? "").ToLower().Contains(searchTerm) ||
        //                (x.Candidate.EMP_CODE ?? "").ToLower().Contains(searchTerm) ||
        //                (x.Candidate.EMAIL_ADDRESS ?? "").ToLower().Contains(searchTerm)
        //            );
        //        }

        //        var filteredCount = await filteredQuery.Select(x => x.Candidate.Id).Distinct().CountAsync();

        //        var finalQuery = filteredCount == 0 && !string.IsNullOrEmpty(searchTerm) ? baseQuery : filteredQuery;

        //        int totalRecords = await finalQuery.Select(x => x.Candidate.Id).Distinct().CountAsync();
        //        int pendingCount = await finalQuery.Select(x => x.Candidate).Distinct().CountAsync(row => row.StatusId == 4);

        //        var list = await finalQuery
        //            .OrderByDescending(x => x.Candidate.Id)
        //            .Skip((pageNumber - 1) * pageSize)
        //            .Take(pageSize)
        //            .Select(x => new
        //            {
        //                ID = (int?)x.Candidate.Id,
        //                FirstName = x.Candidate.FIRST_NAME,
        //                MiddleName = x.Candidate.MIDDLE_NAME,
        //                LastName = x.Candidate.LAST_NAME,
        //                Phone = x.Candidate.MOBILE,
        //                Email = x.Candidate.EMAIL_ADDRESS,
        //                Designation = x.Candidate.DESIGNATION,
        //                DOB = (DateTime?)x.Candidate.DOB,
        //                StatusId = (int?)x.Candidate.StatusId,
        //                HRApprovalStatus = x.Approval != null ? (int?)x.Approval.HRApprovalStatus : null,
        //                AuditApprovalStatus = x.Approval != null ? (int?)x.Approval.AuditApprovalStatus : null,
        //                ClusterManagerApprovalStatus = x.Approval != null ? (int?)x.Approval.ClusterManagerApprovalStatus : null,
        //                StoreLocationName = _context.tblLocations
        //                    .Where(a => a.LocationId == Convert.ToInt32(x.Candidate.LOCATION))
        //                    .Select(a => a.LocationName)
        //                    .FirstOrDefault(),
        //                StoreLocationCode = _context.tblLocations
        //                    .Where(a => a.LocationId == Convert.ToInt32(x.Candidate.LOCATION))
        //                    .Select(a => a.STCode)
        //                    .FirstOrDefault(),
        //                DesignationName = _context.tblDesignations
        //                    .Where(a => a.DesignationId == Convert.ToInt32(x.Candidate.DESIGNATION))
        //                    .Select(a => a.DesignationName)
        //                    .FirstOrDefault() ?? "NA"
        //            })
        //            .ToListAsync();

        //        return new Response
        //        {
        //            Status = true,
        //            Message = "Data Fetched Successfully",
        //            StatusCode = System.Net.HttpStatusCode.OK,
        //            Data = new
        //            {
        //                TotalRecords = totalRecords,
        //                PendingCount = pendingCount,
        //                Candidates = list
        //            }
        //        };
        //    }
        //    catch (Exception ex)
        //    {
        //        return new Response
        //        {
        //            Status = false,
        //            Message = ex.Message,
        //            StatusCode = System.Net.HttpStatusCode.BadRequest,
        //            Data = ex.Data
        //        };
        //    }
        //}
        public async Task<Response> GetCandidateList(int pageNumber, int pageSize, string searchTerm = "", long employeeId = 0, string role = "")
        {
            try
            {
                searchTerm = searchTerm?.Trim().ToLower();

                // Get employee location if role is StoreManager
                int? storeLocationId = null;
                if (role == "StoreManager")
                {
                    storeLocationId = await _context.tblEmployees
                        .Where(e => e.EmployeeId == employeeId)
                        .Select(e => e.LocationId)
                        .FirstOrDefaultAsync();
                }

                // Base query with joins
                var baseQuery = _context.Candidates
                    .AsNoTracking()
                    .Where(row => row.IsActive == true && row.IsDeleted != true && row.IsApplicant != true)
                    .GroupJoin(
                        _context.tblNewCandidateApprovals,
                        candidate => candidate.Id,
                        approval => approval.CandidateId,
                        (candidate, approvals) => new { candidate, approvals })
                    .SelectMany(
                        x => x.approvals.DefaultIfEmpty(),
                        (c, a) => new
                        {
                            Candidate = c.candidate,
                            Approval = a
                        })
                    // Join tblLocations to access STCode
                    .GroupJoin(
                        _context.tblLocations,
                        x => Convert.ToInt32(x.Candidate.LOCATION),
                        location => location.LocationId,
                        (x, locations) => new { x.Candidate, x.Approval, locations })
                    .SelectMany(
                        x => x.locations.DefaultIfEmpty(),
                        (x, l) => new
                        {
                            Candidate = x.Candidate,
                            Approval = x.Approval,
                            Location = l
                        })
                    // Filter out records where all approvals (Cluster, Audit, HR) are 1
                    .Where(x => !(x.Approval != null &&
                                  x.Approval.ClusterManagerApprovalStatus == 1 &&
                                  x.Approval.AuditApprovalStatus == 1 &&
                                  x.Approval.HRApprovalStatus == 1));

                // Apply location filter for StoreManager
                if (storeLocationId.HasValue)
                {
                    baseQuery = baseQuery.Where(x => x.Candidate.LOCATION == storeLocationId.Value.ToString());
                }

                // Apply search filter if searchTerm is provided
                var filteredQuery = baseQuery;
                if (!string.IsNullOrEmpty(searchTerm))
                {
                    filteredQuery = baseQuery.Where(x =>
                        (x.Candidate.FIRST_NAME ?? "").ToLower().Contains(searchTerm) ||
                        (x.Candidate.MIDDLE_NAME ?? "").ToLower().Contains(searchTerm) ||
                        (x.Candidate.LAST_NAME ?? "").ToLower().Contains(searchTerm) ||
                        (x.Candidate.EMP_CODE ?? "").ToLower().Contains(searchTerm) ||
                        (x.Candidate.EMAIL_ADDRESS ?? "").ToLower().Contains(searchTerm) ||
                        (x.Candidate.DESIGNATION ?? "").ToLower().Contains(searchTerm) ||
                        (x.Candidate.LOCATION ?? "").ToLower().Contains(searchTerm) ||
                        (x.Candidate.MOBILE ?? "").ToLower().Contains(searchTerm) ||
                        (x.Location != null && x.Location.STCode != null && x.Location.STCode.ToLower().Contains(searchTerm)) ||
                        (x.Location != null && x.Location.LocationName != null && x.Location.LocationName.ToLower().Contains(searchTerm))
                    );
                }

                var filteredCount = await filteredQuery
                    .Select(x => x.Candidate.Id)
                    .Distinct()
                    .CountAsync();

                var finalQuery = filteredCount == 0 && !string.IsNullOrEmpty(searchTerm) ? baseQuery : filteredQuery;

                int totalRecords = await finalQuery
                    .Select(x => x.Candidate.Id)
                    .Distinct()
                    .CountAsync();

                int pendingCount = await finalQuery
                    .Select(x => x.Candidate)
                    .Distinct()
                    .CountAsync(row => row.StatusId == 4);

                // CanidateDocs.CreatedOn is written as local time (see SaveFile —
                // `DateTime.Now.ToString(...)`), so we must use local time when
                // diffing against it. Otherwise IST docs produce -5.5h ageing.
                var nowLocal = DateTime.Now;
                const int approvedStatusIdForList = 1; // matches CandidateApproval logic

                var list = await finalQuery
                    .OrderByDescending(x => x.Candidate.Id)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(x => new
                    {
                        ID = (int?)x.Candidate.Id,
                        FirstName = x.Candidate.FIRST_NAME,
                        MiddleName = x.Candidate.MIDDLE_NAME,
                        LastName = x.Candidate.LAST_NAME,
                        Phone = x.Candidate.MOBILE,
                        Email = x.Candidate.EMAIL_ADDRESS,
                        Designation = x.Candidate.DESIGNATION,
                        DOB = (DateTime?)x.Candidate.DOB,
                        StatusId = (int?)x.Candidate.StatusId,
                        ReportHeadEcode = (string?)x.Candidate.ReportHeadEcode,

                        // Existing approval statuses
                        HRApprovalStatus = x.Approval != null ? (int?)x.Approval.HRApprovalStatus : null,
                        AuditApprovalStatus = x.Approval != null ? (int?)x.Approval.AuditApprovalStatus : null,
                        ClusterManagerApprovalStatus = x.Approval != null ? (int?)x.Approval.ClusterManagerApprovalStatus : null,

                        // Approval action timestamps (now stamped on approve OR reject)
                        HRApprovedOn = x.Approval != null ? (DateTime?)x.Approval.HRApprovedOn : null,
                        AuditApprovedOn = x.Approval != null ? (DateTime?)x.Approval.AuditApprovedOn : null,
                        ClusterManagerApprovedOn = x.Approval != null ? (DateTime?)x.Approval.ClusterManagerApprovedOn : null,

                        HRRemarks = x.Approval != null ? x.Approval.HRRemarks : null,
                        AuditRemarks = x.Approval != null ? x.Approval.AuditRemarks : null,
                        ClusterManagerRemarks = x.Approval != null ? x.Approval.ClusterManagerRemarks : null,

                        // Reviewer ecodes
                        HRReviewedBy = x.Approval != null ? x.Approval.HRReviewedBy : null,
                        AuditReviewedBy = x.Approval != null ? x.Approval.AuditReviewedBy : null,
                        ClusterManagerReviewBy = x.Approval != null ? x.Approval.ClusterManagerReviewBy : null,

                        // First document upload date — starts the ageing clock for LP.
                        DocumentUploadedOn = _context.CanidateDocs
                            .Where(d => d.CId == x.Candidate.Id && d.IsDeleted != true)
                            .Min(d => (DateTime?)d.CreatedOn),

                        StoreLocationName = x.Location != null ? x.Location.LocationName : null,
                        StoreLocationCode = x.Location != null ? x.Location.STCode : null,

                        // DesignationName (still using lookup – you *could* optimize this with join if needed)
                        DesignationName = _context.tblDesignations
                            .Where(a => a.DesignationId == Convert.ToInt32(x.Candidate.DESIGNATION))
                            .Select(a => a.DesignationName)
                            .FirstOrDefault() ?? "NA",
                        DepartmentName = _context.tblDepartments
                            .Where(d => d.DepartmentId == Convert.ToInt32(x.Candidate.DEPARTMENT))
                            .Select(d => d.DepartmentName)
                            .FirstOrDefault() ?? "NA"
                    })
                    .ToListAsync();

                // Resolve reviewer Name + Ecode in a single round-trip. ReviewedBy
                // columns store EmployeeId as a string (see UpdateCandidateApproval).
                var reviewerEmpIds = list
                    .SelectMany(r => new[] { r.HRReviewedBy, r.AuditReviewedBy, r.ClusterManagerReviewBy })
                    .Where(s => long.TryParse(s, out _))
                    .Select(s => long.Parse(s))
                    .Distinct()
                    .ToList();

                var reviewerLookup = reviewerEmpIds.Count == 0
                    ? new Dictionary<string, (string Name, string Ecode)>()
                    : (await _context.tblEmployees
                        .Where(e => reviewerEmpIds.Contains(e.EmployeeId))
                        .Select(e => new { e.EmployeeId, e.FULL_NAME, e.Ecode })
                        .ToListAsync())
                        .ToDictionary(e => e.EmployeeId.ToString(), e => (e.FULL_NAME, e.Ecode));

                (string Name, string Ecode) ResolveReviewer(string id)
                {
                    if (!string.IsNullOrWhiteSpace(id) && reviewerLookup.TryGetValue(id, out var info))
                        return info;
                    return (null, null);
                }

                // Compute ageing hours in memory (sequential pipeline: LP -> Cluster -> HR).
                // The frontend surfaces a badge whenever a hours value crosses 24.
                var listWithAgeing = list.Select(r =>
                {
                    double? lpAgeHrs = null, clusterAgeHrs = null, hrAgeHrs = null;

                    // LP clock starts at first document upload
                    if (r.DocumentUploadedOn.HasValue)
                    {
                        var lpEnd = r.AuditApprovedOn ?? nowLocal;
                        lpAgeHrs = (lpEnd - r.DocumentUploadedOn.Value).TotalHours;
                    }

                    // Cluster clock starts only after LP approves
                    if (r.AuditApprovalStatus == approvedStatusIdForList && r.AuditApprovedOn.HasValue)
                    {
                        var clusterEnd = r.ClusterManagerApprovedOn ?? nowLocal;
                        clusterAgeHrs = (clusterEnd - r.AuditApprovedOn.Value).TotalHours;
                    }

                    // HR clock starts only after Cluster approves
                    if (r.ClusterManagerApprovalStatus == approvedStatusIdForList && r.ClusterManagerApprovedOn.HasValue)
                    {
                        var hrEnd = r.HRApprovedOn ?? nowLocal;
                        hrAgeHrs = (hrEnd - r.ClusterManagerApprovedOn.Value).TotalHours;
                    }

                    var hrRev = ResolveReviewer(r.HRReviewedBy);
                    var auditRev = ResolveReviewer(r.AuditReviewedBy);
                    var clusterRev = ResolveReviewer(r.ClusterManagerReviewBy);

                    return new
                    {
                        r.ID, r.FirstName, r.MiddleName, r.LastName, r.Phone, r.Email,
                        r.Designation, r.DOB, r.StatusId, r.ReportHeadEcode,
                        r.HRApprovalStatus, r.AuditApprovalStatus, r.ClusterManagerApprovalStatus,
                        r.HRApprovedOn, r.AuditApprovedOn, r.ClusterManagerApprovedOn,
                        r.HRRemarks, r.AuditRemarks, r.ClusterManagerRemarks,
                        r.HRReviewedBy, r.AuditReviewedBy, r.ClusterManagerReviewBy,
                        HRReviewerName = hrRev.Name,
                        HRReviewerEcode = hrRev.Ecode,
                        AuditReviewerName = auditRev.Name,
                        AuditReviewerEcode = auditRev.Ecode,
                        ClusterManagerReviewerName = clusterRev.Name,
                        ClusterManagerReviewerEcode = clusterRev.Ecode,
                        r.DocumentUploadedOn,
                        LpAgeingHours = lpAgeHrs,
                        ClusterAgeingHours = clusterAgeHrs,
                        HrAgeingHours = hrAgeHrs,
                        r.StoreLocationName, r.StoreLocationCode,
                        r.DesignationName, r.DepartmentName
                    };
                }).ToList();

                return new Response
                {
                    Status = true,
                    Message = "Data Fetched Successfully",
                    StatusCode = System.Net.HttpStatusCode.OK,
                    Data = new
                    {
                        TotalRecords = totalRecords,
                        PendingCount = pendingCount,
                        Candidates = listWithAgeing
                    }
                };
            }
            catch (Exception ex)
            {
                return new Response
                {
                    Status = false,
                    Message = ex.Message,
                    StatusCode = System.Net.HttpStatusCode.BadRequest,
                    Data = ex.Data
                };
            }
        }


        //   public async Task<Response> CandidateInitiate(CandidateApprovalDto obj, JwtLoginDetailDto loginDetail)
        //   {
        //       await using var trans = await _context.Database.BeginTransactionAsync();

        //       try
        //       {
        //           if (obj == null)
        //               return new Response { Status = false, StatusCode = HttpStatusCode.BadRequest, Message = "Data cannot be null" };

        //           var candidate = await _context.Candidates.AsNoTracking().FirstOrDefaultAsync(c => c.Id == obj.CandidateId);
        //           if (candidate == null)
        //               return new Response { Status = false, StatusCode = HttpStatusCode.NotFound, Message = $"No Candidate Found for Id: {obj.CandidateId}" };

        //           var candidateApproval = await _context.tblNewCandidateApprovals.FirstOrDefaultAsync(a => a.CandidateId == candidate.Id);
        //           if (candidateApproval == null)
        //               return new Response { Status = false, StatusCode = HttpStatusCode.NotFound, Message = $"No Candidate Approval Found for Id: {obj.CandidateId}" };
        //           var location = Convert.ToInt32(candidate.LOCATION);
        //           var department = Convert.ToInt32(candidate.DEPARTMENT);
        //           var designation = Convert.ToInt32(candidate.DESIGNATION);
        //           decimal salary = Convert.ToDecimal(candidate.GROSS_SALARY);

        //           //if (location < 1 || department < 1 || designation < 1 || salary<Convert.ToDecimal(1.00)) {
        //           //    return new Response
        //           //    {
        //           //        Status = false,
        //           //        Message = "Either Location,Department or Designation mapping is not correct, or Salary is not particularly defined in candidate data for this employee..."
        //           //    };
        //           //}
        //           //var isAllowed = await _context.Database
        //           //    .SqlQueryRaw<bool>("SELECT CAST(dbo.fn_IsVacancyShorterForEmployee({0}, {1}, {2},{3}) AS BIT) AS Value", location, department, designation,salary)
        //           //    .FirstOrDefaultAsync();
        //           //if (!isAllowed)
        //           //{
        //           //    return new Response
        //           //    {
        //           //        Status = false,
        //           //        Message = "No vacancy available or Salary Gross exceeds budgeted"
        //           //    };
        //           //}
        //           int? hrApprovalStatusId = null;
        //           int? auditApprovalStatusId = null;
        //           int? clusterApprovalStatusId = null;

        //           var ifExist = _context.tblNewCandidateApprovals
        //.Any(a => a.ClusterManagerApprovalStatus == 1 &&
        //          a.HRApprovalStatus == 1 &&
        //          a.AuditApprovalStatus == 1 &&
        //          a.CandidateId == obj.CandidateId);

        //           if (ifExist)
        //           {
        //               throw new InvalidOperationException("Candidate approval record already exists.");
        //           }


        //           // Handle CLUSTER role updates
        //           if (loginDetail.role == "ClusterManager")
        //           {
        //               if (obj.ClusterManagerApprovalStatus.HasValue && obj.ClusterManagerApprovalStatus > 0)
        //               {
        //                   clusterApprovalStatusId = await _context.tblStatuses
        //                       .Where(s => s.StatusId == obj.ClusterManagerApprovalStatus)
        //                       .Select(s => (int?)s.StatusId)
        //                       .FirstOrDefaultAsync();

        //                   if (clusterApprovalStatusId == null)
        //                       return new Response { Status = false, StatusCode = HttpStatusCode.BadRequest, Message = $"Invalid ClusterApprovalStatus: {obj.ClusterManagerApprovalStatus}" };

        //                   candidateApproval.ClusterManagerApprovalStatus = clusterApprovalStatusId.Value;
        //                   candidateApproval.ClusterManagerReviewBy = obj.ClusterManagerReviewedBy;
        //                   candidateApproval.UpdatedOn = DateTime.UtcNow;
        //                   candidateApproval.LastUpdatedBy = loginDetail.EmployeeId;
        //               }
        //           }
        //           // Handle AUDIT role updates
        //           else if (loginDetail.role == "Audit")
        //           {
        //               if (obj.AuditApprovalStatus.HasValue && obj.AuditApprovalStatus > 0)
        //               {
        //                   auditApprovalStatusId = await _context.tblStatuses
        //                       .Where(s => s.StatusId == obj.AuditApprovalStatus)
        //                       .Select(s => (int?)s.StatusId)
        //                       .FirstOrDefaultAsync();

        //                   if (auditApprovalStatusId == null)
        //                       return new Response { Status = false, StatusCode = HttpStatusCode.BadRequest, Message = $"Invalid AuditApprovalStatus: {obj.AuditApprovalStatus}" };

        //                   candidateApproval.AuditApprovalStatus = auditApprovalStatusId.Value;
        //                   candidateApproval.AuditReviewedBy = obj.AuditReviewedBy;
        //                   candidateApproval.UpdatedOn = DateTime.UtcNow;
        //                   candidateApproval.LastUpdatedBy = loginDetail.EmployeeId;
        //               }
        //           }
        //           // Handle HR role updates
        //           else if (loginDetail.role == "HR")
        //           {
        //               if (obj.HRApprovalStatus.HasValue && obj.HRApprovalStatus > 0)
        //               {
        //                   hrApprovalStatusId = await _context.tblStatuses
        //                       .Where(s => s.StatusId == obj.HRApprovalStatus)
        //                       .Select(s => (int?)s.StatusId)
        //                       .FirstOrDefaultAsync();

        //                   if (hrApprovalStatusId == null)
        //                       return new Response { Status = false, StatusCode = HttpStatusCode.BadRequest, Message = $"Invalid HRApprovalStatus: {obj.HRApprovalStatus}" };

        //                   candidateApproval.HRApprovalStatus = hrApprovalStatusId.Value;
        //                   candidateApproval.HRReviewedBy = obj.HRReviewedBy;
        //                   candidateApproval.UpdatedOn = DateTime.UtcNow;
        //                   candidateApproval.LastUpdatedBy = loginDetail.EmployeeId;
        //               }
        //           }
        //           // Handle SUPERADMIN role updates
        //           else if (loginDetail.role == "SuperAdmin")
        //           {
        //               // Cluster Manager Approval
        //               clusterApprovalStatusId = await _context.tblStatuses
        //                   .Where(s => s.StatusId == obj.ClusterManagerApprovalStatus)
        //                   .Select(s => (int?)s.StatusId)
        //                   .FirstOrDefaultAsync();

        //               if (clusterApprovalStatusId == null)
        //                   return new Response { Status = false, StatusCode = HttpStatusCode.BadRequest, Message = $"Invalid ClusterApprovalStatus: {obj.ClusterManagerApprovalStatus}" };

        //               candidateApproval.ClusterManagerApprovalStatus = clusterApprovalStatusId.Value;
        //               candidateApproval.ClusterManagerReviewBy = loginDetail.EmployeeId;

        //               // Audit Approval
        //               auditApprovalStatusId = await _context.tblStatuses
        //                   .Where(s => s.StatusId == obj.AuditApprovalStatus)
        //                   .Select(s => (int?)s.StatusId)
        //                   .FirstOrDefaultAsync();

        //               if (auditApprovalStatusId == null)
        //                   return new Response { Status = false, StatusCode = HttpStatusCode.BadRequest, Message = $"Invalid AuditApprovalStatus: {obj.AuditApprovalStatus}" };

        //               candidateApproval.AuditApprovalStatus = auditApprovalStatusId.Value;
        //               candidateApproval.AuditReviewedBy = loginDetail.EmployeeId;

        //               // HR Approval
        //               hrApprovalStatusId = await _context.tblStatuses
        //                   .Where(s => s.StatusId == obj.HRApprovalStatus)
        //                   .Select(s => (int?)s.StatusId)
        //                   .FirstOrDefaultAsync();

        //               if (hrApprovalStatusId == null)
        //                   return new Response { Status = false, StatusCode = HttpStatusCode.BadRequest, Message = $"Invalid HRApprovalStatus: {obj.HRApprovalStatus}" };

        //               candidateApproval.HRApprovalStatus = hrApprovalStatusId.Value;
        //               candidateApproval.HRReviewedBy = loginDetail.EmployeeId;

        //               candidateApproval.UpdatedOn = DateTime.UtcNow;
        //               candidateApproval.LastUpdatedBy = loginDetail.EmployeeId;
        //           }
        //           else
        //           {
        //               return new Response { Status = false, StatusCode = HttpStatusCode.Forbidden, Message = "Unauthorized role for this operation" };
        //           }

        //           _context.tblNewCandidateApprovals.Update(candidateApproval);
        //           await _context.SaveChangesAsync();

        //           // Create employee when all three approvals are received (StatusId = 1)
        //           const int approvedStatusId = 1; // Assuming StatusId 1 = "Approved"
        //           if (candidateApproval.ClusterManagerApprovalStatus == approvedStatusId &&
        // candidateApproval.AuditApprovalStatus == approvedStatusId &&
        // candidateApproval.HRApprovalStatus == approvedStatusId)
        //           {
        //               //                string newEcode = "";
        //               //                string prefix = "";
        //               //                string defaultCode = "";

        //               //                // Set prefix and default code based on CompanyId
        //               //                if (candidate.CompanyId == 1)
        //               //                {
        //               //                    prefix = "V";
        //               //                    //defaultCode = "V00001";
        //               //                }
        //               //                else if (candidate.CompanyId == 2)
        //               //                {
        //               //                    prefix = "V2S";
        //               //                    //defaultCode = "V2S0001";
        //               //                }
        //               //                else if (candidate.CompanyId == 3)
        //               //                {
        //               //                    prefix = "PT";
        //               //                    //defaultCode = "PT00001";
        //               //                }
        //               //                else
        //               //                {
        //               //                    prefix = "V";
        //               //                    //defaultCode = "V00001";
        //               //                }

        //               //                // Fetch lastEmployee with the appropriate prefix
        //               //                var lastEmployee = await _context.tblEmployees.AsNoTracking()
        //               //.Where(e => e.Ecode.StartsWith(prefix))
        //               //.OrderByDescending(e => e.EmployeeId)
        //               //.FirstOrDefaultAsync();

        //               //                // Generate new Ecode
        //               //                if (lastEmployee != null && lastEmployee.Ecode.StartsWith(prefix) &&
        //               //                    lastEmployee.Ecode.Length > prefix.Length &&
        //               //                    int.TryParse(lastEmployee.Ecode.Substring(prefix.Length), out int number))
        //               //                {
        //               //                    newEcode = prefix + (number + 1).ToString().PadLeft(5, '0');
        //               //                }
        //               //                else
        //               //                {
        //               //                    newEcode = defaultCode;
        //               //                }

        //               //                // Use newEcode as needed

        //               string defaultPassword = "V2@123";
        //               string hashedPassword = BCrypt.Net.BCrypt.HashPassword(defaultPassword);

        //               //                var newEmployee = new tblEmployee
        //               //                {
        //               //                    CandidateId = candidate.Id,
        //               //                    FULL_NAME = string.Join(" ", new[] { candidate.FIRST_NAME, candidate.MIDDLE_NAME, candidate.LAST_NAME }
        //               //     .Where(n => !string.IsNullOrWhiteSpace(n))),
        //               //                    FirstName = candidate.FIRST_NAME,
        //               //                    MiddleName = candidate.MIDDLE_NAME,
        //               //                    LastName = candidate.LAST_NAME,
        //               //                    EMAIL_ADDRESS = candidate.EMAIL_ADDRESS,
        //               //                    MOBILE = candidate.MOBILE,
        //               //                    DepartmentId = int.TryParse(candidate.DEPARTMENT, out var departmentId) ? departmentId : (int?)null,
        //               //                    DesignationId = int.TryParse(candidate.DESIGNATION, out var designationId) ? designationId : (int?)null,
        //               //                    LocationId = int.TryParse(candidate.LOCATION, out var locationId) ? locationId : (int?)null,
        //               //                    DOJ = candidate.JOINING_DATE ?? DateTime.UtcNow,
        //               //                    Ecode = newEcode,
        //               //                    LastUpdatedBy = loginDetail.EmployeeId,
        //               //                    PasswordHash = hashedPassword,
        //               //                    PasswordSalt = null,
        //               //                    UpdatedBy = candidate.UpdatedBy,
        //               //                    UpdatedOn = DateTime.UtcNow,

        //               //                    // Additional fields from Candidate
        //               //                    TITLE = candidate.TITLE,
        //               //                  //  HusbandName = candidate.HUSBAND_NAME,
        //               //                    FATHER_S_NAME = candidate.FATHER_NAME,
        //               //                    MOTHER_S_NAME = candidate.MOTHER_NAME,
        //               //                    DOB = candidate.DOB,
        //               //                    GENDER = candidate.GENDER,
        //               //                    GROSS_SALARY = candidate.GROSS_SALARY,
        //               //                    UAN_NO = candidate.UAN_NO,
        //               //                    PAN_NO = candidate.PAN_NO,
        //               //                    AADHAR_NO = candidate.AADHAR_NO,
        //               //                    NAME_ON_ADHAR = candidate.NAME_ON_AADHAR,

        //               //                    PLACE_OF_BIRTH = candidate.PLACE_OF_BIRTH,
        //               //                    PRESENT_ADDRESS = candidate.PRESENT_ADDRESS,
        //               //                    PRESENT_ADDRESS_PIN_CODE = candidate.PRESENT_ADDRESS_PIN_CODE,
        //               //                    PERMANENT_ADDRESS = candidate.PERMANENT_ADDRESS,
        //               //                    PERMANENT_ADDRESS_PIN_CODE = candidate._PERMANENT_ADDRESS_PIN_CODE,

        //               //                    APPLICANT_CODE = candidate.APPLICANT_CODE,
        //               //                    WEEKLY_OFF = candidate.WEEKLY_OFF,
        //               //                    MARITIAL_STATUS = candidate.MARITIAL_STATUS,
        //               //                    ISRELATIVEINCOMPANY = candidate.ISRELATIVEINCOMPANY,
        //               //                    NATIONALITY = candidate.NATIONALITY,
        //               //                    RELIGION = candidate.RELIGION,
        //               //                    BANK_NAME = candidate.BANK_NAME,
        //               //                    A_C_NO = candidate.A_C_NO,
        //               //                    BANK_IFSC_CODE = candidate.BANK_IFSC_CODE,
        //               //                    REFERENCE1__OF_LAST_3_COMPANY = candidate.REFERENCE1__OF_LAST_3_COMPANY,
        //               //                    CONTACT1_OF_LAST_3_COMPANY = candidate.CONTACT1_OF_LAST_3_COMPANY,
        //               //                    REFERENCE2__OF_LAST_3_COMPANY1 = candidate.REFERENCE2__OF_LAST_3_COMPANY,
        //               //                    CONTACT2_OF_LAST_3_COMPANY1 = candidate.CONTACT2_OF_LAST_3_COMPANY,
        //               //                    REFERENCE3__OF_LAST_3_COMPANY11 = candidate.REFERENCE3__OF_LAST_3_COMPANY,
        //               //                    CONTACT3_OF_LAST_3_COMPANY11 = candidate.CONTACT3_OF_LAST_3_COMPANY,
        //               //                    REFERENCE4__OF_LAST_3_COMPANY11 = candidate.REFERENCE4__OF_LAST_3_COMPANY,
        //               //                    CONTACT4_OF_LAST_3_COMPANY11 = candidate.CONTACT4_OF_LAST_3_COMPANY,
        //               //                    REFERENCE5__OF_LAST_3_COMPANY111 = candidate.REFERENCE5__OF_LAST_3_COMPANY,
        //               //                    CONTACT5_OF_LAST_3_COMPANY111 = candidate.CONTACT5_OF_LAST_3_COMPANY,
        //               //                    HIGHEST_QUALIFICATION = candidate.HIGHEST_QUALIFICATION,
        //               //                    BENEFICIARY_ADDRESS = candidate.BENEFICIARY_ADDRESS,
        //               //                    REFERENCE = candidate.REFERENCE,
        //               //                    CreatedOn = candidate.CreatedOn ?? DateTime.UtcNow,
        //               //                    CreatedBy = candidate.CreatedBy,
        //               //                    IsActive = candidate.IsActive ?? true,
        //               //                    IsDeleted = candidate.IsDeleted ?? false,
        //               //                    IsSalarySlipUploaded = candidate.IsSalarySlipUploaded,
        //               //                    IsBankStatementUploaded = candidate.IsBankStatementUploaded,
        //               //                    IsPrevOfferLetterUploaded = candidate.IsPrevOfferLetterUploaded,
        //               //                    IsPassportPhotoUploaded = candidate.IsPassportPhotoUploaded,
        //               //                    IsPanAttachmentUploaded = candidate.IsPanAttachmentUploaded,
        //               //                    IsAadharAttachmentUploaded = candidate.IsAadharAttachmentUploaded,
        //               //                    IsBankPassbookAttachmentUpoaded = candidate.IsBankPassbookAttachmentUpoaded,
        //               //                    IsEducationAttachmentUploaded = candidate.IsEducationAttachmentUploaded,
        //               //                    StatusId = candidate.StatusId,
        //               //                    ApplicantId = candidate.ApplicantId,
        //               //                    BasicSalary = candidate.BasicSalary,
        //               //                    HRA = candidate.HRA,
        //               //                    CCA = candidate.CCA,
        //               //                    SpecialAllowance = candidate.SpecialAllowance,
        //               //                    DA = candidate.DA,
        //               //                    ExtraAllowance = candidate.ExtraAllowance,
        //               //                    monthlyGrossCTC = candidate.monthlyGrossCTC,
        //               //                    annuallyNetCTC = candidate.annuallyNetCTC,
        //               //                    IsResumeUploaded = candidate.IsResumeUploaded,
        //               //                    TotalExperience = candidate.TotalExperience,
        //               //                    SalaryExpectation = candidate.SalaryExpectation,
        //               //                    AdditionalInfoApplicant = candidate.AdditionalInfoApplicant,
        //               //                    Agreement = candidate.Agreement,
        //               //                    IsApplicant = candidate.IsApplicant,
        //               //                    IsApplicantApproved = candidate.IsApplicantApproved,
        //               //                    PFApplicable = candidate.PFApplicable,
        //               //                    BonusApplicable = candidate.BonusApplicable,
        //               //                    ESICApplicable = candidate.ESICApplicable,
        //               //                    // new work 28 may
        //               //                    CompanyId = candidate.CompanyId
        //               //                };

        //               //                await _context.tblEmployees.AddAsync(newEmployee);
        //               //                await _context.SaveChangesAsync();
        //               OutputParameter<string> o = new OutputParameter<String>();
        //               int ra = await _context.GetProcedures().usp_InsertEmployeeAfterInitiateNewAsync(candidate.Id, candidate.FIRST_NAME, candidate.MIDDLE_NAME, candidate.LAST_NAME, candidate.EMAIL_ADDRESS, candidate.MOBILE, int.TryParse(candidate.DEPARTMENT, out var departmentId) ? departmentId : (int?)null, int.TryParse(candidate.DESIGNATION, out var designationId) ? designationId : (int?)null, int.TryParse(candidate.LOCATION, out var locationId) ? locationId : (int?)null, candidate.JOINING_DATE ?? DateTime.Now, hashedPassword, loginDetail.EmployeeId, candidate.TITLE, candidate.FATHER_NAME, candidate.MOTHER_NAME, candidate.DOB, candidate.GENDER, candidate.GROSS_SALARY, candidate.UAN_NO, candidate.PAN_NO, candidate.AADHAR_NO, candidate.NAME_ON_AADHAR, candidate.PLACE_OF_BIRTH, candidate.PRESENT_ADDRESS, candidate.PRESENT_ADDRESS_PIN_CODE, candidate.PERMANENT_ADDRESS, candidate._PERMANENT_ADDRESS_PIN_CODE, candidate.APPLICANT_CODE, candidate.WEEKLY_OFF, candidate.MARITIAL_STATUS, candidate.ISRELATIVEINCOMPANY, candidate.NATIONALITY, candidate.RELIGION, candidate.BANK_NAME, candidate.A_C_NO, candidate.BANK_IFSC_CODE, candidate.REFERENCE1__OF_LAST_3_COMPANY, candidate.CONTACT1_OF_LAST_3_COMPANY, candidate.REFERENCE2__OF_LAST_3_COMPANY, candidate.CONTACT2_OF_LAST_3_COMPANY, candidate.REFERENCE3__OF_LAST_3_COMPANY, candidate.CONTACT3_OF_LAST_3_COMPANY, candidate.REFERENCE4__OF_LAST_3_COMPANY, candidate.CONTACT4_OF_LAST_3_COMPANY, candidate.REFERENCE5__OF_LAST_3_COMPANY, candidate.CONTACT5_OF_LAST_3_COMPANY, candidate.HIGHEST_QUALIFICATION, candidate.BENEFICIARY_ADDRESS, candidate.REFERENCE, candidate.CreatedOn, candidate.CreatedBy, candidate.IsActive, candidate.IsDeleted, candidate.IsSalarySlipUploaded, candidate.IsBankStatementUploaded, candidate.IsPrevOfferLetterUploaded, candidate.IsPassportPhotoUploaded, candidate.IsPanAttachmentUploaded, candidate.IsAadharAttachmentUploaded, candidate.IsBankPassbookAttachmentUpoaded, candidate.IsEducationAttachmentUploaded, candidate.StatusId, candidate.ApplicantId, candidate.BasicSalary, candidate.HRA, candidate.CCA, candidate.SpecialAllowance, candidate.DA, candidate.ExtraAllowance, candidate.monthlyGrossCTC, candidate.annuallyNetCTC, candidate.IsResumeUploaded, candidate.TotalExperience, candidate.SalaryExpectation, candidate.AdditionalInfoApplicant, candidate.Agreement, candidate.IsApplicant, candidate.IsApplicantApproved, candidate.PFApplicable, candidate.BonusApplicable, candidate.ESICApplicable, candidate.CompanyId, candidate.PREV__EST_NO_, candidate.MARITIAL_STATUS, candidate.HUSBAND_NAME, candidate.PreferredLocation,o);

        //               if (String.IsNullOrEmpty(o.Value))
        //                   throw new Exception("Unable to Initiate at the moment, Something went wrong.Contact Administrator");

        //               // Update Password field (plain text) for the newly created employee
        //               var newEmployee = await _context.tblEmployees
        //                   .FirstOrDefaultAsync(e => e.Ecode == o.Value);
        //               if (newEmployee != null)
        //               {
        //                   newEmployee.Password = defaultPassword; // Store plain text password
        //                   _context.tblEmployees.Update(newEmployee);
        //                   await _context.SaveChangesAsync();
        //               }

        //               //// Update Candidate Status
        //               var candidateToUpdate = await _context.Candidates.FirstOrDefaultAsync(c => c.Id == obj.CandidateId);
        //               if (candidateToUpdate != null)
        //               {
        //                   candidateToUpdate.StatusId = approvedStatusId;
        //                   candidateToUpdate.UpdatedOn = DateTime.UtcNow;
        //                   candidateToUpdate.UpdatedBy = loginDetail.EmployeeId;

        //                   _context.Candidates.Update(candidateToUpdate);
        //                   await _context.SaveChangesAsync();
        //               }
        //           }

        //           await trans.CommitAsync();

        //           var sendMail = await _emailService.SendEmailAsync(
        //               new List<string> { candidate.EMAIL_ADDRESS },
        //               new List<string>(),
        //               "Your candidate approval has been processed.",
        //               $"Username: {candidate.EMAIL_ADDRESS} Password: V2@123"
        //           );

        //           return new Response { Status = true, StatusCode = HttpStatusCode.OK, Message = "Candidate approval updated successfully" };
        //       }
        //       catch (Exception ex)
        //       {
        //           await trans.RollbackAsync();
        //           return new Response { Status = false, Message = ex.Message, StatusCode = HttpStatusCode.BadRequest };
        //       }
        //   }
        public async Task<Response> CandidateInitiate(CandidateApprovalDto obj, JwtLoginDetailDto loginDetail)
        {
            await using var trans = await _context.Database.BeginTransactionAsync();

            try
            {
                if (obj == null)
                    return new Response
                    {
                        Status = false,
                        StatusCode = HttpStatusCode.BadRequest,
                        Message = "Data cannot be null"
                    };

                var candidate = await _context.Candidates
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == obj.CandidateId);

                if (candidate == null)
                    return new Response
                    {
                        Status = false,
                        StatusCode = HttpStatusCode.NotFound,
                        Message = $"No Candidate Found for Id: {obj.CandidateId}"
                    };

                var candidateApproval = await _context.tblNewCandidateApprovals
                    .FirstOrDefaultAsync(a => a.CandidateId == candidate.Id);

                if (candidateApproval == null)
                    return new Response
                    {
                        Status = false,
                        StatusCode = HttpStatusCode.NotFound,
                        Message = $"No Candidate Approval Found for Id: {obj.CandidateId}"
                    };

                const int approvedStatusId = 1; // Assuming StatusId 1 = "Approved"

                int? hrApprovalStatusId = null;
                int? auditApprovalStatusId = null;
                int? clusterApprovalStatusId = null;

                // Check if fully approved already
                var ifExist = await _context.tblNewCandidateApprovals
                    .AnyAsync(a =>
                        a.ClusterManagerApprovalStatus == approvedStatusId &&
                        a.HRApprovalStatus == approvedStatusId &&
                        a.AuditApprovalStatus == approvedStatusId &&
                        a.CandidateId == obj.CandidateId);

                if (ifExist)
                {
                    throw new InvalidOperationException("Candidate approval record already exists.");
                }

                // Handle CLUSTER role updates
                if (loginDetail.role == "ClusterManager")
                {
                    if (obj.ClusterManagerApprovalStatus.HasValue && obj.ClusterManagerApprovalStatus > 0)
                    {
                        clusterApprovalStatusId = await _context.tblStatuses
                            .Where(s => s.StatusId == obj.ClusterManagerApprovalStatus)
                            .Select(s => (int?)s.StatusId)
                            .FirstOrDefaultAsync();

                        if (clusterApprovalStatusId == null)
                            return new Response
                            {
                                Status = false,
                                StatusCode = HttpStatusCode.BadRequest,
                                Message = $"Invalid ClusterApprovalStatus: {obj.ClusterManagerApprovalStatus}"
                            };

                        candidateApproval.ClusterManagerApprovalStatus = clusterApprovalStatusId.Value;
                        candidateApproval.ClusterManagerReviewBy = obj.ClusterManagerReviewedBy;
                        candidateApproval.ClusterManagerRemarks = obj.ClusterManagerRemarks;
                        candidateApproval.UpdatedOn = DateTime.UtcNow;
                        candidateApproval.LastUpdatedBy = loginDetail.EmployeeId;

                        // Stamp action date on every decision (approve OR reject), not
                        // only on approval — used by the ageing calc on the list page.
                        // DateTime.Now (local) matches the CanidateDocs.CreatedOn convention.
                        candidateApproval.ClusterManagerApprovedOn = DateTime.Now;
                    }
                }
                // Handle AUDIT role updates
                else if (loginDetail.role == "Audit")
                {
                    if (obj.AuditApprovalStatus.HasValue && obj.AuditApprovalStatus > 0)
                    {
                        auditApprovalStatusId = await _context.tblStatuses
                            .Where(s => s.StatusId == obj.AuditApprovalStatus)
                            .Select(s => (int?)s.StatusId)
                            .FirstOrDefaultAsync();

                        if (auditApprovalStatusId == null)
                            return new Response
                            {
                                Status = false,
                                StatusCode = HttpStatusCode.BadRequest,
                                Message = $"Invalid AuditApprovalStatus: {obj.AuditApprovalStatus}"
                            };

                        candidateApproval.AuditApprovalStatus = auditApprovalStatusId.Value;
                        candidateApproval.AuditReviewedBy = obj.AuditReviewedBy;
                        candidateApproval.AuditRemarks = obj.AuditRemarks;
                        candidateApproval.UpdatedOn = DateTime.UtcNow;
                        candidateApproval.LastUpdatedBy = loginDetail.EmployeeId;

                        // Stamp action date on every decision (approve OR reject) — local time
                        // to match CanidateDocs.CreatedOn for the ageing calc.
                        candidateApproval.AuditApprovedOn = DateTime.Now;
                    }
                }
                // Handle HR role updates
                else if (loginDetail.role == "HR")
                {
                    if (obj.HRApprovalStatus.HasValue && obj.HRApprovalStatus > 0)
                    {
                        hrApprovalStatusId = await _context.tblStatuses
                            .Where(s => s.StatusId == obj.HRApprovalStatus)
                            .Select(s => (int?)s.StatusId)
                            .FirstOrDefaultAsync();

                        if (hrApprovalStatusId == null)
                            return new Response
                            {
                                Status = false,
                                StatusCode = HttpStatusCode.BadRequest,
                                Message = $"Invalid HRApprovalStatus: {obj.HRApprovalStatus}"
                            };

                        candidateApproval.HRApprovalStatus = hrApprovalStatusId.Value;
                        candidateApproval.HRReviewedBy = obj.HRReviewedBy;
                        candidateApproval.HRRemarks = obj.HRRemarks;
                        candidateApproval.UpdatedOn = DateTime.UtcNow;
                        candidateApproval.LastUpdatedBy = loginDetail.EmployeeId;

                        // Stamp action date on every decision (approve OR reject) — local time
                        // to match CanidateDocs.CreatedOn for the ageing calc.
                        candidateApproval.HRApprovedOn = DateTime.Now;
                    }
                }
                // Handle SUPERADMIN role updates
                else if (loginDetail.role == "SuperAdmin")
                {
                    // Build the common SuperAdmin remark text
                    var superAdminRemark = string.IsNullOrWhiteSpace(obj.SuperAdminRemarks)
                        ? "Approved by SuperAdmin"
                        : $"Approved by SuperAdmin - {obj.SuperAdminRemarks.Trim()}";

                    // SuperAdmin should not override already approved stages

                    // Cluster Manager Approval
                    if (obj.ClusterManagerApprovalStatus.HasValue && obj.ClusterManagerApprovalStatus > 0 &&
                        candidateApproval.ClusterManagerApprovalStatus != approvedStatusId)
                    {
                        clusterApprovalStatusId = await _context.tblStatuses
                            .Where(s => s.StatusId == obj.ClusterManagerApprovalStatus)
                            .Select(s => (int?)s.StatusId)
                            .FirstOrDefaultAsync();

                        if (clusterApprovalStatusId == null)
                            return new Response
                            {
                                Status = false,
                                StatusCode = HttpStatusCode.BadRequest,
                                Message = $"Invalid ClusterApprovalStatus: {obj.ClusterManagerApprovalStatus}"
                            };

                        candidateApproval.ClusterManagerApprovalStatus = clusterApprovalStatusId.Value;
                        candidateApproval.ClusterManagerReviewBy = loginDetail.EmployeeId.ToString();
                        candidateApproval.ClusterManagerRemarks = superAdminRemark;

                        // Stamp action date on every decision (approve OR reject).
                        candidateApproval.ClusterManagerApprovedOn = DateTime.UtcNow;
                    }

                    // Audit Approval
                    if (obj.AuditApprovalStatus.HasValue && obj.AuditApprovalStatus > 0 &&
                        candidateApproval.AuditApprovalStatus != approvedStatusId)
                    {
                        auditApprovalStatusId = await _context.tblStatuses
                            .Where(s => s.StatusId == obj.AuditApprovalStatus)
                            .Select(s => (int?)s.StatusId)
                            .FirstOrDefaultAsync();

                        if (auditApprovalStatusId == null)
                            return new Response
                            {
                                Status = false,
                                StatusCode = HttpStatusCode.BadRequest,
                                Message = $"Invalid AuditApprovalStatus: {obj.AuditApprovalStatus}"
                            };

                        candidateApproval.AuditApprovalStatus = auditApprovalStatusId.Value;
                        candidateApproval.AuditReviewedBy = loginDetail.EmployeeId.ToString();
                        candidateApproval.AuditRemarks = superAdminRemark;

                        // Stamp action date on every decision (approve OR reject) — local time
                        // to match CanidateDocs.CreatedOn for the ageing calc.
                        candidateApproval.AuditApprovedOn = DateTime.Now;
                    }

                    // HR Approval
                    if (obj.HRApprovalStatus.HasValue && obj.HRApprovalStatus > 0 &&
                        candidateApproval.HRApprovalStatus != approvedStatusId)
                    {
                        hrApprovalStatusId = await _context.tblStatuses
                            .Where(s => s.StatusId == obj.HRApprovalStatus)
                            .Select(s => (int?)s.StatusId)
                            .FirstOrDefaultAsync();

                        if (hrApprovalStatusId == null)
                            return new Response
                            {
                                Status = false,
                                StatusCode = HttpStatusCode.BadRequest,
                                Message = $"Invalid HRApprovalStatus: {obj.HRApprovalStatus}"
                            };

                        candidateApproval.HRApprovalStatus = hrApprovalStatusId.Value;
                        candidateApproval.HRReviewedBy = loginDetail.EmployeeId.ToString();
                        candidateApproval.HRRemarks = superAdminRemark;

                        // Stamp action date on every decision (approve OR reject) — local time
                        // to match CanidateDocs.CreatedOn for the ageing calc.
                        candidateApproval.HRApprovedOn = DateTime.Now;
                    }

                    candidateApproval.UpdatedOn = DateTime.UtcNow;
                    candidateApproval.LastUpdatedBy = loginDetail.EmployeeId;
                }
                else
                {
                    return new Response
                    {
                        Status = false,
                        StatusCode = HttpStatusCode.Forbidden,
                        Message = "Unauthorized role for this operation"
                    };
                }

                _context.tblNewCandidateApprovals.Update(candidateApproval);

                // Create employee when all three approvals are received (StatusId = 1)
                if (candidateApproval.ClusterManagerApprovalStatus == approvedStatusId &&
                    candidateApproval.AuditApprovalStatus == approvedStatusId &&
                    candidateApproval.HRApprovalStatus == approvedStatusId)
                {
                    string defaultPassword = "V2@123";
                    string hashedPassword = BCrypt.Net.BCrypt.HashPassword(defaultPassword);

                    // Create output parameter for the stored procedure
                    var outputParam = new OutputParameter<string>();

                    int ra = await _context.GetProcedures().usp_InsertEmployeeAfterInitiate01Async(
                        candidateId: candidate.Id,
                        firstName: candidate.FIRST_NAME,
                        middleName: candidate.MIDDLE_NAME,
                        lastName: candidate.LAST_NAME,
                        eMAIL_ADDRESS: candidate.EMAIL_ADDRESS,
                        mOBILE: candidate.MOBILE,
                        departmentId: int.TryParse(candidate.DEPARTMENT, out var departmentId) ? departmentId : (int?)null,
                        designationId: int.TryParse(candidate.DESIGNATION, out var designationId) ? designationId : (int?)null,
                        locationId: int.TryParse(candidate.LOCATION, out var locationId) ? locationId : (int?)null,
                        dOJ: candidate.JOINING_DATE ?? DateTime.Now,
                        passwordHash: hashedPassword,
                        updatedBy: loginDetail.EmployeeId,
                        tITLE: candidate.TITLE,
                        fATHER_S_NAME: candidate.FATHER_NAME,
                        mOTHER_S_NAME: candidate.MOTHER_NAME,
                        dOB: candidate.DOB,
                        gENDER: candidate.GENDER,
                        gROSS_SALARY: candidate.GROSS_SALARY,
                        uAN_NO: candidate.UAN_NO,
                        pAN_NO: candidate.PAN_NO,
                        aADHAR_NO: candidate.AADHAR_NO,
                        nAME_ON_ADHAR: candidate.NAME_ON_AADHAR,
                        pLACE_OF_BIRTH: candidate.PLACE_OF_BIRTH,
                        pRESENT_ADDRESS: candidate.PRESENT_ADDRESS,
                        pRESENT_ADDRESS_PIN_CODE: candidate.PRESENT_ADDRESS_PIN_CODE,
                        pERMANENT_ADDRESS: candidate.PERMANENT_ADDRESS,
                        pERMANENT_ADDRESS_PIN_CODE: candidate._PERMANENT_ADDRESS_PIN_CODE,
                        aPPLICANT_CODE: candidate.APPLICANT_CODE,
                        wEEKLY_OFF: candidate.WEEKLY_OFF,
                        mARITIAL_STATUS: candidate.MARITIAL_STATUS,
                        iSRELATIVEINCOMPANY: candidate.ISRELATIVEINCOMPANY,
                        nATIONALITY: candidate.NATIONALITY,
                        rELIGION: candidate.RELIGION,
                        bANK_NAME: candidate.BANK_NAME,
                        a_C_NO: candidate.A_C_NO,
                        bANK_IFSC_CODE: candidate.BANK_IFSC_CODE,
                        rEFERENCE1__OF_LAST_3_COMPANY: candidate.REFERENCE1__OF_LAST_3_COMPANY,
                        cONTACT1_OF_LAST_3_COMPANY: candidate.CONTACT1_OF_LAST_3_COMPANY,
                        rEFERENCE2__OF_LAST_3_COMPANY1: candidate.REFERENCE2__OF_LAST_3_COMPANY,
                        cONTACT2_OF_LAST_3_COMPANY1: candidate.CONTACT2_OF_LAST_3_COMPANY,
                        rEFERENCE3__OF_LAST_3_COMPANY11: candidate.REFERENCE3__OF_LAST_3_COMPANY,
                        cONTACT3_OF_LAST_3_COMPANY11: candidate.CONTACT3_OF_LAST_3_COMPANY,
                        rEFERENCE4__OF_LAST_3_COMPANY11: candidate.REFERENCE4__OF_LAST_3_COMPANY,
                        cONTACT4_OF_LAST_3_COMPANY11: candidate.CONTACT4_OF_LAST_3_COMPANY,
                        rEFERENCE5__OF_LAST_3_COMPANY111: candidate.REFERENCE5__OF_LAST_3_COMPANY,
                        cONTACT5_OF_LAST_3_COMPANY111: candidate.CONTACT5_OF_LAST_3_COMPANY,
                        hIGHEST_QUALIFICATION: candidate.HIGHEST_QUALIFICATION,
                        bENEFICIARY_ADDRESS: candidate.BENEFICIARY_ADDRESS,
                        rEFERENCE: candidate.REFERENCE,
                        createdOn: candidate.CreatedOn,
                        createdBy: loginDetail.EmployeeId, // UPDATE 24/11
                        isActive: candidate.IsActive,
                        isDeleted: candidate.IsDeleted,
                        isSalarySlipUploaded: candidate.IsSalarySlipUploaded,
                        isBankStatementUploaded: candidate.IsBankStatementUploaded,
                        isPrevOfferLetterUploaded: candidate.IsPrevOfferLetterUploaded,
                        isPassportPhotoUploaded: candidate.IsPassportPhotoUploaded,
                        isPanAttachmentUploaded: candidate.IsPanAttachmentUploaded,
                        isAadharAttachmentUploaded: candidate.IsAadharAttachmentUploaded,
                        isBankPassbookAttachmentUpoaded: candidate.IsBankPassbookAttachmentUpoaded,
                        isEducationAttachmentUploaded: candidate.IsEducationAttachmentUploaded,
                        statusId: candidate.StatusId,
                        applicantId: candidate.ApplicantId,
                        basicSalary: candidate.BasicSalary,
                        hRA: candidate.HRA,
                        cCA: candidate.CCA,
                        specialAllowance: candidate.SpecialAllowance,
                        dA: candidate.DA,
                        extraAllowance: candidate.ExtraAllowance,
                        monthlyGrossCTC: candidate.monthlyGrossCTC,
                        annuallyNetCTC: candidate.annuallyNetCTC,
                        isResumeUploaded: candidate.IsResumeUploaded,
                        totalExperience: candidate.TotalExperience,
                        salaryExpectation: candidate.SalaryExpectation,
                        additionalInfoApplicant: candidate.AdditionalInfoApplicant,
                        agreement: candidate.Agreement,
                        isApplicant: candidate.IsApplicant,
                        isApplicantApproved: candidate.IsApplicantApproved,
                        pFApplicable: candidate.PFApplicable,
                        bonusApplicable: candidate.BonusApplicable,
                        eSICApplicable: candidate.ESICApplicable,
                        companyId: candidate.CompanyId,
                        eSICNO: candidate.PREV__EST_NO_,
                        maritalStatus: candidate.MARITIAL_STATUS,
                        husbandName: candidate.HUSBAND_NAME,
                        preferredLocation: candidate.PreferredLocation,
                        reportHeadEcode: obj.ReportHeadEcode,
                        shiftId: candidate.ShiftID,
                        newEcode: outputParam
                    );
                    // int ra = await _context.GetProcedures().usp_InsertEmployeeAfterInitiateNewAsync(
                    //    candidateId: candidate.Id,
                    //    firstName: candidate.FIRST_NAME,
                    //    middleName: candidate.MIDDLE_NAME,
                    //    lastName: candidate.LAST_NAME,
                    //    eMAIL_ADDRESS: candidate.EMAIL_ADDRESS,
                    //    mOBILE: candidate.MOBILE,
                    //    departmentId: int.TryParse(candidate.DEPARTMENT, out var departmentId) ? departmentId : (int?)null,
                    //    designationId: int.TryParse(candidate.DESIGNATION, out var designationId) ? designationId : (int?)null,
                    //    locationId: int.TryParse(candidate.LOCATION, out var locationId) ? locationId : (int?)null,
                    //    dOJ: candidate.JOINING_DATE ?? DateTime.Now,
                    //    passwordHash: hashedPassword,
                    //    updatedBy: loginDetail.EmployeeId,
                    //    tITLE: candidate.TITLE,
                    //    fATHER_S_NAME: candidate.FATHER_NAME,
                    //    mOTHER_S_NAME: candidate.MOTHER_NAME,
                    //    dOB: candidate.DOB,
                    //    gENDER: candidate.GENDER,
                    //    gROSS_SALARY: candidate.GROSS_SALARY,
                    //    uAN_NO: candidate.UAN_NO,
                    //    pAN_NO: candidate.PAN_NO,
                    //    aADHAR_NO: candidate.AADHAR_NO,
                    //    nAME_ON_ADHAR: candidate.NAME_ON_AADHAR,
                    //    pLACE_OF_BIRTH: candidate.PLACE_OF_BIRTH,
                    //    pRESENT_ADDRESS: candidate.PRESENT_ADDRESS,
                    //    pRESENT_ADDRESS_PIN_CODE: candidate.PRESENT_ADDRESS_PIN_CODE,
                    //    pERMANENT_ADDRESS: candidate.PERMANENT_ADDRESS,
                    //    pERMANENT_ADDRESS_PIN_CODE: candidate._PERMANENT_ADDRESS_PIN_CODE,
                    //    aPPLICANT_CODE: candidate.APPLICANT_CODE,
                    //    wEEKLY_OFF: candidate.WEEKLY_OFF,
                    //    mARITIAL_STATUS: candidate.MARITIAL_STATUS,
                    //    iSRELATIVEINCOMPANY: candidate.ISRELATIVEINCOMPANY,
                    //    nATIONALITY: candidate.NATIONALITY,
                    //    rELIGION: candidate.RELIGION,
                    //    bANK_NAME: candidate.BANK_NAME,
                    //    a_C_NO: candidate.A_C_NO,
                    //    bANK_IFSC_CODE: candidate.BANK_IFSC_CODE,
                    //    rEFERENCE1__OF_LAST_3_COMPANY: candidate.REFERENCE1__OF_LAST_3_COMPANY,
                    //    cONTACT1_OF_LAST_3_COMPANY: candidate.CONTACT1_OF_LAST_3_COMPANY,
                    //    rEFERENCE2__OF_LAST_3_COMPANY1: candidate.REFERENCE2__OF_LAST_3_COMPANY,
                    //    cONTACT2_OF_LAST_3_COMPANY1: candidate.CONTACT2_OF_LAST_3_COMPANY,
                    //    rEFERENCE3__OF_LAST_3_COMPANY11: candidate.REFERENCE3__OF_LAST_3_COMPANY,
                    //    cONTACT3_OF_LAST_3_COMPANY11: candidate.CONTACT3_OF_LAST_3_COMPANY,
                    //    rEFERENCE4__OF_LAST_3_COMPANY11: candidate.REFERENCE4__OF_LAST_3_COMPANY,
                    //    cONTACT4_OF_LAST_3_COMPANY11: candidate.CONTACT4_OF_LAST_3_COMPANY,
                    //    rEFERENCE5__OF_LAST_3_COMPANY111: candidate.REFERENCE5__OF_LAST_3_COMPANY,
                    //    cONTACT5_OF_LAST_3_COMPANY111: candidate.CONTACT5_OF_LAST_3_COMPANY,
                    //    hIGHEST_QUALIFICATION: candidate.HIGHEST_QUALIFICATION,
                    //    bENEFICIARY_ADDRESS: candidate.BENEFICIARY_ADDRESS,
                    //    rEFERENCE: candidate.REFERENCE,
                    //    createdOn: candidate.CreatedOn,
                    //    createdBy: loginDetail.EmployeeId, // UPDATE 24/11
                    //    isActive: candidate.IsActive,
                    //    isDeleted: candidate.IsDeleted,
                    //    isSalarySlipUploaded: candidate.IsSalarySlipUploaded,
                    //    isBankStatementUploaded: candidate.IsBankStatementUploaded,
                    //    isPrevOfferLetterUploaded: candidate.IsPrevOfferLetterUploaded,
                    //    isPassportPhotoUploaded: candidate.IsPassportPhotoUploaded,
                    //    isPanAttachmentUploaded: candidate.IsPanAttachmentUploaded,
                    //    isAadharAttachmentUploaded: candidate.IsAadharAttachmentUploaded,
                    //    isBankPassbookAttachmentUpoaded: candidate.IsBankPassbookAttachmentUpoaded,
                    //    isEducationAttachmentUploaded: candidate.IsEducationAttachmentUploaded,
                    //    statusId: candidate.StatusId,
                    //    applicantId: candidate.ApplicantId,
                    //    basicSalary: candidate.BasicSalary,
                    //    hRA: candidate.HRA,
                    //    cCA: candidate.CCA,
                    //    specialAllowance: candidate.SpecialAllowance,
                    //    dA: candidate.DA,
                    //    extraAllowance: candidate.ExtraAllowance,
                    //    monthlyGrossCTC: candidate.monthlyGrossCTC,
                    //    annuallyNetCTC: candidate.annuallyNetCTC,
                    //    isResumeUploaded: candidate.IsResumeUploaded,
                    //    totalExperience: candidate.TotalExperience,
                    //    salaryExpectation: candidate.SalaryExpectation,
                    //    additionalInfoApplicant: candidate.AdditionalInfoApplicant,
                    //    agreement: candidate.Agreement,
                    //    isApplicant: candidate.IsApplicant,
                    //    isApplicantApproved: candidate.IsApplicantApproved,
                    //    pFApplicable: candidate.PFApplicable,
                    //    bonusApplicable: candidate.BonusApplicable,
                    //    eSICApplicable: candidate.ESICApplicable,
                    //    companyId: candidate.CompanyId,
                    //    eSICNO: candidate.PREV__EST_NO_,
                    //    maritalStatus: candidate.MARITIAL_STATUS,
                    //    husbandName: candidate.HUSBAND_NAME,
                    //    preferredLocation: candidate.PreferredLocation,
                    //    reportHeadEcode: obj.ReportHeadEcode,
                    //    newEcode: outputParam
                    //);

                    if (string.IsNullOrEmpty(outputParam.Value))
                        throw new Exception("Unable to Initiate at the moment, Something went wrong. Contact Administrator");

                    // Update Password field (plain text) for the newly created employee
                    var newEmployee = await _context.tblEmployees
                        .FirstOrDefaultAsync(e => e.Ecode == outputParam.Value);

                    if (newEmployee != null)
                    {
                        newEmployee.Password = defaultPassword; // Store plain text password
                        _context.tblEmployees.Update(newEmployee);
                    }

                    // Update Candidate Status
                    var candidateToUpdate = await _context.Candidates
                        .FirstOrDefaultAsync(c => c.Id == obj.CandidateId);

                    if (candidateToUpdate != null)
                    {
                        candidateToUpdate.StatusId = approvedStatusId;
                        candidateToUpdate.UpdatedOn = DateTime.UtcNow;
                        candidateToUpdate.UpdatedBy = loginDetail.EmployeeId;

                        _context.Candidates.Update(candidateToUpdate);
                    }
                }

                // Single SaveChanges
                await _context.SaveChangesAsync();
                await trans.CommitAsync();

                var sendMail = await _emailService.SendEmailAsync(
                    new List<string> { candidate.EMAIL_ADDRESS },
                    new List<string>(),
                    "Your candidate approval has been processed.",
                    $"Username: {candidate.EMAIL_ADDRESS} Password: V2@123"
                );

                return new Response
                {
                    Status = true,
                    StatusCode = HttpStatusCode.OK,
                    Message = "Candidate approval updated successfully"
                };
            }
            catch (Exception ex)
            {
                await trans.RollbackAsync();
                return new Response
                {
                    Status = false,
                    Message = ex.Message,
                    StatusCode = HttpStatusCode.BadRequest
                };
            }
        }


        public async Task<Response> GetCandidateInfo(int candidateID)
        {
            try
            {
                // Fetch candidate details
                var candidateEntity = await _context.Candidates.AsNoTracking()
                    .Where(c => c.Id == candidateID)
                    .FirstOrDefaultAsync();

                if (candidateEntity == null)
                {
                    return new Response
                    {
                        Status = false,
                        Message = "Candidate not found",
                        StatusCode = System.Net.HttpStatusCode.NotFound
                    };
                }
                var reportingHeadId = _context.tblEmployees
                  .Where(e => e.Ecode == candidateEntity.ReportHeadEcode)
                  .Select(a => (int?)a.EmployeeId)
                   .FirstOrDefault();

                var reportingheadname = _context.tblEmployees
                                      .Where(e => e.EmployeeId == reportingHeadId)
                                      .Select(a => a.FirstName ?? a.FULL_NAME)
                                      .FirstOrDefault();

                var reportingheadecode = _context.tblEmployees
                                      .Where(e => e.EmployeeId == reportingHeadId)
                                      .Select(a => a.Ecode)
                                      .FirstOrDefault();

                // Map to Candidate Model
                var candidate = new Candidate
                {
                    reportingHeadId = reportingHeadId ?? 0,
                    reportingHeadName = reportingheadname ?? string.Empty,// or null if reportingHeadId is nullable
                    reportinHeadEcode = reportingheadecode ?? "",
                    id = candidateEntity.Id,
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
                    isOfferLetterAttachmentUploaded = candidateEntity.IsOfferLetterAttachmentUploaded ?? false,
                    prevEstNo = candidateEntity.PREV__EST_NO_ ?? "",
                    // Audit fields
                    createdBy = candidateEntity.CreatedBy ?? "",
                    createdOn = candidateEntity.CreatedOn,
                    updatedBy = candidateEntity.UpdatedBy ?? "",
                    updatedOn = candidateEntity.UpdatedOn,
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
                    companyId = candidateEntity.CompanyId,
                    ShiftID = candidateEntity.ShiftID ?? 0,
                    IsUANRegistered = candidateEntity.IsUANRegistered ?? false,
                    PreferredLocation = candidateEntity.PreferredLocation ?? "",
                    AoCode = candidateEntity.AOCode ?? ""

                };

                // Fetch Family Details
                var familyData = await _context.tblFamilies.AsNoTracking()
                    .Where(f => f.CID == candidateID)
                    .Select(f => new CandidateUpdateFamilyMember
                    {
                        familyMemberName = f.Family_Member_Name ?? "",
                        relation = f.Relation ?? "",
                        dob = f.DOB.GetValueOrDefault()
                    })
                    .ToListAsync() ?? new List<CandidateUpdateFamilyMember>();

                // Fetch Experience Details
                var experienceData = await _context.tblExperiences.AsNoTracking()
                    .Where(e => e.CID == candidateID)
                    .Select(e => new CandidateUpdateExperience
                    {
                        nameOfCompany = e.Name_of_Company ?? "",
                        workLocation = e.Work_Location ?? "",
                        positionHeld = e.Position_Held ?? "",
                        from = e.From.GetValueOrDefault(),
                        to = e.To.GetValueOrDefault(),
                        inHand = e.InHand,
                        lastCtc = e.Last_CTC
                    })
                    .ToListAsync() ?? new List<CandidateUpdateExperience>();

                // Fetch Qualification Details
                var qualificationData = await _context.tblQualifications.AsNoTracking()
                    .Where(q => q.CID == candidateID)
                    .Select(q => new CandidateUpdateQualification
                    {
                        education = q.Education ?? "",
                        yop = q.YOP ?? "",
                        grade = q.Grade ?? "",
                        type = q.Type ?? ""
                    })
                    .ToListAsync() ?? new List<CandidateUpdateQualification>();

                // Fetch Document Details
                var documentData = await _context.CanidateDocs.AsNoTracking()
                    .Where(d => d.CId == candidateID && d.IsDeleted == false)
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
                    .Where(alh => alh.CandidateId == candidateID)
                    .OrderByDescending(alh => alh.AssignedOnDate)
                    .Select(alh => new
                    {
                        AssignedLocation = alh.AssignedLocation,
                        AssignedReason = alh.AssignedReason,
                        IsActive = alh.IsActive,
                        AssignedOnDate = alh.AssignedOnDate,
                        ReleasedOnDate = alh.ReleasedOnDate
                    })
                    .ToListAsync();

                return new Response
                {
                    Status = true,
                    Message = "Data Fetched Successfully",
                    StatusCode = System.Net.HttpStatusCode.OK,
                    Data = new
                    {
                        CandidateInfo = candidate,
                        FamilyMembersList = familyData,
                        ExperienceList = experienceData,
                        QualificationList = qualificationData,
                        Documents = documentData,
                        AssignLocations = assignLocationData
                    }
                };
            }
            catch (Exception ex)
            {
                return new Response
                {
                    Status = false,
                    Message = $"Error fetching data: {ex.Message}",
                    StatusCode = System.Net.HttpStatusCode.InternalServerError
                };
            }
        }


        public async Task<List<CandidateSearchResult>> SearchCandidatesAsync(
          DateTime? startDate,
          DateTime? endDate,
          List<string> locationIds,
          List<string> designationIds,
          List<string> departmentIds,
          List<int> statusIds,
          List<int> hrApprovalStatuses,
          List<int> auditApprovalStatuses,
          List<int> clusterManagerApprovalStatuses)
        {
            try
            {
                Console.WriteLine("Starting SearchCandidatesAsync...");

                using (var connection = _context.Database.GetDbConnection())
                using (var command = SetupConnectionAndCommand(connection))
                {
                    SetupParameters(command, startDate, endDate, locationIds, designationIds, departmentIds,
                        statusIds, hrApprovalStatuses, auditApprovalStatuses, clusterManagerApprovalStatuses);

                    var candidates = await FetchAndProcessCandidates(command);

                    Console.WriteLine($"Returning {candidates.Count} candidates.");
                    return candidates;
                }
            }
            catch (SqlException sqlEx)
            {
                Console.WriteLine($"SQL Error in SearchCandidatesAsync: {sqlEx.Message}, Error Number: {sqlEx.Number}, Procedure: {sqlEx.Procedure}, Line: {sqlEx.LineNumber}");
                throw new ApplicationException("A database error occurred while fetching candidate details.", sqlEx);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"General Error in SearchCandidatesAsync: {ex.Message}, Stack Trace: {ex.StackTrace}");
                throw new ApplicationException("An error occurred while fetching candidate details.", ex);
            }
            finally
            {
                Console.WriteLine("SearchCandidatesAsync completed.");
            }
        }

        private SqlCommand SetupConnectionAndCommand(IDbConnection connection)
        {
            Console.WriteLine("Opening database connection...");
            if (connection.State != ConnectionState.Open)
            {
                try
                {
                    connection.Open();
                }
                catch (SqlException sqlEx)
                {
                    Console.WriteLine($"Failed to open connection: {sqlEx.Message}, Error Number: {sqlEx.Number}");
                    throw;
                }
            }

            var command = connection.CreateCommand();
            command.CommandText = "sp_SearchCandidates";
            command.CommandType = CommandType.StoredProcedure;
            command.CommandTimeout = 300;

            return (SqlCommand)command;
        }

        private void SetupParameters(
            SqlCommand command,
            DateTime? startDate,
            DateTime? endDate,
            List<string> locationIds,
            List<string> designationIds,
            List<string> departmentIds,
            List<int> statusIds,
            List<int> hrApprovalStatuses,
            List<int> auditApprovalStatuses,
            List<int> clusterManagerApprovalStatuses)
        {
            Console.WriteLine("Setting up parameters...");

            command.Parameters.Add(new SqlParameter("@StartDate", SqlDbType.DateTime) { Value = (object)startDate ?? DBNull.Value });
            command.Parameters.Add(new SqlParameter("@EndDate", SqlDbType.DateTime) { Value = (object)endDate ?? DBNull.Value });

            command.Parameters.Add(new SqlParameter("@LocationIds", SqlDbType.Structured)
            {
                TypeName = "StringList",
                Value = CreateStringTable(locationIds)
            });
            Console.WriteLine($"LocationIds count: {(locationIds != null ? locationIds.Count : 0)}");

            command.Parameters.Add(new SqlParameter("@DesignationIds", SqlDbType.Structured)
            {
                TypeName = "StringList",
                Value = CreateStringTable(designationIds)
            });
            Console.WriteLine($"DesignationIds count: {(designationIds != null ? designationIds.Count : 0)}");

            command.Parameters.Add(new SqlParameter("@DepartmentIds", SqlDbType.Structured)
            {
                TypeName = "StringList",
                Value = CreateStringTable(departmentIds)
            });
            Console.WriteLine($"DepartmentIds count: {(departmentIds != null ? departmentIds.Count : 0)}");

            command.Parameters.Add(new SqlParameter("@StatusIds", SqlDbType.Structured)
            {
                TypeName = "IntList",
                Value = CreateIntTable(statusIds)
            });
            Console.WriteLine($"StatusIds count: {(statusIds != null ? statusIds.Count : 0)}");

            command.Parameters.Add(new SqlParameter("@HrApprovalStatuses", SqlDbType.Structured)
            {
                TypeName = "IntList",
                Value = CreateIntTable(hrApprovalStatuses)
            });
            Console.WriteLine($"HrApprovalStatuses count: {(hrApprovalStatuses != null ? hrApprovalStatuses.Count : 0)}");

            command.Parameters.Add(new SqlParameter("@AuditApprovalStatuses", SqlDbType.Structured)
            {
                TypeName = "IntList",
                Value = CreateIntTable(auditApprovalStatuses)
            });
            Console.WriteLine($"AuditApprovalStatuses count: {(auditApprovalStatuses != null ? auditApprovalStatuses.Count : 0)}");

            command.Parameters.Add(new SqlParameter("@ClusterManagerApprovalStatuses", SqlDbType.Structured)
            {
                TypeName = "IntList",
                Value = CreateIntTable(clusterManagerApprovalStatuses)
            });
            Console.WriteLine($"ClusterManagerApprovalStatuses count: {(clusterManagerApprovalStatuses != null ? clusterManagerApprovalStatuses.Count : 0)}");
        }

        private DataTable CreateStringTable(List<string> values)
        {
            var table = new DataTable();
            table.Columns.Add("Value", typeof(string));
            if (values != null && values.Count > 0)
            {
                foreach (var value in values)
                {
                    table.Rows.Add(value);
                }
            }
            return table;
        }

        private DataTable CreateIntTable(List<int> values)
        {
            var table = new DataTable();
            table.Columns.Add("Id", typeof(int));
            if (values != null && values.Count > 0)
            {
                foreach (var value in values)
                {
                    table.Rows.Add(value);
                }
            }
            return table;
        }

        private async Task<List<CandidateSearchResult>> FetchAndProcessCandidates(SqlCommand command)
        {
            Console.WriteLine("Executing stored procedure...");
            var candidates = new List<CandidateSearchResult>();

            using (var reader = await command.ExecuteReaderAsync())
            {
                int rowCount = 0;
                Console.WriteLine("Starting to read rows...");

                while (await reader.ReadAsync())
                {
                    rowCount++;
                    var candidate = ProcessRow(reader, rowCount);
                    candidates.Add(candidate);

                    if (rowCount % 100 == 0)
                    {
                        Console.WriteLine($"Processed {rowCount} rows...");
                        if (rowCount % 1000 == 0)
                        {
                            GC.Collect();
                            Console.WriteLine("Forced garbage collection to manage memory.");
                        }
                    }
                }

                Console.WriteLine($"Finished reading rows. Total rows: {rowCount}");
            }

            return candidates;
        }

        private CandidateSearchResult ProcessRow(SqlDataReader reader, int rowCount)
        {
            try
            {
                var candidate = new CandidateSearchResult();

                Console.WriteLine($"Processing row {rowCount}...");

                try
                {
                    candidate.Id = reader.IsDBNull(reader.GetOrdinal("Id")) ? 0 : reader.GetInt64(reader.GetOrdinal("Id"));
                }
                catch (Exception ex)
                {
                    throw new ApplicationException($"Error mapping Id for row {rowCount}: {ex.Message}", ex);
                }

                try
                {
                    candidate.FirstName = reader.IsDBNull(reader.GetOrdinal("FirstName")) ? string.Empty : reader.GetString(reader.GetOrdinal("FirstName"));
                }
                catch (Exception ex)
                {
                    throw new ApplicationException($"Error mapping FirstName for row {rowCount}: {ex.Message}", ex);
                }

                try
                {
                    candidate.MiddleName = reader.IsDBNull(reader.GetOrdinal("MiddleName")) ? null : reader.GetString(reader.GetOrdinal("MiddleName"));
                }
                catch (Exception ex)
                {
                    throw new ApplicationException($"Error mapping MiddleName for row {rowCount}: {ex.Message}", ex);
                }

                try
                {
                    candidate.LastName = reader.IsDBNull(reader.GetOrdinal("LastName")) ? string.Empty : reader.GetString(reader.GetOrdinal("LastName"));
                }
                catch (Exception ex)
                {
                    throw new ApplicationException($"Error mapping LastName for row {rowCount}: {ex.Message}", ex);
                }

                try
                {
                    candidate.Phone = reader.IsDBNull(reader.GetOrdinal("Phone")) ? string.Empty : reader.GetString(reader.GetOrdinal("Phone"));
                }
                catch (Exception ex)
                {
                    throw new ApplicationException($"Error mapping Phone for row {rowCount}: {ex.Message}", ex);
                }

                try
                {
                    candidate.Email = reader.IsDBNull(reader.GetOrdinal("Email")) ? string.Empty : reader.GetString(reader.GetOrdinal("Email"));
                }
                catch (Exception ex)
                {
                    throw new ApplicationException($"Error mapping Email for row {rowCount}: {ex.Message}", ex);
                }

                try
                {
                    candidate.Designation = reader.IsDBNull(reader.GetOrdinal("Designation")) ? string.Empty : reader.GetString(reader.GetOrdinal("Designation"));
                }
                catch (Exception ex)
                {
                    throw new ApplicationException($"Error mapping Designation for row {rowCount}: {ex.Message}", ex);
                }

                try
                {
                    candidate.Dob = reader.IsDBNull(reader.GetOrdinal("Dob")) ? DateTime.MinValue : reader.GetDateTime(reader.GetOrdinal("Dob"));
                }
                catch (Exception ex)
                {
                    throw new ApplicationException($"Error mapping Dob for row {rowCount}: {ex.Message}", ex);
                }

                try
                {
                    candidate.StatusId = reader.IsDBNull(reader.GetOrdinal("StatusId")) ? 0 : reader.GetInt32(reader.GetOrdinal("StatusId"));
                }
                catch (Exception ex)
                {
                    throw new ApplicationException($"Error mapping StatusId for row {rowCount}: {ex.Message}", ex);
                }

                try
                {
                    candidate.HrApprovalStatus = reader.IsDBNull(reader.GetOrdinal("hrApprovalStatus")) ? null : reader.GetInt32(reader.GetOrdinal("hrApprovalStatus"));
                }
                catch (Exception ex)
                {
                    throw new ApplicationException($"Error mapping HrApprovalStatus for row {rowCount}: {ex.Message}", ex);
                }

                try
                {
                    candidate.AuditApprovalStatus = reader.IsDBNull(reader.GetOrdinal("auditApprovalStatus")) ? null : reader.GetInt32(reader.GetOrdinal("auditApprovalStatus"));
                }
                catch (Exception ex)
                {
                    throw new ApplicationException($"Error mapping AuditApprovalStatus for row {rowCount}: {ex.Message}", ex);
                }

                try
                {
                    candidate.ClusterManagerApprovalStatus = reader.IsDBNull(reader.GetOrdinal("clusterManagerApprovalStatus")) ? null : reader.GetInt32(reader.GetOrdinal("clusterManagerApprovalStatus"));
                }
                catch (Exception ex)
                {
                    throw new ApplicationException($"Error mapping ClusterManagerApprovalStatus for row {rowCount}: {ex.Message}", ex);
                }

                try
                {
                    candidate.StoreLocationName = reader.IsDBNull(reader.GetOrdinal("StoreLocationName")) ? string.Empty : reader.GetString(reader.GetOrdinal("StoreLocationName"));
                }
                catch (Exception ex)
                {
                    throw new ApplicationException($"Error mapping StoreLocationName for row {rowCount}: {ex.Message}", ex);
                }

                try
                {
                    candidate.StoreLocationCode = reader.IsDBNull(reader.GetOrdinal("StoreLocationCode")) ? string.Empty : reader.GetString(reader.GetOrdinal("StoreLocationCode"));
                }
                catch (Exception ex)
                {
                    throw new ApplicationException($"Error mapping StoreLocationCode for row {rowCount}: {ex.Message}", ex);
                }

                return candidate;
            }
            catch (Exception rowEx)
            {
                Console.WriteLine($"Error processing row {rowCount}: {rowEx.Message}, Stack Trace: {rowEx.StackTrace}");
                throw new ApplicationException($"Error processing row {rowCount}.", rowEx);
            }
        }


        public async Task<Candidate> GetApplicantByIdAsync(int candidateId)
        {
            var dataCandidate = await _context.Candidates
                .Include(c => c.CandidateStatusHistories)
                .Include(c => c.InterviewRounds)
                .ThenInclude(r => r.Interviewers)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == candidateId);

            if (dataCandidate == null)
            {
                return null;
            }

            return MapToCandidateModel(dataCandidate);
        }
        public async Task<ScheduleInterview> GetScheduleInterviewDetailsById(int ScheduleId)
        {
            try
            {
                await using var conn = _context.Database.GetDbConnection();
                await conn.OpenAsync();

                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "dbo.sp_GetScheduleInterviewById";
                cmd.CommandType = CommandType.StoredProcedure;

                var param = cmd.CreateParameter();
                param.ParameterName = "@ScheduleId";
                param.Value = ScheduleId;
                param.DbType = DbType.Int32;
                cmd.Parameters.Add(param);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var json = reader.GetString(0);
                    var result = JsonConvert.DeserializeObject<ScheduleInterview>(json);
                    return result;
                }

                return null;
            }
            catch (Exception ex)
            {

                Console.WriteLine($"[sp_GetScheduleInterviewById] Error: {ex.Message}");
                throw;
            }
        }
        //public async Task<bool> UpdateApplicantStatusAsync(UpdateStatusDto dto)
        //{
        //    try
        //    {
        //        var dataCandidate = await _context.Candidates.FindAsync(dto.CandidateId);
        //        if (dataCandidate == null)
        //        {
        //            return false;
        //        }

        //        dataCandidate.StatusId = dto.StatusId;
        //        _context.Candidates.Update(dataCandidate);

        //        var history = new CandidateStatusHistory
        //        {
        //            CandidateId = dto.CandidateId,
        //            StatusId = dto.StatusId,
        //            HRName = dto.HRName,
        //            CallDate = dto.CallDate,
        //            CallStartTime = dto.CallStartTime,
        //            CallEndTime = dto.CallEndTime,
        //            CallResponse = dto.CallResponse,
        //            UpdatedAt = DateTime.UtcNow
        //        };
        //        _context.CandidateStatusHistories.Add(history);

        //        await _context.SaveChangesAsync();
        //        return true;
        //    }
        //    catch (DbUpdateException ex)
        //    {
        //        Console.WriteLine($"Error updating candidate status: {ex.Message}");
        //        return false;
        //    }
        //}
        //
        public async Task<bool> UpdateApplicantStatusAsync(UpdateStatusDto dto)
        {
            try
            {
                var dataCandidate = await _context.Candidates.FindAsync(dto.CandidateId);
                if (dataCandidate == null)
                {
                    return false;
                }

                dataCandidate.IsApplicantApproved = dto.IsApplicantApproved;
                if (dataCandidate.IsApplicantApproved == true)
                {
                    dataCandidate.StatusId = 4;
                }
                else
                {
                    dataCandidate.StatusId = 2;
                }
                dataCandidate.IsApplicant = false;
                _context.Candidates.Update(dataCandidate);

                await _context.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateException ex)
            {
                Console.WriteLine($"Error updating candidate status: {ex.Message}");
                return false;
            }
        }

        public async Task<Candidate> GetApplicantDetailsAsync(int candidateId)
        {
            var dataCandidate = await _context.Candidates
                .Include(c => c.Status)
                .Include(c => c.CandidateStatusHistories)
                .Include(c => c.InterviewRounds)
                .ThenInclude(r => r.Interviewers)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == candidateId);

            if (dataCandidate == null)
            {
                return null;
            }

            return MapToCandidateModel(dataCandidate);
        }

        private Candidate MapToCandidateModel(HRMSAPI.Data.Candidate dataCandidate)
        {
            return new Candidate
            {
                id = dataCandidate.Id,
                firstName = dataCandidate.FIRST_NAME,
                emailAddress = dataCandidate.EMAIL_ADDRESS,
                dob = dataCandidate.DOB,
                mobile = dataCandidate.MOBILE,
                statusId = dataCandidate.StatusId,
                StateId = dataCandidate.StateId,
                designation = dataCandidate.DESIGNATION,
                StatusHistory = dataCandidate.CandidateStatusHistories?.ToList() ?? new List<CandidateStatusHistory>(),
                InterviewRounds = dataCandidate.InterviewRounds?.ToList() ?? new List<InterviewRound>()
            };
        }
        public async Task<Response> GetApplicantList(int pageNumber, int pageSize, int StatusId, string searchTerm = "")
        {
            try
            {
                searchTerm = searchTerm?.Trim().ToLower();

                var baseQuery = _context.Candidates
                    .AsNoTracking()
                    .Where(c => c.IsApplicant == true && (StatusId == 0 || c.StatusId == StatusId) && c.IsActive == true && c.IsDeleted == false)
                    .GroupJoin(
                        _context.tblLocations,
                        c => Convert.ToInt32(c.LOCATION),
                        l => l.LocationId,
                        (c, locations) => new { Candidate = c, Locations = locations })
                    .SelectMany(
                        x => x.Locations.DefaultIfEmpty(),
                        (x, location) => new { x.Candidate, Location = location });

                // Apply search filter if needed
                if (!string.IsNullOrEmpty(searchTerm))
                {
                    baseQuery = baseQuery.Where(x =>
                        (x.Candidate.FIRST_NAME ?? "").ToLower().Contains(searchTerm) ||
                        (x.Candidate.MIDDLE_NAME ?? "").ToLower().Contains(searchTerm) ||
                        (x.Candidate.LAST_NAME ?? "").ToLower().Contains(searchTerm) ||
                        (x.Candidate.EMP_CODE ?? "").ToLower().Contains(searchTerm) ||
                        (x.Candidate.EMAIL_ADDRESS ?? "").ToLower().Contains(searchTerm) ||
                        (x.Candidate.DESIGNATION ?? "").ToLower().Contains(searchTerm) ||
                        (x.Candidate.LOCATION ?? "").ToLower().Contains(searchTerm) ||
                        (x.Candidate.MOBILE ?? "").ToLower().Contains(searchTerm));
                }

                var totalRecords = await baseQuery.Select(x => x.Candidate.Id).Distinct().CountAsync();
                var pendingCount = await baseQuery.CountAsync(x => x.Candidate.StatusId == 4);

                var candidates = await baseQuery
                    .OrderByDescending(x => x.Candidate.Id)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(x => new
                    {
                        ID = x.Candidate.Id,
                        FirstName = x.Candidate.FIRST_NAME,
                        MiddleName = x.Candidate.MIDDLE_NAME,
                        LastName = x.Candidate.LAST_NAME,
                        Phone = x.Candidate.MOBILE,
                        Email = x.Candidate.EMAIL_ADDRESS,
                        Designation = x.Candidate.DESIGNATION,
                        DOB = x.Candidate.DOB,
                        StatusId = x.Candidate.StatusId,
                        DesignationName = _context.tblDesignations
                            .Where(d => d.DesignationId == Convert.ToInt32(x.Candidate.DESIGNATION))
                            .Select(d => d.DesignationName)
                            .FirstOrDefault() ?? "NA",
                        PositionHeldInPreviousCompany = x.Candidate.POSITION_HELD_IN_PREVIOUS_COMPANY,
                        ApplicantCode = x.Candidate.EMP_CODE,
                        IsApplicant = x.Candidate.IsApplicant,
                        // Fetch the resume FilePath where FileType is "Resume"
                        ResumeLink = _context.CanidateDocs
                            .AsNoTracking()
                            .Where(d => d.CId == x.Candidate.Id && d.FileType == "Resume" && d.IsDeleted == false)
                            .OrderByDescending(d => d.CreatedOn)
                            .Select(d => d.FilePath)
                            .FirstOrDefault() ?? "",
                        OfferLetterLink = _context.CanidateDocs
                            .AsNoTracking()
                            .Where(d => d.CId == x.Candidate.Id && d.FileType == "OfferLetter" && d.IsDeleted == false)
                            .OrderByDescending(d => d.CreatedOn)
                            .Select(d => d.FilePath)
                            .FirstOrDefault() ?? ""
                    })
                    .ToListAsync();

                return new Response
                {
                    Status = true,
                    Message = "Data Fetched Successfully",
                    StatusCode = HttpStatusCode.OK,
                    Data = new
                    {
                        TotalRecords = totalRecords,
                        PendingCount = pendingCount,
                        Candidates = candidates
                    }
                };
            }
            catch (Exception ex)
            {
                return new Response
                {
                    Status = false,
                    Message = ex.Message,
                    StatusCode = HttpStatusCode.InternalServerError,
                    Data = ex.Data
                };
            }
        }


        public async Task<byte[]> ExportApplicantListByStatusToExcelAsync(
            JwtLoginDetailDto loginDetail,
            int statusId = 0,
            string searchTerm = "")
        {
            searchTerm = searchTerm?.Trim().ToLower();
            var connection = _context.Database.GetDbConnection();

            // Resume / offer-letter files are hosted on the production server only.
            // Use the configured production base URL (Reports:ResumeBaseUrl) so links
            // in the exported Excel work no matter where this API instance is running
            // (dev, staging, local). Falls back to the current request host if absent.
            var configuredBase = _configuration?["Reports:ResumeBaseUrl"];
            string baseUrl;
            if (!string.IsNullOrWhiteSpace(configuredBase))
            {
                baseUrl = configuredBase;
            }
            else
            {
                var request = _httpContextAccessor.HttpContext.Request;
                baseUrl = $"{request.Scheme}://{request.Host}/";
            }
            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync();

            int? roleId = await (from e in _context.tblEmployees
                                 join er in _context.tblEmployeeRoles on e.EmployeeId equals er.EmployeeId
                                 join r in _context.tblRoles on er.RoleId equals r.RoleId
                                 where e.EmployeeId.ToString() == loginDetail.EmployeeId
                                 select r.RoleId)
                                 .FirstOrDefaultAsync();

            int? employeeId = loginDetail.EmployeeId != null
                ? Convert.ToInt32(loginDetail.EmployeeId)
                : (int?)null;

            await using var command = connection.CreateCommand();
            command.CommandText = "dbo.sp_GetApplicantListNew01";
            command.CommandType = CommandType.StoredProcedure;
            command.CommandTimeout = 120;

            command.Parameters.Add(new SqlParameter("@PageNumber", SqlDbType.Int) { Value = 1 });
            command.Parameters.Add(new SqlParameter("@PageSize", SqlDbType.Int) { Value = int.MaxValue });
            command.Parameters.Add(new SqlParameter("@StatusId", SqlDbType.Int) { Value = statusId });
            command.Parameters.Add(new SqlParameter("@SearchTerm", SqlDbType.NVarChar, 200)
            {
                Value = string.IsNullOrWhiteSpace(searchTerm) ? (object)DBNull.Value : searchTerm
            });
            command.Parameters.Add(new SqlParameter("@RoleId", SqlDbType.Int) { Value = (object?)roleId ?? DBNull.Value });
            command.Parameters.Add(new SqlParameter("@EmployeeId", SqlDbType.Int) { Value = (object?)employeeId ?? DBNull.Value });

            using var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                using var emptyWb = new XLWorkbook();
                var wsEmpty = emptyWb.Worksheets.Add("Applicants");
                using var emptyStream = new MemoryStream();
                emptyWb.SaveAs(emptyStream);
                return emptyStream.ToArray();
            }

            await reader.NextResultAsync();

            var dt = new DataTable();
            dt.Load(reader);

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Applicants");

            var exportColumns = dt.Columns
                .Cast<DataColumn>()
                .Where(c => !IsPrimaryKeyColumn(c.ColumnName))
                .ToList();

            // Identify document-link columns by NAME — column positions shift after
            // IsPrimaryKeyColumn filtering, so a hard-coded index lands on the wrong
            // column and the actual ResumeLink stays as a raw DB path.
            var resumeColIdx = exportColumns.FindIndex(c =>
                string.Equals(c.ColumnName, "ResumeLink", StringComparison.OrdinalIgnoreCase));
            var offerLetterColIdx = exportColumns.FindIndex(c =>
                string.Equals(c.ColumnName, "OfferLetterLink", StringComparison.OrdinalIgnoreCase));

            // Approval pipeline columns (present once the SP is upgraded; absent on
            // older SPs — handled with -1 / skip).
            int IdxOf(string name) =>
                exportColumns.FindIndex(c => string.Equals(c.ColumnName, name, StringComparison.OrdinalIgnoreCase));

            var statusColIdxs = new[] { IdxOf("LpStatus"), IdxOf("ClusterStatus"), IdxOf("HrStatus") };
            var ageingColIdxs = new[] { IdxOf("LpAgeingHours"), IdxOf("ClusterAgeingHours"), IdxOf("HrAgeingHours") };
            var dateColIdxs   = new[] {
                IdxOf("DocumentUploadedOn"),
                IdxOf("LpActionedOn"),
                IdxOf("ClusterActionedOn"),
                IdxOf("HrActionedOn"),
            };

            string ApprovalStatusText(object raw)
            {
                if (raw == null || raw == DBNull.Value) return "Pending";
                if (!int.TryParse(raw.ToString(), out var s)) return raw.ToString();
                return s switch
                {
                    1 => "Approved",
                    2 => "Rejected",
                    0 => "Pending",
                    _ => $"Status {s}",
                };
            }

            string AgeingText(object raw)
            {
                if (raw == null || raw == DBNull.Value) return string.Empty;
                if (!double.TryParse(raw.ToString(), out var hrs)) return raw.ToString();
                if (hrs < 0) hrs = 0;
                if (hrs < 24) return $"{hrs:0.#} hrs";
                var days = (int)(hrs / 24);
                var rem = hrs - days * 24;
                return rem < 0.05 ? $"{days}d ({hrs:0.#} hrs)" : $"{days}d {rem:0.#}h ({hrs:0.#} hrs)";
            }

            for (int i = 0; i < exportColumns.Count; i++)
            {
                worksheet.Cell(1, i + 1).Value = exportColumns[i].ColumnName;
            }

            for (int row = 0; row < dt.Rows.Count; row++)
            {
                for (int col = 0; col < exportColumns.Count; col++)
                {
                    var cell = worksheet.Cell(row + 2, col + 1);
                    var value = dt.Rows[row][exportColumns[col]];

                    if (value == DBNull.Value)
                    {
                        cell.Value = string.Empty;
                        continue;
                    }

                    if (col == resumeColIdx || col == offerLetterColIdx)
                    {
                        var rawPath = value.ToString();
                        if (string.IsNullOrWhiteSpace(rawPath))
                        {
                            cell.Value = string.Empty;
                            continue;
                        }

                        // DB stores Windows-style relative paths
                        // ("92975_user@x.com\Resume\18052026_foo.pdf"). Convert to a
                        // URL: normalize separators, URL-encode each segment, prepend
                        // the production base URL (where the file actually exists).
                        var normalized = rawPath.Replace('\\', '/').TrimStart('/');
                        var encoded = string.Join("/",
                            normalized.Split('/').Select(Uri.EscapeDataString));
                        var url = baseUrl.TrimEnd('/') + "/" + encoded;

                        var displayName = Path.GetFileName(rawPath.Replace('\\', '/'));
                        if (string.IsNullOrEmpty(displayName)) displayName = url;

                        cell.Value = displayName;
                        cell.SetHyperlink(new XLHyperlink(url));
                        cell.Style.Font.FontColor = XLColor.Blue;
                        cell.Style.Font.Underline = XLFontUnderlineValues.Single;
                    }
                    else if (statusColIdxs.Contains(col))
                    {
                        var txt = ApprovalStatusText(value);
                        cell.Value = txt;
                        if (txt == "Approved") cell.Style.Font.FontColor = XLColor.DarkGreen;
                        else if (txt == "Rejected") cell.Style.Font.FontColor = XLColor.DarkRed;
                        else cell.Style.Font.FontColor = XLColor.DarkGray;
                    }
                    else if (ageingColIdxs.Contains(col))
                    {
                        var txt = AgeingText(value);
                        cell.Value = txt;
                        // Highlight ageing > 24h in red, otherwise normal
                        if (double.TryParse(value.ToString(), out var hrs) && hrs > 24)
                        {
                            cell.Style.Font.FontColor = XLColor.Red;
                            cell.Style.Font.Bold = true;
                        }
                    }
                    else if (dateColIdxs.Contains(col) && value is DateTime dt2)
                    {
                        cell.Value = dt2.ToString("dd-MMM-yyyy HH:mm");
                    }
                    else
                    {
                        cell.Value = value.ToString();
                    }
                }
            }


            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        private static bool IsPrimaryKeyColumn(string columnName)
        {
            if (string.IsNullOrWhiteSpace(columnName)) return false;
            columnName = columnName.Trim();
            if (string.Equals(columnName, "ID", StringComparison.OrdinalIgnoreCase)) return true;
            if (columnName.EndsWith("Id", StringComparison.OrdinalIgnoreCase)) return true;
            if (columnName.EndsWith("_ID", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }


        public async Task InsertInterviewForm(int? positionAppliedId, int? applicantCode, string? preferredWorkLocationIds, string? name, string? maritalStatus, string? presentAddress, bool? declarationConfirmed, string? place, string? Ques1, string? Ques2, string? Ques3, string? BiggestChallenges, string? Strength1, string? Strength2, string? weakness1, string? weakness2, DateTime? dateOfFilling, List<FamilyDto>? familyList, List<ExperienceDto>? experienceList, List<KRAKPIDto>? kraKpiList, List<ReferenceDto>? refList)
        {
            try
            {
                using (var connection = _context.Database.GetDbConnection())
                {
                    await connection.OpenAsync();

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "dbo.InsertInterviewForm";
                        command.CommandType = CommandType.StoredProcedure;
                        command.CommandTimeout = 60;

                        command.Parameters.Add(new SqlParameter("@PositionAppliedId", SqlDbType.Int) { Value = (object?)positionAppliedId ?? DBNull.Value });
                        command.Parameters.Add(new SqlParameter("@ApplicantCode", SqlDbType.Int) { Value = (object?)applicantCode ?? DBNull.Value });
                        command.Parameters.Add(new SqlParameter("@PreferredWorkLocationIds", SqlDbType.NVarChar, 200) { Value = (object?)preferredWorkLocationIds ?? DBNull.Value });
                        command.Parameters.Add(new SqlParameter("@Name", SqlDbType.NVarChar, 100) { Value = (object?)name ?? DBNull.Value });
                        command.Parameters.Add(new SqlParameter("@MaritalStatus", SqlDbType.NVarChar, 50) { Value = (object?)maritalStatus ?? DBNull.Value });
                        command.Parameters.Add(new SqlParameter("@PresentAddress", SqlDbType.NVarChar, -1) { Value = (object?)presentAddress ?? DBNull.Value });
                        command.Parameters.Add(new SqlParameter("@DeclarationConfirmed", SqlDbType.Bit) { Value = (object?)declarationConfirmed ?? DBNull.Value });
                        command.Parameters.Add(new SqlParameter("@Place", SqlDbType.NVarChar, 100) { Value = (object?)place ?? DBNull.Value });
                        command.Parameters.Add(new SqlParameter("@DateOfFilling", SqlDbType.Date) { Value = (object?)dateOfFilling ?? DBNull.Value });

                        command.Parameters.Add(new SqlParameter("@Ques1", SqlDbType.NVarChar, 200) { Value = (object?)Ques1 ?? DBNull.Value });
                        command.Parameters.Add(new SqlParameter("@Ques2", SqlDbType.NVarChar, 200) { Value = (object?)Ques2 ?? DBNull.Value });
                        command.Parameters.Add(new SqlParameter("@Ques3", SqlDbType.NVarChar, 200) { Value = (object?)Ques3 ?? DBNull.Value });
                        command.Parameters.Add(new SqlParameter("@BiggestChallenges", SqlDbType.NVarChar, -1) { Value = (object?)BiggestChallenges ?? DBNull.Value });

                        command.Parameters.Add(new SqlParameter("@Strength1", SqlDbType.NVarChar, 200) { Value = (object?)Strength1 ?? DBNull.Value });
                        command.Parameters.Add(new SqlParameter("@Strength2", SqlDbType.NVarChar, 200) { Value = (object?)Strength2 ?? DBNull.Value });
                        command.Parameters.Add(new SqlParameter("@Weakness1", SqlDbType.NVarChar, 200) { Value = (object?)weakness1 ?? DBNull.Value });
                        command.Parameters.Add(new SqlParameter("@Weakness2", SqlDbType.NVarChar, 200) { Value = (object?)weakness2 ?? DBNull.Value });

                        command.Parameters.Add(new SqlParameter("@FamilyInfo", SqlDbType.Structured)
                        {
                            TypeName = "dbo.FamilyTableType",
                            Value = DataTableHelper.ToFamilyDataTable(familyList ?? new List<FamilyDto>())
                        });

                        command.Parameters.Add(new SqlParameter("@ExperienceInfo", SqlDbType.Structured)
                        {
                            TypeName = "dbo.ExperienceTableType",
                            Value = DataTableHelper.ToExperienceDataTable(experienceList ?? new List<ExperienceDto>())
                        });

                        command.Parameters.Add(new SqlParameter("@KRAKPIInfo", SqlDbType.Structured)
                        {
                            TypeName = "dbo.KRAKPITableType",
                            Value = DataTableHelper.ToKRAKPIDataTable(kraKpiList ?? new List<KRAKPIDto>())
                        });

                        command.Parameters.Add(new SqlParameter("@ReferenceInfo", SqlDbType.Structured)
                        {
                            TypeName = "dbo.ReferencesTableType",
                            Value = DataTableHelper.ToReferenceDataTable(refList ?? new List<ReferenceDto>())
                        });

                        await command.ExecuteNonQueryAsync();
                    }
                }

            }
            catch (Exception ex)
            {


                throw new ApplicationException("An error occurred while inserting the interview form.", ex);
            }
        }

        public async Task<ApplicantDto?> GetApplicantById(int applicantId)
        {
            try
            {
                await using var conn = _context.Database.GetDbConnection();
                await conn.OpenAsync();

                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "dbo.GetApplicantById";
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 60;

                var param = cmd.CreateParameter();
                param.ParameterName = "@ApplicantId";
                param.Value = applicantId;
                cmd.Parameters.Add(param);

                await using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {

                    if (reader.FieldCount == 1 && reader.GetName(0) == "Message")
                    {

                        return null;
                    }

                    return new ApplicantDto
                    {

                        ApplicantId = Convert.ToInt32(reader["ApplicantId"]),
                        IsApplicant = Convert.ToBoolean(reader["IsApplicant"]),
                        FullName = reader["FullName"]?.ToString() ?? string.Empty,
                        DesignationId = Convert.ToInt32(reader["DesignationId"]),
                        LocationId = Convert.ToInt32(reader["LocationId"])

                    };
                }

                return null;
            }
            catch (SqlException sqlEx)
            {
                throw new Exception("A database error occurred while retrieving the applicant.", sqlEx);
            }
            catch (Exception ex)
            {
                throw new Exception("An unexpected error occurred while retrieving the applicant.", ex);
            }
        }


        public async Task<InterviewFormRequest> GetInterviewFormDataById(int applicantid)
        {
            try
            {
                await using var conn = _context.Database.GetDbConnection();
                await conn.OpenAsync();

                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "dbo.sp_GetInterviewFormdataById";
                cmd.CommandType = CommandType.StoredProcedure;

                var param = cmd.CreateParameter();
                param.ParameterName = "@ApplicantId";
                param.Value = applicantid;
                param.DbType = DbType.Int32;
                cmd.Parameters.Add(param);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var json = reader.GetString(0);
                    var result = JsonConvert.DeserializeObject<InterviewFormRequest>(json);
                    return result;
                }

                return null;
            }
            catch (Exception ex)
            {

                Console.WriteLine($"[GetInterviewFormDataById] Error: {ex.Message}");
                throw;
            }
        }

        public async Task<Response> InsertOfferLetter(CandidateOfferLetter details, CandidateOfferLetterDoc candidateDocs, string updatedBy)
        {
            try
            {
                int candidateId = details.ApplicantId;
                string email = details.Email ?? "unknown";

                async Task SaveFileIfExists1(IFormFile? file, string folder, string docType, int index = 0)
                {
                    if (file?.Length > 0)
                    {
                        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                        var allowedExtensions = docType switch
                        {
                            "OfferLetter" => new[] { ".pdf", ".docx" },
                            _ => new[] { ".pdf" }
                        };

                        if (!allowedExtensions.Contains(extension))
                            throw new Exception($"Invalid file type for {docType}{(index > 0 ? $" - {index}" : "")}. Allowed: {string.Join(", ", allowedExtensions)}");

                        var filePath = await SaveFile(file, folder, $"{candidateId}_{email}");

                        int result = await _context.GetProcedures().sp_InsertCandidateDocsAsync(
                            candidateId, filePath, docType, file.Length.ToString(), updatedBy);

                        if (result < 1)
                            throw new Exception($"Unable to save {docType}{(index > 0 ? $" - {index}" : "")}");
                    }
                }

                await SaveFileIfExists1(candidateDocs.OfferLetterAttachment?.FirstOrDefault(), "OfferLetter", "OfferLetter");

                return new Response
                {
                    Status = true,
                    Message = "Offer letter inserted successfully.",
                    StatusCode = HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                return new Response
                {
                    Status = false,
                    Message = ex.Message,
                    StatusCode = HttpStatusCode.InternalServerError
                };
            }
        }


        public async Task<IEnumerable<ApplicantStatusTypeDto>> GetApplicantStatusType()
        {
            return await _context.CandidateProcessStatuses
                .Select(d => new ApplicantStatusTypeDto
                {
                    StatusId = d.StatusId,
                    StatusName = d.StatusName
                })
                .ToListAsync();
        }
        public async Task<Response> UpdateApplicantStatus(UpdateStatusRequest obj, JwtLoginDetailDto loginDetail)
        {
            try
            {
                string empCode = await _context.tblEmployees
                    .Where(e => e.EmployeeId.ToString() == loginDetail.EmployeeId)
                    .Select(e => e.Ecode)
                    .FirstOrDefaultAsync();

                await using var conn = _context.Database.GetDbConnection();
                await conn.OpenAsync();

                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "dbo.sp_UpdateApplicantStatus";
                cmd.CommandType = CommandType.StoredProcedure;

                var applicantParam = cmd.CreateParameter();
                applicantParam.ParameterName = "@ApplicantId";
                applicantParam.Value = obj.ApplicantId;
                cmd.Parameters.Add(applicantParam);

                var statusParam = cmd.CreateParameter();
                statusParam.ParameterName = "@StatusId";
                statusParam.Value = obj.StatusId;
                cmd.Parameters.Add(statusParam);

                var empCodeParam = cmd.CreateParameter();
                empCodeParam.ParameterName = "@EmpCode";
                empCodeParam.Value = empCode;
                cmd.Parameters.Add(empCodeParam);

                await using var reader = await cmd.ExecuteReaderAsync();

                return new Response
                {
                    Status = true,
                    Message = "Applicant Status Updated Successfully",
                    StatusCode = HttpStatusCode.OK,
                    Data = null
                };
            }
            catch (Exception ex)
            {


                return new Response
                {
                    Status = false,
                    Message = $"An error occurred while updating applicant status: {ex.Message}",
                    StatusCode = HttpStatusCode.InternalServerError,
                    Data = null
                };
            }
        }




        public async Task<Response> InsertScheduleInterview(ScheduleInterviewDto dto)
        {
            try
            {
                var connection = _context.Database.GetDbConnection();

                using var command = connection.CreateCommand();
                command.CommandText = "dbo.sp_InsertScheduleInterview";
                command.CommandType = CommandType.StoredProcedure;
                command.CommandTimeout = 60;

                command.Parameters.Add(new SqlParameter("@ApplicantId", SqlDbType.Int)
                {
                    Value = dto.ApplicantId
                });

                command.Parameters.Add(new SqlParameter("@CandidateName", SqlDbType.NVarChar, 100)
                {
                    Value = dto.CandidateName
                });

                command.Parameters.Add(new SqlParameter("@InterviewDateTime", SqlDbType.DateTime)
                {
                    Value = DateTime.ParseExact(dto.InterviewDateTime, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                });

                // NVARCHAR(50) in proc, so keep length 50
                command.Parameters.Add(new SqlParameter("@InterviewMode", SqlDbType.NVarChar, 50)
                {
                    Value = (object?)dto.InterviewMode ?? string.Empty
                });

                command.Parameters.Add(new SqlParameter("@InterviewLocation", SqlDbType.NVarChar, 100)
                {
                    Value = (object?)dto.InterviewLocation ?? string.Empty
                });

                command.Parameters.Add(new SqlParameter("@Notes", SqlDbType.NVarChar, -1)
                {
                    Value = (object?)dto.Notes ?? DBNull.Value
                });

                command.Parameters.Add(new SqlParameter("@RoundId", SqlDbType.Int)
                {
                    Value = dto.Round
                });

                command.Parameters.Add(new SqlParameter("@CreatedBy", SqlDbType.NVarChar, -1)
                {
                    Value = dto.CreatedBy
                });

                // NEW: LocationId parameter (assuming int, adjust if different)
                command.Parameters.Add(new SqlParameter("@LocationId", SqlDbType.Int)
                {
                    Value = (object?)dto.LocationId ?? DBNull.Value
                });

                // Table-valued parameter for Interviewers
                var interviewers = dto.Interviewers ?? new List<long>();
                var interviewerTable = new DataTable();
                interviewerTable.Columns.Add("InterviewerId", typeof(long));

                foreach (var id in interviewers)
                {
                    interviewerTable.Rows.Add(id);
                }

                var tvpParam = new SqlParameter("@InterviewRounds", SqlDbType.Structured)
                {
                    TypeName = "dbo.InterviewRoundsType",
                    Value = interviewerTable
                };
                command.Parameters.Add(tvpParam);

                if (connection.State != ConnectionState.Open)
                    await _context.Database.OpenConnectionAsync();

                await command.ExecuteNonQueryAsync();

                return new Response
                {
                    Status = true,
                    Message = "Interview scheduled successfully.",
                    StatusCode = HttpStatusCode.OK,
                    Data = null
                };
            }
            catch (Exception ex)
            {
                return new Response
                {
                    Status = false,
                    Message = $"Error scheduling interview: {ex.Message}",
                    StatusCode = HttpStatusCode.InternalServerError,
                    Data = null
                };
            }
        }




        public async Task<ResponseWithList<TransferEmployeeDto>> GetAllEmployeeTransferListByManagerId(JwtLoginDetailDto loginDetail)
        {
            if (loginDetail == null || string.IsNullOrEmpty(loginDetail.EmployeeId))
            {
                return new ResponseWithList<TransferEmployeeDto>
                {
                    Message = "Invalid employee details.",
                    Status = false,
                    Data = { }
                };
            }

            string Ecode = await _context.tblEmployees
                .Where(e => e.EmployeeId.ToString() == loginDetail.EmployeeId)
                .Select(e => e.Ecode)
                .FirstOrDefaultAsync();

            if (string.IsNullOrEmpty(Ecode))
            {
                return new ResponseWithList<TransferEmployeeDto>
                {
                    Message = "Manager's report head code not found.",
                    Status = false,
                    Data = new List<TransferEmployeeDto>()
                };
            }

            var transferList = new List<TransferEmployeeDto>();
            var response = new ResponseWithList<TransferEmployeeDto>();

            await using var conn = _context.Database.GetDbConnection();
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "dbo.sp_GetAllEmployeeTransferByManagerId";
            cmd.CommandType = CommandType.StoredProcedure;

            var empCodeParam = cmd.CreateParameter();
            empCodeParam.ParameterName = "@ReportHeadCode";
            empCodeParam.Value = Ecode;
            cmd.Parameters.Add(empCodeParam);

            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                if (reader.FieldCount == 1 && reader["Message"] != null)
                {
                    response.Message = reader["Message"].ToString();
                    response.Status = response.Message.Contains("success", StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    transferList.Add(new TransferEmployeeDto
                    {
                        CandidateId = reader["CandidateId"] != DBNull.Value ? Convert.ToInt32(reader["CandidateId"]) : 0,
                        CandidateName = reader["ReportHeadEcode"] != DBNull.Value ? reader["ReportHeadEcode"].ToString() : null,
                        AssignedLocation = reader["AssignedLocation"] != DBNull.Value ? Convert.ToInt32(reader["AssignedLocation"]) : (int?)null,
                        AssignedReason = reader["AssignedReason"] != DBNull.Value ? reader["AssignedReason"].ToString() : null,
                        IsActive = reader["IsActive"] != DBNull.Value && Convert.ToBoolean(reader["IsActive"]),
                        AssignedOnDate = reader["AssignedOnDate"] != DBNull.Value ? Convert.ToDateTime(reader["AssignedOnDate"]) : DateTime.MinValue,
                        ReleasedOnDate = reader["ReleasedOnDate"] != DBNull.Value ? Convert.ToDateTime(reader["ReleasedOnDate"]) : (DateTime?)null,
                        TransferApprovalStatus = reader["TransferApprovalStatus"] != DBNull.Value ? Convert.ToInt32(reader["TransferApprovalStatus"]) : (int?)null,
                        IsReportingHeadApproval = reader["IsReportingHeadApproval"] != DBNull.Value ? Convert.ToInt32(reader["IsReportingHeadApproval"]) : (int?)null,
                        IsHRApproval = reader["IsHRApproval"] != DBNull.Value ? Convert.ToInt32(reader["IsHRApproval"]) : (int?)null,
                        ReportHeadEcode = reader["ReportHeadEcode"] != DBNull.Value ? reader["ReportHeadEcode"].ToString() : null
                    });


                }
            }


            if (transferList.Count == 0)
            {
                response.Message = "Transfer list has no data.";
                response.Status = false;
                response.Data = new List<TransferEmployeeDto>();
            }
            else
            {
                response.Data = transferList;
                response.Status = true;
                response.Message ??= "Transfer list fetched successfully.";
            }

            return response;
        }
        public async Task<ResponseWithList<TransferApprovalRequestDto>> UpdateTransferApproval(TransferApprovalRequestDto request, JwtLoginDetailDto loginDetail)
        {
            var response = new ResponseWithList<TransferApprovalRequestDto>();

            if (loginDetail == null || string.IsNullOrEmpty(loginDetail.EmployeeId))
            {
                response.Message = "Invalid employee details.";
                response.Status = false;

                return response;
            }

            string? ecode = await _context.tblEmployees
                .Where(e => e.EmployeeId.ToString() == loginDetail.EmployeeId)
                .Select(e => e.Ecode)
                .FirstOrDefaultAsync();

            string? roleName = await (from e in _context.tblEmployees
                                      join er in _context.tblEmployeeRoles on e.EmployeeId equals er.EmployeeId
                                      join r in _context.tblRoles on er.RoleId equals r.RoleId
                                      where e.EmployeeId.ToString() == loginDetail.EmployeeId
                                      select r.RoleName)
                          .FirstOrDefaultAsync();

            if (string.IsNullOrEmpty(roleName))
            {
                return new ResponseWithList<TransferApprovalRequestDto>
                {
                    Message = "No role assigned to the employee.",
                    Status = false,

                };
            }


            if (roleName == "HR")
            {
                if (!roleName.Equals("HR", StringComparison.OrdinalIgnoreCase))
                {
                    return new ResponseWithList<TransferApprovalRequestDto>
                    {
                        Message = "You do not have HR approval access. Only users with HR role can approve this transfer.",
                        Status = false,

                    };
                }


            }


            await using var conn = _context.Database.GetDbConnection();
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "dbo.sp_UpdateTransferApproval";
            cmd.CommandType = CommandType.StoredProcedure;


            var candidateIdParam = cmd.CreateParameter();
            candidateIdParam.ParameterName = "@CandidateId";
            candidateIdParam.Value = request.CandidateId;
            cmd.Parameters.Add(candidateIdParam);

            var roleNameParam = cmd.CreateParameter();
            roleNameParam.ParameterName = "@RoleName";
            roleNameParam.Value = roleName;
            cmd.Parameters.Add(roleNameParam);

            var statusIdParam = cmd.CreateParameter();
            statusIdParam.ParameterName = "@StatusId";
            statusIdParam.Value = request.StatusId;
            cmd.Parameters.Add(statusIdParam);

            var remarkParam = cmd.CreateParameter();
            remarkParam.ParameterName = "@Remark";
            remarkParam.Value = request.Remark ?? (object)DBNull.Value;
            cmd.Parameters.Add(remarkParam);

            var empCodeParam = cmd.CreateParameter();
            empCodeParam.ParameterName = "@Ecode";
            empCodeParam.Value = ecode;
            cmd.Parameters.Add(empCodeParam);


            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                response.Message = reader["Message"]?.ToString();
                response.Status = true;
            }
            else
            {
                response.Message = "No message returned from the database.";
                response.Status = false;
            }

            return response;
        }

        public async Task<Response> GetCheckListByCandidateIdAsync(int candidateId)
        {
            var candidate = await _context.Candidates
                .Where(c => c.Id == candidateId)
                .Select(c => new CandidateChecklistDto
                {
                    CandidateId = c.Id,
                    IsSalarySlipUploaded = c.IsSalarySlipUploaded ?? false,
                    IsBankStatementUploaded = c.IsBankStatementUploaded ?? false,
                    IsPrevOfferLetterUploaded = c.IsPrevOfferLetterUploaded ?? false,
                    IsPassportPhotoUploaded = c.IsPassportPhotoUploaded ?? false,
                    IsPanAttachmentUploaded = c.IsPanAttachmentUploaded ?? false,
                    IsAadharAttachmentUploaded = c.IsAadharAttachmentUploaded ?? false,
                    IsBankPassbookAttachmentUpoaded = c.IsBankPassbookAttachmentUpoaded ?? false,
                    IsEducationAttachmentUploaded = c.IsEducationAttachmentUploaded ?? false,
                    IsEvaluationAttachmentUploaded = c.IsEvaluationAttachmentUploaded ?? false,
                    IsOfferLetterAttachmentUploaded = c.IsOfferLetterAttachmentUploaded ?? false,
                    IsInterviewVideoUploaded = c.IsInterviewVideoUploaded ?? false,
                    IsResumeUploaded = c.IsResumeUploaded ?? false
                })
                .FirstOrDefaultAsync();

            if (candidate == null)
            {
                return new Response
                {
                    StatusCode = HttpStatusCode.NotFound,
                    Status = false,
                    Message = $"Candidate with ID {candidateId} not found"
                };
            }

            return new Response
            {
                StatusCode = HttpStatusCode.OK,
                Status = true,
                Message = "Checklist retrieved successfully",
                Data = candidate
            };
        }

        public async Task<Response> UpdateInterviewerFeedBack(int scheduleId, string feedback, string StatusName, JwtLoginDetailDto loginDetail)
        {
            try
            {

                var response = new Response();

                if (loginDetail == null || string.IsNullOrEmpty(loginDetail.EmployeeId))
                {
                    response.Message = "Invalid employee details.";
                    response.Status = false;
                    return response;
                }


                string? roleName = await (from e in _context.tblEmployees
                                          join er in _context.tblEmployeeRoles on e.EmployeeId equals er.EmployeeId
                                          join r in _context.tblRoles on er.RoleId equals r.RoleId
                                          where e.EmployeeId.ToString() == loginDetail.EmployeeId
                                          select r.RoleName)
                              .FirstOrDefaultAsync();

                var connection = _context.Database.GetDbConnection();

                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                await using var command = connection.CreateCommand();
                command.CommandText = "dbo.sp_UpdateInterviewFeedback";
                command.CommandType = CommandType.StoredProcedure;
                command.CommandTimeout = 60;


                command.Parameters.Add(new SqlParameter("@InterviewId", SqlDbType.BigInt) { Value = loginDetail.EmployeeId });
                command.Parameters.Add(new SqlParameter("@ScheduleId", SqlDbType.Int) { Value = scheduleId });
                command.Parameters.Add(new SqlParameter("@Feedback", SqlDbType.NVarChar, -1) { Value = feedback ?? (object)DBNull.Value });
                command.Parameters.Add(new SqlParameter("@StatusName", SqlDbType.NVarChar, 100) { Value = StatusName ?? (object)DBNull.Value });
                command.Parameters.Add(new SqlParameter("@RoleName", SqlDbType.NVarChar, 100) { Value = roleName ?? (object)DBNull.Value });



                await using var reader = await command.ExecuteReaderAsync();


                return new Response
                {
                    Status = true,
                    Message = "FeedBack Updated Successfully",
                    StatusCode = HttpStatusCode.OK,
                    Data = null

                };



            }
            catch (Exception ex)
            {
                return new Response
                {
                    Status = false,
                    Message = ex.Message,
                    StatusCode = HttpStatusCode.InternalServerError,

                };
            }

        }

        public async Task<Response> GetApplicantListByStatus(
    JwtLoginDetailDto loginDetail,
    int pageNumber = 1,
    int pageSize = 10,
    int statusId = 0,
    string searchTerm = "")
        {
            try
            {
                searchTerm = searchTerm?.Trim().ToLower();
                var applicants = new List<ApplicantDetailDto>();
                int totalRecords = 0;
                int pendingCount = 0;

                var connection = _context.Database.GetDbConnection();

                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                int? RoleId = await (from e in _context.tblEmployees
                                     join er in _context.tblEmployeeRoles on e.EmployeeId equals er.EmployeeId
                                     join r in _context.tblRoles on er.RoleId equals r.RoleId
                                     where e.EmployeeId.ToString() == loginDetail.EmployeeId
                                     select r.RoleId)
                                     .FirstOrDefaultAsync();

                int? EmployeeId = loginDetail.EmployeeId != null
                    ? Convert.ToInt32(loginDetail.EmployeeId)
                    : (int?)null;

                await using var command = connection.CreateCommand();
                command.CommandText = "dbo.sp_GetApplicantListNew01";
                command.CommandType = CommandType.StoredProcedure;
                command.CommandTimeout = 60;

                command.Parameters.Add(new SqlParameter("@PageNumber", SqlDbType.Int) { Value = pageNumber });
                command.Parameters.Add(new SqlParameter("@PageSize", SqlDbType.Int) { Value = pageSize });
                command.Parameters.Add(new SqlParameter("@StatusId", SqlDbType.Int) { Value = statusId });
                command.Parameters.Add(new SqlParameter("@SearchTerm", SqlDbType.NVarChar, 200)
                {
                    Value = string.IsNullOrWhiteSpace(searchTerm) ? (object)DBNull.Value : searchTerm
                });
                command.Parameters.Add(new SqlParameter("@RoleId", SqlDbType.Int) { Value = (object?)RoleId ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@EmployeeId", SqlDbType.Int) { Value = (object?)EmployeeId ?? DBNull.Value });

                using (var reader = await command.ExecuteReaderAsync())
                {
                    // First result set: totals
                    if (await reader.ReadAsync())
                    {
                        totalRecords = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                        pendingCount = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                    }

                    // Second result set: applicants
                    if (await reader.NextResultAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var dto = new ApplicantDetailDto
                            {
                                ID = reader["ID"] != DBNull.Value ? Convert.ToInt32(reader["ID"]) : 0,
                                FirstName = reader["FirstName"]?.ToString(),
                                MiddleName = reader["MiddleName"]?.ToString(),
                                LastName = reader["LastName"]?.ToString(),
                                Phone = reader["Phone"]?.ToString(),
                                Email = reader["Email"]?.ToString(),
                                IsReopenAllowed = reader["IsReopenAllowed"] != DBNull.Value ? Convert.ToBoolean(reader["IsReopenAllowed"]) : (bool?)null,
                                Designation = reader["Designation"]?.ToString(),
                                DOB = reader["DOB"] != DBNull.Value ? (DateTime?)reader["DOB"] : null,
                                StatusId = reader["StatusId"] != DBNull.Value ? Convert.ToInt32(reader["StatusId"]) : 0,
                                DesignationName = reader["DesignationName"]?.ToString(),
                                LocationName = reader["LocationName"]?.ToString(),
                                PositionHeldInPreviousCompany = string.IsNullOrWhiteSpace(reader["PositionHeldInPreviousCompany"]?.ToString())
                                    ? null
                                    : reader["PositionHeldInPreviousCompany"].ToString(),
                                ApplicantCode = reader["ApplicantCode"]?.ToString(),
                                IsApplicant = reader["IsApplicant"] != DBNull.Value && Convert.ToBoolean(reader["IsApplicant"]),
                                ResumeLink = reader["ResumeLink"]?.ToString(),
                                OfferLetterLink = reader["OfferLetterLink"]?.ToString(),
                                InterviewRounds = reader["InterviewRounds"]?.ToString(),
                                Type = reader["Type"]?.ToString(),
                                CurrentRound = reader["CurrentRound"] != DBNull.Value ? Convert.ToInt32(reader["CurrentRound"]) : 0,
                                LastInterviewDateTime = reader["LastInterviewDateTime"]?.ToString(),
                                LastScheduleId = reader["LastScheduleId"] != DBNull.Value ? Convert.ToInt32(reader["LastScheduleId"]) : 0,
                                FinalResult = reader["FinalResult"]?.ToString(),
                                IsStatus = reader["IsStatus"] != DBNull.Value && Convert.ToBoolean(reader["IsStatus"]),

                                // From temp table
                                IsActive = reader["IsActive"] != DBNull.Value ? Convert.ToBoolean(reader["IsActive"]) : (bool?)null,
                                IsDeleted = reader["IsDeleted"] != DBNull.Value ? Convert.ToBoolean(reader["IsDeleted"]) : (bool?)null,
                                CreatedBy = reader["CreatedBy"]?.ToString(),
                                UpdatedBy = reader["UpdatedBy"]?.ToString(),
                                CreatedOn = reader["CreatedOn"] != DBNull.Value ? (DateTime?)reader["CreatedOn"] : null,
                                UpdatedOn = reader["UpdatedOn"] != DBNull.Value ? (DateTime?)reader["UpdatedOn"] : null,

                                // New columns from SP
                                DateOfApply = reader["DateOfApply"] != DBNull.Value
                                    ? (DateTime?)reader["DateOfApply"]
                                    : null,

                                WorkLocation = reader["WORK LOCATION"]?.ToString(),
                                ApplicantCodeNew = reader["APPLICANT CODE"]?.ToString(),
                                Company1 = reader["COMPANY 1"]?.ToString(),
                                Company2 = reader["COMPANY 2"]?.ToString(),
                                Company3 = reader["COMPANY 3"]?.ToString(),
                                InHandSalary = reader["In Hand Salary"]?.ToString(),
                                LastCTCAnnual = reader["LAST CTC(ANNUAL)"]?.ToString(),

                                // Experience fields from tblExperience
                                TotalIndustryExperienceYrs = reader["TotalIndustryExperience_yrs"] != DBNull.Value
                                    ? Convert.ToDecimal(reader["TotalIndustryExperience_yrs"])
                                    : (decimal?)null,
                                TotalRetailExperienceYrs = reader["TotalRetailExperience_yrs"] != DBNull.Value
                                    ? Convert.ToDecimal(reader["TotalRetailExperience_yrs"])
                                    : (decimal?)null,

                                // NEW: nullable location fields
                                CurrentLocation = reader["CurrentLocation"] != DBNull.Value
                                    ? reader["CurrentLocation"].ToString()
                                    : null,
                                PreferredLocation = reader["PreferredLocation"] != DBNull.Value
                                    ? reader["PreferredLocation"].ToString()
                                    : null,
                                StateId = reader["StateId"] != DBNull.Value
                                    ? Convert.ToInt32(reader["StateId"])
                                    : (int?)null,
                                StateName = reader["StateName"] != DBNull.Value
                                    ? reader["StateName"].ToString()
                                    : null,
                                NoticePeriod = reader["NoticePeriod"] != DBNull.Value
                                    ? Convert.ToInt32(reader["NoticePeriod"])
                                    : (int?)null
                            };

                            applicants.Add(dto);
                        }
                    }
                }

                if (applicants == null || totalRecords == 0)
                {
                    return new Response
                    {
                        StatusCode = HttpStatusCode.OK,
                        Status = false,
                        Message = "Data Not Found",
                        Data = new { }
                    };
                }

                return new Response
                {
                    Status = true,
                    Message = "Data Fetched Successfully",
                    StatusCode = HttpStatusCode.OK,
                    Data = new
                    {
                        TotalRecords = totalRecords,
                        PendingCount = pendingCount,
                        Candidates = applicants
                    }
                };
            }
            catch (Exception ex)
            {
                return new Response
                {
                    Status = false,
                    Message = ex.Message,
                    StatusCode = HttpStatusCode.InternalServerError,
                    Data = ex.Data
                };
            }
        }



        #region updatecandidatedata

        public async Task<Response> UpdateData(CandidateUpdate candidateUpdate, CandidateDocs files, string updatedBy, JwtLoginDetailDto loginDetail)
        {
            using var trans = await _context.Database.BeginTransactionAsync();
            try
            {
                var rreportHeadEcode = _context.tblEmployees
                                  .Where(a => a.EmployeeId == candidateUpdate.reportingHeadId)
                                  .Select(r => r.Ecode)
                                  .FirstOrDefault() ?? "";
                long location = Convert.ToInt64(candidateUpdate.location);
                long department = Convert.ToInt64(candidateUpdate.department);
                long designation = Convert.ToInt64(candidateUpdate.designation);
                decimal salary = Convert.ToDecimal(candidateUpdate.grossSalary);
                // Input validation
                if (candidateUpdate == null || string.IsNullOrEmpty(updatedBy) || loginDetail == null)
                {

                    return new Response
                    {
                        Status = false,
                        StatusCode = System.Net.HttpStatusCode.BadRequest,
                        Message = "Invalid input parameters"
                    };
                }

                HRMSAPI.Data.Candidate data;
                bool isNewEntry = candidateUpdate.id <= 0;

                if (isNewEntry)
                {

                    //if (location < 1 || department < 1 || designation < 1 || salary < Convert.ToDecimal(1.00)) {
                    //    return new Response
                    //    {
                    //        Status = false,
                    //        Message = "Either Location,Department or Designation mapping is not correct, or Salary is not particularly defined in candidate data..."
                    //    };
                    //}
                    //                var isAllowed = await _context.Database
                    //.SqlQueryRaw<bool>(
                    //    "SELECT CAST(dbo.fn_IsVacancyShorter({0}, {1}, {2}, {3}) AS BIT) AS Value",
                    //    location, department, designation, salary)
                    //.FirstOrDefaultAsync();

                    //                if (!isAllowed)
                    //                {
                    //                    return new Response
                    //                    {
                    //                        Status = false,
                    //                        Message = "No vacancy available or Salary Gross exceeds budgeted"
                    //                    };
                    //                }


                    //if(result)
                    // Generate new ApplicantId

                    //By Gautam

        //            var canCreate = await _context.Database.SqlQueryRaw<int>(
        //@"SELECT [Value]FROM (SELECT CAST(dbo.fn_CanCreateCandidate({0},{1},{2}) AS INT) AS [Value]) s",
        //                    candidateUpdate.location,
        //                    candidateUpdate.department,
        //                    candidateUpdate.designation
        //                    ).FirstOrDefaultAsync();

        //            if (canCreate == 0)
        //            {
        //                await trans.RollbackAsync();
        //                return new Response
        //                {
        //                    Status = false,
        //                    StatusCode = System.Net.HttpStatusCode.BadRequest,
        //                    Message = "No vacant seat available for the selected Location, Department and Designation."
        //                };
        //            }

                    var lastApplicant = await _context.Candidates.AsNoTracking().AsQueryable()
                        .Where(row => row.IsActive == true && row.IsDeleted == false)
                        .OrderByDescending(c => c.Id)
                        .Select(c => c.ApplicantId)
                        .FirstOrDefaultAsync();

                    int lastNumber = lastApplicant != null ? int.Parse(lastApplicant.Substring(2)) : 0;
                    string newApplicantId = $"AV{(lastNumber + 1).ToString("D6")}";

                    // Create new candidate/applicant with ApplicantId
                    data = new HRMSAPI.Data.Candidate
                    {
                        ApplicantId = newApplicantId,
                        CreatedBy = updatedBy,
                        CreatedOn = DateTime.UtcNow,
                        StatusId = 4
                    };
                    // Check if Aadhaar number is provided and validate if it already exists
                    if (!string.IsNullOrWhiteSpace(candidateUpdate.aadharNo) && candidateUpdate.aadharNo != data.AADHAR_NO)
                    {
                        var existingAadhaar = await _context.Candidates
                            .AnyAsync(c => c.AADHAR_NO == candidateUpdate.aadharNo && c.IsActive == true && c.IsDeleted == false);

                        if (existingAadhaar)
                        {
                            throw new InvalidOperationException("Aadhaar number already exists for another candidate.");
                        }
                    }
                    _context.Candidates.Add(data);
                }
                else
                {
                    // Update existing candidate
                    data = await _context.Candidates
                        .FirstOrDefaultAsync(row => row.Id == candidateUpdate.id)
                        ?? throw new KeyNotFoundException($"No Candidate Found for Id: {candidateUpdate.id}");

                    // ===============================
                    // SEAT CHECK FOR UPDATE (ONLY IF COMBO CHANGES)
                    // ===============================

              //      string oldLocation = data.LOCATION;
              //      string oldDepartment = data.DEPARTMENT;
              //      string oldDesignation = data.DESIGNATION;

              //      string newLocation = candidateUpdate.location;
              //      string newDepartment = candidateUpdate.department;
              //      string newDesignation = candidateUpdate.designation;

              //      bool isSeatChange =
              //             !string.Equals(oldLocation, newLocation, StringComparison.OrdinalIgnoreCase)
              //          || !string.Equals(oldDepartment, newDepartment, StringComparison.OrdinalIgnoreCase)
              //          || !string.Equals(oldDesignation, newDesignation, StringComparison.OrdinalIgnoreCase);

              //      if (isSeatChange)
              //      {
              //          var canCreate = await _context.Database.SqlQueryRaw<int>(
              //              @"SELECT [Value]
              //FROM (SELECT CAST(dbo.fn_CanCreateCandidate({0},{1},{2}) AS INT) AS [Value]) s",
              //              newLocation,
              //              newDepartment,
              //              newDesignation
              //          ).FirstOrDefaultAsync();

              //          if (canCreate == 0)
              //          {
              //              await trans.RollbackAsync();
              //              return new Response
              //              {
              //                  Status = false,
              //                  StatusCode = HttpStatusCode.BadRequest,
              //                  Message = "No vacant seat available for the selected Location, Department and Designation."
              //              };
              //          }
              //      }
                
            }

                // Check document uploads with null-safe operations matching CandidateDocs
                bool isPassportUploaded = files.PassportPhoto?.Length > 0;
                bool isLast3Slips = files.Last3SalarySlip?.Any(f => f.Length > 0) == true;
                bool isBankStatement = files.Last3BankStatement?.Length > 0;
                bool isPrevOfferLetter = files.PrevOfferLetter?.Length > 0;
                bool isPanAttachment = files.PanAttachment?.Any(f => f.Length > 0) == true;
                bool isAadharAttachment = files.AadharAttachment?.Any(f => f.Length > 0) == true;
                bool isAadharBackAttachment = files.AadharBackAttachment?.Any(f => f.Length > 0) == true;
                bool isBankPassbook = files.BankPassbookAttachment?.Any(f => f.Length > 0) == true;
                bool isEducationAttachment = files.EducationAttachment?.Any(f => f.Length > 0) == true;
                bool isResumeAttachment = files.ResumeAttachment?.Any(f => f.Length >= 0) == true;
                bool isEvaluationAttachment = files.EvaluationAttachment?.Any(f => f.Length > 0) == true;
                bool isOfferLetterAttachment = files.OfferLetterAttachment?.Any(f => f.Length > 0) == true;
                bool isInterviewVideo = files.InterviewVideo?.Any(f => f.Length > 0) == true;
                bool isOtherAttachment = files.OtherAttachment?.Any(f => f.Length > 0) == true;

                bool isForm11Attachment = files.Form11Attachment?.Any(f => f.Length > 0) == true;
                bool isForm2Attachment = files.Form2Attachment?.Any(f => f.Length > 0) == true;
                bool isGratuityAttachment = files.GratuityFormAttachment?.Any(f => f.Length > 0) == true;

                // Update basic details
                data.TITLE = candidateUpdate.title ?? data.TITLE;
                //Update Applicant Fields

                if (candidateUpdate.IsApplicant == true)
                {
                    UpdateApplicantField(data, candidateUpdate);
                }
                // Update name fields and other fields
                UpdateNameFields(data, candidateUpdate);
                UpdateCandidateFields(data, candidateUpdate);

                // Update document flags
                UpdateDocumentFlags(data, isPassportUploaded, isLast3Slips, isBankStatement,
                    isPrevOfferLetter, isPanAttachment, isAadharAttachment,
                    isBankPassbook, isEducationAttachment, isResumeAttachment, isEvaluationAttachment, isOfferLetterAttachment, isInterviewVideo, isOtherAttachment, isAadharBackAttachment);

                // Update audit fields for existing records
                if (!isNewEntry)
                {
                    data.StatusId = 4;
                    data.UpdatedBy = updatedBy;
                    data.UpdatedOn = DateTime.UtcNow;
                }

                // Save main candidate to get ID for new entries
                await _context.SaveChangesAsync();

                // If new candidate, add entry in tblNewCandidateApprovals
                if (isNewEntry)
                {
                    var newCandidateApproval = new tblNewCandidateApproval
                    {
                        CandidateId = data.Id,
                        HRApprovalStatus = 4,
                        AuditApprovalStatus = 4,
                        ClusterManagerApprovalStatus = 4,
                    };

                    await _context.tblNewCandidateApprovals.AddAsync(newCandidateApproval);
                    await _context.SaveChangesAsync();
                }
                // Update AssignLocationHistory
                if (candidateUpdate.assignLocations != null && candidateUpdate.assignLocations.Any())
                {
                    // Deactivate existing active assignments
                    var existingHistory = await _context.AssignLocationHistories
                        .Where(alh => alh.CandidateId == data.Id && alh.IsActive == true)
                        .ToListAsync();
                    foreach (var history in existingHistory)
                    {
                        history.IsActive = false;
                        history.ReleasedOnDate = DateTime.UtcNow;
                    }

                    // Add new assignment (assuming only one new assignment per update)
                    var newAssignment = candidateUpdate.assignLocations.First();
                    var newLocationHistory = new AssignLocationHistory
                    {
                        CandidateId = data.Id,
                        AssignedLocation = newAssignment.assignedLocation,
                        AssignedReason = newAssignment.assignedReason,
                        IsActive = true,
                        AssignedOnDate = newAssignment.assignedOnDate,
                        ReleasedOnDate = newAssignment.releasedOnDate,
                        CreatedBy = updatedBy,
                        UpdatedBy = updatedBy,
                    };
                    await _context.AssignLocationHistories.AddAsync(newLocationHistory);
                }


                // Update related entities
                await UpdateRelatedEntities(data.Id, candidateUpdate, isNewEntry);

                // Handle new attachments
                await SaveNewAttachments(data.Id, data.EMAIL_ADDRESS, files, updatedBy);

                // Final save and commit transaction
                await trans.CommitAsync();
                await _context.SaveChangesAsync();
                return new Response
                {
                    Status = true,
                    StatusCode = System.Net.HttpStatusCode.OK,
                    Message = isNewEntry ? "Created Successfully" : "Updated Successfully",
                    Data = new { CandidateId = data.Id, ApplicantId = data.ApplicantId }
                };
            }
            catch (KeyNotFoundException ex)
            {
                await trans.RollbackAsync();
                return new Response
                {
                    Status = false,
                    StatusCode = System.Net.HttpStatusCode.NotFound,
                    Message = ex.Message
                };
            }
            catch (Exception ex)
            {
                await trans.RollbackAsync();
                return new Response
                {
                    Status = false,
                    StatusCode = System.Net.HttpStatusCode.InternalServerError,
                    Message = $"Update failed: {ex.Message}"
                };
            }
        }

        private void UpdateNameFields(HRMSAPI.Data.Candidate data, CandidateUpdate update)
        {
            // Priority 1: If firstName or lastName is provided, use individual fields
            if (!string.IsNullOrWhiteSpace(update.firstName) || !string.IsNullOrWhiteSpace(update.lastName))
            {
                data.FIRST_NAME = update.firstName ?? data.FIRST_NAME;
                data.MIDDLE_NAME = update.middleName ?? "";
                data.LAST_NAME = update.lastName ?? data.LAST_NAME;

            }
            // Priority 2: If no individual names provided but fullName exists, split it
            else if (string.IsNullOrWhiteSpace(data.FIRST_NAME) &&
                     string.IsNullOrWhiteSpace(data.LAST_NAME) &&
                     !string.IsNullOrWhiteSpace(update.fullName))
            {
                var nameParts = update.fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                data.FIRST_NAME = nameParts.FirstOrDefault() ?? "";
                data.LAST_NAME = nameParts.Length > 1 ? nameParts.Last() : "";
                data.MIDDLE_NAME = nameParts.Length > 2 ? string.Join(" ", nameParts.Skip(1).Take(nameParts.Length - 2)) : "";
            }
            // If nothing new is provided, keep existing values
            else
            {
                data.FIRST_NAME = data.FIRST_NAME;
                data.MIDDLE_NAME = data.MIDDLE_NAME;
                data.LAST_NAME = data.LAST_NAME;
            }
        }


        private void UpdateCandidateFields(HRMSAPI.Data.Candidate data, CandidateUpdate update)
        {
            var reportHeadEcode = _context.tblEmployees
                                    .Where(a => a.EmployeeId == update.reportingHeadId)
                                    .Select(r => r.Ecode)
                                    .FirstOrDefault() ?? "";
            reportHeadEcode = _context.tblEmployees
                                    .Where(a => a.EmployeeId == update.reportingHeadId)
                                    .Select(r => r.Ecode)
                                    .FirstOrDefault() ?? "";
            //new work 28 may
            data.CompanyId = update.companyId ?? data.CompanyId;
            //
            data.EMP_CODE = update.empCode ?? data.EMP_CODE ?? "NA";
            data.FATHER_NAME = update.fathersName ?? data.FATHER_NAME;
            data.MOTHER_NAME = update.mothersName ?? data.MOTHER_NAME;
            data.DESIGNATION = update.designation ?? data.DESIGNATION;
            data.DEPARTMENT = update.department ?? data.DEPARTMENT;
            data.JOINING_DATE = update.joiningDate ?? data.JOINING_DATE;
            data.DOB = update.dob ?? data.DOB;
            data.LOCATION = update.location ?? data.LOCATION;
            if (decimal.TryParse(update.grossSalary, out var grossSalary))
            {
                data.GROSS_SALARY = grossSalary;
            }
            else
            {
                data.GROSS_SALARY = data.GROSS_SALARY;
            }
            data.GENDER = update.gender ?? data.GENDER;
            data.PAN_NO = update.panNo ?? data.PAN_NO;
            data.AADHAR_NO = update.aadharNo ?? data.AADHAR_NO;
            data.NAME_ON_AADHAR = update.nameOnAadhar ?? data.NAME_ON_AADHAR;
            data.PRESENT_ADDRESS = update.presentAddress ?? data.PRESENT_ADDRESS;
            data.PERMANENT_ADDRESS = update.permanentAddress ?? data.PERMANENT_ADDRESS;
            data._PERMANENT_ADDRESS_PIN_CODE = update.permanentAddressPinCode ?? data._PERMANENT_ADDRESS_PIN_CODE;
            data.PRESENT_ADDRESS_PIN_CODE = update.presentAddressPinCode ?? data.PRESENT_ADDRESS_PIN_CODE;
            // Fixed to match new code
            data.MARITIAL_STATUS = update.maritalStatus ?? data.MARITIAL_STATUS;
            data.MOBILE = update.mobile ?? data.MOBILE;
            data.EMAIL_ADDRESS = update.emailAddress ?? data.EMAIL_ADDRESS;
            data.NATIONALITY = update.nationality ?? data.NATIONALITY;
            data.RELIGION = update.religion ?? data.RELIGION;
            data.BANK_NAME = update.bankName ?? data.BANK_NAME;
            data.A_C_NO = update.accountNo ?? data.A_C_NO;
            data.BANK_IFSC_CODE = update.bankIfscCode ?? data.BANK_IFSC_CODE;
            data.PLACE_OF_BIRTH = update.placeOfBirth ?? data.PLACE_OF_BIRTH;
            data.HUSBAND_NAME = update.husbandName ?? data.HUSBAND_NAME;
            data.UAN_NO = update.uanNo ?? data.UAN_NO;
            data.BENEFICIARY_ADDRESS = update.beneficiaryAddress ?? update.beneficaryAddress ?? data.BENEFICIARY_ADDRESS;
            data.PREV__EST_NO_ = update.previousEstno ?? data.PREV__EST_NO_;
            data.REFERENCE = update.reference ?? data.REFERENCE;
            data.ISRELATIVEINCOMPANY = update.isRelativeInCompany ?? data.ISRELATIVEINCOMPANY;
            data.PreferredLocation = update.PreferredLocation ?? data.PreferredLocation;

            // Added missing contact fields
            data.CONTACT1_OF_LAST_3_COMPANY = update.contact1LastCompany ?? data.CONTACT1_OF_LAST_3_COMPANY;
            data.CONTACT2_OF_LAST_3_COMPANY = update.contact2LastCompany ?? data.CONTACT2_OF_LAST_3_COMPANY;
            data.CONTACT3_OF_LAST_3_COMPANY = update.contact3LastCompany ?? data.CONTACT3_OF_LAST_3_COMPANY;
            data.CONTACT4_OF_LAST_3_COMPANY = update.contact4LastCompany ?? data.CONTACT4_OF_LAST_3_COMPANY;
            data.CONTACT5_OF_LAST_3_COMPANY = update.contact5LastCompany ?? data.CONTACT5_OF_LAST_3_COMPANY;

            // Added missing reference fields
            data.REFERENCE1__OF_LAST_3_COMPANY = update.reference1LastCompany ?? data.REFERENCE1__OF_LAST_3_COMPANY;
            data.REFERENCE2__OF_LAST_3_COMPANY = update.reference2LastCompany ?? data.REFERENCE2__OF_LAST_3_COMPANY;
            data.REFERENCE3__OF_LAST_3_COMPANY = update.reference3LastCompany ?? data.REFERENCE3__OF_LAST_3_COMPANY;
            data.REFERENCE4__OF_LAST_3_COMPANY = update.reference4LastCompany ?? data.REFERENCE4__OF_LAST_3_COMPANY;
            data.REFERENCE5__OF_LAST_3_COMPANY = update.reference5LastCompany ?? data.REFERENCE5__OF_LAST_3_COMPANY;
            data.WEEKLY_OFF = update.weeklyOff ?? data.WEEKLY_OFF;
            data.IsActive = true;
            //new feilds added on 30 apr 2025
            data.BasicSalary = update.BasicSalary;
            data.HRA = update.HRA;
            data.SpecialAllowance = update.SpecialAllowance;
            data.DA = update.DA;
            data.ExtraAllowance = update.ExtraAllowance;
            data.CCA = update.CCA;
            data.monthlyGrossCTC = update.monthlyGrossCTC;
            data.annuallyNetCTC = update.annuallyNetCTC;
            data.PFApplicable = update.PFApplicable;
            data.BonusApplicable = update.bonusApplicable;
            data.ESICApplicable = update.ESICApplicable;
            data.PREV__EST_NO_ = update.prevEstNo;
            data.ReportHeadEcode = _context.tblEmployees
                                    .Where(a => a.EmployeeId == update.reportingHeadId)
                                    .Select(r => r.Ecode)
                                    .FirstOrDefault() ?? "";
            data.DifferentlyAbled = update.differentlyAbled ?? data.DifferentlyAbled;
            data.DifferentlyAbledReason = update.differentlyAbledReason ?? data.DifferentlyAbledReason;
            data.DifferentlyAbledRemarks = update.differentlyAbledRemarks ?? data.DifferentlyAbledRemarks;
            data.SkillType = update.skillType ?? data.SkillType;
            data.ShiftID = update.ShiftID ?? data.ShiftID;
            data.Source = update.Source ?? data.Source;
            data.ReferenceEmployee = update.ReferenceEmployee ?? data.ReferenceEmployee;
            data.IsUANRegistered = update.IsUANRegistered;
            data.AOCode = update.AoCode ?? data.AOCode;
            data.StateId = update.StateId ?? data.StateId;
            data.NoticePeriod = update.NoticePeriod ?? data.NoticePeriod;
        }
        // for applicant
        private void UpdateApplicantField(HRMSAPI.Data.Candidate data, CandidateUpdate update)
        {
            data.TotalExperience = update.TotalExperience ?? data.TotalExperience ?? 0;
            data.SalaryExpectation = update.SalaryExpectation ?? data.SalaryExpectation ?? 0;
            data.AdditionalInfoApplicant = update.AdditionalInfoApplicant ?? data.AdditionalInfoApplicant ?? "NA";
            data.Agreement = update.Aggreement ?? data.Agreement;
            data.IsApplicant = update.IsApplicant ?? data.IsApplicant;
            data.ReferenceEmployee = update.ReferenceEmployee ?? data.ReferenceEmployee;
            data.Source = update.Source ?? data.Source;
            data.CurrentLocation = update.CurrentLocation ?? data.CurrentLocation;
            data.PreferredLocation = update.PreferredLocation ?? data.PreferredLocation;

        }
        private void UpdateDocumentFlags(HRMSAPI.Data.Candidate data, bool isPassportUploaded, bool isLast3Slips,
     bool isBankStatement, bool isPrevOfferLetter, bool isPanAttachment,
     bool isAadharAttachment, bool isBankPassbook, bool isEducationAttachment, bool isResumeUploaded,
     bool isEvaluationAttachment, bool isOfferLetterAttachment, bool isInterviewVideo, bool isOtherAttachment, bool isAadharBackAttachment)
        {
            data.IsPassportPhotoUploaded = isPassportUploaded ? true : data.IsPassportPhotoUploaded;
            data.IsSalarySlipUploaded = isLast3Slips ? true : data.IsSalarySlipUploaded;
            data.IsBankStatementUploaded = isBankStatement ? true : data.IsBankStatementUploaded;
            data.IsPrevOfferLetterUploaded = isPrevOfferLetter ? true : data.IsPrevOfferLetterUploaded;
            data.IsPanAttachmentUploaded = isPanAttachment ? true : data.IsPanAttachmentUploaded;
            data.IsAadharAttachmentUploaded = isAadharAttachment ? true : data.IsAadharAttachmentUploaded;
            data.IsAadharBackAttachmentUploaded = isAadharBackAttachment ? true : data.IsAadharBackAttachmentUploaded;
            data.IsBankPassbookAttachmentUpoaded = isBankPassbook ? true : data.IsBankPassbookAttachmentUpoaded;
            data.IsEducationAttachmentUploaded = isEducationAttachment ? true : data.IsEducationAttachmentUploaded;
            data.IsResumeUploaded = isResumeUploaded ? true : data.IsResumeUploaded;
            data.IsEvaluationAttachmentUploaded = isEvaluationAttachment ? true : data.IsEvaluationAttachmentUploaded;
            data.IsOfferLetterAttachmentUploaded = isOfferLetterAttachment ? true : data.IsOfferLetterAttachmentUploaded;
            data.IsInterviewVideoUploaded = isInterviewVideo ? true : data.IsInterviewVideoUploaded;
            data.IsOtherAttachmentUploaded = isOtherAttachment ? true : data.IsOtherAttachmentUploaded;
        }

        private async Task UpdateRelatedEntities(long candidateId, CandidateUpdate update, bool isNewEntry = false)
        {
            // Family Members
            if (update.familyMembersList?.Any() == true)
            {
                if (!isNewEntry) // Only check for existing records if it's an update
                {
                    var existingFamily = await _context.tblFamilies.Where(f => f.CID == candidateId).ToListAsync();
                    if (existingFamily.Any())
                    {
                        _context.tblFamilies.RemoveRange(existingFamily);
                    }
                }
                await _context.tblFamilies.AddRangeAsync(update.familyMembersList.Select(family => new tblFamily
                {
                    CID = candidateId,
                    Family_Member_Name = family.familyMemberName,
                    DOB = family.dob,
                    Relation = family.relation,
                }));
            }

            // Experience
            if (update.experienceList?.Any() == true)
            {
                if (!isNewEntry) // Only check for existing records if it's an update
                {
                    var existingExperience = await _context.tblExperiences.Where(e => e.CID == candidateId).ToListAsync();
                    if (existingExperience.Any())
                    {
                        _context.tblExperiences.RemoveRange(existingExperience);
                    }
                }
                await _context.tblExperiences.AddRangeAsync(update.experienceList.Select(exp => new tblExperience
                {
                    CID = candidateId,
                    Name_of_Company = exp.nameOfCompany,
                    Work_Location = exp.workLocation,
                    Position_Held = exp.positionHeld,
                    From = exp.from,
                    To = exp.to,
                    InHand = Convert.ToInt64(exp.inHand ?? 0),
                    Last_CTC = Convert.ToInt64(exp.lastCtc ?? 0)
                }));
            }

            // Qualifications
            if (update.qualificationList?.Any() == true)
            {
                if (!isNewEntry) // Only check for existing records if it's an update
                {
                    var existingEducation = await _context.tblQualifications.Where(q => q.CID == candidateId).ToListAsync();
                    if (existingEducation.Any())
                    {
                        _context.tblQualifications.RemoveRange(existingEducation);
                    }
                }
                await _context.tblQualifications.AddRangeAsync(update.qualificationList.Select(edu => new tblQualification
                {
                    CID = candidateId,
                    Education = edu.education,
                    YOP = edu.yop,
                    Grade = edu.grade,
                    Type = edu.type
                }));
            }
            await _context.SaveChangesAsync();
        }

        private async Task SaveNewAttachments(long candidateId, string email, CandidateDocs files, string updatedBy)
        {
            async Task SaveFileIfExists(IFormFile? file, string folder, string docType, int index = 0)
            {
                if (file?.Length > 0)
                {
                    var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

                    // Broad allowed list matching DocumentValidationHelper and user requirements
                    var commonAllowed = new[]
                    {
                        ".pdf", ".doc", ".docx", ".xls", ".xlsx",
                        ".png", ".jpg", ".jpeg", ".gif", ".bmp",
                        ".txt", ".rtf", ".mp4"
                    };

                    var allowedExtensions = docType switch
                    {
                        // For video types, allow common video formats in addition to the common list
                        "InterviewVideo" or "BankStatementVideo" => commonAllowed.Concat(new[] { ".mov", ".avi" }).ToArray(),
                        // For all other doc types, use the common allowed list
                        _ => commonAllowed
                    };

                    if (!allowedExtensions.Contains(extension))
                        throw new Exception($"Invalid file type for {docType}{(index > 0 ? $" - {index}" : "")}. Allowed: {string.Join(", ", allowedExtensions)}");
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
            await SaveFileListIfExists(files.PanAttachment, "Pan", "Pan");
            await SaveFileListIfExists(files.AadharAttachment, "Aadhar", "Aadhar");
            await SaveFileListIfExists(files.AadharBackAttachment, "AadharBack", "AadharBack");
            await SaveFileListIfExists(files.BankPassbookAttachment, "BankPassbook", "BankPassbook");
            await SaveFileListIfExists(files.EducationAttachment, "Education", "Education");
            await SaveFileListIfExists(files.ResumeAttachment, "Resume", "Resume");
            await SaveFileListIfExists(files.EvaluationAttachment, "Evaluations", "Evaluation");
            await SaveFileListIfExists(files.OfferLetterAttachment, "OfferLetter", "OfferLetter");
            await SaveFileListIfExists(files.InterviewVideo, "InterviewVideos", "InterviewVideo");
            await SaveFileListIfExists(files.OtherAttachment, "OtherAttachment", "Other");
            await SaveFileListIfExists(files.BankStatementVideo, "BankStatementVideo", "BankStatementVideo");

            await SaveFileListIfExists(files.Form11Attachment, "Form11", "Form11");
            await SaveFileListIfExists(files.Form2Attachment, "Form2", "Form2");
            await SaveFileListIfExists(files.Form11Attachment, "GratuityForm", "GratuityForm");
        }
        #endregion


        public async Task<DeleteCandidateDocResult> DeleteCandidateDocAsync(DeleteCandidateDocRequest req, CancellationToken ct = default)
        {
            if (req == null) throw new ArgumentNullException(nameof(req));
            if (string.IsNullOrWhiteSpace(req.DeletedBy)) throw new ArgumentException("DeletedBy is required.", nameof(req.DeletedBy));
            if (req.Id == null && req.CId == null) throw new ArgumentException("Either Id or CId must be provided.");

            await using var conn = _context.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open) await conn.OpenAsync(ct);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "dbo.usp_DeleteCandidateDoc";
            cmd.CommandType = CommandType.StoredProcedure;

            SqlParameter P(string name, SqlDbType type, object? val, int? size = null)
            {
                var p = new SqlParameter(name, type) { Value = val ?? DBNull.Value };
                if (size.HasValue) p.Size = size.Value;
                return p;
            }

            cmd.Parameters.Add(P("@Id", SqlDbType.BigInt, req.Id));
            cmd.Parameters.Add(P("@CId", SqlDbType.BigInt, req.CId));
            cmd.Parameters.Add(P("@DeletedBy", SqlDbType.VarChar, req.DeletedBy, 30));
            cmd.Parameters.Add(P("@IsHardDelete", SqlDbType.Bit, false));

            var result = new DeleteCandidateDocResult();

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                int Ord(string col) => reader.GetOrdinal(col);
                result.RowsAffected = reader.GetInt32(Ord("RowsAffected"));
                result.HardDelete = reader.GetBoolean(Ord("HardDelete"));
            }

            return result;
        }
        public async Task<bool> ReopenCandidateAsync(ReopenCandidateDto dto, JwtLoginDetailDto loginDetail)
        {
            try
            {
                var candidate = await _context.Candidates
                .FirstOrDefaultAsync(x => x.Id == dto.CandidateId && x.IsDeleted == false);

                if (candidate == null)
                    throw new Exception("Candidate not found");

                if (candidate.StatusId != 2)
                    throw new Exception("Only rejected candidates can be reopened");

                string? ename = await _context.tblEmployees
                    .Where(e => e.EmployeeId.ToString() == loginDetail.EmployeeId)
                    .Select(e => e.FULL_NAME).FirstOrDefaultAsync();

                candidate.StatusId = 4;
                candidate.UpdatedOn = DateTime.Now;

                var history = new CandidateStatus_History
                {
                    ApplicantId = dto.CandidateId,
                    NewStatusId = 4,
                    NewStatusName = "Pending",
                    OldStatusId = 2,
                    OldStatusName = "Rejected",
                    CreatedDate = DateTime.Now,
                    CreatedBy = ename
                };

                _context.CandidateStatus_Histories.Add(history);

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception("Some error occured");
            }

        }
        public async Task<List<InterviewScheduleDto>> GetInterviewsByInterviewerAsync(long interviewerId)
        {
            var result = new List<InterviewScheduleDto>();

            var connection = _context.Database.GetDbConnection();
            await using (connection)
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                await using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                SELECT 
                    s.ApplicantId,
                    s.CandidateName,
                    s.InterviewDateTime,
                    r.RoundId,
                    r.Status
                FROM tblInterviewRounds r WITH (NOLOCK)
                INNER JOIN tblScheduleInterview s WITH (NOLOCK)
                    ON r.ScheduleId = s.ScheduleId
                WHERE r.InterviewerId = @InterviewerId";

                    var param = command.CreateParameter();
                    param.ParameterName = "@InterviewerId";
                    param.Value = interviewerId;
                    command.Parameters.Add(param);

                    await using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            result.Add(new InterviewScheduleDto
                            {
                                ApplicantId = reader["ApplicantId"] != DBNull.Value
                                    ? Convert.ToInt32(reader["ApplicantId"])
                                    : 0,

                                CandidateName = reader["CandidateName"]?.ToString(),

                                InterviewDateTime = reader["InterviewDateTime"] != DBNull.Value
                                    ? Convert.ToDateTime(reader["InterviewDateTime"])
                                    : DateTime.MinValue,

                                RoundId = reader["RoundId"] != DBNull.Value
                                    ? Convert.ToInt32(reader["RoundId"])
                                    : 0,

                                Status = reader["Status"]?.ToString()
                            });
                        }
                    }
                }
            }

            return result;
        }
        public async Task<List<InterviewAssignedDto>> GetApplicantAssignDetails()
        {
            var data = await _context.GetProcedures().SP_InterviewAssignedAsync();

            if (data == null || !data.Any())
                return new List<InterviewAssignedDto>();

            return data.Select(x => new InterviewAssignedDto
            {
                AppliedDate = x.AppliedDate,
                CurrentLocation = x.CurrentLocation,
                PreferredLocation = x.PreferredLocation,
                PreferredState = x.PreferredState,
                StoreCode = x.StoreCode,
                Name = x.Name,
                Email = x.Email,
                Designation = x.Designation,
                Mobile = x.MOBILE,
                Experience = x.Experience.ToString(),
                CurrentCompany = x.CurrentCompany,
                CurrentSalary = x.CurrentSalary,
                InterviewMode = x.InterviewMode,
                InterviewDate = x.InterviewDate,
                IsResumeUploaded = x.IsResumeUploaded,
                AssignBy = x.AssignBy,
                AssignTo = x.AssignTo,
                RoundId = x.RoundId,
                CreatedOn = x.CreatedOn,
                UpdatedBy = x.UpdatedBy,
                UpdatedOn = x.UpdatedOn
            }).ToList();
        }
        public async Task<List<ApplicantFeedbackDto>> GetApplicantFeedBack()
        {
            var data = await _context.GetProcedures().SP_GetApplicantFeedBackAsync();

            if (data == null || !data.Any())
                return new List<ApplicantFeedbackDto>();

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            return data.Select(x =>
            {
                FeedbackDetailDto? feedback = null;

                if (!string.IsNullOrWhiteSpace(x.FeedBack))
                {
                    try
                    {
                        feedback = System.Text.Json.JsonSerializer.Deserialize<FeedbackDetailDto>(
                            x.FeedBack, jsonOptions);
                    }
                    catch
                    {
                        feedback = null; // invalid JSON fallback
                    }
                }

                return new ApplicantFeedbackDto
                {
                    CandidateName = x.CandidateName,
                    Email = x.EMAILADDRESS,
                    Designation = x.Designation,
                    Mobile = x.MOBILE,
                    RoundId = x.RoundId,
                    Interviewer = x.Interviewer,
                    Feedback = feedback,
                    Status = x.Status,
                    InterviewerStatus = x.InterviewerStatus,
                    CreatedBy = x.CreatedBy,
                    CreatedOn = x.CreatedOn
                };
            }).ToList();
        }
        public async Task<Response> CreateBackgroundProcessAsync(InterviewBackgroundProcessDto dto, JwtLoginDetailDto loginDetail, CancellationToken ct = default)
        {
            await using var conn = _context.Database.GetDbConnection();

            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync(ct);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "dbo.SP_InterviewBackgroundProcess";
            cmd.CommandType = CommandType.StoredProcedure;

            var applicantParam = cmd.CreateParameter();
            applicantParam.ParameterName = "@CandidateId";
            applicantParam.Value = dto.CandidateId;

            cmd.Parameters.Add(applicantParam);

            var statusParam = cmd.CreateParameter();
            statusParam.ParameterName = "@Status";
            statusParam.Value = string.IsNullOrEmpty(dto.Status) ? DBNull.Value : dto.Status;

            cmd.Parameters.Add(statusParam);

            var createdByParam = cmd.CreateParameter();
            createdByParam.ParameterName = "@CreatedBy";
            createdByParam.Value = loginDetail.EmployeeId;

            cmd.Parameters.Add(createdByParam);

            var remarksParam = cmd.CreateParameter();
            remarksParam.ParameterName = "@Remarks";
            remarksParam.Value = string.IsNullOrEmpty(dto.Remarks) ? DBNull.Value : dto.Remarks;
            cmd.Parameters.Add(remarksParam);

            var lastUpdatedParam = cmd.CreateParameter();
            lastUpdatedParam.ParameterName = "@LastUpdated";
            lastUpdatedParam.Value = loginDetail.EmployeeId;
            cmd.Parameters.Add(lastUpdatedParam);

            await using var reader = await cmd.ExecuteReaderAsync(ct);

            if (await reader.ReadAsync(ct))
            {
                return new Response
                {
                    Status = Convert.ToBoolean(reader["Success"]),
                    Message = reader["Message"]?.ToString()
                };
            }

            return new Response
            {
                Status = false,
                Message = "Unexpected error occurred"
            };
        }
        public async Task<List<InterviewBackgroundProcessResponseDto>> GetInterviewBackgroundProcess()
        {
            var data = await _context.GetProcedures().SP_GetInterviewBackgroundProcessAsync();

            if (data == null || !data.Any())
                return new List<InterviewBackgroundProcessResponseDto>();

            return data.Select(x => new InterviewBackgroundProcessResponseDto
            {
                FirstName = x.FirstName,
                LastName = x.LastName,
                Designation = x.Designation,
                Mobile = x.Mobile,
                Email = x.Email,
                Status = x.Status,
                Remarks = x.Remarks
            }).ToList();
        }

        public async Task<Response> MoveCandidateToBackgroundVerification(long candidateId)
        {
            await using var conn = _context.Database.GetDbConnection();

            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync();

            var cmd = conn.CreateCommand();

            cmd.CommandText = "dbo.sp_MoveCandidateToBackgroundVerification";
            cmd.CommandType = CommandType.StoredProcedure;

            var candidateParam = cmd.CreateParameter();
            candidateParam.ParameterName = "@CandidateId";
            candidateParam.Value = candidateId;

            cmd.Parameters.Add(candidateParam);

            await using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                var success = Convert.ToBoolean(reader["Success"]);
                var message = reader["Message"]?.ToString();

                return new Response
                {
                    Status = success,
                    Message = message
                };
            }

            return new Response
            {
                Status = false,
                Message = "Unexpected error occurred or no results returned."
            };
        }
    }
}



