using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Common;

/// <summary>
/// Classification of the iterated source expression for syntactic loop scopes.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LoopSourceKind
{
    Unspecified = 0,
    Bounded = 1,
    Partitioned = 2,
    Collection = 3,
    Unpartitioned = 4
}
