namespace Vendorea.PartnerConnect.Validation.Core;

/// <summary>
/// Result of a validation operation.
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Whether all validation rules passed.
    /// </summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    /// List of validation errors.
    /// </summary>
    public IList<ValidationError> Errors { get; } = new List<ValidationError>();

    /// <summary>
    /// List of validation warnings (non-blocking).
    /// </summary>
    public IList<ValidationWarning> Warnings { get; } = new List<ValidationWarning>();

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static ValidationResult Success() => new();

    /// <summary>
    /// Creates a failed validation result with errors.
    /// </summary>
    public static ValidationResult Failure(params ValidationError[] errors)
    {
        var result = new ValidationResult();
        foreach (var error in errors)
        {
            result.Errors.Add(error);
        }
        return result;
    }

    /// <summary>
    /// Creates a failed validation result with a single error.
    /// </summary>
    public static ValidationResult Failure(string code, string message, string? field = null)
    {
        return Failure(new ValidationError(code, message, field));
    }

    /// <summary>
    /// Adds an error to the result.
    /// </summary>
    public ValidationResult AddError(string code, string message, string? field = null)
    {
        Errors.Add(new ValidationError(code, message, field));
        return this;
    }

    /// <summary>
    /// Adds a warning to the result.
    /// </summary>
    public ValidationResult AddWarning(string code, string message, string? field = null)
    {
        Warnings.Add(new ValidationWarning(code, message, field));
        return this;
    }

    /// <summary>
    /// Merges another validation result into this one.
    /// </summary>
    public ValidationResult Merge(ValidationResult other)
    {
        foreach (var error in other.Errors)
        {
            Errors.Add(error);
        }
        foreach (var warning in other.Warnings)
        {
            Warnings.Add(warning);
        }
        return this;
    }
}

/// <summary>
/// Represents a validation error.
/// </summary>
public record ValidationError(
    string Code,
    string Message,
    string? Field = null,
    object? AttemptedValue = null);

/// <summary>
/// Represents a validation warning (non-blocking).
/// </summary>
public record ValidationWarning(
    string Code,
    string Message,
    string? Field = null);
