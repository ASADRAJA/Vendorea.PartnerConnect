using Vendorea.PartnerConnect.Validation.Core;

namespace Vendorea.PartnerConnect.Validation.Interfaces;

/// <summary>
/// Interface for a validation rule.
/// </summary>
/// <typeparam name="T">The type of object being validated.</typeparam>
public interface IValidationRule<in T>
{
    /// <summary>
    /// The unique code for this rule.
    /// </summary>
    string RuleCode { get; }

    /// <summary>
    /// Description of what this rule validates.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Whether this rule should stop validation on failure.
    /// </summary>
    bool StopOnFailure { get; }

    /// <summary>
    /// Validates the item.
    /// </summary>
    /// <param name="item">The item to validate.</param>
    /// <param name="context">Validation context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The validation result.</returns>
    Task<ValidationResult> ValidateAsync(
        T item,
        ValidationContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Base class for validation rules providing common functionality.
/// </summary>
/// <typeparam name="T">The type of object being validated.</typeparam>
public abstract class ValidationRuleBase<T> : IValidationRule<T>
{
    public abstract string RuleCode { get; }
    public abstract string Description { get; }
    public virtual bool StopOnFailure => false;

    public abstract Task<ValidationResult> ValidateAsync(
        T item,
        ValidationContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    protected ValidationResult Success() => ValidationResult.Success();

    /// <summary>
    /// Creates a failure result.
    /// </summary>
    protected ValidationResult Failure(string message, string? field = null)
    {
        return ValidationResult.Failure(RuleCode, message, field);
    }

    /// <summary>
    /// Creates a warning result.
    /// </summary>
    protected ValidationResult Warning(string message, string? field = null)
    {
        var result = new ValidationResult();
        result.AddWarning(RuleCode, message, field);
        return result;
    }
}
