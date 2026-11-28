using CommandLine;

namespace CodeExplorer.Options;

[Verb("domains", Hidden = true, HelpText = "Hidden compatibility alias for 'clusters'.")]
public sealed class DomainsOptions : ClustersOptions
{
}
