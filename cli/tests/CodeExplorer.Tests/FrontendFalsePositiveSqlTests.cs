using CodeExplorer.Core.Conventions.Services;
using CodeExplorer.Parser.TypeScript;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class FrontendFalsePositiveSqlTests
{
    [Test]
    public void TryParseSql_RejectsUiStringsAndNaturalLanguage()
    {
        // 1. Strings mentioned by the user
        NestedSqlParser.TryParseSql("'Select person'", out _, out _).Should().BeFalse();
        NestedSqlParser.TryParseSql("'agRichSelectCellEditor'", out _, out _).Should().BeFalse();
        NestedSqlParser.TryParseSql("'Delete custom rate'", out _, out _).Should().BeFalse();

        // 2. Sentences and questions with SQL words
        NestedSqlParser.TryParseSql("Are you sure you want to delete the rate?", out _, out _).Should().BeFalse();
        NestedSqlParser.TryParseSql("Select person from dropdown.", out _, out _).Should().BeFalse();
        NestedSqlParser.TryParseSql("Select user from dropdown", out _, out _).Should().BeFalse();
        NestedSqlParser.TryParseSql("Select: Choose an item", out _, out _).Should().BeFalse();
        NestedSqlParser.TryParseSql("Update: status was refreshed", out _, out _).Should().BeFalse();
        NestedSqlParser.TryParseSql("Delete from options", out _, out _).Should().BeFalse();
    }

    [Test]
    public void TryParseSql_AcceptsValidSqlQueries()
    {
        NestedSqlParser.TryParseSql("SELECT id, name FROM users WHERE active = 1", out var f1, out _).Should().BeTrue();
        f1.Should().Be("SELECT");

        NestedSqlParser.TryParseSql("SELECT * FROM warehouse_inventory WHERE product_id = @Id", out var f2, out _).Should().BeTrue();
        f2.Should().Be("SELECT");

        NestedSqlParser.TryParseSql("DELETE FROM orders WHERE status = 'cancelled'", out var f3, out _).Should().BeTrue();
        f3.Should().Be("DELETE");

        NestedSqlParser.TryParseSql("UPDATE accounts SET balance = balance + 10 WHERE id = 1", out var f4, out _).Should().BeTrue();
        f4.Should().Be("UPDATE");

        NestedSqlParser.TryParseSql("INSERT INTO audit_logs (msg) VALUES ('test')", out var f5, out _).Should().BeTrue();
        f5.Should().Be("INSERT");
    }

    [Test]
    public void IsFrontendFilePath_IdentifiesUiComponentsAndPagesCorrectly()
    {
        SourcePathClassifier.IsFrontendFilePath("services/fe/src/app/pages/rates/modules/rates-custom/rates-custom.component.ts").Should().BeTrue();
        SourcePathClassifier.IsFrontendFilePath("services/fe/src/app/pages/quartal-plans/models/okr-grid.model.ts").Should().BeTrue();
        SourcePathClassifier.IsFrontendFilePath("services/fe/projects/ui/src/lib/button/button.component.ts").Should().BeTrue();
        SourcePathClassifier.IsFrontendFilePath("src/components/UserModal.tsx").Should().BeTrue();
        SourcePathClassifier.IsFrontendFilePath("src/views/DashboardView.vue").Should().BeTrue();

        // Backend files should not be flagged as frontend
        SourcePathClassifier.IsFrontendFilePath("services/bff/src/user/user.repository.ts").Should().BeFalse();
        SourcePathClassifier.IsFrontendFilePath("services/sources/src/services/sources.service.ts").Should().BeFalse();
        SourcePathClassifier.IsFrontendFilePath("services/order/OrderController.cs").Should().BeFalse();
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
            isProj.Should().BeFalse("Secondary entry points without package name must not be standalone projects");

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
            isRootProj.Should().BeTrue("Root library project with name in package.json must be a project");
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
