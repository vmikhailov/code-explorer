namespace CodeExplorer.Parser.Terraform;

/// <summary>File-type rules shared by <see cref="TerraformProjectParser"/> and <see cref="TerraformFileParser"/>.</summary>
internal static class TerraformFiles
{
    private const string LockFileName = ".terraform.lock.hcl";

    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".tf", ".tofu", ".tfvars", ".hcl"
    };

    /// <summary>True for Terraform, OpenTofu, tfvars, and Terragrunt/HCL file extensions.</summary>
    public static bool IsTerraformExtension(string extension) => Extensions.Contains(extension);

    /// <summary>
    /// True for <c>.terraform.lock.hcl</c>, which is generated provider metadata rather than
    /// Terraform source.
    /// </summary>
    public static bool IsLockFile(string filePath) =>
        Path.GetFileName(filePath).Equals(LockFileName, StringComparison.OrdinalIgnoreCase);
}
