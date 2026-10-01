using CommandLine;

namespace CodeExplorer.Options;

[Verb("domain", HelpText = "Manage architectural domains, metadata, and service-to-domain overrides.")]
public class DomainOptions : ITargetOption
{
    [Value(0, MetaName = "action", Required = false, HelpText = "Action to perform: 'list' (default), 'add', 'remove', 'assign', 'unassign'.")]
    public string? Action { get; set; } = "list";

    [Value(1, MetaName = "nameOrService", Required = false, HelpText = "Domain name (for add/remove) or service name (for assign/unassign).")]
    public string? Target { get; set; }

    [Option('d', "domain", Required = false, HelpText = "Target domain name when assigning a service.")]
    public string? Domain { get; set; }

    [Option('c', "context", Required = false, HelpText = "Bounded context name when assigning a service.")]
    public string? Context { get; set; }

    [Option("display", Required = false, HelpText = "Human-readable display name for the domain.")]
    public string? DisplayName { get; set; }

    [Option("desc", Required = false, HelpText = "Description of the domain's business capabilities.")]
    public string? Description { get; set; }

    [Option("icon", Required = false, HelpText = "Emoji or icon identifier.")]
    public string? Icon { get; set; }

    [Option("color", Required = false, HelpText = "Hex color code for visualization.")]
    public string? Color { get; set; }

    [Option("reassign", Required = false, HelpText = "Target domain to reassign services to when removing a domain.")]
    public string? ReassignTo { get; set; }

    [Option('r', "root", Required = false, HelpText = "Workspace root directory (defaults to current directory).")]
    public string? Root { get; set; }
}
