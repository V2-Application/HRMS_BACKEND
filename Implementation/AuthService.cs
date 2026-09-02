using HRMSAPI.DTO;
using HRMSAPI.Extension;
using HRMSAPI.Interfaces;
using HRMSAPI.Models.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Concurrent;
using HRMSAPI.Data;
using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Roomsy.DTOS.GenericsResponses;
using System.Data;


namespace HRMSAPI.Implementation
{
    public class AuthService : IAuthService
    {
        private readonly HRMSContext _context;
        private readonly IConfiguration _configuration;
        private static readonly ConcurrentDictionary<string, string> _refreshTokens = new();
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IEmailService _emailService;
        private readonly ILogger<AuthService> _logger;

        public AuthService(IConfiguration configuration, IHttpContextAccessor httpContextAccessor, HRMSContext context, IEmailService emailService, ILogger<AuthService> logger)
        {
            _configuration = configuration;
            _httpContextAccessor = httpContextAccessor;
            _context = context;
            _emailService = emailService;
            _logger = logger;
        }


        //public async Task<Response> Authenticate(LoginDto loginDto, string ipAddress, string userAgent)
        //{
        //    _logger.LogInformation("Authenticating user with username: {Username}", loginDto?.Username);
        //    var loginHistory = new LoginHistory
        //    {
        //        Username = loginDto?.Username,
        //        IpAddress = ipAddress,
        //        UserAgent = userAgent,
        //        LoginTime = DateTime.UtcNow
        //    };

        //    try
        //    {
        //        if (string.IsNullOrWhiteSpace(loginDto?.Username) || string.IsNullOrWhiteSpace(loginDto.Password))
        //        {
        //            _logger.LogWarning("Authentication failed: Username or password is empty");
        //            loginHistory.Success = false;
        //            loginHistory.Message = "Username and password are required.";
        //            await _context.LoginHistories.AddAsync(loginHistory);
        //            await _context.SaveChangesAsync();
        //            return new Response { Status = false, Message = "Username and password are required.", StatusCode = HttpStatusCode.BadRequest };
        //        }

        //        var userDetail = await ValidateUser(loginDto);

        //        if (userDetail.EmployeeId == 0 || userDetail.IsActive == false)
        //        {
        //            _logger.LogWarning("Authentication failed: Invalid username or password for {Username}", loginDto.Username);
        //            loginHistory.Success = false;
        //            loginHistory.Message = "Invalid username or password.";
        //            await _context.LoginHistories.AddAsync(loginHistory);
        //            await _context.SaveChangesAsync();
        //            return new Response { Status = false, Message = "Invalid username or password.", StatusCode = HttpStatusCode.Unauthorized };
        //        }

        //        var reportHead = new tblEmployee();
        //        if (!string.IsNullOrEmpty(userDetail.ReportHeadEcode))
        //        {
        //            _logger.LogInformation("Fetching report head for Ecode: {ReportHeadEcode}", userDetail.ReportHeadEcode);
        //            reportHead = await _context.tblEmployees
        //                .Where(a => a.Ecode == userDetail.ReportHeadEcode)
        //                .FirstOrDefaultAsync();
        //        }

        //        var rolename = _context.tblEmployeeRoles
        //            .Where(a => a.EmployeeId == userDetail.EmployeeId)
        //            .Select(a => a.Role.RoleName)
        //            .FirstOrDefault() ?? "Employee";
        //        _logger.LogInformation("User role for {Username}: {Role}", userDetail.EmailAddress, rolename);

        //        var accessToken = await GenerateAccessToken(userDetail.EmailAddress, userDetail.EmployeeId, rolename, userDetail.ReportHeadEcode, userDetail.Reportheadid);
        //        var refreshToken = await GenerateRefreshToken();
        //        var locationList = await _context.GetProcedures().usp_GetLocationByRoleAsync(userDetail.Ecode);

        //        bool hasReports = HasReports(userDetail.Ecode);

        //        var user = new UserWithTokens
        //        {
        //            Username = userDetail.EmailAddress,
        //            Role = rolename,
        //            AccessToken = accessToken,
        //            RefreshToken = refreshToken,
        //            FirstName = userDetail.FirstName,
        //            LastName = userDetail.LastName,
        //            EmployeeId = userDetail.EmployeeId,
        //            EmailAddress = userDetail.EmailAddress,
        //            Ecode = userDetail.Ecode,
        //            ReportHeadEcode = userDetail.ReportHeadEcode,
        //            Reportheadid = userDetail.Reportheadid,
        //            ReportHeadName = reportHead?.FirstName + " " + reportHead?.LastName,
        //            StoreCode = userDetail.StoreCode ?? "",
        //            LocationName = userDetail.LocationName ?? "",
        //            DepartmentName = userDetail.DepartmentName,
        //            Joiningdate = userDetail.Joiningdate,
        //            LocationList = locationList ?? new List<usp_GetLocationByRoleResult>(),
        //            IsStore = userDetail.IsStore,
        //            IsActive = userDetail.IsActive,
        //            HasReports = hasReports
        //        };

        //        loginHistory.Success = true;
        //        loginHistory.Message = "Authenticated successfully.";
        //        loginHistory.EmployeeId = userDetail.EmployeeId;
        //        loginHistory.Role = rolename;
        //        await _context.LoginHistories.AddAsync(loginHistory);
        //        await _context.SaveChangesAsync();

        //        _refreshTokens[user.RefreshToken] = user.Username;
        //        _logger.LogInformation("Authentication successful for {Username}. Access token and refresh token generated", userDetail.EmailAddress);
        //        return new Response { Status = true, Message = "Authenticated successfully.", StatusCode = HttpStatusCode.OK, Data = user };
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error during authentication for {Username}", loginDto?.Username);
        //        loginHistory.Success = false;
        //        loginHistory.Message = ex.Message;
        //        await _context.LoginHistories.AddAsync(loginHistory);
        //        await _context.SaveChangesAsync();
        //        return new Response { Status = false, Message = "An unexpected error occurred. Please try again later.", StatusCode = HttpStatusCode.InternalServerError };
        //    }
        //}

        //public async Task<Response> Authenticate(LoginDto loginDto, string ipAddress, string userAgent)
        //{
        //    _logger.LogInformation("Authenticating user with username: {Username}", loginDto?.Username);
        //    var loginHistory = new LoginHistory
        //    {
        //        Username = loginDto?.Username,
        //        IpAddress = ipAddress,
        //        UserAgent = userAgent,
        //        LoginTime = DateTime.UtcNow
        //    };

        //    try
        //    {
        //        if (string.IsNullOrWhiteSpace(loginDto?.Username) || string.IsNullOrWhiteSpace(loginDto.Password))
        //        {
        //            _logger.LogWarning("Authentication failed: Username or password is empty");
        //            loginHistory.Success = false;
        //            loginHistory.Message = "Username and password are required.";
        //            await _context.LoginHistories.AddAsync(loginHistory);
        //            await _context.SaveChangesAsync();
        //            return new Response { Status = false, Message = "Username and password are required.", StatusCode = HttpStatusCode.BadRequest };
        //        }

        //        var userDetail = await ValidateUser(loginDto);

        //        if (userDetail.EmployeeId == 0 || userDetail.IsActive == false)
        //        {
        //            _logger.LogWarning("Authentication failed: Invalid username or password for {Username}", loginDto.Username);
        //            loginHistory.Success = false;
        //            loginHistory.Message = "Invalid username or password.";
        //            await _context.LoginHistories.AddAsync(loginHistory);
        //            await _context.SaveChangesAsync();
        //            return new Response { Status = false, Message = "Invalid username or password.", StatusCode = HttpStatusCode.Unauthorized };
        //        }

        //        var reportHead = new tblEmployee();
        //        if (!string.IsNullOrEmpty(userDetail.ReportHeadEcode))
        //        {
        //            _logger.LogInformation("Fetching report head for Ecode: {ReportHeadEcode}", userDetail.ReportHeadEcode);
        //            reportHead = await _context.tblEmployees
        //                .Where(a => a.Ecode == userDetail.ReportHeadEcode)
        //                .FirstOrDefaultAsync();
        //        }

        //        var rolename = _context.tblEmployeeRoles
        //            .Where(a => a.EmployeeId == userDetail.EmployeeId)
        //            .Select(a => a.Role.RoleName)
        //            .FirstOrDefault() ?? "Employee";
        //        _logger.LogInformation("User role for {Username}: {Role}", userDetail.EmailAddress, rolename);

