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

                // File downloads / exports: DO NOT buffer or log the body. Buffering re-holds the whole
                // file in memory (defeats streaming, causes spikes/failures on large files) and logging
                // binary bloats the logs. Stream these straight through to the client.
                var path = context.Request.Path.Value ?? "";
                bool isDownload = path.Contains("download", StringComparison.OrdinalIgnoreCase)
                                  || path.Contains("export", StringComparison.OrdinalIgnoreCase)
                                  || path.Contains("excel", StringComparison.OrdinalIgnoreCase);

                if (isDownload)
                {
                    await _next(context);
                    logger.Info($"Request Completed | StatusCode: {context.Response.StatusCode} (file download; body not logged)");
                    return;
                }

                // ---- Capture Response (JSON/text APIs only; capped) ----
                var originalResponseBody = context.Response.Body;
                using (var newResponseBody = new MemoryStream())
                {
                    context.Response.Body = newResponseBody;

                    await _next(context); // Call the next middleware

                    newResponseBody.Seek(0, SeekOrigin.Begin);
                    var contentType = context.Response.ContentType ?? "";
                    string responseBodyText;
                    if ((contentType.Contains("json") || contentType.Contains("text") || contentType == "")
                        && newResponseBody.Length <= 200_000)
                    {
                        responseBodyText = await new StreamReader(newResponseBody).ReadToEndAsync();
                        newResponseBody.Seek(0, SeekOrigin.Begin);
                    }
                    else
                    {
                        responseBodyText = $"[{newResponseBody.Length} bytes, {contentType}]";
                    }

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
