using HRMSAPI.Data;
using HRMSAPI.Extension;
using HRMSAPI.Models.Candidate;
using Microsoft.AspNetCore.Http;
using Emgu.CV;
using HRMSAPI.Data;
using HRMSAPI.Extension;
using HRMSAPI.Models.Candidate;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using Candidate = HRMSAPI.Models.Candidate.Candidate;
using CandidateDocs = HRMSAPI.Models.Candidate.CandidateDocs;
[ApiController]
[Route("api/documents")]
public class DocumentsController : ControllerBase
{
    private readonly HRMSContext _context;
    private readonly string savePath = Path.Combine("wwwroot");
    private readonly ILogger<DocumentsController> _logger;
    

    public DocumentsController(HRMSContext context , ILogger<DocumentsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpPost("upload-documents")]
    public async Task<IActionResult> UploadDocuments([FromForm] CandidateDocs docs)
    {
       
        var documentUrls = new List<string>();

        async Task SaveFileIfExists(IFormFile? file, string folder, string docType)
        {
            if (file?.Length > 0)
            {
                var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "Uploads", folder);
                if (!Directory.Exists(uploadDir))
                {
                    Directory.CreateDirectory(uploadDir);
                }

                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
                var filePath = Path.Combine(uploadDir, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Save to database using stored procedure
                int result = await _context.GetProcedures().sp_InsertCandidateDocsAsync(
                    0, // No candidateId, using 0 or null as placeholder
                    filePath,
                    docType,
                    file.Length.ToString(),
                    "System");

                if (result < 1)
                    throw new Exception($"Unable to save {docType}");

                // Store the file path (URL) for response
                documentUrls.Add($"/Uploads/{folder}/{fileName}");
            }
        }

        async Task SaveFileListIfExists(List<IFormFile>? files, string folder, string docType)
        {
            if (files?.Count > 0)
            {
                for (int i = 0; i < files.Count; i++)
                {
                    await SaveFileIfExists(files[i], folder, docType);
                }
            }
        }

        // Process file uploads
        await SaveFileListIfExists(docs.Last3SalarySlip, "SalarySlips", "SalarySlip");
        await SaveFileIfExists(docs.Last3BankStatement, "BankStatements", "BankStatement");
        await SaveFileIfExists(docs.PrevOfferLetter, "PrevOfferLetters", "PrevOfferLetter");
        await SaveFileListIfExists(docs.PanAttachment, "Pan", "Pan");
        await SaveFileListIfExists(docs.AadharAttachment, "Aadhar", "Aadhar");
        await SaveFileListIfExists(docs.BankPassbookAttachment, "BankPassbook", "BankPassbook");
        await SaveFileListIfExists(docs.EducationAttachment, "Education", "Education");
        await SaveFileListIfExists(docs.ResumeAttachment, "Resume", "Resume");

        if (!documentUrls.Any())
        {
            return BadRequest("No valid files uploaded.");
        }

        return Ok(documentUrls);
    }

    [HttpPost("upload-any-document")]
    public async Task<IActionResult> UploadAnyDocument([FromForm] CandidateDocument doc)
    {
        _logger.LogInformation("Uploading documents");

        try
        {
            var userIdentity = User.Identity as ClaimsIdentity;
            if (userIdentity == null || !userIdentity.IsAuthenticated)
            {
                _logger.LogWarning("Unauthorized access attempt for Upload Documents");
                return Unauthorized(new
                {
                    Status = false,
                    Message = "User is not authenticated"
                });
            }

            var loginDetail = AuthenticUserDetails.GetCurrentUserDetails(userIdentity);
            var updatedBy = userIdentity.FindFirst("EmployeeId")?.Value;
            _logger.LogInformation("Processing Upload Documents for employee ID: {EmployeeId}", updatedBy);
            string documentUrl = "";

            async Task SaveFileIfExists(IFormFile? file, string folder)
            {
                if (file?.Length > 0)
                {
                    var filePath = await SaveFile(file, folder);
                    documentUrl = filePath;
                }
            }
            async Task<string> SaveFile(IFormFile file, string folderName)
            {
                // Create a directory for the candidate if not exists
                //var directoryPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "CandidateFiles", candidateId.ToString(),folderName);
                var directoryPath = Path.Combine(savePath, folderName);
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                // Create the file path
                var fileName = $"{DateTime.Now.ToString("ddMMyyyyHHmmssffff")}_{file.FileName}";
                var filePath = Path.Combine(directoryPath, fileName);
                var returnPath = Path.Combine(folderName, fileName);

                // Save the file
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(fileStream);
                }

                // Return the file path to be stored in the database
                return returnPath;
            }
            await SaveFileIfExists(doc.documentAttachment, "Documents");

            if (string.IsNullOrEmpty(documentUrl))
            {
                return BadRequest(new
                {
                    status = false,
                    message = "No valid file uploaded"
                });
            }

            var result = new
            {
                status = true,
                message = "Upload successful",
                data = new
                {
                    url = documentUrl,
                    filename = doc.documentAttachment?.FileName
                }
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading document  for employee ID: {EmployeeId}", HttpContext.User.FindFirst("EmployeeId")?.Value);
            return StatusCode(500, new
            {
                status = false,
                message = "An unexpected error occurred while uploading the document.",
                error = ex.Message
            });
        }
    }
}
