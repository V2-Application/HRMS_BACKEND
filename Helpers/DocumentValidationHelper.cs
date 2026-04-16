using HRMSAPI.Models.Candidate;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HRMSAPI.Helpers
{
    public static class DocumentValidationHelper
    {
        public const long MaxFileSizeBytes = 200 * 1024 * 1024; // 200 MB
        public const string MaxFileSizeDisplay = "200 MB";

        private static readonly string[] AllowedDocumentExtensionsArray = new[]
        {
            ".pdf", ".doc", ".docx", ".xls", ".xlsx",
            ".png", ".jpg", ".jpeg", ".gif", ".bmp",
            ".txt", ".rtf", ".mp4"
        };

        private static readonly IReadOnlyCollection<string> AllowedDocumentExtensionsReadOnly =
            Array.AsReadOnly(AllowedDocumentExtensionsArray);

        private static readonly HashSet<string> AllowedExtensionsSet =
            new HashSet<string>(AllowedDocumentExtensionsArray, StringComparer.OrdinalIgnoreCase);

        public static IReadOnlyCollection<string> AllowedDocumentExtensions => AllowedDocumentExtensionsReadOnly;

        public static FileValidationError? ValidateCandidateDocuments(CandidateDocs? docs)
        {
            if (docs == null)
                return null;

            foreach (var file in EnumerateFiles(docs))
            {
                var error = ValidateFile(file);
                if (error != null)
                    return error;
            }

            return null;
        }

        private static FileValidationError? ValidateFile(IFormFile? file)
        {
            if (file == null)
                return null;

            if (file.Length > MaxFileSizeBytes)
            {
                return new FileValidationError(
                    $"File '{file.FileName}' exceeds the maximum allowed size of {MaxFileSizeDisplay}.",
                    true
                );
            }

            var extension = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensionsSet.Contains(extension))
            {
                return new FileValidationError(
                    $"File '{file.FileName}' has an invalid type. Allowed types: {string.Join(", ", AllowedDocumentExtensions)}.",
                    false
                );
            }

            return null;
        }

        private static IEnumerable<IFormFile> EnumerateFiles(CandidateDocs docs)
        {
            if (docs.PassportPhoto != null) yield return docs.PassportPhoto;
            if (docs.Last3BankStatement != null) yield return docs.Last3BankStatement;
            if (docs.PrevOfferLetter != null) yield return docs.PrevOfferLetter;

            foreach (var file in docs.Last3SalarySlip ?? Enumerable.Empty<IFormFile>()) yield return file;
            foreach (var file in docs.PanAttachment ?? Enumerable.Empty<IFormFile>()) yield return file;
            foreach (var file in docs.AadharAttachment ?? Enumerable.Empty<IFormFile>()) yield return file;
            foreach (var file in docs.AadharBackAttachment ?? Enumerable.Empty<IFormFile>()) yield return file;
            foreach (var file in docs.BankPassbookAttachment ?? Enumerable.Empty<IFormFile>()) yield return file;
            foreach (var file in docs.EducationAttachment ?? Enumerable.Empty<IFormFile>()) yield return file;
            foreach (var file in docs.ResumeAttachment ?? Enumerable.Empty<IFormFile>()) yield return file;
            foreach (var file in docs.EvaluationAttachment ?? Enumerable.Empty<IFormFile>()) yield return file;
            foreach (var file in docs.OfferLetterAttachment ?? Enumerable.Empty<IFormFile>()) yield return file;
            foreach (var file in docs.InterviewVideo ?? Enumerable.Empty<IFormFile>()) yield return file;
            foreach (var file in docs.OtherAttachment ?? Enumerable.Empty<IFormFile>()) yield return file;
            foreach (var file in docs.BankStatementVideo ?? Enumerable.Empty<IFormFile>()) yield return file;
        }
    }

    public class FileValidationError
    {
        public FileValidationError(string message, bool isFileSizeViolation)
        {
            Message = message;
            IsFileSizeViolation = isFileSizeViolation;
        }

        public string Message { get; }
        public bool IsFileSizeViolation { get; }
    }
}
