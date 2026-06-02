using System.Collections.Generic;

namespace CodeExplorer.Database;

public record Node(string Id, string Kind, Dictionary<string, object> Properties);
