using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeExplorer.Core.Parser;

public static class LibraryParserRegistry
{
    private static readonly List<ILibraryParser> RegisteredParsers = new();

    public static void Register(ILibraryParser parser)
    {
        lock (RegisteredParsers)
        {
            if (RegisteredParsers.All(p => p.GetType() != parser.GetType()))
            {
                RegisteredParsers.Add(parser);
            }
        }
    }

    public static List<ILibraryParser> GetParsersFor(HashSet<string> importedLibraries)
    {
        lock (RegisteredParsers)
        {
            return RegisteredParsers
                .Where(p => importedLibraries.Any(p.CanParse))
                .ToList();
        }
    }
}
