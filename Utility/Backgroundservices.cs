using HRMSAPI.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Data;

namespace HRMSAPI.Utility
{
    public class Backgroundservices : IHostedService, IDisposable
    {
        private const int MAX_RETRY_ATTEMPTS = 3;
        private const int RETRY_DELAY_MS = 1000;

        private Timer? _timer;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<Backgroundservices> _logger;
        private readonly SemaphoreSlim _executionLock = new(1, 1);

        private CancellationTokenSource? _cts;
        private bool _isDisposed;

        public Backgroundservices(
            IServiceScopeFactory serviceScopeFactory,
            ILogger<Backgroundservices> logger,
            IHttpClientFactory httpClientFactory) // keeping your signature as-is
        {
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Backgroundservices starting...");

            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // Align the first run to the NEXT 5-minute boundary, then every 5 minutes.
            var dueTime = GetDelayToNextFiveMinuteBoundaryUtc();
            var period = TimeSpan.FromMinutes(5);

            _logger.LogInformation("Backgroundservices scheduled. First run in {Delay}, then every {Period}.",
                dueTime, period);

            _timer = new Timer(
                TimerCallback,           // IMPORTANT: non-async callback
                null,
                dueTime,
                period
            );

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Backgroundservices is stopping.");

            try { _cts?.Cancel(); } catch { /* ignore */ }

            _timer?.Change(Timeout.Infinite, Timeout.Infinite);
            return Task.CompletedTask;
        }

        private void TimerCallback(object? state)
        {
            // Timer doesn't await async callbacks; run safely in background.
            _ = ExecuteTasksWithLock(state, _cts?.Token ?? CancellationToken.None);
        }

        private static TimeSpan GetDelayToNextFiveMinuteBoundaryUtc()
        {
            var now = DateTime.UtcNow;

            // next boundary at minute 0/5/10/15...
            var next = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, DateTimeKind.Utc);

            int remainder = next.Minute % 5;
            int minutesToAdd = remainder == 0 ? 5 : (5 - remainder);

            next = next.AddMinutes(minutesToAdd);

            var delay = next - now;
            return delay < TimeSpan.Zero ? TimeSpan.Zero : delay;
        }

        private async Task ExecuteTasksWithLock(object? state, CancellationToken ct)
        {
            if (!await _executionLock.WaitAsync(0, ct))
            {
                _logger.LogWarning("Previous execution is still running, skipping this iteration.");
                return;
            }

            try
            {
                await ExecuteTasks(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // normal shutdown
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in ExecuteTasksWithLock");
            }
            finally
            {
                _executionLock.Release();
            }
        }

        private async Task ExecuteTasks(CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            _logger.LogInformation("ExecuteTasks triggered at {TimeUtc}", now);

            // Since the timer is aligned to 5-minute boundary, no need for Minute%5 checks.
            await ExecuteWithRetry(() => RefreshAttendance(ct), "attendance refresh", ct);
            await ExecuteWithRetry(() => RefreshMonthlyPunchesRangeOptimized(ct), "monthly punches range optimized refresh", ct);
        }

        private async Task ExecuteWithRetry(Func<Task> operation, string operationName, CancellationToken ct)
        {
            for (int attempt = 1; attempt <= MAX_RETRY_ATTEMPTS; attempt++)
            {
                try
                {
                    ct.ThrowIfCancellationRequested();
                    await operation();
                    return;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Error during {Operation} (attempt {Attempt}/{MaxAttempts})",
                        operationName, attempt, MAX_RETRY_ATTEMPTS);

                    if (attempt == MAX_RETRY_ATTEMPTS)
                        throw;

                    await Task.Delay(RETRY_DELAY_MS * attempt, ct);
                }
            }
        }

        private async Task RefreshAttendance(CancellationToken ct)
        {
            using var scope = _serviceScopeFactory.CreateScope();

            var attendanceService = scope.ServiceProvider.GetRequiredService<IEmpAttendanceService>();

            // If your method supports CancellationToken, pass ct.
            await attendanceService.FetchAndSavePunchesAsync();

            _logger.LogInformation("Attendance data refreshed successfully at {TimeUtc}", DateTime.UtcNow);
        }

        private async Task RefreshMonthlyPunchesRangeOptimized(CancellationToken ct)
        {
            // SQL equivalent:
            // DECLARE @ToDate DATE = CONVERT(DATE, GETDATE());
            // DECLARE @FromDate DATE = CONVERT(DATE, GETDATE());
            // EXEC dbo.usp_MergeMonthlyPunchesRange_Optimized @FromDate, @ToDate;

            // Set both dates to today to fetch only today's attendance
            var today = DateTime.Today;

            using var scope = _serviceScopeFactory.CreateScope();
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var connStr = configuration.GetConnectionString("DefaultConnection");

            await using var conn = new SqlConnection(connStr);
            await conn.OpenAsync(ct);

            await using var cmd = new SqlCommand("dbo.usp_MergeMonthlyPunchesRange_Optimized", conn)
            {
                CommandType = CommandType.StoredProcedure,
                CommandTimeout = 0
            };

            cmd.Parameters.Add(new SqlParameter("@FromDate", SqlDbType.Date) { Value = today });
            cmd.Parameters.Add(new SqlParameter("@ToDate", SqlDbType.Date) { Value = today });

            // Many stored procedures return result sets; ExecuteNonQuery typically returns -1 in that case.
            var result = await cmd.ExecuteNonQueryAsync(ct);

            _logger.LogInformation(
                "Executed usp_MergeMonthlyPunchesRange_Optimized for today's attendance {Date}. Result={Result}",
                today.ToString("yyyy-MM-dd"),
                result);
        }

        public void Dispose()
        {
            if (_isDisposed) return;

            _timer?.Dispose();
            _executionLock.Dispose();
            _cts?.Dispose();

            _isDisposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
