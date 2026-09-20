namespace OntologyGen;

/// <summary>Metadata extracted from a single node class.</summary>
public sealed record NodeInfo(
    string ClassName,
    string Label,
    string IdScheme,
    string Purpose,
    string Layer,
    List<EdgeInfo> OutEdges,
    List<PropertyInfo> Properties
);

/// <summary>One outbound edge declared on a node via [OntologyEdge&lt;TTo&gt;].</summary>
public sealed record EdgeInfo(
    string FromLabel,
    string Rel,
    string ToTypeName   // simple class name, e.g. "ClassNode"
);

/// <summary>One constructor parameter annotated with [OntologyProperty].</summary>
public sealed record PropertyInfo(
    string Name,
    string Type,
    string Description
);

/// <summary>Metadata extracted from a relationship record class.</summary>
public sealed record RelInfo(
    string Label,
    string Description
);
