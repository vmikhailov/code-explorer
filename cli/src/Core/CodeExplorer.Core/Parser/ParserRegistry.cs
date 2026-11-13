using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace CodeExplorer.Core.Parser;

/// <summary>
/// Central registry for language <see cref="IProjectParser"/> and <see cref="IFileParser"/> implementations.
/// </summary>
public static class ParserRegistry
{
    public static Action? OnIndexCompletedHook { get; set; }

    internal static readonly List<IProjectParser> ProjectParsers = [];
    internal static readonly List<IFileParser> FileParsers = [];
    private static readonly object RegistrationLock = new();

    public static void Register(object parser)
    {
        lock (RegistrationLock)
        {
            if (parser is IProjectParser projectParser)
            {
                if (ProjectParsers.All(p => p.GetType() != projectParser.GetType()))
                {
                    var ordered = ProjectParserPrecedence.Order([.. ProjectParsers, projectParser]);
                    ProjectParsers.Clear();
                    ProjectParsers.AddRange(ordered);
                }

                foreach (var desc in projectParser.ConfigurationDescriptors)
                {
                    LibraryConfigurationRegistry.Register(desc);
                }
            }

            if (parser is IFileParser fileParser)
            {
                if (FileParsers.All(p => p.GetType() != fileParser.GetType()))
                    FileParsers.Add(fileParser);
            }

            if (parser is ISharedFileParser sharedParser)
            {
                SharedFileParserRegistry.Register(sharedParser);
            }

            if (parser is ILibraryConfigurationDescriptor configDesc)
            {
                LibraryConfigurationRegistry.Register(configDesc);
            }

            if (parser is Components.IComponentLibraryParser componentParser)
            {
                Components.ComponentLibraryParserRegistry.Register(componentParser);
            }

            WorkspaceFileFilter.ResetDefault();
        }
    }

    /// <summary>
    /// Discovers parser assemblies (or scans the given ones) and registers every parser type they export.
    /// </summary>
    public static IReadOnlyList<Type> AutoRegisterParsers(IEnumerable<Assembly>? assemblies = null)
    {
        var parserTypes = (assemblies ?? ParserAssemblyDiscovery.Discover())
            .SelectMany(ParserAssemblyDiscovery.GetParserTypes)
            .ToList();

        foreach (var type in parserTypes)
        {
            Register(CreateParser(type));
        }

        return parserTypes;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "Parser types are confirmed to have public parameterless constructor in IsParserType")]
    private static object CreateParser(Type type)
    {
        try
        {
            return Activator.CreateInstance(type) ?? throw new InvalidOperationException($"Failed to instantiate parser '{type.FullName}'");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"[AutoRegisterParsers] Failed to instantiate parser '{type.FullName}' from assembly '{type.Assembly.GetName().Name}': {ex.Message}",
                ex);
        }
    }

    public static IReadOnlyList<IProjectParser> GetAllProjectParsers() => ProjectParsers;
    public static IReadOnlyList<IFileParser> GetAllFileParsers() => FileParsers;

    public static IFileParser? GetParserForFile(string filePath)
    {
        if (FileParsers.Count == 0) AutoRegisterParsers();
        return FileParsers.FirstOrDefault(p => p.CanParseFile(filePath));
    }

    public static IFileParser? GetParserByLanguage(string languageName)
    {
        if (string.IsNullOrEmpty(languageName)) return null;
        if (FileParsers.Count == 0) AutoRegisterParsers();
        var normalized = languageName.Equals("csharp", StringComparison.OrdinalIgnoreCase)
            ? "c-sharp"
            : languageName;
        return FileParsers.FirstOrDefault(p =>
            string.Equals(p.LanguageName, normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(p.LanguageName, languageName, StringComparison.OrdinalIgnoreCase));
    }

    public static IProjectParser? GetProjectParser(string projectType)
    {
        return ProjectParsers.FirstOrDefault(p => string.Equals(p.ProjectType, projectType, StringComparison.OrdinalIgnoreCase));
    }
}
