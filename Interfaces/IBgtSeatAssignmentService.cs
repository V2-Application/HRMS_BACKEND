using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Roomsy.DTOS.GenericsResponses;

namespace HRMSAPI.Interfaces
{
    public interface IBgtSeatAssignmentService
    {
        Task<FetchAndResponse> UploadBgtSeatAssignmentExcelAsync(IFormFile file);
        Task<FetchAndResponse> GetAllBgtSeatAssignmentAsync();
    }
} 