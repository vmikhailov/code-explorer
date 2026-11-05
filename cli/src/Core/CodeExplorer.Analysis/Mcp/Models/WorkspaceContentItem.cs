namespace CodeExplorer.Core.Mcp.Models;

public record WorkspaceContentItem(NodeKind Type, string Name, string Symbol, string FilePath)
{
    public WorkspaceContentItem(string Type, string Name, string Symbol, string FilePath)
        : this(EnumExtensions.ParseNodeKind(Type), Name, Symbol, FilePath)
    {
    }
}
