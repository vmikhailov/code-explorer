using System;
using System.IO;
using System.Linq;

namespace CodeExplorer.Core.Parser;

public static class ProjectProcessorFactory
{
    public static ProjectProcessor? CreateProcessor(ParsingContext ctx, string projectDir, string parentContainerId)
    {
        var files = Directory.GetFiles(projectDir);
        IProjectParser? matchedParser;
        lock (WorkspaceParser.ProjectParsers)
        {
            matchedParser = WorkspaceParser.ProjectParsers.FirstOrDefault(p => p.IsProjectDirectory(projectDir, files));
        }

        if (matchedParser == null) return null;

        return new ProjectProcessor(ctx, projectDir, parentContainerId, matchedParser);
    }
}
