using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CodeExplorer.Core.Parser;

public static class LibraryParserRegistry
{
    private static readonly List<ILibraryParser> RegisteredParsers = new();
    private static readonly Dictionary<string, List<ILibraryParser>> ParsersByLibrary = new(StringComparer.OrdinalIgnoreCase);
    private static bool _discovered = false;
    private static readonly object LockObj = new();

    static LibraryParserRegistry()
    {
        EnsureDiscovered();
    }

    public static void EnsureDiscovered()
    {
        lock (LockObj)
        {
            if (_discovered) return;
            _discovered = true;

            var parserType = typeof(ILibraryParser);
            var assemblies = AppDomain.CurrentDomain.GetAssemblies().ToList();

            // Load any parser assemblies in the execution directory that haven't been loaded yet
            var baseDir = Path.GetDirectoryName(typeof(LibraryParserRegistry).Assembly.Location)
                          ?? AppContext.BaseDirectory;
            Console.WriteLine($"[LibraryParserRegistry] Scanning directory: {baseDir}");
            if (System.IO.Directory.Exists(baseDir))
            {
                var files = System.IO.Directory.GetFiles(baseDir, "CodeExplorer.Parser.*.dll");
                foreach (var file in files)
                {
                    try
                    {
                        var assemblyName = AssemblyName.GetAssemblyName(file);
                        if (assemblies.All(a => a.GetName().Name != assemblyName.Name))
                        {
                            Console.WriteLine($"[LibraryParserRegistry] Loading assembly: {assemblyName.Name}");
                            var asm = Assembly.Load(assemblyName);
                            assemblies.Add(asm);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[LibraryParserRegistry] Failed to load assembly {file}: {ex.Message}");
                    }
                }
            }

            foreach (var assembly in assemblies)
            {
                try
                {
                    var types = assembly.GetTypes()
                        .Where(t => parserType.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);
                    foreach (var type in types)
                    {
                        if (Activator.CreateInstance(type) is ILibraryParser parser)
                        {
                            RegisterInternal(parser);
                        }
                    }
                }
                catch
                {
                    // Ignore assemblies that fail to load types
                }
            }
        }
    }

    public static void Register(ILibraryParser parser)
    {
        lock (LockObj)
        {
            RegisterInternal(parser);
        }
    }

    private static void RegisterInternal(ILibraryParser parser)
    {
        if (RegisteredParsers.All(p => p.GetType() != parser.GetType()))
        {
            RegisteredParsers.Add(parser);
            Console.WriteLine($"[LibraryParserRegistry] Registered parser: {parser.Name} ({parser.GetType().FullName})");
            foreach (var lib in parser.SupportedLibraries)
            {
                if (!ParsersByLibrary.TryGetValue(lib, out var list))
                {
                    list = new List<ILibraryParser>();
                    ParsersByLibrary[lib] = list;
                }
                list.Add(parser);
            }
        }
    }

    public static List<ILibraryParser> GetParsersFor(HashSet<string> importedLibraries)
    {
        EnsureDiscovered();
        lock (LockObj)
        {
            var result = new List<ILibraryParser>();
            foreach (var lib in importedLibraries)
            {
                if (ParsersByLibrary.TryGetValue(lib, out var list))
                {
                    result.AddRange(list);
                }
            }
            return result.Distinct().ToList();
        }
    }
}
