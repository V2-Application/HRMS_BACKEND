using HRMSAPI.Data;
using HRMSAPI.Models.Auth;

namespace HRMSAPI.Interfaces
{
    public interface IFnfDetailsService
    {
        Task<Response> GetFnfDetailsByEcodeAsync(string ecode);
    }
}



