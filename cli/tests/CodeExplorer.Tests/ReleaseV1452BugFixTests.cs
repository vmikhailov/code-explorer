using CodeExplorer.Core.Parser;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class ReleaseV1452BugFixTests
{
    [Test]
    [TestCase("public", true)]
    [TestCase("dbo", true)]
    [TestCase("auth", true)]
    [TestCase("orders_v1", true)]
    [TestCase("create table venue_sources (id int)", false)]
    [TestCase("responseSchema", false)]
    [TestCase("festivalDescriptionSchema", false)]
    [TestCase("SELECT * FROM users", false)]
    [TestCase("a; DROP TABLE users;", false)]
    [TestCase("SchemaName", false)]
    [TestCase("DefaultSchemaName", false)]
    public void SyntaxEnricher_IsValidSchemaName_ValidatesCorrectly(string schema, bool expected)
    {
        var isValid = SyntaxEnricher.IsValidSchemaName(schema);
        isValid.Should().Be(expected);
    }

    [Test]
    [TestCase("public", true)]
    [TestCase("dbo", true)]
    [TestCase("analytics", true)]
    [TestCase("create table venue_sources(id uuid primary key);", false)]
    [TestCase("responseSchema", false)]
    [TestCase("table", false)]
    public void PostIndexAnalyzer_IsValidSchemaIdentifier_ValidatesCorrectly(string schema, bool expected)
    {
        var isValid = PostIndexAnalyzer.IsValidSchemaIdentifier(schema);
        isValid.Should().Be(expected);
    }

    [Test]
    public void PostIndexAnalyzer_IsLikelyDatabaseName_RejectsDdlInTechnologySchemaFormat()
    {
        const string ddlName = "PostgreSQL.create table venue_sources(id uuid primary key, parser_name text not null);";
        PostIndexAnalyzer.IsLikelyDatabaseName(ddlName, "PostgreSQL").Should().BeFalse();

        const string validName = "PostgreSQL.public";
        PostIndexAnalyzer.IsLikelyDatabaseName(validName, "PostgreSQL").Should().BeTrue();
    }

    [Test]
    public void PostIndexAnalyzer_CanonicalizeDatabase_IgnoresInvalidRawSchema()
    {
        const string rawDdlSchema = "create table venue_sources(id uuid primary key, parser_name text not null);";
        var (canonicalName, canonicalType, canonicalKey) = PostIndexAnalyzer.CanonicalizeDatabase(
            "PostgreSQL",
            "relational",
            rawEngine: "PostgreSQL",
            rawSchema: rawDdlSchema,
            fallbackEngine: "PostgreSQL");

        canonicalName.Should().NotContain("create table");
        canonicalName.Should().Be("PostgreSQL");
    }
}
