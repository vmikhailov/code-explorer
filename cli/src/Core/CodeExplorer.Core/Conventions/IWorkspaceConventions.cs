using CodeExplorer.Core.Conventions.Services;

namespace CodeExplorer.Core.Conventions;

/// <summary>
/// Context-bound conventions interface for a workspace.
/// </summary>
public interface IWorkspaceConventions
{
    ServiceIdentityResolver ServiceResolver { get; }
    MessagingConventionService Messaging { get; }
    DomainTaxonomyEngine Domains { get; }
    NetworkEntityValidator NetworkValidator { get; }

    bool TryGetDatabaseAlias(string name, out string alias);
    bool TryMatchRouteFunction(string text, out string routeKey);
    bool IsTestFilePath(string? path);
    bool IsFrontendFilePath(string? path);
}
