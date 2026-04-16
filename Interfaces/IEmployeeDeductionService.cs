using Roomsy.DTOS.GenericsResponses;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace HRMSAPI.Interfaces
{
    public interface IEmployeeDeductionService
    {
        Task<ExecuteAndReponse> UploadEmployeeDeductionExcel(IFormFile file, string user);
    }
} 