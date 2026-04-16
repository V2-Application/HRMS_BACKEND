using HRMSAPI.Data;
using HRMSAPI.Models.Candidate;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace HRMSAPI.Helpers
{
    public static class EmployeeValidationHelper
    {
        public class ValidationResult
        {
            public bool IsValid { get; set; }
            public string Message { get; set; } = string.Empty;
            public string FieldName { get; set; } = string.Empty;
        }

        public static async Task<ValidationResult> ValidateEmployeeFieldsAsync(CandidateUpdate details, HRMSContext context, bool isInitialPost = false)
        {
            var results = new List<ValidationResult>();

            // Account Number validation
            if (string.IsNullOrWhiteSpace(details.accountNo))
            {
                results.Add(new ValidationResult { IsValid = false, Message = "Account number is required", FieldName = "accountNo" });
            }
            else
            {
                if (!Regex.IsMatch(details.accountNo, @"^[0-9]{9,18}$"))
                {
                    results.Add(new ValidationResult { IsValid = false, Message = "Account number must be 9-18 digits", FieldName = "accountNo" });
                }
            }

            // IFSC Code validation
            if (string.IsNullOrWhiteSpace(details.bankIfscCode))
            {
                results.Add(new ValidationResult { IsValid = false, Message = "IFSC code is required", FieldName = "bankIfscCode" });
            }
            else
            {
                if (!Regex.IsMatch(details.bankIfscCode, @"^[A-Z]{4}0[A-Z0-9]{6}$", RegexOptions.IgnoreCase))
                {
                    results.Add(new ValidationResult { IsValid = false, Message = "Invalid IFSC code format", FieldName = "bankIfscCode" });
                }
            }

            // Store Code validation (if applicable)
            if (string.IsNullOrWhiteSpace(details.location))
            {
                results.Add(new ValidationResult { IsValid = false, Message = "Location is required", FieldName = "location" });
            }

            // Aadhar Number validation
            if (string.IsNullOrWhiteSpace(details.aadharNo))
            {
                results.Add(new ValidationResult { IsValid = false, Message = "Aadhar number is required", FieldName = "aadharNo" });
            }
            else
            {
                // Remove spaces and hyphens for validation
                var cleanAadhar = Regex.Replace(details.aadharNo, @"[\s-]", "");
                if (!Regex.IsMatch(cleanAadhar, @"^[0-9]{12}$"))
                {
                    results.Add(new ValidationResult { IsValid = false, Message = "Aadhar number must be 12 digits", FieldName = "aadharNo" });
                }
                else
                {
                    // Check if Aadhar already exists for different employee
                    var aadharExists = await CheckAadharExistsAsync(cleanAadhar, details.empCode, details.cid, context);
                    if (aadharExists)
                    {
                        results.Add(new ValidationResult { IsValid = false, Message = "Aadhar number already exists for another employee", FieldName = "aadharNo" });
                    }
                }
            }

            // PAN Number validation
            if (string.IsNullOrWhiteSpace(details.panNo))
            {
                results.Add(new ValidationResult { IsValid = false, Message = "PAN number is required", FieldName = "panNo" });
            }
            else
            {
                if (!Regex.IsMatch(details.panNo, @"^[A-Z]{5}[0-9]{4}[A-Z]{1}$", RegexOptions.IgnoreCase))
                {
                    results.Add(new ValidationResult { IsValid = false, Message = "Invalid PAN number format", FieldName = "panNo" });
                }
                else
                {
                    // Check if PAN already exists for different employee
                    var panExists = await CheckPanExistsAsync(details.panNo.ToUpper(), details.empCode, details.cid, context);
                    if (panExists)
                    {
                        results.Add(new ValidationResult { IsValid = false, Message = "PAN number already exists for another employee", FieldName = "panNo" });
                    }
                }
            }

            // Return first validation error if any
            if (results.Any(r => !r.IsValid))
            {
                var firstError = results.First(r => !r.IsValid);
                return firstError;
            }

            return new ValidationResult { IsValid = true, Message = "All validations passed" };
        }

        private static async Task<bool> CheckAadharExistsAsync(string aadharNo, string ecode, long candidateId, HRMSContext context)
        {
            try
            {
                // Check if Aadhar exists for any employee except the current one (by Ecode)
                var existingAadhar = await context.tblEmployees
                    .Where(e => e.AADHAR_NO == aadharNo && 
                               e.Ecode != ecode && e.IsActive == true && e.IsDeleted != true)
                    .FirstOrDefaultAsync();

                return existingAadhar != null;
            }
            catch (Exception)
            {
                // If database check fails, allow the operation (fail-safe approach)
                return false;
            }
        }

        private static async Task<bool> CheckPanExistsAsync(string panNo, string ecode, long candidateId, HRMSContext context)
        {
            try
            {
                // Check if PAN exists for any employee except the current one (by Ecode)
                var existingPan = await context.tblEmployees
                    .Where(e => e.PAN_NO.ToUpper() == panNo && 
                               e.Ecode != ecode && e.IsActive == true && e.IsDeleted == false)
                    .FirstOrDefaultAsync();

                return existingPan != null;
            }
            catch (Exception)
            {
                // If database check fails, allow the operation (fail-safe approach)
                return false;
            }
        }

        public static List<ValidationResult> GetAllValidationErrors(CandidateUpdate details, bool isInitialPost = false)
        {
            var results = new List<ValidationResult>();

            // Account Number validation
            if (string.IsNullOrWhiteSpace(details.accountNo))
            {
                results.Add(new ValidationResult { IsValid = false, Message = "Account number is required", FieldName = "accountNo" });
            }
            else if (!Regex.IsMatch(details.accountNo, @"^[0-9]{9,18}$"))
            {
                results.Add(new ValidationResult { IsValid = false, Message = "Account number must be 9-18 digits", FieldName = "accountNo" });
            }

            // IFSC Code validation
            if (string.IsNullOrWhiteSpace(details.bankIfscCode))
            {
                results.Add(new ValidationResult { IsValid = false, Message = "IFSC code is required", FieldName = "bankIfscCode" });
            }
            else if (!Regex.IsMatch(details.bankIfscCode, @"^[A-Z]{4}0[A-Z0-9]{6}$", RegexOptions.IgnoreCase))
            {
                results.Add(new ValidationResult { IsValid = false, Message = "Invalid IFSC code format", FieldName = "bankIfscCode" });
            }

            // Store Code validation (if applicable)
            if (string.IsNullOrWhiteSpace(details.location))
            {
                results.Add(new ValidationResult { IsValid = false, Message = "Location is required", FieldName = "location" });
            }

            // Aadhar Number validation
            if (string.IsNullOrWhiteSpace(details.aadharNo))
            {
                results.Add(new ValidationResult { IsValid = false, Message = "Aadhar number is required", FieldName = "aadharNo" });
            }
            else
            {
                // Remove spaces and hyphens for validation
                var cleanAadhar = Regex.Replace(details.aadharNo, @"[\s-]", "");
                if (!Regex.IsMatch(cleanAadhar, @"^[0-9]{12}$"))
                {
                    results.Add(new ValidationResult { IsValid = false, Message = "Aadhar number must be 12 digits", FieldName = "aadharNo" });
                }
            }

            // PAN Number validation
            if (string.IsNullOrWhiteSpace(details.panNo))
            {
                results.Add(new ValidationResult { IsValid = false, Message = "PAN number is required", FieldName = "panNo" });
            }
            else if (!Regex.IsMatch(details.panNo, @"^[A-Z]{5}[0-9]{4}[A-Z]{1}$", RegexOptions.IgnoreCase))
            {
                results.Add(new ValidationResult { IsValid = false, Message = "Invalid PAN number format", FieldName = "panNo" });
            }

            return results;
        }
    }
}
