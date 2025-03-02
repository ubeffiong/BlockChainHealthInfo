using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlockChainHealthInfo.DigitalSignatureManagement
{
    public class KeyRotationService : BackgroundService
    {
        private readonly IKeyStorage _keyStorage;
        private readonly IDigitalSignatureService _digitalSignature;
        private readonly ILogger<KeyRotationService> _logger;
        private readonly TimeSpan _shiftInterval = TimeSpan.FromHours(2);
        private readonly TimeSpan _regenerateInterval = TimeSpan.FromHours(24);
        private readonly TimeSpan _overlapWindow = TimeSpan.FromHours(1);

        public KeyRotationService(
            IKeyStorage keyStorage,
            IDigitalSignatureService digitalSignature,
            ILogger<KeyRotationService> logger)
        {
            _keyStorage = keyStorage;
            _digitalSignature = digitalSignature;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Key Rotation Service starting...");

            // Initial delay to prevent immediate execution on startup
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            var lastShiftTime = DateTime.UtcNow;
            var lastRegenerateTime = DateTime.UtcNow;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var utcNow = DateTime.UtcNow;

                    // Handle key shifting every 2 hours
                    if (utcNow - lastShiftTime >= _shiftInterval)
                    {
                        _logger.LogInformation("Initiating scheduled key shift...");
                        await _digitalSignature.ShiftSignatureKeys();
                        lastShiftTime = DateTime.UtcNow;
                    }

                    // Handle full regeneration every 24 hours
                    if (utcNow - lastRegenerateTime >= _regenerateInterval)
                    {
                        _logger.LogInformation("Initiating full key regeneration...");
                        await _digitalSignature.RegenerateKeys();
                        lastRegenerateTime = DateTime.UtcNow;

                        // Schedule old key cleanup after overlap window
                        _ = Task.Delay(_overlapWindow, stoppingToken)
                            .ContinueWith(async _ =>
                            {
                                _logger.LogInformation("Cleaning up old keys...");
                                await _keyStorage.RemoveOldKeys();
                            }, stoppingToken);
                    }

                    // Check every minute for rotation conditions
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    // Service is stopping
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Key rotation error");
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
            }

            _logger.LogInformation("Key Rotation Service stopping...");
        }
    }


    public class KeyVersioned
    {
        public byte[] Key { get; }
        public DateTime Timestamp { get; }
        public object Version { get; internal set; }

        public KeyVersioned(byte[] key, DateTime timestamp, object version)
        {
            Key = key;
            Timestamp = timestamp;
            Version = version;
        }
    }

}
