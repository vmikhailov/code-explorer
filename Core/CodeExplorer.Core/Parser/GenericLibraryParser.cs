using System.Collections.Generic;
using CodeExplorer.Common;
using TreeSitter;

namespace CodeExplorer.Core.Parser;

public class GenericLibraryParser : ILibraryParser
{
    public string Name { get; }
    public string Category { get; }
    public IEnumerable<string> SupportedLibraries { get; }
    public bool IsImplemented => false;

    public string? DbEngine { get; }
    public string? DbType { get; }
    public string? ApiLibrary { get; }
    public string? CloudService { get; }

    public GenericLibraryParser(
        string name,
        string category,
        IEnumerable<string> supportedLibraries,
        string? dbEngine = null,
        string? dbType = null,
        string? apiLibrary = null,
        string? cloudService = null)
    {
        Name = name;
        Category = category;
        SupportedLibraries = supportedLibraries;
        DbEngine = dbEngine;
        DbType = dbType;
        ApiLibrary = apiLibrary;
        CloudService = cloudService;
    }

    public string? MapNodeType(Node node, ParsingContext ctx) => null;
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => null;
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) { }
}
