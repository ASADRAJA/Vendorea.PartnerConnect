using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities.Integration;

namespace Vendorea.PartnerConnect.Application.Services;

/// <inheritdoc />
public class IntegrationCredentialStore : IIntegrationCredentialStore
{
    private readonly IIntegrationCredentialRepository _repository;
    private readonly ICredentialProtector _protector;
    private readonly ILogger<IntegrationCredentialStore> _logger;

    public IntegrationCredentialStore(
        IIntegrationCredentialRepository repository,
        ICredentialProtector protector,
        ILogger<IntegrationCredentialStore> logger)
    {
        _repository = repository;
        _protector = protector;
        _logger = logger;
    }

    public async Task PutAsync(
        Guid connectionId, IntegrationCredentialType type, string plaintext,
        DateTime? expiresAt = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plaintext);

        var protectedValue = _protector.Protect(plaintext)
            ?? throw new InvalidOperationException("Protecting the credential returned null.");

        await _repository.UpsertAsync(new IntegrationCredential
        {
            IntegrationConnectionId = connectionId,
            CredentialType = type,
            ProtectedValue = protectedValue,
            ExpiresAt = expiresAt
        }, cancellationToken);

        // Type and connection only. The value, its length and its shape all stay out of the log.
        _logger.LogInformation(
            "Stored {CredentialType} for connection {ConnectionId}", type, connectionId);
    }

    public async Task<string?> GetAsync(
        Guid connectionId, IntegrationCredentialType type, CancellationToken cancellationToken = default)
    {
        var row = await _repository.GetAsync(connectionId, type, cancellationToken);
        if (row is null)
        {
            return null;
        }

        // Strict: a plaintext value here means encryption did not happen when it was written.
        // The lenient Unprotect would hand it back and the integration would keep working, so
        // the defect would never surface.
        return _protector.UnprotectRequired(row.ProtectedValue);
    }

    public async Task RevokeAllAsync(Guid connectionId, CancellationToken cancellationToken = default)
    {
        await _repository.DeleteAllAsync(connectionId, cancellationToken);
        _logger.LogInformation("Destroyed all credentials for connection {ConnectionId}", connectionId);
    }
}
