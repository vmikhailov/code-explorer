using CodeExplorer.Core.Database;

namespace CodeExplorer.Core.Analysis;

public interface IIntentPredictor : IDisposable
{
    int Concurrency { get; }
    string ConcurrencyReason { get; }
    string ExecutionDevice { get; }

    Task<BatchInferenceResult?> PredictAsync(
        string filePath,
        string content,
        string? projectName = null,
        IReadOnlyList<string>? knownDomains = null,
        string? projectDomain = null,
        string? projectRole = null,
        CancellationToken cancellationToken = default);

    Task<SystemDomainsResult?> PredictSystemDomainsAsync(
        IReadOnlyList<ProjectSignature> signatures,
        CancellationToken cancellationToken = default);

    Task<ProjectBoundedContextResult?> PredictProjectBoundedContextAsync(
        ProjectSignature signature,
        CancellationToken cancellationToken = default);

    Task<ProjectBoundedContextResult?> PredictProjectBoundedContextAgenticAsync(
        ProjectSignature signature,
        IServiceGraphExplorer explorer,
        int maxTurns = 3,
        Action<string>? logger = null,
        CancellationToken cancellationToken = default)
    {
        return PredictProjectBoundedContextAsync(signature, cancellationToken);
    }

    Task<SystemDomainsResult?> PredictMacroDomainsFromContextsAsync(
        IReadOnlyList<ProjectBoundedContextResult> contexts,
        CancellationToken cancellationToken = default);

    Task<ProjectIntentResult?> PredictProjectIntentAsync(
        ProjectSignature signature,
        CancellationToken cancellationToken = default);
}
