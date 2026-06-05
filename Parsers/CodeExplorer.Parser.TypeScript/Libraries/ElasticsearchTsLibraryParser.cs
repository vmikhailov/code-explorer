using System;
using System.Collections.Generic;
using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class ElasticsearchTsLibraryParser : ILibraryParser
{
    public string Name => "ElasticsearchTsLibraryParser";
    public string Category => "database";
    public IEnumerable<string> SupportedLibraries => new[] { "@elastic/elasticsearch" };

    public string? MapNodeType(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) => throw new NotImplementedException();
}