        //        // Fetch component permissions for the user's role
        //        //var componentPermissions = await _context.tblRoleComponentPermissions
        //        //    .Where(rc => rc.RoleId == _context.tblEmployeeRoles
        //        //        .Where(er => er.EmployeeId == userDetail.EmployeeId)
        //        //        .Select(er => er.RoleId)
        //        //        .FirstOrDefault())
        //        //    .Select(rc => new ComponentPermission
        //        //    {
        //        //        ComponentName = rc.ComponentName,
        //        //        IsRead = rc.IsRead,
        //        //        IsWrite = rc.IsWrite
        //        //    })
        //        //    .ToListAsync();
        //        //_logger.LogInformation("Component permissions for {Username}: {Permissions}",
        //        //    userDetail.EmailAddress,
        //        //    string.Join(", ", componentPermissions.Select(p => $"{p.ComponentName}: Read={p.IsRead}, Write={p.IsWrite}")));

        //        var accessToken = await GenerateAccessToken(userDetail.EmailAddress, userDetail.EmployeeId, rolename, userDetail.ReportHeadEcode, userDetail.Reportheadid);
        //        var refreshToken = await GenerateRefreshToken();
        //        var locationList = await _context.GetProcedures().usp_GetLocationByRoleAsync(userDetail.Ecode);

        //        bool hasReports = HasReports(userDetail.Ecode);

        //        var user = new UserWithTokens
        //        {
        //            Username = userDetail.EmailAddress,
        //            Role = rolename,
        //            AccessToken = accessToken,
        //            RefreshToken = refreshToken,
        //            FirstName = userDetail.FirstName,
        //            LastName = userDetail.LastName,
        //            EmployeeId = userDetail.EmployeeId,
        //            EmailAddress = userDetail.EmailAddress,
        //            Ecode = userDetail.Ecode,
        //            ReportHeadEcode = userDetail.ReportHeadEcode,
        //            Reportheadid = userDetail.Reportheadid,
        //            ReportHeadName = reportHead?.FirstName + " " + reportHead?.LastName,
        //            StoreCode = userDetail.StoreCode ?? "",
        //            LocationName = userDetail.LocationName ?? "",
        //            DepartmentName = userDetail.DepartmentName,
        //            Joiningdate = userDetail.Joiningdate,
        //            LocationList = locationList ?? new List<usp_GetLocationByRoleResult>(),
        //            IsStore = userDetail.IsStore,
        //            IsActive = userDetail.IsActive,
        //            HasReports = hasReports,
        //            //ComponentPermissions = componentPermissions
        //        };

        //        loginHistory.Success = true;
        //        loginHistory.Message = "Authenticated successfully.";
        //        loginHistory.EmployeeId = userDetail.EmployeeId;
        //        loginHistory.Role = rolename;
        //        await _context.LoginHistories.AddAsync(loginHistory);
        //        await _context.SaveChangesAsync();

