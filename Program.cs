using HRMSAPI.Extensions;
using HRMSAPI.Hubs;
using HRMSAPI.Middlewares;
using HRMSAPI.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
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