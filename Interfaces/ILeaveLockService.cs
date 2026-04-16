using HRMSAPI.Models.Auth;

namespace HRMSAPI.Interfaces
{
    public interface ILeaveLockService
    {
        Task<Response> CheckLeaveLockStatusAsync();
    }
}

