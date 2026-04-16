using HRMSAPI.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HRMSAPI.Utility
{
    public class ScheduledShiftApplicationService : IHostedService, IDisposable
    {
        private Timer? _timer;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<ScheduledShiftApplicationService> _logger;
        private readonly SemaphoreSlim _executionLock = new(1, 1);
        private CancellationTokenSource? _cts;
        private bool _isDisposed;

        // 4 AM in local time
        private const int TARGET_HOUR = 4;
        private const int TARGET_MINUTE = 0;

        public ScheduledShiftApplicationService(
            IServiceScopeFactory serviceScopeFactory,
            ILogger<ScheduledShiftApplicationService> logger)
        {
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("ScheduledShiftApplicationService starting...");

            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // Calculate delay to next 4 AM
            var dueTime = GetDelayToNext4AM();
            var period = TimeSpan.FromDays(1); // Run daily

            _logger.LogInformation("ScheduledShiftApplicationService scheduled. First run in {Delay}, then daily at 4:00 AM.",
                dueTime);

            _timer = new Timer(
                TimerCallback,
                null,
                dueTime,
                period
            );

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("ScheduledShiftApplicationService is stopping.");

            try { _cts?.Cancel(); } catch { /* ignore */ }

            _timer?.Change(Timeout.Infinite, Timeout.Infinite);
            return Task.CompletedTask;
        }

        private void TimerCallback(object? state)
        {
            // Timer doesn't await async callbacks; run safely in background.
            _ = ExecuteTaskWithLock(state, _cts?.Token ?? CancellationToken.None);
        }

        private static TimeSpan GetDelayToNext4AM()
        {
            var now = DateTime.Now; // Use local time
            var targetToday = new DateTime(now.Year, now.Month, now.Day, TARGET_HOUR, TARGET_MINUTE, 0, DateTimeKind.Local);

            // If 4 AM today has already passed, schedule for tomorrow
            if (now >= targetToday)
            {
                targetToday = targetToday.AddDays(1);
            }

            var delay = targetToday - now;
            return delay < TimeSpan.Zero ? TimeSpan.Zero : delay;
        }

        private async Task ExecuteTaskWithLock(object? state, CancellationToken ct)
        {
            if (!await _executionLock.WaitAsync(0, ct))
            {
                _logger.LogWarning("Previous execution is still running, skipping this iteration.");
                return;
            }

            try
            {
                await ApplyScheduledShifts(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // normal shutdown
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in ExecuteTaskWithLock");
            }
            finally
            {
                _executionLock.Release();
            }
        }

        private async Task ApplyScheduledShifts(CancellationToken ct)
        {
            var now = DateTime.Now;
            _logger.LogInformation("ApplyScheduledShifts triggered at {TimeLocal}", now);

            using var scope = _serviceScopeFactory.CreateScope();
            var shiftMapService = scope.ServiceProvider.GetRequiredService<IShiftMapService>();

            try
            {
                var (success, message) = await shiftMapService.ApplyScheduledShiftsAsync();

                if (success)
                {
                    _logger.LogInformation("Scheduled shifts applied successfully at {TimeLocal}. Message: {Message}", now, message);
                }
                else
                {
                    _logger.LogError("Failed to apply scheduled shifts at {TimeLocal}. Message: {Message}", now, message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying scheduled shifts at {TimeLocal}", now);
            }
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

