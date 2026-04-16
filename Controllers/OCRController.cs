using HRMSAPI.DTO;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using static Org.BouncyCastle.Math.EC.ECCurve;

namespace HRMSAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OCRController : ControllerBase
    {
        private readonly ILogger<OCRController> _logger;
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _config;

        public OCRController(ILogger<OCRController> logger, IWebHostEnvironment env,IConfiguration config)
        {
            _logger = logger;
            _env = env;
            _config = config;
        }
        [HttpPost("extract")]
        public async Task<ActionResult<OCRResult>> ExtractData(
            [FromForm]FileDTO obj,
            [FromForm] string columns)
        {
            try
            {
                // Validate input
                if (obj.File == null || obj.File.Length == 0)
                {
                    return BadRequest(new OCRResult { Error = "No file uploaded" });
                }

                if (string.IsNullOrWhiteSpace(columns))
                {
                    return BadRequest(new OCRResult { Error = "Columns parameter is required" });
                }

                // Validate file type
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".gif", ".pdf", ".docx" };
                var fileExtension = Path.GetExtension(obj.File.FileName).ToLower();

                if (!allowedExtensions.Contains(fileExtension))
                {
                    return BadRequest(new OCRResult
                    {
                        Error = $"Unsupported file type: {fileExtension}. Supported types: {string.Join(", ", allowedExtensions)}"
                    });
                }

                // Save uploaded file temporarily
                var tempFileName = Path.GetTempFileName() + fileExtension;
                using (var stream = new FileStream(tempFileName, FileMode.Create))
                {
                    await obj.File.CopyToAsync(stream);
                }

                try
                {
                    // Call Python OCR
                    var result = await CallPythonOCR(tempFileName, columns);
                    return Ok(result);
                }
                finally
                {
                    // Clean up temporary file
                    if (System.IO.File.Exists(tempFileName))
                    {
                        System.IO.File.Delete(tempFileName);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing OCR request");
                return StatusCode(500, new OCRResult { Error = $"Internal server error: {ex.Message}" });
            }
        }

        [HttpGet("health")]
        public IActionResult Health()
        {
            return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
        }

        [HttpPost("extract-from-path")]
        public async Task<ActionResult<OCRResult>> ExtractFromPath(
            [FromBody] ExtractFromPathRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.FilePath))
                {
                    return BadRequest(new OCRResult { Error = "FilePath is required" });
                }

                if (string.IsNullOrWhiteSpace(request.Columns))
                {
                    return BadRequest(new OCRResult { Error = "Columns is required" });
                }

                if (!System.IO.File.Exists(request.FilePath))
                {
                    return NotFound(new OCRResult { Error = $"File not found: {request.FilePath}" });
                }

                var result = await CallPythonOCR(request.FilePath, request.Columns);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing OCR request from path");
                return StatusCode(500, new OCRResult { Error = $"Internal server error: {ex.Message}" });
            }
        }

        private async Task<string> CallPythonOCR(string filePath, string columns)
        {
            var tempOutputFile = Path.GetTempFileName() + ".json";
            var scriptPath = Path.Combine(_env.ContentRootPath, "ocr_cli.py");
            // Resolve the absolute path to your script
            // var scriptPath = Path.Combine(_env.ContentRootPath, "Scripts", "ocr_cli.py");

            if (!System.IO.File.Exists(scriptPath))
            {
                return ErrorJson($"Python script not found at: {scriptPath}");
            }

            string pythonExe;
            try
            {
                pythonExe = ResolvePythonExe();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Python resolution failed");
                return ErrorJson(ex.Message);
            }

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = pythonExe,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(scriptPath)!
                }
            };

            // Safer than string-quoted Arguments
            process.StartInfo.ArgumentList.Add(scriptPath);
            process.StartInfo.ArgumentList.Add(filePath);
            process.StartInfo.ArgumentList.Add(columns);
            process.StartInfo.ArgumentList.Add(tempOutputFile);

            try
            {
                process.Start();

                // Wait up to 5 minutes
#if NET7_0_OR_GREATER
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
                try
                {
                    await process.WaitForExitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
                    return ErrorJson("OCR process timed out");
                }
#else
        var completed = process.WaitForExit(300_000);
        if (!completed)
        {
            try { process.Kill(); } catch { /* ignore */ }
            return ErrorJson("OCR process timed out");
        }
#endif

                if (process.ExitCode != 0)
                {
                    var stderr = await process.StandardError.ReadToEndAsync();
                    // Fallback to stdout if stderr empty (some scripts write errors to stdout)
                    if (string.IsNullOrWhiteSpace(stderr))
                        stderr = await process.StandardOutput.ReadToEndAsync();

                    return ErrorJson($"Python script failed: {stderr?.Trim()}");
                }

                string jsonResult;
                if (System.IO.File.Exists(tempOutputFile))
                {
                    jsonResult = await System.IO.File.ReadAllTextAsync(tempOutputFile);
                }
                else
                {
                    // If the script didn't write a file, read stdout
                    jsonResult = await process.StandardOutput.ReadToEndAsync();
                }

                if (string.IsNullOrWhiteSpace(jsonResult))
                {
                    return ErrorJson("Empty result from OCR script");
                }

                return jsonResult;
            }
            finally
            {
                try
                {
                    if (System.IO.File.Exists(tempOutputFile))
                        System.IO.File.Delete(tempOutputFile);
                }
                catch { /* ignore cleanup errors */ }
            }
        }

        private static string ErrorJson(string message) =>
            JsonSerializer.Serialize(new { error = message });

        private string ResolvePythonExe()
        {
            var configured = _config["Python:ExePath"];
            if (!string.IsNullOrWhiteSpace(configured))
            {
                // If absolute path, ensure it exists
                if (configured.Contains(Path.DirectorySeparatorChar) || configured.Contains(Path.AltDirectorySeparatorChar))
                {
                    if (!System.IO.File.Exists(configured))
                        throw new FileNotFoundException($"Configured Python executable not found at '{configured}'.");
                    return configured;
                }
                // else fall through; it’s a name like "python3"
                return configured;
            }

            // Fallbacks if not configured
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "python"; // requires PATH in prod; strongly prefer configuring absolute path
            return "python3";
        }
    }
    public class ExtractFromPathRequest
    {
        public string FilePath { get; set; }
        public string Columns { get; set; }
    }

    public class OCRResult
    {
        public string FilePath { get; set; }
        public int TotalPages { get; set; }
        public ExtractedData[] ExtractedData { get; set; }
        public string Error { get; set; }
    }

    public class ExtractedData
    {
        public int PageNumber { get; set; }
        public System.Collections.Generic.Dictionary<string, object> Data { get; set; }
        public string Error { get; set; }
    }
}