        //        _refreshTokens[user.RefreshToken] = user.Username;
        //        _logger.LogInformation("Authentication successful for {Username}. Access token and refresh token generated", userDetail.EmailAddress);
        //        return new Response { Status = true, Message = "Authenticated successfully.", StatusCode = HttpStatusCode.OK, Data = user };
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error during authentication for {Username}", loginDto?.Username);
        //        loginHistory.Success = false;
        //        loginHistory.Message = ex.Message;
        //        await _context.LoginHistories.AddAsync(loginHistory);
        //        await _context.SaveChangesAsync();
        //        return new Response { Status = false, Message = "An unexpected error occurred. Please try again later.", StatusCode = HttpStatusCode.InternalServerError };
        //    }
        //}
        public async Task<Response> Authenticate(LoginDto loginDto, string ipAddress, string userAgent)
        {
            _logger.LogInformation("Authenticating user with username: {Username}", loginDto?.Username);

            var loginHistory = new LoginHistory
            {
                Username = loginDto?.Username,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                LoginTime = DateTime.UtcNow
            };

            // helper to avoid failing the whole request if history write bombs
            async Task SafeWriteHistoryAsync()
            {
                try
                {
                    await _context.LoginHistories.AddAsync(loginHistory);
                    await _context.SaveChangesAsync();
                }
                catch (Exception hx)
                {
                    _logger.LogWarning(hx, "Failed to write login history for {Username}", loginDto?.Username);
                }
            }

            try
            {
                // 1) Basic input validation
                if (string.IsNullOrWhiteSpace(loginDto?.Username) || string.IsNullOrWhiteSpace(loginDto?.Password))
                {
                    _logger.LogWarning("Authentication failed: Username or password is empty");
                    loginHistory.Success = false;
                    loginHistory.Message = "Username and password are required.";
                    await SafeWriteHistoryAsync();

                    return new Response
                    {
                        Status = false,
                        Message = "Username and password are required.",
                        StatusCode = HttpStatusCode.BadRequest
                    };
                }

                // 2) Validate user
                var userDetail = await ValidateUser(loginDto);
                if (userDetail.EmployeeId == 0 || userDetail.IsActive == false)
                {
                    _logger.LogWarning("Authentication failed: Invalid username or password for {Username}", loginDto.Username);
                    loginHistory.Success = false;
                    loginHistory.Message = "Invalid username or password.";
                    await SafeWriteHistoryAsync();

                    return new Response
                    {
                        Status = false,
                        Message = "Invalid username or password.",
                        StatusCode = HttpStatusCode.Unauthorized
                    };
                }

                // 3) Optional lookup: report head (isolated)
                tblEmployee reportHead = null;
                if (!string.IsNullOrEmpty(userDetail.ReportHeadEcode))
                {
                    try
                    {
                        _logger.LogInformation("Fetching report head for Ecode: {ReportHeadEcode}", userDetail.ReportHeadEcode);
                        reportHead = await _context.tblEmployees
                            .Where(a => a.Ecode == userDetail.ReportHeadEcode)
                            .FirstOrDefaultAsync();
                    }
                    catch (Exception rhx)
                    {
                        _logger.LogWarning(rhx, "Failed to fetch report head for {Ecode}", userDetail.ReportHeadEcode);
                    }
                }

                // A manager who has gone inactive or separated must not be presented as the
                // reporting manager: the code stays on tblEmployee for history, but every
                // consumer sees the field as unassigned so the employee gets reassigned.
                bool reportHeadUsable =
                    reportHead != null
                    && reportHead.IsActive == true
                    && reportHead.IsDeleted != true
                    && reportHead.DateOfLeft == null;

                if (!reportHeadUsable)
                {
                    if (reportHead != null)
                    {
                        _logger.LogInformation(
                            "Report head {ReportHeadEcode} for {Ecode} is inactive/separated - presenting reporting manager as unassigned.",
                            userDetail.ReportHeadEcode, userDetail.Ecode);
                    }
                    reportHead = null;
                }

                // 4) Role (isolated, with default)
                string roleName = "Employee";
                try
                {
                    roleName = await _context.tblEmployeeRoles
                        .Where(a => a.EmployeeId == userDetail.EmployeeId)
                        .Select(a => a.Role.RoleName)
                        .FirstOrDefaultAsync() ?? "Employee";
                }
                catch (Exception rx)
                {
                    _logger.LogWarning(rx, "Failed to resolve role for {EmployeeId}", userDetail.EmployeeId);
                }
                _logger.LogInformation("User role for {Username}: {Role}", userDetail.EmailAddress, roleName);

                // 5) Tokens
                var accessToken = await GenerateAccessToken(userDetail.EmailAddress, userDetail.EmployeeId, roleName, userDetail.ReportHeadEcode, userDetail.Reportheadid);
                var refreshToken = await GenerateRefreshToken();

                // 6) Stored proc for locations — **INSULATED** so auth still succeeds if this fails
                List<usp_GetLocationByRoleResult> locationList = new();
                try
                {
                    locationList = await _context.GetProcedures()
                        .usp_GetLocationByRoleAsync(userDetail.Ecode)
                        ?? new List<usp_GetLocationByRoleResult>();
                }
                catch (Exception lxe)
                {
                    _logger.LogWarning(lxe, "usp_GetLocationByRoleAsync failed for Ecode {Ecode}", userDetail.Ecode);
                    locationList = new List<usp_GetLocationByRoleResult>(); // graceful fallback
                }

                // 7) HasReports — also insulated
                bool hasReports = false;
                try
                {
                    hasReports = HasReports(userDetail.Ecode);
                }
                catch (Exception hrx)
                {
                    _logger.LogWarning(hrx, "HasReports check failed for {Ecode}", userDetail.Ecode);
                }

                // 8) Build response user object
                var user = new UserWithTokens
                {
                    Username = userDetail.EmailAddress,
                    Role = roleName,
                    AccessToken = accessToken,
                    RefreshToken = refreshToken,
                    FirstName = userDetail.FirstName,
                    LastName = userDetail.LastName,
                    EmployeeId = userDetail.EmployeeId,
                    EmailAddress = userDetail.EmailAddress,
                    Ecode = userDetail.Ecode,
                    // Blanked when the stored manager is inactive/separated, so the client
                    // shows "no reporting manager" and prompts for reassignment.
                    ReportHeadEcode = reportHeadUsable ? userDetail.ReportHeadEcode : string.Empty,
                    Reportheadid = reportHeadUsable ? userDetail.Reportheadid : 0,
                    ReportHeadName = reportHeadUsable
                        ? $"{reportHead?.FirstName} {reportHead?.LastName}".Trim()
                        : string.Empty,
                    StoreCode = userDetail.StoreCode ?? string.Empty,
                    LocationName = userDetail.LocationName ?? string.Empty,
                    DepartmentName = userDetail.DepartmentName,
                    Joiningdate = userDetail.Joiningdate,
                    LocationList = locationList,
                    IsStore = userDetail.IsStore,
                    IsActive = userDetail.IsActive,
                    HasReports = hasReports,
                    DesignationName=userDetail.DesignationName ?? string.Empty,
                    // ComponentPermissions = componentPermissions
                };

                // 9) Login history (success)
                loginHistory.Success = true;
                loginHistory.Message = "Authenticated successfully.";
                loginHistory.EmployeeId = userDetail.EmployeeId;
                loginHistory.Role = roleName;
                await SafeWriteHistoryAsync();

                _refreshTokens[user.RefreshToken] = user.Username;
                _logger.LogInformation("Authentication successful for {Username}. Access/refresh tokens generated.", userDetail.EmailAddress);

                return new Response
                {
                    Status = true,
                    Message = "Authenticated successfully.",
                    StatusCode = HttpStatusCode.OK,
                    Data = user
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during authentication for {Username}", loginDto?.Username);

                loginHistory.Success = false;
                loginHistory.Message = "Unhandled exception";
                await SafeWriteHistoryAsync();

                return new Response
                {
                    Status = false,
                    Message = "An unexpected error occurred. Please try again later.",
                    StatusCode = HttpStatusCode.InternalServerError
                };
            }
        }

        // Shared validation for the two new login entry points below. Matches by Ecode only
        // (not email) and additionally gates on whether the employee's Ecode equals their
        // store's STCode — @wantStoreAccount = true only allows STCode-equal ("store code")
        // accounts through; false only allows normal employee accounts through.
        private async Task<(long EmployeeId, string FirstName, string LastName, string EmailAddress, string Role, string Ecode, string ReportHeadEcode, long Reportheadid, string LocationName, string StoreCode, string DepartmentName, string DesignationName,
            DateTime? Joiningdate, bool IsStore, bool IsActive, string PasswordHash)> ValidateUserByAccountType(string username, bool wantStoreAccount)
        {
            if (string.IsNullOrEmpty(username))
                return (0, "", "", "", "", "", "", 0, "", "NA", "NA", null, null, false, false, null);

            var result = await (
                from emp in _context.tblEmployees.AsNoTracking()
                join empRole in _context.tblEmployeeRoles.AsNoTracking()
                    on emp.EmployeeId equals empRole.EmployeeId into empRoles
                from empRole in empRoles.DefaultIfEmpty()
                join role in _context.tblRoles.AsNoTracking()
                    on empRole.RoleId equals role.RoleId into roles
                from role in roles.DefaultIfEmpty()
                join reportHead in _context.tblEmployees.AsNoTracking()
                    on emp.ReportHeadEcode equals reportHead.Ecode into reportHeads
                from reportHead in reportHeads.DefaultIfEmpty()
                join loc in _context.tblLocations.AsNoTracking()
                    on emp.LocationId equals loc.LocationId into locations
                from loc in locations.DefaultIfEmpty()
                join dep in _context.tblDepartments.AsNoTracking()
                    on emp.DepartmentId equals dep.DepartmentId into department
                from dep in department.DefaultIfEmpty()
                join des in _context.tblDesignations.AsNoTracking()
                    on emp.DesignationId equals des.DesignationId into designation
                from des in designation.DefaultIfEmpty()
                where emp.Ecode == username && emp.IsActive == true
                select new
                {
                    emp.EmployeeId,
                    emp.FirstName,
                    emp.LastName,
                    emp.EMAIL_ADDRESS,
                    emp.Ecode,
                    RoleName = role != null ? role.RoleName : "Employee",
                    emp.ReportHeadEcode,
                    Reportheadid = reportHead != null ? reportHead.EmployeeId : 0,
                    LocationName = loc != null ? loc.LocationName : "NA",
                    StoreCode = loc != null ? (loc.STCode ?? "") : "",
                    DepartmentName = dep != null ? dep.DepartmentName : "NA",
                    DesignationName = des != null ? des.DesignationName : "NA",
                    Joiningdate = emp.JOINING_DATE,
                    emp.IsActive,
                    emp.PasswordHash
                }
            ).SingleOrDefaultAsync();

            if (result == null)
                return (0, "", "", "", "", "", "", 0, "", "NA", "NA", null, null, false, false, null);

            bool isStoreAccount = !string.IsNullOrEmpty(result.StoreCode) &&
                string.Equals(result.Ecode?.Trim(), result.StoreCode?.Trim(), StringComparison.OrdinalIgnoreCase);

            if (isStoreAccount != wantStoreAccount)
                return (0, "", "", "", "", "", "", 0, "", "NA", "NA", null, null, false, false, null);

            return (result.EmployeeId, result.FirstName, result.LastName, result.EMAIL_ADDRESS, result.RoleName, result.Ecode, result.ReportHeadEcode, result.Reportheadid, result.LocationName, result.StoreCode, result.DepartmentName, result.DesignationName, result.Joiningdate, isStoreAccount, result.IsActive.GetValueOrDefault(), result.PasswordHash);
        }

        // New: login for normal (non-store) active ecodes only.
        public async Task<Response> EcodeLogin(LoginDto loginDto, string ipAddress, string userAgent)
        {
            return await LoginByAccountType(loginDto, ipAddress, userAgent, wantStoreAccount: false);
        }

        // New: login for store-code accounts only (Ecode equals the store's STCode).
        public async Task<Response> StoreLogin(LoginDto loginDto, string ipAddress, string userAgent)
        {
            return await LoginByAccountType(loginDto, ipAddress, userAgent, wantStoreAccount: true);
        }

        private async Task<Response> LoginByAccountType(LoginDto loginDto, string ipAddress, string userAgent, bool wantStoreAccount)
        {
            var loginHistory = new LoginHistory
            {
                Username = loginDto?.Username,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                LoginTime = DateTime.UtcNow
            };

            async Task SafeWriteHistoryAsync()
            {
                try
                {
                    await _context.LoginHistories.AddAsync(loginHistory);
                    await _context.SaveChangesAsync();
                }
                catch (Exception hx)
                {
                    _logger.LogWarning(hx, "Failed to write login history for {Username}", loginDto?.Username);
                }
            }

            try
            {
                if (string.IsNullOrWhiteSpace(loginDto?.Username) || string.IsNullOrWhiteSpace(loginDto?.Password))
                {
                    loginHistory.Success = false;
                    loginHistory.Message = "Username and password are required.";
                    await SafeWriteHistoryAsync();
                    return new Response { Status = false, Message = "Username and password are required.", StatusCode = HttpStatusCode.BadRequest };
                }

                var userDetail = await ValidateUserByAccountType(loginDto.Username, wantStoreAccount);
                if (userDetail.EmployeeId == 0 || !userDetail.IsActive || string.IsNullOrEmpty(userDetail.PasswordHash) ||
                    !BCrypt.Net.BCrypt.Verify(loginDto.Password, userDetail.PasswordHash))
                {
                    loginHistory.Success = false;
                    loginHistory.Message = "Invalid username or password.";
                    await SafeWriteHistoryAsync();
                    return new Response { Status = false, Message = "Invalid username or password.", StatusCode = HttpStatusCode.Unauthorized };
                }

                var accessToken = await GenerateAccessToken(userDetail.EmailAddress, userDetail.EmployeeId, userDetail.Role, userDetail.ReportHeadEcode, userDetail.Reportheadid);
                var refreshToken = await GenerateRefreshToken();

                bool hasReports = false;
                try { hasReports = HasReports(userDetail.Ecode); }
                catch (Exception hrx) { _logger.LogWarning(hrx, "HasReports check failed for {Ecode}", userDetail.Ecode); }

                var user = new UserWithTokens
                {
                    Username = userDetail.EmailAddress,
                    Role = userDetail.Role,
                    AccessToken = accessToken,
                    RefreshToken = refreshToken,
                    FirstName = userDetail.FirstName,
                    LastName = userDetail.LastName,
                    EmployeeId = userDetail.EmployeeId,
                    EmailAddress = userDetail.EmailAddress,
                    Ecode = userDetail.Ecode,
                    ReportHeadEcode = userDetail.ReportHeadEcode,
                    Reportheadid = userDetail.Reportheadid,
                    StoreCode = userDetail.StoreCode ?? string.Empty,
                    LocationName = userDetail.LocationName ?? string.Empty,
                    DepartmentName = userDetail.DepartmentName,
                    Joiningdate = userDetail.Joiningdate,
                    IsStore = userDetail.IsStore,
                    IsActive = userDetail.IsActive,
                    HasReports = hasReports,
                    DesignationName = userDetail.DesignationName ?? string.Empty,
                };

                loginHistory.Success = true;
                loginHistory.Message = "Authenticated successfully.";
                loginHistory.EmployeeId = userDetail.EmployeeId;
                loginHistory.Role = userDetail.Role;
                await SafeWriteHistoryAsync();

                _refreshTokens[user.RefreshToken] = user.Username;

                return new Response { Status = true, Message = "Authenticated successfully.", StatusCode = HttpStatusCode.OK, Data = user };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during {LoginType} login for {Username}", wantStoreAccount ? "store" : "ecode", loginDto?.Username);
                loginHistory.Success = false;
                loginHistory.Message = "Unhandled exception";
                await SafeWriteHistoryAsync();
                return new Response { Status = false, Message = "An unexpected error occurred. Please try again later.", StatusCode = HttpStatusCode.InternalServerError };
            }
        }

        public async Task<Response> AuthenticateNew(LoginDto loginDto, string ipAddress, string userAgent)
        {
            _logger.LogInformation("Authenticating user with username: {Username}", loginDto?.Username);

            var loginHistory = new LoginHistory
            {
                Username = loginDto?.Username,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                LoginTime = DateTime.UtcNow
            };

            try
            {
                // Basic validation
                if (string.IsNullOrWhiteSpace(loginDto?.Username) || string.IsNullOrWhiteSpace(loginDto.Password))
                {
                    _logger.LogWarning("Authentication failed: Username or password is empty");
                    loginHistory.Success = false;
                    loginHistory.Message = "Username and password are required.";
                    await _context.LoginHistories.AddAsync(loginHistory);
                    await _context.SaveChangesAsync();

                    return new Response
                    {
                        Status = false,
                        Message = "Username and password are required.",
                        StatusCode = HttpStatusCode.BadRequest
                    };
                }

                var userDetail = await ValidateUser(loginDto);

                if (userDetail.EmployeeId == 0 || userDetail.IsActive == false)
                {
                    _logger.LogWarning("Authentication failed: Invalid username or password for {Username}", loginDto.Username);
                    loginHistory.Success = false;
                    loginHistory.Message = "Invalid username or password.";
                    await _context.LoginHistories.AddAsync(loginHistory);
                    await _context.SaveChangesAsync();

                    return new Response
                    {
                        Status = false,
                        Message = "Invalid username or password.",
                        StatusCode = HttpStatusCode.Unauthorized
                    };
                }

                // Report Head details
                var reportHead = new tblEmployee();
                if (!string.IsNullOrEmpty(userDetail.ReportHeadEcode))
                {
                    _logger.LogInformation("Fetching report head for Ecode: {ReportHeadEcode}", userDetail.ReportHeadEcode);

                    reportHead = await _context.tblEmployees
                        .Where(a => a.Ecode == userDetail.ReportHeadEcode)
                        .FirstOrDefaultAsync();
                }

                // A manager who has gone inactive or separated must not be presented as the
                // reporting manager: the code stays on tblEmployee for history, but every
                // consumer sees the field as unassigned so the employee gets reassigned.
                bool reportHeadUsable =
                    reportHead != null
                    && reportHead.IsActive == true
                    && reportHead.IsDeleted != true
                    && reportHead.DateOfLeft == null;

                if (!reportHeadUsable)
                {
                    if (reportHead != null && !string.IsNullOrEmpty(reportHead.Ecode))
                    {
                        _logger.LogInformation(
                            "Report head {ReportHeadEcode} for {Ecode} is inactive/separated - presenting reporting manager as unassigned.",
                            userDetail.ReportHeadEcode, userDetail.Ecode);
                    }
                    reportHead = null;
                }

                // Role
                var role = _context.tblEmployeeRoles
                    .Where(a => a.EmployeeId == userDetail.EmployeeId)
                    .FirstOrDefault();

                if (role == null)
                {
                    throw new Exception("No Roles have assigned on this account,contact to HR Dept.");
                }

                var rolenameres = await _context.tblRoles
                    .AsQueryable()
                    .FirstOrDefaultAsync(row => row.RoleId == role.RoleId);

                var rolename = rolenameres?.RoleName ?? "Employee";

                _logger.LogInformation("User role for {Username}: {Role}", userDetail.EmailAddress, rolename);

                // Tokens
                var accessToken = await GenerateAccessToken(
                    userDetail.EmailAddress,
                    userDetail.EmployeeId,
                    rolename,
                    userDetail.ReportHeadEcode,
                    userDetail.Reportheadid
                );

                var refreshToken = await GenerateRefreshToken();

                // Locations by role
                List<usp_GetLocationByRoleResult> locationList = new();
                try
                {
                    locationList = await _context.GetProcedures()
                        .usp_GetLocationByRoleAsync(userDetail.Ecode)
                        ?? new List<usp_GetLocationByRoleResult>();
                }
                catch (Exception lxe)
                {
                    _logger.LogWarning(lxe, "usp_GetLocationByRoleAsync failed for Ecode {Ecode}", userDetail.Ecode);
                    locationList = new List<usp_GetLocationByRoleResult>(); // graceful fallback
                }

                bool hasReports = HasReports(userDetail.Ecode);

                var flatData = await _context.fn_GetRbacHierarchyByRole(role.RoleId).ToListAsync();

                var permissions = flatData
                    .GroupBy(r => new { r.RoleId, r.RoleName })
                    .Select(roleGrp => new
                    {
                        roleGrp.Key.RoleId,
                        roleGrp.Key.RoleName,
                        Modules = roleGrp
                            .GroupBy(m => new { m.ModuleId, m.ModuleName, m.ModuleStatus })
                            .Select(modGrp => new
                            {
                                modGrp.Key.ModuleId,
                                modGrp.Key.ModuleName,
                                ModuleStatus = modGrp.Key.ModuleStatus,
                                SubModules = modGrp
                                    .Where(sm => sm.SubModuleId != null)
                                    .GroupBy(sm => new { sm.SubModuleId, sm.SubModuleName, sm.SubModuleStatus })
                                    .Select(subGrp => new
                                    {
                                        subGrp.Key.SubModuleId,
                                        subGrp.Key.SubModuleName,
                                        SubModuleStatus = subGrp.Key.SubModuleStatus,
                                        Actions = subGrp
                                            .Where(a => a.ActionId != null)
                                            .GroupBy(a => new { a.ActionId, a.ActionName, a.ActionStatus })
                                            .Select(actGrp => new
                                            {
                                                actGrp.Key.ActionId,
                                                actGrp.Key.ActionName,
                                                ActionStatus = actGrp.Key.ActionStatus,
                                                FurtherParts = actGrp
                                                    .Where(fp => fp.ActionFurtherPartId != null)
                                                    .Select(fp => new
                                                    {
                                                        fp.ActionFurtherPartId,
                                                        fp.ActionFurtherPartName,
                                                        FurtherPartStatus = fp.FurtherPartStatus
                                                    }).ToList()
                                            }).ToList()
                                    }).ToList()
                            }).ToList()
                    }).ToList();

                // 🔹 NEW: AssignedLocation + IsGeofenceEnabled via helper
                var (assignedLocation, isGeofenceEnabled) =
                    await GetAssignedLocationAndGeofenceAsync(userDetail.EmployeeId);

                var user = new UserWithTokens
                {
                    Username = userDetail.EmailAddress,
                    Role = rolename,
                    AccessToken = accessToken,
                    RefreshToken = refreshToken,
                    FirstName = userDetail.FirstName,
                    LastName = userDetail.LastName,
                    EmployeeId = userDetail.EmployeeId,
                    EmailAddress = userDetail.EmailAddress,
                    Ecode = userDetail.Ecode,
                    // Blanked when the stored manager is inactive/separated, so the client
                    // shows "no reporting manager" and prompts for reassignment.
                    ReportHeadEcode = reportHeadUsable ? userDetail.ReportHeadEcode : string.Empty,
                    Reportheadid = reportHeadUsable ? userDetail.Reportheadid : 0,
                    ReportHeadName = reportHeadUsable
                        ? $"{reportHead?.FirstName} {reportHead?.LastName}".Trim()
                        : string.Empty,
                    StoreCode = userDetail.StoreCode ?? "",
                    LocationName = userDetail.LocationName ?? "",
                    DepartmentName = userDetail.DepartmentName,
                    Joiningdate = userDetail.Joiningdate,
                    LocationList = locationList ?? new List<usp_GetLocationByRoleResult>(),
                    IsStore = userDetail.IsStore,
                    IsActive = userDetail.IsActive,
                    HasReports = hasReports,
                    Permissions = permissions,
                    DesignationName = userDetail.DesignationName ?? "",
                    AssignedLocation = assignedLocation,
                    IsGeofenceEnabled = isGeofenceEnabled
                };

                loginHistory.Success = true;
                loginHistory.Message = "Authenticated successfully.";
                loginHistory.EmployeeId = userDetail.EmployeeId;
                loginHistory.Role = rolename;

                await _context.LoginHistories.AddAsync(loginHistory);
                await _context.SaveChangesAsync();

                _refreshTokens[user.RefreshToken] = user.Username;

                _logger.LogInformation("Authentication successful for {Username}. Access token and refresh token generated", userDetail.EmailAddress);

                return new Response
                {
                    Status = true,
                    Message = "Authenticated successfully.",
                    StatusCode = HttpStatusCode.OK,
                    Data = user
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during authentication for {Username}", loginDto?.Username);

                loginHistory.Success = false;
                loginHistory.Message = ex.Message;
                await _context.LoginHistories.AddAsync(loginHistory);
                await _context.SaveChangesAsync();

                return new Response
                {
                    Status = false,
                    Message = $"An unexpected error occurred. Please try again later :- {ex.Message}",
                    StatusCode = HttpStatusCode.BadRequest
                };
            }
        }
        private async Task<(string AssignedLocation, bool IsGeofenceEnabled)> GetAssignedLocationAndGeofenceAsync(long employeeId)
        {
            string assignedLocation = string.Empty;
            bool isGeofenceEnabled = false;

            try
            {
                var today = DateTime.UtcNow.Date;

                // 1️⃣ Try: active assigned location for today from AssignLocationHistory
                var assignedLocationInfo = await
                    (from a in _context.AssignLocationHistories
                     join l in _context.tblLocations on a.AssignedLocation equals l.LocationId
                     where a.EmployeeId == employeeId
                           && a.AssignedOnDate <= today
                           && (a.ReleasedOnDate == null || a.ReleasedOnDate >= today)
                           && a.IsActive == true
                     orderby a.AssignLocationHistoryId descending
                     select new
                     {
                         l.STCode,
                         l.IsGeofenceEnabled
                     })
                    .FirstOrDefaultAsync();

                if (assignedLocationInfo != null)
                {
                    // Use assigned location
                    assignedLocation = assignedLocationInfo.STCode ?? string.Empty;
                    isGeofenceEnabled = assignedLocationInfo.IsGeofenceEnabled;
                }
                else
                {
                    // 2️⃣ Fallback: Employee's base location from tblEmployee.LocationId
                    var empLocationInfo = await
                        (from e in _context.tblEmployees
                         join l in _context.tblLocations on e.LocationId equals l.LocationId
                         where e.EmployeeId == employeeId
                         select new
                         {
                             l.STCode,
                             l.IsGeofenceEnabled
                         })
                        .FirstOrDefaultAsync();

                    if (empLocationInfo != null)
                    {
                        assignedLocation = empLocationInfo.STCode ?? string.Empty;
                        isGeofenceEnabled = empLocationInfo.IsGeofenceEnabled;
                    }
                }
            }
            catch (Exception exLoc)
            {
                _logger.LogWarning(exLoc, "Failed to fetch AssignedLocation / IsGeofenceEnabled for EmployeeId {EmployeeId}", employeeId);
                assignedLocation = string.Empty;
                isGeofenceEnabled = false;
            }

            return (assignedLocation, isGeofenceEnabled);
        }


        public bool HasReports(string reportHeadEcode)
        {
            // Get the EmployeeId based on ReportHeadEcode
            var reportingHeadId = _context.tblEmployees
                .Where(e => e.Ecode == reportHeadEcode)
                .Select(a => (int?)a.EmployeeId)
                .FirstOrDefault();

            // Check if any employee reports to this reportingHeadId
            return reportingHeadId.HasValue && _context.tblEmployees
                .Any(e => e.ReportHeadEcode == _context.tblEmployees
                    .Where(emp => emp.EmployeeId == reportingHeadId.Value)
                    .Select(emp => emp.Ecode)
                    .FirstOrDefault());
        }
        // In the method ValidateUser, update all return statements and the tuple construction to handle bool? to bool conversion

        private async Task<(long EmployeeId, string FirstName, string LastName, string EmailAddress, string Role, string Ecode, string ReportHeadEcode, long Reportheadid, string LocationName, string StoreCode, string DepartmentName,string DesignationName,
            DateTime? Joiningdate, bool IsStore, bool IsActive)> ValidateUser(LoginDto loginDto)
        {
            _logger.LogInformation("Validating user with username: {Username}", loginDto.Username);
            if (string.IsNullOrEmpty(loginDto.Username))
            {
                _logger.LogWarning("Validation failed: Username is empty");
                return (0, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, 0, "", "NA", "NA", null, null, false, false);
            }

            // Use READ UNCOMMITTED isolation level (equivalent to NOLOCK) to prevent blocking
            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadUncommitted);
            try
            {
                // First check: Get user for password verification
                var user = await _context.tblEmployees
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.Ecode == loginDto.Username);
                
                if (user == null)
                {
                    _logger.LogWarning("Validation failed: User not found for {Username}", loginDto.Username);
                    await transaction.CommitAsync();
                    return (0, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, 0, "", "NA", "NA", null, null, false, false);
                }

                var verifypassword = BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash);
                if (!verifypassword)
                {
                    _logger.LogWarning("Validation failed: Incorrect password for {Username}", loginDto.Username);
                    await transaction.CommitAsync();
                    return (0, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, 0, "", "NA", "NA", null, null, false, false);
                }

                // Second query: Get full user details with joins
                var result = await (
                    from emp in _context.tblEmployees.AsNoTracking()
                    join empRole in _context.tblEmployeeRoles.AsNoTracking()
                        on emp.EmployeeId equals empRole.EmployeeId into empRoles
                    from empRole in empRoles.DefaultIfEmpty()
                    join role in _context.tblRoles.AsNoTracking()
                        on empRole.RoleId equals role.RoleId into roles
                    from role in roles.DefaultIfEmpty()
                    join reportHead in _context.tblEmployees.AsNoTracking()
                        on emp.ReportHeadEcode equals reportHead.Ecode into reportHeads
                    from reportHead in reportHeads.DefaultIfEmpty()
                    join loc in _context.tblLocations.AsNoTracking()
                        on emp.LocationId equals loc.LocationId into locations
                    from loc in locations.DefaultIfEmpty()
                    join dep in _context.tblDepartments.AsNoTracking()
                        on emp.DepartmentId equals dep.DepartmentId into department
                    from dep in department.DefaultIfEmpty()
                    join des in _context.tblDesignations.AsNoTracking()
                        on emp.DesignationId equals des.DesignationId into designation
                    from des in designation.DefaultIfEmpty()
                    where emp.EMAIL_ADDRESS == loginDto.Username
                       || emp.Ecode == loginDto.Username
                    select new
                    {
                        emp.EmployeeId,
                        emp.FirstName,
                        emp.LastName,
                        emp.EMAIL_ADDRESS,
                        emp.Ecode,
                        RoleName = role != null ? role.RoleName : "Employee",
                        emp.ReportHeadEcode,
                        Reportheadid = reportHead != null ? reportHead.EmployeeId : 0,
                        LocationName = loc != null ? loc.LocationName : "NA",
                        StoreCode = loc != null ? loc.STCode ?? "" : "",
                        DepartmentName = dep != null ? dep.DepartmentName : "NA",
                        DesignationName = des != null ? des.DesignationName : "NA",
                        Joiningdate = emp.JOINING_DATE,
                        emp.IsStore,
                        emp.IsActive
                    }
                ).SingleOrDefaultAsync();

                await transaction.CommitAsync();

                if (result == null)
                {
                    _logger.LogWarning("Validation failed: No valid user data found for {Username}", loginDto.Username);
                    return (0, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, 0, "", "NA", "NA", null, null, false, false);
                }

                _logger.LogInformation("User validated successfully: {Username}", loginDto.Username);
                // Convert bool? to bool using GetValueOrDefault()
                return (result.EmployeeId, result.FirstName, result.LastName, result.EMAIL_ADDRESS, result.RoleName, result.Ecode, result.ReportHeadEcode, result.Reportheadid, result.LocationName, result.StoreCode, result.DepartmentName, result.DesignationName, result.Joiningdate, result.IsStore.GetValueOrDefault(), result.IsActive.GetValueOrDefault());
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error during user validation for {Username}", loginDto.Username);
                throw;
            }


