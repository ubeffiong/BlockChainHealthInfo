using Hl7.Fhir.Model;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlockChainHealthInfo.DigitalSignatureManagement
{
    public class SignatureValidationInterceptor : SaveChangesInterceptor
    {
        private readonly IDigitalSignatureService _signatureService;
        private readonly ILogger<SignatureValidationInterceptor> _logger;

        public SignatureValidationInterceptor(
            IDigitalSignatureService signatureService,
            ILogger<SignatureValidationInterceptor> logger)
        {
            _signatureService = signatureService;
            _logger = logger;
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
    DbContextEventData eventData,
    InterceptionResult<int> result,
    CancellationToken cancellationToken = default)
        {
            var context = eventData.Context;
            if (context == null) return base.SavingChangesAsync(eventData, result, cancellationToken);

            foreach (var entry in context.ChangeTracker.Entries())
            {
                if (entry.Entity is DbPatient patient)
                {
                    try
                    {
                        // Use the stored signed data blob and snapshot version
                        if (patient.SignedDataBlob == null || patient.Signature == null || patient.SnapshotVersion == null)
                        {
                            throw new InvalidOperationException("Missing signature data");
                        }

                        if (!_signatureService.VerifySignature(
                            patient.SignedDataBlob,
                            Convert.FromBase64String(patient.Signature),
                            patient.SnapshotVersion))
                        {
                            _logger.LogError("Signature validation failed for patient {Id}", patient.Id);
                            throw new InvalidOperationException("Invalid signature");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Signature validation error for patient {Id}", patient.Id);
                        throw;
                    }
                }
            }
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

}
