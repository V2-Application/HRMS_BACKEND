using HRMSAPI.DTO;
using System.Security.Claims;

namespace HRMSAPI.Extension
{
    public class AuthenticUserDetails
    {
        public static JwtLoginDetailDto GetCurrentUserDetails(ClaimsIdentity identity)
        {
            if (identity != null)
            {
                var userClaims = identity.Claims;

                return new JwtLoginDetailDto
                {
                    EmployeeId = userClaims.FirstOrDefault(o => o.Type == "EmployeeId")?.Value ?? string.Empty,
                    role = userClaims.FirstOrDefault(o => o.Type == "Role")?.Value ?? string.Empty,
                };
            }
            return null;
        }
    }
}