            //if (result == null)
            //{
            //    _logger.LogWarning("Validation failed: No valid user data found for {Username}", loginDto.Username);
            //    return (0, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, 0, "", "NA", "NA" ,null,null, false, false);
            //}

            //_logger.LogInformation("User validated successfully: {Username}", loginDto.Username);
            //// Convert bool? to bool using GetValueOrDefault()
            //return (result.EmployeeId, result.FirstName, result.LastName, result.EMAIL_ADDRESS, result.RoleName, result.Ecode, result.ReportHeadEcode, result.Reportheadid, result.LocationName, result.StoreCode, result.DepartmentName,result.DesignationName, result.Joiningdate, result.IsStore.GetValueOrDefault(), result.IsActive.GetValueOrDefault());
        }

        private async Task<string> GenerateAccessToken(string username, long employeeId, string role, string reportHeadEcode, long reportheadid)
        {
            _logger.LogInformation("Generating access token for {Username}", username);
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]);

                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.Name, username ?? string.Empty),
                        new Claim("EmployeeId", employeeId.ToString()),
                        new Claim("Role", role ?? string.Empty),
                        new Claim("ReportHeadEcode", reportHeadEcode ?? string.Empty),
                        new Claim("ReportHeadId", reportheadid.ToString())
                    }),
                    Expires = DateTime.UtcNow.AddDays(1),
                    Issuer = _configuration["Jwt:Issuer"],
                    Audience = _configuration["Jwt:Audience"],
                    SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256)
                };

                var token = tokenHandler.CreateToken(tokenDescriptor);
                var accessToken = tokenHandler.WriteToken(token);
                _logger.LogInformation("Access token generated successfully for {Username}", username);
                return accessToken;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating access token for {Username}", username);
                throw;
            }
        }

        private async Task<string> GenerateRefreshToken()
        {
            _logger.LogInformation("Generating refresh token");
            try
            {
                var randomNumber = new byte[32];
                using (var rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(randomNumber);
                    var refreshToken = Convert.ToBase64String(randomNumber);
                    _logger.LogInformation("Refresh token generated successfully");
                    return refreshToken;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating refresh token");
                throw;
            }
        }

        public Task<Response> RefreshToken(string refreshToken)
        {
            _logger.LogInformation("RefreshToken method called with token: {RefreshToken}", refreshToken);
            _logger.LogWarning("RefreshToken method not implemented");
            throw new NotImplementedException();
        }

        public async Task<Response> ChangePassword(ChangePasswordDto dto, JwtLoginDetailDto userClaims)
        {
            _logger.LogInformation("Changing password for employee ID: {EmployeeId}", userClaims?.EmployeeId);
            try
            {
                var username = userClaims?.EmployeeId;

                if (string.IsNullOrWhiteSpace(username))
                {
                    _logger.LogWarning("Change password failed: Invalid access token");
                    return new Response
                    {
                        Status = false,
                        Message = "Invalid access token.",
                        StatusCode = HttpStatusCode.Unauthorized
                    };
                }

                if (string.IsNullOrWhiteSpace(dto.OldPassword) || string.IsNullOrWhiteSpace(dto.NewPassword))
                {
                    _logger.LogWarning("Change password failed: Old or new password is empty");
                    return new Response
                    {
                        Status = false,
                        Message = "Old and new passwords are required.",
                        StatusCode = HttpStatusCode.BadRequest
                    };
                }

                if (dto.OldPassword == dto.NewPassword)
                {
                    _logger.LogWarning("Change password failed: New password cannot be the same as old password");
                    return new Response
                    {
                        Status = false,
                        Message = "New password cannot be the same as the old password.",
                        StatusCode = HttpStatusCode.BadRequest
                    };
                }

                var user = await _context.tblEmployees
                    .FirstOrDefaultAsync(u => u.EmployeeId == Convert.ToInt64(username));

                if (user == null)
                {
                    _logger.LogWarning("Change password failed: User not found for employee ID: {EmployeeId}", username);
                    return new Response
                    {
                        Status = false,
                        Message = "User not found.",
                        StatusCode = HttpStatusCode.NotFound
                    };
                }

                var isOldPasswordValid = BCrypt.Net.BCrypt.Verify(dto.OldPassword, user.PasswordHash);
                if (!isOldPasswordValid)
                {
                    _logger.LogWarning("Change password failed: Incorrect old password for employee ID: {EmployeeId}", username);
                    return new Response
                    {
                        Status = false,
                        Message = "Old password is incorrect.",
                        StatusCode = HttpStatusCode.Unauthorized
                    };
                }

                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);

                _context.tblEmployees.Update(user);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Password changed successfully for employee ID: {EmployeeId}", username);
                return new Response
                {
                    Status = true,
                    Message = "Password changed successfully.",
                    StatusCode = HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password for employee ID: {EmployeeId}", userClaims?.EmployeeId);
                return new Response
                {
                    Status = false,
                    Message = "An error occurred while changing the password.",
                    StatusCode = HttpStatusCode.InternalServerError
                };
            }
        }

        public async Task<Response> ForgotPassword(ForgetPasswordDto dto)
        {
            var user = await _context.tblEmployees
           .FirstOrDefaultAsync(u => u.Ecode == dto.ECode && u.DOB.HasValue && u.DOB.Value.Date == dto.DOB.Value.Date);

            if (user == null)
            {
                return new Response
                {
                    Status = false,
                    Message = "Invalid ECode or DOB.",
                    StatusCode = HttpStatusCode.NotFound
                };
            }

            var roleName = _context.tblEmployeeRoles
                .Where(a => a.EmployeeId == user.EmployeeId)
                .Select(a => a.Role.RoleName)
                .FirstOrDefault() ?? "Employee";

            var FullName = _context.tblEmployees
                 .Where(a => a.EmployeeId == user.EmployeeId)
                .Select(a => a.FULL_NAME)
               .FirstOrDefault() ?? "Employee";

            var reportHeadId = 0L;
            if (!string.IsNullOrEmpty(user.ReportHeadEcode))
            {
                var reportHead = await _context.tblEmployees
                    .FirstOrDefaultAsync(a => a.Ecode == user.ReportHeadEcode);
                reportHeadId = reportHead?.EmployeeId ?? 0;
            }


            var accessToken = await GenerateAccessToken(
                user.EMAIL_ADDRESS, user.EmployeeId, roleName, user.ReportHeadEcode, reportHeadId
            );



            _context.PasswordResetTokens.Add(new HRMSAPI.Data.PasswordResetToken
            {
                EmployeeId = user.EmployeeId,
                Token = accessToken,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(1),
                IsUsed = false
            });
            await _context.SaveChangesAsync();


            var resetLink = $"https://v2parivar.v2retail.com:9988/change_password/{accessToken}";
            var subject = "Reset Password";
            var body = $"Dear Mr/Ms {FullName},<br><br>We received your request to change your account password.<br><br>To reset your password, please click the link below (valid for one-time use):<br><br><a href='{resetLink}'>{resetLink}</a><br><br>Best Regards,<br>V2 IT Department";



            if (string.IsNullOrEmpty(user.EMAIL_ADDRESS))
            {
                return new Response
                {
                    Status = false,
                    Message = "User email address is not available.",
                    StatusCode = HttpStatusCode.BadRequest
                };
            }

            try
            {
                await _emailService.SendEmailAsync(
                    new List<string> { user.EMAIL_ADDRESS },
                    null,
                    subject,
                    body
                );
            }
            catch (Exception ex)
            {
                throw new ApplicationException("Failed to send email.", ex);
            }




            return new Response
            {
                Status = true,
                Message = "Password reset link sent to email.",
                StatusCode = HttpStatusCode.OK,
                Data = { }

            };
        }





        public async Task<Response> ResetPassword([FromBody] ResetPasswordDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Token))
            {
                return new Response
                {
                    Status = false,
                    Message = "Token is required.",
                    StatusCode = HttpStatusCode.BadRequest
                };
            }

            var tokenRecord = await _context.PasswordResetTokens
                .FirstOrDefaultAsync(t => t.Token == dto.Token);

            if (tokenRecord == null || tokenRecord.IsUsed == true || tokenRecord.ExpiresAt < DateTime.UtcNow)
            {
                return new Response
                {
                    Status = false,
                    Message = "Link Expired!",
                    StatusCode = HttpStatusCode.BadRequest
                };
            }

            var user = await _context.tblEmployees
                .FirstOrDefaultAsync(u => u.EmployeeId == tokenRecord.EmployeeId);

            if (user == null)
            {
                return new Response
                {
                    Status = false,
                    Message = "User not found.",
                    StatusCode = HttpStatusCode.NotFound
                };
            }


            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);


            tokenRecord.IsUsed = true;
            _context.tblEmployees.Update(user);
            await _context.SaveChangesAsync();

            return new Response
            {
                Status = true,
                Message = "Password has been reset successfully.",
                StatusCode = HttpStatusCode.OK
            };
        }

        public async Task<Response> AdminResetPassword([FromBody] AdminResetPasswordDto dto, JwtLoginDetailDto userClaims)
        {
            try
            {
                if (userClaims == null || string.IsNullOrWhiteSpace(userClaims.EmployeeId))
                {
                    return new Response
                    {
                        Status = false,
                        Message = "User authentication required.",
                        StatusCode = HttpStatusCode.Unauthorized
                    };
                }

                if (dto.EmployeeId == null && string.IsNullOrWhiteSpace(dto.Ecode))
                {
                    return new Response
                    {
                        Status = false,
                        Message = "Either EmployeeId or Ecode is required.",
                        StatusCode = HttpStatusCode.BadRequest
                    };
                }

                tblEmployee user = null;

                if (dto.EmployeeId.HasValue)
                {
                    user = await _context.tblEmployees
                        .FirstOrDefaultAsync(u => u.EmployeeId == dto.EmployeeId.Value);
                }
                else if (!string.IsNullOrWhiteSpace(dto.Ecode))
                {
                    user = await _context.tblEmployees
                        .FirstOrDefaultAsync(u => u.Ecode == dto.Ecode);
                }

                if (user == null)
                {
                    return new Response
                    {
                        Status = false,
                        Message = "User not found.",
                        StatusCode = HttpStatusCode.NotFound
                    };
                }

                const string defaultPassword = "V2@123";
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(defaultPassword);
                user.Password = defaultPassword; // Store plain text password
                user.LastUpdatedBy = userClaims.EmployeeId;
                user.UpdatedBy = userClaims.EmployeeId;
                user.UpdatedOn = DateTime.UtcNow;

                _context.tblEmployees.Update(user);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Password reset to default for EmployeeId: {EmployeeId}, Ecode: {Ecode} by Admin: {AdminEmployeeId}", 
                    user.EmployeeId, user.Ecode, userClaims.EmployeeId);

                return new Response
                {
                    Status = true,
                    Message = "Password has been reset to default successfully.",
                    StatusCode = HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during admin reset password");
                return new Response
                {
                    Status = false,
                    Message = $"An error occurred: {ex.Message}",
                    StatusCode = HttpStatusCode.InternalServerError
                };
            }
        }

        #region rbac
        //public async Task<Response> CreateOrUpdatePermission(List<RoleComponentPermissionDto> dtos, JwtLoginDetailDto user)
        //{
        //    _logger.LogInformation("Attempting to create/update {Count} permissions for EmployeeId: {EmployeeId}", dtos?.Count ?? 0, user.EmployeeId);

        //    try
        //    {
        //        if (dtos == null || !dtos.Any())
        //        {
        //            _logger.LogWarning("Invalid input: No permissions provided.");
        //            return new Response
        //            {
        //                Status = false,
        //                Message = "At least one permission must be provided.",
        //                StatusCode = HttpStatusCode.BadRequest
        //            };
        //        }

        //        var errors = new List<string>();
        //        var createdCount = 0;
        //        var updatedCount = 0;

        //        using var transaction = await _context.Database.BeginTransactionAsync();

        //        foreach (var dto in dtos)
        //        {
        //            // Validate input
        //            if (string.IsNullOrWhiteSpace(dto.ComponentName) 
        //                //|| (!dto.IsRead && !dto.IsWrite)
        //                )
        //            {
        //                _logger.LogWarning("Invalid input for RoleId: {RoleId}, Component: {ComponentName}. Skipping.", dto.RoleId, dto.ComponentName);
        //                errors.Add($"Invalid input for RoleId: {dto.RoleId}, Component: {dto.ComponentName}. ComponentName and at least one permission (IsRead or IsWrite) are required.");
        //                continue;
        //            }

        //            // Check if role exists
        //            var roleExists = await _context.tblRoles.AnyAsync(r => r.RoleId == dto.RoleId);
        //            if (!roleExists)
        //            {
        //                _logger.LogWarning("Role with RoleId {RoleId} not found. Skipping.", dto.RoleId);
        //                errors.Add($"Role with ID {dto.RoleId} not found.");
        //                continue;
        //            }

        //            // Check if permission already exists
        //            var existingPermission = await _context.tblRoleComponentPermissions
        //                .FirstOrDefaultAsync(p => p.RoleId == dto.RoleId && p.ComponentName == dto.ComponentName);

        //            if (existingPermission == null)
        //            {
        //                // Create new permission
        //                var newPermission = new tblRoleComponentPermission
        //                {
        //                    RoleId = dto.RoleId,
        //                    ComponentName = dto.ComponentName,
        //                    IsRead = dto.IsRead,
        //                    IsWrite = dto.IsWrite,
        //                    CreatedBy = user.EmployeeId.ToString(),
        //                    CreatedOn = DateTime.UtcNow
        //                };

        //                _context.tblRoleComponentPermissions.Add(newPermission);
        //                _logger.LogInformation("Creating new permission for RoleId: {RoleId}, Component: {ComponentName}", dto.RoleId, dto.ComponentName);
        //                createdCount++;
        //            }
        //            else
        //            {
        //                // Update existing permission
        //                existingPermission.IsRead = dto.IsRead;
        //                existingPermission.IsWrite = dto.IsWrite;
        //                existingPermission.UpdatedBy = user.EmployeeId.ToString();
        //                existingPermission.UpdatedOn = DateTime.UtcNow;
        //                _logger.LogInformation("Updating permission for RoleId: {RoleId}, Component: {ComponentName}", dto.RoleId, dto.ComponentName);
        //                updatedCount++;
        //            }
        //        }

        //        if (errors.Any())
        //        {
        //            await transaction.RollbackAsync();
        //            return new Response
        //            {
        //                Status = false,
        //                Message = $"Failed to process some permissions: {string.Join("; ", errors)}",
        //                StatusCode = HttpStatusCode.BadRequest
        //            };
        //        }

        //        await _context.SaveChangesAsync();
        //        await transaction.CommitAsync();

        //        return new Response
        //        {
        //            Status = true,
        //            Message = $"Processed {dtos.Count} permissions: {createdCount} created, {updatedCount} updated.",
        //            StatusCode = HttpStatusCode.OK
        //        };
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error creating/updating permissions for EmployeeId: {EmployeeId}", user.EmployeeId);
        //        return new Response
        //        {
        //            Status = false,
        //            Message = "An unexpected error occurred. Please try again later.",
        //            StatusCode = HttpStatusCode.InternalServerError
        //        };
        //    }
        //}

        //public async Task<Response> GetPermissions(int? roleId = null)
        //{
        //    _logger.LogInformation("Fetching permissions for RoleId: {RoleId}", roleId.HasValue ? roleId.ToString() : "All");

        //    try
        //    {
        //        IQueryable<tblRoleComponentPermission> query = _context.tblRoleComponentPermissions;

        //        if (roleId.HasValue)
        //        {
        //            query = query.Where(p => p.RoleId == roleId.Value);
        //        }

        //        var permissions = await query
        //            .Select(p => new RoleComponentPermissionResponseDto
        //            {
        //                RoleComponentId = p.RoleComponentId,
        //                RoleId = p.RoleId,
        //                RoleName = _context.tblRoles
        //                    .Where(r => r.RoleId == p.RoleId)
        //                    .Select(r => r.RoleName)
        //                    .FirstOrDefault(),
        //                ComponentName = p.ComponentName,
        //                IsRead = p.IsRead,
        //                IsWrite = p.IsWrite,
        //                CreatedOn = p.CreatedOn,
        //                CreatedBy = p.CreatedBy,
        //                UpdatedOn = p.UpdatedOn,
        //                UpdatedBy = p.UpdatedBy
        //            })
        //            .ToListAsync();

        //        if (!permissions.Any() && roleId.HasValue)
        //        {
        //            _logger.LogWarning("No permissions found for RoleId: {RoleId}", roleId);
        //            return new Response
        //            {
        //                Status = false,
        //                Message = $"No permissions found for RoleId {roleId}.",
        //                StatusCode = HttpStatusCode.NotFound
        //            };
        //        }

        //        _logger.LogInformation("Retrieved {Count} permissions", permissions.Count);
        //        return new Response
        //        {
        //            Status = true,
        //            Message = "Permissions retrieved successfully.",
        //            StatusCode = HttpStatusCode.OK,
        //            Data = permissions
        //        };
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error retrieving permissions for RoleId: {RoleId}", roleId.HasValue ? roleId.ToString() : "All");
        //        return new Response
        //        {
        //            Status = false,
        //            Message = "An unexpected error occurred. Please try again later.",
        //            StatusCode = HttpStatusCode.InternalServerError
        //        };
        //    }
        //}
        #endregion

        #region 
        public async Task<FetchAndResponse> GetRoleList()
        {
            try {
                var data = await _context.tblRoles.AsQueryable().Where(row => row.IsActive == true).ToListAsync();
                if (data == null || data.Count < 1)
                    throw new Exception("No Roles Found");
                return new FetchAndResponse { 
                    Status = true,
                    Message = "Fetched Successfully",
                    Data = data,
                    Code = HttpStatusCode.OK
                };
            }
            catch (Exception ex) {
                return new FetchAndResponse { 
                    Status = false,
                    Message = ex.Message,
                    Data = ex.Data,
                    Code = HttpStatusCode.BadRequest
                };
            }
        }
        #endregion

        #region Refresh Permissions
        public async Task<FetchAndResponse> RefreshPermissions(int userId)
        {
            try
            {
                // Get user's role from employee roles table
                var userRole = await _context.tblEmployeeRoles
                    .AsQueryable()
                    .Where(er => er.EmployeeId == userId)
                    .Select(er => new { er.RoleId, RoleName = er.Role.RoleName })
                    .FirstOrDefaultAsync();

                if (userRole == null)
                {
                    return new FetchAndResponse
                    {
                        Status = false,
                        Message = "User has no assigned role",
                        Code = HttpStatusCode.BadRequest
                    };
                }

                // Get RBAC hierarchy data for the specific role using the view
                var flatData = await _context.vw_RBACHierarchies
                    .AsQueryable()
                    .Where(v => v.RoleId == userRole.RoleId)
                    .ToListAsync();

                if (!flatData.Any())
                {
                    return new FetchAndResponse
                    {
                        Status = false,
                        Message = "No permissions found for user role",
                        Code = HttpStatusCode.BadRequest
                    };
                }

                // Build the hierarchy structure using the same pattern as GetRbacHierarchyAsync
                var result = flatData
                    .GroupBy(r => new { r.RoleId, r.RoleName })
                    .Select(roleGrp => new
                    {
                        roleGrp.Key.RoleId,
                        roleGrp.Key.RoleName,
                        modules = roleGrp
                            .Where(m => m.ModuleRefId != null && m.ModuleRefId > 0)
                            .GroupBy(m => new { m.ModuleId, m.ModuleRefId, m.ModuleName, m.ModuleStatus })
                            .Select(modGrp => new
                            {
                                moduleId = modGrp.Key.ModuleId,
                                moduleRefId = modGrp.Key.ModuleRefId,
                                moduleName = modGrp.Key.ModuleName,
                                moduleStatus = modGrp.Key.ModuleStatus,
                                subModules = modGrp
                                    .Where(sm => sm.SubModuleRefId != null && sm.SubModuleRefId > 0)
                                    .GroupBy(sm => new { sm.SubModuleId, sm.SubModuleRefId, sm.SubModuleName, sm.SubModuleStatus })
                                    .Select(subGrp => new
                                    {
                                        subModuleId = subGrp.Key.SubModuleId,
                                        subModuleRefId = subGrp.Key.SubModuleRefId,
                                        subModuleName = subGrp.Key.SubModuleName,
                                        subModuleStatus = subGrp.Key.SubModuleStatus,
                                        actions = subGrp
                                            .Where(a => a.ActionRefId != null && a.ActionRefId > 0)
                                            .GroupBy(a => new { a.ActionId, a.ActionRefId, a.ActionName, a.ActionStatus })
                                            .Select(actGrp => new
                                            {
                                                actionId = actGrp.Key.ActionId,
                                                actionRefId = actGrp.Key.ActionRefId,
                                                actionName = actGrp.Key.ActionName,
                                                actionStatus = actGrp.Key.ActionStatus,
                                                furtherParts = actGrp
                                                    .Where(fp => fp.ActionFurtherPartRefId != null && fp.ActionFurtherPartRefId > 0)
                                                    .GroupBy(fp => new { fp.ActionFurtherPartId, fp.ActionFurtherPartRefId, fp.ActionFurtherPartName, fp.FurtherPartStatus })
                                                    .Select(fp => new
                                                    {
                                                        actionFurtherPartId = fp.Key.ActionFurtherPartId,
                                                        actionFurtherPartRefId = fp.Key.ActionFurtherPartRefId,
                                                        actionFurtherPartName = fp.Key.ActionFurtherPartName,
                                                        furtherPartStatus = fp.Key.FurtherPartStatus
                                                    })
                                                    .ToList()
                                            })
                                            .ToList()
                                    })
                                    .ToList()
                            })
                            .ToList()
                    })
                    .FirstOrDefault();

                if (result == null)
                {
                    return new FetchAndResponse
                    {
                        Status = false,
                        Message = "Failed to build permission hierarchy",
                        Code = HttpStatusCode.BadRequest
                    };
                }

                return new FetchAndResponse
                {
                    Status = true,
                    Message = "Permissions refreshed successfully",
                    Data = new { permissions = new List<object> { result } },
                    Code = HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing permissions for user {UserId}", userId);
                return new FetchAndResponse
                {
                    Status = false,
                    Message = ex.Message,
                    Code = HttpStatusCode.InternalServerError
                };
            }
        }
        #endregion
    }
}