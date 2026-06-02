using HRMSAPI.Extensions;
using HRMSAPI.Hubs;
using HRMSAPI.Middlewares;
using HRMSAPI.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Kestrel + form limits: medical-card bulk upload accepts a ZIP of ~3000 PDFs
// (~150 MB compressed). Default Kestrel cap is 30 MB which would reject it.
builder.WebHost.ConfigureKestrel(o => { o.Limits.MaxRequestBodySize = 2_147_483_648; }); // 2 GB
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 2_147_483_648; // 2 GB
    o.ValueLengthLimit = int.MaxValue;
});
// Configure Serilog
//Log.Logger = new LoggerConfiguration()
//    .MinimumLevel.Information()
//    .WriteTo.File(
//        path: "logs/app-.log",
//        rollingInterval: RollingInterval.Day, // Creates a new file daily
//        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}")
//    .CreateLogger();

//builder.Host.UseSerilog(); // Use Serilog for logging
// Register services using extension method
builder.Services.RegisterServices(builder.Configuration);

// Add SignalR
builder.Services.AddSignalR();

builder.Services.AddHttpContextAccessor();  // added for testing 

// Build App
var app = builder.Build();

app.UseRouting();
app.UseCors("Default");

app.UseMiddleware<RequestLoggingMiddleware>();

app.UseAuthentication();
app.UseAuthorization();
app.UseStaticFiles();
app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
});

// Map SignalR Hub
app.MapHub<PermissionHub>("/permissionHub");

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "HRMS");
});

string defaultPassword = "NIKHIL@IT@123";
string hashedPassword = BCrypt.Net.BCrypt.HashPassword(defaultPassword);
var s = hashedPassword.ToString();

app.Run();