using System.Security.Claims;
using System.Security.Principal;

namespace HRMSAPI.DTO
{
    public class JwtLoginDetailDto
    {
        public string EmployeeId { get; set; } 
       public string role { get; set; } 
       

    }
}
