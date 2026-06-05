using System;
using System.Collections.Generic;
using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class SequelizeLibraryParser : ILibraryParser
{
    public string Name => "SequelizeLibraryParser";
    public string Category => "database";
    public IEnumerable<string> SupportedLibraries => new[] { "sequelize" };

    public string? MapNodeType(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) => throw new NotImplementedException();
}
