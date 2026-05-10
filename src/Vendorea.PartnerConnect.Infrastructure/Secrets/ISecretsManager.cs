namespace Vendorea.PartnerConnect.Infrastructure.Secrets;

/// <summary>
/// Interface for managing secrets across different secret stores.
/// </summary>
public interface ISecretsManager
{
    /// <summary>
    /// Gets a secret value by name.
    /// </summary>
    /// <param name="secretName">The name of the secret.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The secret value, or null if not found.</returns>
    Task<string?> GetSecretAsync(string secretName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a secret value with a specific version.
    /// </summary>
    /// <param name="secretName">The name of the secret.</param>
    /// <param name="version">The version of the secret.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The secret value, or null if not found.</returns>
    Task<string?> GetSecretAsync(string secretName, string version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a secret value.
    /// </summary>
    /// <param name="secretName">The name of the secret.</param>
    /// <param name="value">The secret value.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetSecretAsync(string secretName, string value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a secret value with metadata.
    /// </summary>
    /// <param name="secretName">The name of the secret.</param>
    /// <param name="value">The secret value.</param>
    /// <param name="metadata">Additional metadata for the secret.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetSecretAsync(string secretName, string value, SecretMetadata metadata, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a secret.
    /// </summary>
    /// <param name="secretName">The name of the secret.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteSecretAsync(string secretName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a secret exists.
    /// </summary>
    /// <param name="secretName">The name of the secret.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the secret exists, false otherwise.</returns>
    Task<bool> SecretExistsAsync(string secretName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all secret names.
    /// </summary>
    /// <param name="prefix">Optional prefix to filter secrets.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of secret names.</returns>
    Task<IReadOnlyList<string>> ListSecretsAsync(string? prefix = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets secret metadata without retrieving the value.
    /// </summary>
    /// <param name="secretName">The name of the secret.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The secret metadata, or null if not found.</returns>
    Task<SecretMetadata?> GetSecretMetadataAsync(string secretName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Metadata associated with a secret.
/// </summary>
public class SecretMetadata
{
    /// <summary>
    /// The name of the secret.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// When the secret was created.
    /// </summary>
    public DateTime? CreatedAt { get; set; }

    /// <summary>
    /// When the secret was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// When the secret expires (if applicable).
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Whether the secret is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// The content type of the secret.
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// Custom tags/attributes for the secret.
    /// </summary>
    public Dictionary<string, string> Tags { get; set; } = new();
}

/// <summary>
/// Exception thrown when a secret operation fails.
/// </summary>
public class SecretsException : Exception
{
    public SecretsException(string message) : base(message) { }
    public SecretsException(string message, Exception innerException) : base(message, innerException) { }
}
