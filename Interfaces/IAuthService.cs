using HRMSAPI.DTO;
using HRMSAPI.Models.Auth;
using Microsoft.AspNetCore.Mvc;
using Roomsy.DTOS.GenericsResponses;
using System.Security.Claims;

namespace HRMSAPI.Interfaces
{
    public interface IAuthService
    {
        Task<Response> Authenticate(LoginDto loginDto, string ipAddress, string userAgent);
        Task<Response> AuthenticateNew(LoginDto loginDto, string ipAddress, string userAgent);
        Task<Response> RefreshToken(string refreshToken);
        Task<Response> ChangePassword(ChangePasswordDto dto, JwtLoginDetailDto userClaims);

        Task<Response> ForgotPassword([FromBody] ForgetPasswordDto dto);

        Task<Response> ResetPassword([FromBody] ResetPasswordDto dto);
        
        Task<Response> AdminResetPassword([FromBody] AdminResetPasswordDto dto, JwtLoginDetailDto userClaims);
        //Task<Response> GetPermissions(int? roleId = null);
        //Task<Response> CreateOrUpdatePermission(List<RoleComponentPermissionDto> dtos, JwtLoginDetailDto user);
        Task<FetchAndResponse> GetRoleList();
        Task<FetchAndResponse> RefreshPermissions(int userId);
    }
}
