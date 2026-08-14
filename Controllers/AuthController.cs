using HRMSAPI.Data;
using HRMSAPI.DTO;
using HRMSAPI.Extension;
using HRMSAPI.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Net;
using Roomsy.DTOS.GenericsResponses;

namespace HRMSAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IAuthService authService, ILogger<AuthController> logger)
        {
            _authService = authService;
            _logger = logger;
        }

      
        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
        {
            _logger.LogInformation("Login attempt for username: {Username}", loginDto?.Username);
            try
            {
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();
                var user = await _authService.Authenticate(loginDto, ipAddress, userAgent);
                _logger.LogInformation("Login completed for username: {Username} with status: {Status}, StatusCode: {StatusCode}",
                    loginDto?.Username, user.Status, user.StatusCode);

                return StatusCode((int)user.StatusCode, new
                {
                    Status = user.Status,
                    Message = user.Message,
                    Data = user.Data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for username: {Username}", loginDto?.Username);
                return BadRequest(new
                {
                    Status = false,
                    Message = ex.Message
                });
            }
        }

        [AllowAnonymous]
        [HttpPost("loginnew")]
        public async Task<IActionResult> LoginNew([FromBody] LoginDto loginDto)
        {
            _logger.LogInformation("Login attempt for username: {Username}", loginDto?.Username);
            try
            {
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();
                var user = await _authService.AuthenticateNew(loginDto, ipAddress, userAgent);
                _logger.LogInformation("Login completed for username: {Username} with status: {Status}, StatusCode: {StatusCode}",
                    loginDto?.Username, user.Status, user.StatusCode);

                return StatusCode((int)user.StatusCode, new
                {
                    Status = user.Status,
                    Message = user.Message,
                    Data = user.Data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for username: {Username}", loginDto?.Username);
                return BadRequest(new
                {
                    Status = false,
                    Message = ex.Message
                });
            }
        }


        // New: login for normal (non-store) active ecodes only. Rejects store-code accounts.
        [AllowAnonymous]
        [HttpPost("EcodeLogin")]
        public async Task<IActionResult> EcodeLogin([FromBody] LoginDto loginDto)
        {
            _logger.LogInformation("Ecode login attempt for username: {Username}", loginDto?.Username);
            try
            {
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();
                var user = await _authService.EcodeLogin(loginDto, ipAddress, userAgent);

                return StatusCode((int)user.StatusCode, new
                {
                    Status = user.Status,
                    Message = user.Message,
                    Data = user.Data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during ecode login for username: {Username}", loginDto?.Username);
                return BadRequest(new
                {
                    Status = false,
                    Message = ex.Message
                });
            }
        }

        // New: login for store-code accounts only (Ecode equals the store's STCode). Rejects normal employees.
        [AllowAnonymous]
        [HttpPost("StoreLogin")]
        public async Task<IActionResult> StoreLogin([FromBody] LoginDto loginDto)
        {
            _logger.LogInformation("Store login attempt for username: {Username}", loginDto?.Username);
            try
            {
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();
                var user = await _authService.StoreLogin(loginDto, ipAddress, userAgent);

                return StatusCode((int)user.StatusCode, new
                {
                    Status = user.Status,
                    Message = user.Message,
                    Data = user.Data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during store login for username: {Username}", loginDto?.Username);
                return BadRequest(new
                {
                    Status = false,
                    Message = ex.Message
                });
            }
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] string refreshToken)
        {
            _logger.LogInformation("Refresh token request initiated");
            try
            {
                var user = await _authService.RefreshToken(refreshToken);
                _logger.LogInformation("Refresh token request completed with status: {Status}, StatusCode: {StatusCode}",
                    user.Status, user.StatusCode);

                return StatusCode((int)user.StatusCode, new
                {
                    Status = user.Status,
                    Message = user.Message,
                    Data = user.Data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during refresh token request");
                return BadRequest(new
                {
                    Status = false,
                    Message = ex.Message
                });
            }
        }

        [HttpPost("ChangePassword"), Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto changePasswordDto)
        {
            _logger.LogInformation("Change password request initiated for user");
            try
            {
                var identity = HttpContext.User.Identity as ClaimsIdentity;
                var userClaims = AuthenticUserDetails.GetCurrentUserDetails(identity);
                _logger.LogInformation("Change password for employee ID: {EmployeeId}", userClaims?.EmployeeId);

                var user = await _authService.ChangePassword(changePasswordDto, userClaims);
                _logger.LogInformation("Change password completed for employee ID: {EmployeeId} with status: {Status}, StatusCode: {StatusCode}",
                    userClaims?.EmployeeId, user.Status, user.StatusCode);

                return StatusCode((int)user.StatusCode, new
                {
                    Status = user.Status,
                    Message = user.Message,
                    Data = user.Data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during change password for user");
                return BadRequest(new
                {
                    Status = false,
                    Message = ex.Message
                });
            }
        }

        [HttpPost("ForgotPassword")]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgetPasswordDto forgotPasswordDto)
        {
            _logger.LogInformation("Forgot password request for ECode: {ECode}", forgotPasswordDto?.ECode);
            try
            {
                var response = await _authService.ForgotPassword(forgotPasswordDto);
                _logger.LogInformation("Forgot password completed for ECode: {ECode} with status: {Status}, StatusCode: {StatusCode}",
                    forgotPasswordDto?.ECode, response.Status, response.StatusCode);

                return StatusCode((int)response.StatusCode, new
                {
                    Status = response.Status,
                    Message = response.Message,
                    Data = response.Data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during forgot password for ECode: {ECode}", forgotPasswordDto?.ECode);
                return BadRequest(new
                {
                    Status = false,
                    Message = ex.Message
                });
            }
        }

        [HttpPost("ResetPassword")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto resetPasswordDto)
        {
            _logger.LogInformation("Reset password request initiated");
            try
            {
                var response = await _authService.ResetPassword(resetPasswordDto);
                _logger.LogInformation("Reset password completed with status: {Status}, StatusCode: {StatusCode}",
                    response.Status, response.StatusCode);

                return StatusCode((int)response.StatusCode, new
                {
                    Status = response.Status,
                    Message = response.Message,
                    Data = response.Data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during reset password");
                return BadRequest(new
                {
                    Status = false,
                    Message = ex.Message
                });
            }
        }

        [HttpPost("AdminResetPassword")]
        [Authorize]
        public async Task<IActionResult> AdminResetPassword([FromBody] AdminResetPasswordDto adminResetPasswordDto)
        {
            _logger.LogInformation("Admin reset password request initiated");
            try
            {
                var identity = HttpContext.User.Identity as ClaimsIdentity;
                if (identity == null || !identity.IsAuthenticated)
                {
                    _logger.LogWarning("Unauthorized access attempt for AdminResetPassword");
                    return Unauthorized(new
                    {
                        Status = false,
                        Message = "User is not authenticated"
                    });
                }

                var userClaims = AuthenticUserDetails.GetCurrentUserDetails(identity);
                _logger.LogInformation("Processing AdminResetPassword for employee ID: {EmployeeId}", userClaims?.EmployeeId);

                var response = await _authService.AdminResetPassword(adminResetPasswordDto, userClaims);
                _logger.LogInformation("Admin reset password completed with status: {Status}, StatusCode: {StatusCode}",
                    response.Status, response.StatusCode);

                return StatusCode((int)response.StatusCode, new
                {
                    Status = response.Status,
                    Message = response.Message,
                    Data = response.Data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during admin reset password");
                return BadRequest(new
                {
                    Status = false,
                    Message = ex.Message
                });
            }
        }


        #region rbac

        //[HttpPost("RBAC/permissions")]
        //public async Task<IActionResult> CreateOrUpdatePermission([FromBody] List<RoleComponentPermissionDto> dto)
        //{
        //    var userIdentity = User.Identity as ClaimsIdentity;
        //    if (userIdentity == null || !userIdentity.IsAuthenticated)
        //    {
        //        _logger.LogWarning("Unauthorized access attempt for GetEmployeeDetailsUpdateView");
        //        return Unauthorized(new
        //        {
        //            Status = false,
        //            Message = "User is not authenticated"
        //        });
        //    }

        //    var loginDetail = AuthenticUserDetails.GetCurrentUserDetails(userIdentity);
        //    var response = await _authService.CreateOrUpdatePermission(dto, loginDetail);
        //    return StatusCode((int)response.StatusCode, new
        //    {
        //        response.Status,
        //        response.Message
        //    });
        //}

        // GET: api/rbac/permissions
        //[HttpGet("RBAC/permissions")]
        //public async Task<IActionResult> GetPermissions([FromQuery] int? roleId = null)
        //{
        //    var response = await _authService.GetPermissions(roleId);
        //    return StatusCode((int)response.StatusCode, new
        //    {
        //        response.Status,
        //        response.Message,
        //        response.Data
        //    });
        //}

        [HttpGet("refresh-permissions")]
        public async Task<ActionResult<FetchAndResponse>> RefreshPermissions([FromQuery]int userId)
        {
            try
            {
                // Get current user from JWT token
                //var userId = GetCurrentUserId();
                if (userId == null)
                {
                    return Unauthorized(new FetchAndResponse
                    {
                        Status = false,
                        Message = "User not authenticated",
                        Code = HttpStatusCode.Unauthorized
                    });
                }

                // Call the service method
                var result = await _authService.RefreshPermissions(userId);
                return StatusCode((int)result.Code, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing permissions");
                return StatusCode((int)HttpStatusCode.InternalServerError, new FetchAndResponse
                {
                    Status = false,
                    Message = ex.Message,
                    Code = HttpStatusCode.InternalServerError
                });
            }
        }

        private int? GetCurrentUserId()
        {
            try
            {
                var userIdClaim = User.FindFirst("UserId");
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                {
                    return userId;
                }
                return null;
            }
            catch
            {
                return null;
            }
        }
        #endregion

        #region roles
        [HttpGet("Roles")]
        public async Task<IActionResult> GetRoles()
        {
            var response = await _authService.GetRoleList();
            return StatusCode((int)response.Code, new
            {
                response.Status,
                response.Message,
                response.Data
            });
        }
        #endregion
    }
}