using NLog;
using System.IO;
using System.Text;

namespace HRMSAPI.Middlewares
{
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;

        public RequestLoggingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            var endpoint = context.GetEndpoint();
            var controllerName = endpoint?.Metadata
                .OfType<Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor>()
                .FirstOrDefault()?.ControllerName ?? "UnknownController";

            var logger = LogManager.GetLogger(controllerName);

            try
            {
                // ---- Log Request ----
                context.Request.EnableBuffering();
                string requestBody = "";
                if (context.Request.ContentLength > 0)
                {
                    using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true))
                    {
                        requestBody = await reader.ReadToEndAsync();
                        context.Request.Body.Position = 0; // Reset position so controller can read it
                    }
                }
                logger.Info($"Request Started | Path: {context.Request.Path}, Method: {context.Request.Method}, Body: {requestBody}");

                // ---- Capture Response ----
                var originalResponseBody = context.Response.Body;
                using (var newResponseBody = new MemoryStream())
                {
                    context.Response.Body = newResponseBody;

                    await _next(context); // Call the next middleware

                    // Read response
                    newResponseBody.Seek(0, SeekOrigin.Begin);
                    var responseBodyText = await new StreamReader(newResponseBody).ReadToEndAsync();
                    newResponseBody.Seek(0, SeekOrigin.Begin);

                    logger.Info($"Request Completed | StatusCode: {context.Response.StatusCode}, Response Body: {responseBodyText}");

                    await newResponseBody.CopyToAsync(originalResponseBody);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Request Failed | Path: {context.Request.Path}");
                throw;
            }
        }
    }
}
