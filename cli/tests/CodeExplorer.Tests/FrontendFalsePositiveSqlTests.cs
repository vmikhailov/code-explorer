using NUnit.Framework;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Conventions.Services;
using CodeExplorer.Core.Parser;
using CodeExplorer.Parser.TypeScript;

namespace CodeExplorer.Tests;

[TestFixture]
public class FrontendFalsePositiveSqlTests
{
    [Test]
    public void TryParseSql_RejectsUiStringsAndNaturalLanguage()
    {
        // 1. Strings mentioned by the user
        Assert.That(NestedSqlParser.TryParseSql("'Select person'", out _, out _), Is.False);
        Assert.That(NestedSqlParser.TryParseSql("'agRichSelectCellEditor'", out _, out _), Is.False);
        Assert.That(NestedSqlParser.TryParseSql("'Delete custom rate'", out _, out _), Is.False);

        // 2. Sentences and questions with SQL words
        Assert.That(NestedSqlParser.TryParseSql("Are you sure you want to delete the rate?", out _, out _), Is.False);
        Assert.That(NestedSqlParser.TryParseSql("Select person from dropdown.", out _, out _), Is.False);
        Assert.That(NestedSqlParser.TryParseSql("Select user from dropdown", out _, out _), Is.False);
        Assert.That(NestedSqlParser.TryParseSql("Select: Choose an item", out _, out _), Is.False);
        Assert.That(NestedSqlParser.TryParseSql("Update: status was refreshed", out _, out _), Is.False);
        Assert.That(NestedSqlParser.TryParseSql("Delete from options", out _, out _), Is.False);
    }

    [Test]
    public void TryParseSql_AcceptsValidSqlQueries()
    {
        Assert.That(NestedSqlParser.TryParseSql("SELECT id, name FROM users WHERE active = 1", out var f1, out _), Is.True);
        Assert.That(f1, Is.EqualTo("SELECT"));

        Assert.That(NestedSqlParser.TryParseSql("SELECT * FROM warehouse_inventory WHERE product_id = @Id", out var f2, out _), Is.True);
        Assert.That(f2, Is.EqualTo("SELECT"));

        Assert.That(NestedSqlParser.TryParseSql("DELETE FROM orders WHERE status = 'cancelled'", out var f3, out _), Is.True);
        Assert.That(f3, Is.EqualTo("DELETE"));

        Assert.That(NestedSqlParser.TryParseSql("UPDATE accounts SET balance = balance + 10 WHERE id = 1", out var f4, out _), Is.True);
        Assert.That(f4, Is.EqualTo("UPDATE"));

        Assert.That(NestedSqlParser.TryParseSql("INSERT INTO audit_logs (msg) VALUES ('test')", out var f5, out _), Is.True);
        Assert.That(f5, Is.EqualTo("INSERT"));
    }

    [Test]
    public void IsFrontendFilePath_IdentifiesUiComponentsAndPagesCorrectly()
    {
        Assert.That(SourcePathClassifier.IsFrontendFilePath("services/fe/src/app/pages/rates/modules/rates-custom/rates-custom.component.ts"), Is.True);
        Assert.That(SourcePathClassifier.IsFrontendFilePath("services/fe/src/app/pages/quartal-plans/models/okr-grid.model.ts"), Is.True);
        Assert.That(SourcePathClassifier.IsFrontendFilePath("services/fe/projects/ui/src/lib/button/button.component.ts"), Is.True);
        Assert.That(SourcePathClassifier.IsFrontendFilePath("src/components/UserModal.tsx"), Is.True);
        Assert.That(SourcePathClassifier.IsFrontendFilePath("src/views/DashboardView.vue"), Is.True);

        // Backend files should not be flagged as frontend
        Assert.That(SourcePathClassifier.IsFrontendFilePath("services/bff/src/user/user.repository.ts"), Is.False);
        Assert.That(SourcePathClassifier.IsFrontendFilePath("services/sources/src/services/sources.service.ts"), Is.False);
        Assert.That(SourcePathClassifier.IsFrontendFilePath("services/order/OrderController.cs"), Is.False);
    }

    [Test]
    public void IsProjectDirectory_RejectsNgSecondaryEntryPointsInsideSrcLib()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_ng_sec_test_" + Guid.NewGuid().ToString("N"));
        var buttonDir = Path.Combine(tempDir, "services", "fe", "projects", "ui", "src", "lib", "button");
        Directory.CreateDirectory(buttonDir);

        try
        {
            var secondaryPkgJson = Path.Combine(buttonDir, "package.json");
            File.WriteAllText(secondaryPkgJson, """
            {
              "ngPackage": {
                "lib": {
                  "entryFile": "public-api.ts",
                  "cssUrl": "inline"
                }
              }
            }
            """);

            var parser = new TypeScriptParser();
            var isProj = parser.IsProjectDirectory(buttonDir, Directory.GetFiles(buttonDir));
            Assert.That(isProj, Is.False, "Secondary entry points without package name must not be standalone projects");

            // Now test a root library directory with package name
            var rootUiDir = Path.Combine(tempDir, "services", "fe", "projects", "ui");
            var rootPkgJson = Path.Combine(rootUiDir, "package.json");
            File.WriteAllText(rootPkgJson, """
            {
              "name": "ui",
              "version": "1.0.0"
            }
            """);

            var isRootProj = parser.IsProjectDirectory(rootUiDir, Directory.GetFiles(rootUiDir));
            Assert.That(isRootProj, Is.True, "Root library project with name in package.json must be a project");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
