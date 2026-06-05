using System;

namespace CodeExplorer.Core.Parser;

public static class PatternMatcher
{
    public static bool IsMatch(string import, string pattern)
    {
        if (string.IsNullOrEmpty(import) || string.IsNullOrEmpty(pattern)) return false;

        // Wildcard prefix/folder matching: e.g. "@nestjs/*"
        if (pattern.EndsWith("/*"))
        {
            var prefix = pattern.Substring(0, pattern.Length - 2);
            return import.Equals(prefix, StringComparison.OrdinalIgnoreCase) || 
                   import.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase);
        }
        // General wildcard matching: e.g. "firebase*"
        if (pattern.EndsWith("*"))
        {
            var prefix = pattern.Substring(0, pattern.Length - 1);
            return import.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
        
        // Default part-based namespace match
        return ILibraryParser.IsLibraryMatch(import, pattern);
    }
}
