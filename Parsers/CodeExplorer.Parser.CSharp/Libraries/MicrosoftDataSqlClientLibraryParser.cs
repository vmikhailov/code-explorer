using System;
using System.Collections.Generic;
using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class MicrosoftDataSqlClientLibraryParser : ILibraryParser
{
    public string Name => "MicrosoftDataSqlClientLibraryParser";
    public string Category => "database";
    public IEnumerable<string> SupportedLibraries => new[] { "Microsoft.Data.SqlClient" };

    public string? MapNodeType(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) => throw new NotImplementedException();
}
