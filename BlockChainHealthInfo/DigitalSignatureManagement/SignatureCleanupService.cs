using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlockChainHealthInfo.DigitalSignatureManagement
{
    public class SignatureCleanupService : BackgroundService
    {
        private readonly IDigitalSignatureService _digitalSignatureService;
        private readonly ILogger<SignatureCleanupService> _logger;
        private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(30); // Run every 30 minutes
        private readonly List<SignedData> _signatures = new(); // In-memory storage for signatures

        public SignatureCleanupService(IDigitalSignatureService digitalSignatureService, ILogger<SignatureCleanupService> logger)
        {
            _digitalSignatureService = digitalSignatureService;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Running signature cleanup...");
                    CleanupExpiredSignatures();
                    _logger.LogInformation("Signature cleanup completed.");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Signature cleanup failed: {ex.Message}");
                }
                await Task.Delay(_cleanupInterval, stoppingToken);
            }
        }

        private void CleanupExpiredSignatures()
        {
            var expiredSignatures = _signatures.Where(s => IsSignatureExpired(s.Signature)).ToList();

            foreach (var expiredSignature in expiredSignatures)
            {
                try
                {
                    byte[] newSignature = null;//_digitalSignatureService.SignData(expiredSignature.Data, DateTime.UtcNow, TimeSpan.FromHours(1));
                    expiredSignature.Signature = newSignature;
                    expiredSignature.ExpiryTime = DateTime.UtcNow.AddHours(1);
                    _logger.LogInformation($"Replaced expired signature for data: {Encoding.UTF8.GetString(expiredSignature.Data)}");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to replace expired signature: {ex.Message}");
                }
            }

            _signatures.RemoveAll(s => IsSignatureExpired(s.Signature) && s.ExpiryTime < DateTime.UtcNow);
            _logger.LogInformation($"Removed {expiredSignatures.Count} expired signatures.");
        }

        private bool IsSignatureExpired(byte[] signature)
        {
            try
            {
                int expirySize = sizeof(long);
                byte[] expiryBytes = signature.Skip(signature.Length - expirySize).Take(expirySize).ToArray();
                long expiryTicks = BitConverter.ToInt64(expiryBytes, 0);
                DateTime expiryTime = new DateTime(expiryTicks, DateTimeKind.Utc);
                return DateTime.UtcNow > expiryTime;
            }
            catch
            {
                return true;
            }
        }

        private class SignedData
        {
            public byte[] Data { get; set; }
            public byte[] Signature { get; set; }
            public DateTime ExpiryTime { get; set; }
        }


    }
}
