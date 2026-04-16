using Roomsy.DTOS.GenericsResponses;

namespace HRMSAPI.Interfaces
{
    public interface IEmployeeSalaryAddOnsService
    {
        Task<ExecuteAndReponse> UploadSalaryAddOnsExcel(IFormFile file, string user);
    }
}
