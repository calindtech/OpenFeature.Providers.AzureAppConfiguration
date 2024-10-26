using Microsoft.Extensions.Logging;
using OpenFeature.Model;

namespace OpenFeature.Providers.AzureAppConfiguration.Hooks
{
    /// <summary>
    /// Hook implementation for Azure App Configuration provider that provides logging and monitoring
    /// capabilities around feature flag evaluations.
    /// </summary>
    public class AzureProviderHook : Hook
    {
        private readonly ILogger<AzureProviderHook>? _logger;

        public AzureProviderHook(ILogger<AzureProviderHook>? logger = null)
        {
            _logger = logger;
        }

        public override ValueTask<EvaluationContext> BeforeAsync<T>(
            HookContext<T> context,
            IReadOnlyDictionary<string, object>? hints = null,
            CancellationToken cancellationToken = default)
        {
            _logger?.LogDebug("Starting evaluation of feature flag: {FlagKey}", context.FlagKey);

            // Return the existing context or empty if null
            return new ValueTask<EvaluationContext>(context.EvaluationContext ?? EvaluationContext.Empty);
        }

        public override ValueTask AfterAsync<T>(
            HookContext<T> context,
            FlagEvaluationDetails<T> details,
            IReadOnlyDictionary<string, object>? hints = null,
            CancellationToken cancellationToken = default)
        {
            _logger?.LogDebug(
                "Feature flag {FlagKey} evaluated successfully. Value: {Value}, Variant: {Variant}, Reason: {Reason}",
                context.FlagKey,
                details.Value,
                details.Variant,
                details.Reason);

            return new ValueTask();
        }

        public override ValueTask ErrorAsync<T>(
            HookContext<T> context,
            Exception error,
            IReadOnlyDictionary<string, object>? hints = null,
            CancellationToken cancellationToken = default)
        {
            _logger?.LogError(
                error,
                "Error evaluating feature flag {FlagKey}: {ErrorMessage}",
                context.FlagKey,
                error.Message);

            return new ValueTask();
        }

        public override ValueTask FinallyAsync<T>(
            HookContext<T> context,
            IReadOnlyDictionary<string, object>? hints = null,
            CancellationToken cancellationToken = default)
        {
            _logger?.LogTrace(
                "Completed evaluation process for feature flag {FlagKey}",
                context.FlagKey);

            return new ValueTask();
        }
    }
}