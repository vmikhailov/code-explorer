using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;

namespace CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

[OntologyNode(
    label: OntologyConstants.NodeLabels.Policy,
    idScheme: "{workspaceId}:pol:{subKind}:{policyName}",
    purpose: "Represents an authentication, role, permission, guard, or explicit public-access security policy protecting endpoints and CQRS messages.",
    layer: OntologyConstants.Layers.Semantic,
    icon: "lock",
    order: 13,
    pluralLabel: "Authorization Policies & Guards"
)]
[OntologyEdge<FunctionNode>(OntologyConstants.Relationships.EnforcedBy)]
[OntologyEdge<TypeNode>(OntologyConstants.Relationships.ImplementedBy)]
[OntologyEdge<FileNode>(OntologyConstants.Relationships.DeclaredIn)]
public record PolicyNode(
    string Id,
    [property: OntologyProperty("The policy, role, guard, or permission name (e.g. Authenticated, Admin, JwtAuthGuard, Anonymous).")] string Name,
    [property: OntologyProperty("The workspace-relative path of the declaring guard or endpoint file.")] string Path,
    [property: JsonPropertyName("sub_kind"), OntologyProperty("The authorization policy classification (authenticated, role, permission, custom_guard, anonymous).")] PolicyKind SubKind,
    Dictionary<string, string>? CustomProperties = null
) : CompositeNode(Id, CustomProperties)
{
    [JsonPropertyName("requirement"), OntologyProperty("Raw role, claim, SpEL expression, or permission requirement.")]
    public string? Requirement { get; set; }

    [JsonPropertyName("framework"), OntologyProperty("Detected security framework (e.g. aspnetcore-auth, nestjs-guards, spring-security, fastapi-security).")]
    public string? Framework { get; set; }

    [JsonPropertyName("file_path"), OntologyProperty("Workspace-relative source file declaring or referencing the policy.")]
    public string? FilePath { get; set; }

    [JsonPropertyName("start_line"), OntologyProperty("1-indexed starting line number of the policy declaration or guard.")]
    public int? StartLine { get; set; }

    [JsonIgnore]
    public override NodeKind Kind => NodeKind.Policy;
}
