using System;
using System.Collections.Generic;
using CodeExplorer.Core.Common.Nodes;
using TreeSitter;

namespace CodeExplorer.Core.Parser;

public class SyntaxTree : IDisposable
{
    public string FilePath { get; }
    public string RelativePath { get; }
    public Tree? Tree { get; }
    public TreeSitter.Parser? Parser { get; }
    public Language? Language { get; }
    public FileNode FileNode { get; }
    public List<RawImport> RawImports { get; }
    public List<RawVariable> RawVariables { get; }

    public SyntaxTree(
        string filePath,
        string relativePath,
        Tree? tree,
        TreeSitter.Parser? parser,
        Language? language,
        FileNode fileNode,
        List<RawImport> rawImports,
        List<RawVariable> rawVariables)
    {
        FilePath = filePath;
        RelativePath = relativePath;
        Tree = tree;
        Parser = parser;
        Language = language;
        FileNode = fileNode;
        RawImports = rawImports;
        RawVariables = rawVariables;
    }

    public void Dispose()
    {
        Tree?.Dispose();
        Parser?.Dispose();
        Language?.Dispose();
    }
}
