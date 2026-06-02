namespace CodeExplorer.Parser;

internal class FileContext(string workspacePath, string filePath, string sourceText, IFileParser parser)
{
    public string WorkspacePath { get; } = workspacePath;
    public string FilePath { get; } = filePath;
    public string SourceText { get; } = sourceText;
    public IFileParser Parser { get; } = parser;
    public List<Database.Node> Nodes { get; } = new();
    public List<Database.Relationship> Relationships { get; } = new();
    public List<Reference> References { get; } = new();
}
