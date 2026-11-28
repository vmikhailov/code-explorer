using CodeExplorer.Options;

namespace CodeExplorer.Commands;

public sealed class DomainsCommandHandler : BaseCommandHandler
{
    public static Task<int> HandleAsync(DomainsOptions opts) =>
        ClustersCommandHandler.HandleAsync(opts);
}
