using Vendorea.PartnerConnect.Validation.Core;

namespace Vendorea.PartnerConnect.Validation.Interfaces;

/// <summary>
/// Interface for document validators.
/// </summary>
/// <typeparam name="T">The type of document being validated.</typeparam>
public interface IDocumentValidator<in T>
{
    /// <summary>
    /// Validates a single document.
    /// </summary>
    /// <param name="document">The document to validate.</param>
    /// <param name="context">Validation context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The validation result.</returns>
    Task<ValidationResult> ValidateAsync(
        T document,
        ValidationContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a batch of documents.
    /// </summary>
    /// <param name="documents">The documents to validate.</param>
    /// <param name="context">Validation context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Results for each document, keyed by index.</returns>
    Task<IDictionary<int, ValidationResult>> ValidateBatchAsync(
        IEnumerable<T> documents,
        ValidationContext context,
        CancellationToken cancellationToken = default);
}
