using HRMSAPI.Controllers;
using HRMSAPI.Data;
using HRMSAPI.Implementation;
using HRMSAPI.Interfaces;
using HRMSAPI.Services;
using HRMSAPI.Utility;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using NLog.Extensions.Logging;
using SuzukiVidms.Infrastructure.Utilities;
using System.Text;

namespace HRMSAPI.Extensions
{
    public static class ServiceRegistrationExtensions
    {
        public static IServiceCollection RegisterServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Controllers
            services.AddControllers();

            // Swagger Configuration
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "HRMS API", Version = "v1" });

                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "JWT Authorization header",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer"
                });

                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });
            });

            // Memory Cache Configuration
            services.AddMemoryCache();
            services.AddHttpContextAccessor();
            // Database Configuration
            services.AddDbContext<HRMSContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

            // JWT Configuration
            var key = Encoding.ASCII.GetBytes(configuration["Jwt:Key"]);
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.RequireHttpsMetadata = false;
                    options.SaveToken = true;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(key),
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidIssuer = configuration["Jwt:Issuer"],
                        ValidAudience = configuration["Jwt:Audience"],
                        ClockSkew = TimeSpan.Zero
                    };
                });

            // Service Registrations
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<ICandidateService, CandidateService>();
            services.AddScoped<ILeaveService, LeaveService>();
            services.AddScoped<ILeaveLockService, LeaveLockService>();
            services.AddScoped<IEmployeeService, EmployeeService>();
            services.AddScoped<IEmployeeServiceNew, EmployeeServiceNew>();
            services.AddScoped<IDropDownService, DropDownService>();
            services.AddHostedService<Backgroundservices>();
            services.AddHostedService<ScheduledShiftApplicationService>();
            services.AddHttpContextAccessor();
            services.Configure<EmailSettings>(configuration.GetSection("EmailSettings"));
            services.AddScoped<IEmailService, EmailService>();
            services.AddScoped<IEmpAttendanceService, EmpAttendanceService>();
            services.AddScoped<IStoreService, StoreService>();
            services.AddHttpClient();
            services.AddSingleton<FaceValidator>();
            services.AddScoped<IJobOpeningService, JobOpeningService>();
            services.AddScoped<PayrollController>();
            services.AddScoped<IPayrollService, PayrollRepository>();
            services.AddScoped<IPaidByBankService, PaidByBankService>();
            services.AddScoped<IBankTransferService, BankTransferService>();
            services.AddScoped<IDCEmployeeService, EmployeeDCService>();
            services.AddScoped<IDDCAttendanceService, DCAttendanceService>();
            services.AddScoped<ILocationDesignationPolicyService, LocationDesignationPolicyService>();
            services.AddScoped<ILocationDesignationWeeklyOffHolidayMasterService, LocationDesignationWeeklyOffHolidayMasterService>();
            services.AddScoped<IEmployeeSalaryAddOnsService, EmployeeSalaryAddOnsService>();
            services.AddScoped<IEmployeeDeductionService, EmployeeDeductionService>();
            services.AddScoped<IEmployeeSeparationService, EmployeeSeparationService>();
            services.AddScoped<IEmployeeStoreVisibilityMappingService, EmployeeStoreVisibilityMappingService>();
            //services.AddScoped<IEmployeeLeavesService, EmployeeLeavesService>();
            services.AddScoped<IReturnByBankService, ReturnByBankService>();
            services.AddScoped<IViewService, ViewsService>();
            services.AddScoped<ILocationService, LocationService>();
            services.AddScoped<IBgtSeatMasterService, BgtSeatMasterService>();
            services.AddScoped<IBgtSeatAssignmentService, BgtSeatAssignmentService>();
            services.AddScoped<IUploaderService, UploaderService>();
            services.AddScoped<IAssignLocationService, AssignLocationService>();
            services.AddScoped<IShiftMapService, ShiftMapService>();
            services.AddScoped<IStoreRoutingService, StoreRoutingService>();
            services.AddScoped<IPaidInCashService, PaidInCashService>();
            services.AddScoped<ISalaryRecalculate, SalaryRecalculateRepository>();
            services.AddScoped<IRBACService, RBACService>();
            services.AddScoped<IPageAccessService, PageAccessService>();
            services.AddScoped<IPermissionNotificationService, PermissionNotificationService>();
            services.AddScoped<IGroupService, GroupService>();
            services.AddScoped<IGroupWiseStoreCodeMappingService, GroupWiseStoreCodeMappingService>();
            services.AddScoped<IHolidayMasterService, HolidayMasterService>();
            services.AddScoped<IEmployeeRoleService, EmployeeRoleService>();
            services.AddScoped<IJDService, JDService>();
            services.AddScoped<IShiftMasterService, ShiftMasterService>();
            services.AddScoped<IFnfService, FnfService>();
            services.AddScoped<IGeoService, GeoService>();
            services.AddScoped<IIncentiveService, IncentiveService>();
            services.AddScoped<INetPaybleBatchService, NetPaybleBatchService>();
            services.AddScoped<IEmpAttendanceViewSnapshotService, EmpAttendanceViewSnapshotService>();
            services.AddScoped<IMinWageService, MinWageService>();
            services.AddScoped<IEmployeeChangeLogService, EmployeeChangeLogService>();
            services.AddScoped<IEmployeeMultiPunchesChangeLogService, EmployeeMultiPunchesChangeLogService>();
            services.AddScoped<IEcodeWiseWeekOffMappingService, EcodeWiseWeekOffMappingService>();
            services.AddScoped<IEcodeWiseBonusProvisioningPolicyMappingService, EcodeWiseBonusProvisioningPolicyMappingService>();
            services.AddScoped<IRetentionService, RetentionService>();
            services.AddScoped<IApplicantUploadService, ApplicantUploadService>();
            services.AddScoped<IAttendanceRegularizationService, AttendanceRegularizationService>();
            services.AddScoped<IFnfDetailsService, FnfDetailsService>();
            services.AddScoped<IVendorService, VendorService>();
            services.AddScoped<IOCRservice, OCRservice>();
            services.AddScoped<IBackgroundVerificationService, BackgroundVerificationService>();


            // CORS Configuration
            services.AddCors(options =>
            {
                options.AddPolicy("Default", policy =>
                {
                    policy.SetIsOriginAllowed(origin => true).AllowAnyHeader().AllowAnyMethod().AllowCredentials(); 
                });
            });

            // HttpClient Configuration
            services.AddHttpClient("StoreApiClient", client =>
            {
                client.BaseAddress = new Uri("http://192.168.151.24:8080/");
                client.Timeout = TimeSpan.FromMinutes(5);
            });
            //services.AddLogging(loggingBuilder =>
            //{
            //    loggingBuilder.ClearProviders();
            //    loggingBuilder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
            //    loggingBuilder.AddNLog("nlog.config");
            //    loggingBuilder.AddFilter("System.Net.Http.HttpClient", Microsoft.Extensions.Logging.LogLevel.None);
            //});
            return services;
        }

    }
}