namespace CodeExplorer.Core.Parser;

public readonly record struct AddressableSymbolStub(
    string Id,
    string Name,
    string FilePath,
    string? EnclosingTypeId,
    NodeKind Kind,
    SymbolVisibility Visibility,
    string? DeclaredTypeExpr = null)
{
    private const string VisPublic = "public";
    private const string VisExport = "export";
    private const string VisPub = "pub";
    private const string VisProtected = "protected";
    private const string VisInternal = "internal";
    private const string VisPackage = "package";
    private const string VisPubCrate = "pub(crate)";
    private const string VisPrivate = "private";
    private const string VisFile = "file";

    public static SymbolVisibility ParseVisibility(string? rawVisibility, bool? isExported)
    {
        if (isExported == true)
        {
            return SymbolVisibility.Public;
        }

        if (!string.IsNullOrWhiteSpace(rawVisibility))
        {
            var trimmed = rawVisibility.Trim();
            if (trimmed.StartsWith(VisPublic, StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals(VisExport, StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals(VisPub, StringComparison.OrdinalIgnoreCase))
            {
                return SymbolVisibility.Public;
            }

            if (trimmed.StartsWith(VisProtected, StringComparison.OrdinalIgnoreCase))
            {
                return SymbolVisibility.Protected;
            }

            if (trimmed.StartsWith(VisInternal, StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals(VisPackage, StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith(VisPubCrate, StringComparison.OrdinalIgnoreCase))
            {
                return SymbolVisibility.Internal;
            }

            if (trimmed.StartsWith(VisFile, StringComparison.OrdinalIgnoreCase))
            {
                return SymbolVisibility.FilePrivate;
            }

            if (trimmed.StartsWith(VisPrivate, StringComparison.OrdinalIgnoreCase))
            {
                return SymbolVisibility.Private;
            }
        }

        return isExported == false ? SymbolVisibility.Internal : SymbolVisibility.Public;
    }
}
