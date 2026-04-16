using Microsoft.AspNetCore.Http;
using Roomsy.DTOS.GenericsResponses;
using System.Threading.Tasks;

namespace HRMSAPI.Interfaces
{
    public interface IApplicantUploadService
    {
        Task<ExecuteAndReponse> UploadApplicantsAsync(IFormFile file, string uploadedBy);
    }
}

