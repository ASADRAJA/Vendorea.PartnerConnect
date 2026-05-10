using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Validation.Interfaces;

namespace Vendorea.PartnerConnect.Validation.Core;

/// <summary>
/// Generic document validator that applies multiple validation rules.
/// </summary>
/// <typeparam name="T">The type of document being validated.</typeparam>
public class DocumentValidator<T> : IDocumentValidator<T>
{
    private readonly IEnumerable<IValidationRule<T>> _rules;
    private readonly ILogger<DocumentValidator<T>> _logger;

    public DocumentValidator(
        IEnumerable<IValidationRule<T>> rules,
        ILogger<DocumentValidator<T>> logger)
    {
        _rules = rules;
        _logger = logger;
    }

    public async Task<ValidationResult> ValidateAsync(
        T document,
        ValidationContext context,
        CancellationToken cancellationToken = default)
    {
        var result = new ValidationResult();

        foreach (var rule in _rules)
        {
            try
            {
                var ruleResult = await rule.ValidateAsync(document, context, cancellationToken);
                result.Merge(ruleResult);

                if (!ruleResult.IsValid && (rule.StopOnFailure || context.StopOnFirstError))
                {
                    _logger.LogDebug(
                        "Validation stopped by rule {RuleCode} for {DocumentType}",
                        rule.RuleCode, context.DocumentType);
                    break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error executing validation rule {RuleCode} for {DocumentType}",
                    rule.RuleCode, context.DocumentType);

                result.AddError(
                    "RULE_EXECUTION_ERROR",
                    $"Error executing rule {rule.RuleCode}: {ex.Message}");

                if (context.StopOnFirstError)
                {
                    break;
                }
            }
        }

        if (!result.IsValid)
        {
            _logger.LogWarning(
                "Validation failed for {DocumentType} with {ErrorCount} errors",
                context.DocumentType, result.Errors.Count);
        }

        return result;
    }

    public async Task<IDictionary<int, ValidationResult>> ValidateBatchAsync(
        IEnumerable<T> documents,
        ValidationContext context,
        CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<int, ValidationResult>();
        var index = 0;

        foreach (var document in documents)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            results[index] = await ValidateAsync(document, context, cancellationToken);
            index++;
        }

        var validCount = results.Count(r => r.Value.IsValid);
        var invalidCount = results.Count - validCount;

        _logger.LogInformation(
            "Batch validation completed for {DocumentType}: {ValidCount} valid, {InvalidCount} invalid",
            context.DocumentType, validCount, invalidCount);

        return results;
    }
}
