using CodeExplorer.Commands;
using CodeExplorer.Core.Analysis;
using CodeExplorer.Core.Database;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class WorkspaceDoctorTests
{
    [SetUp]
    public void SetUp()
    {
        WorkspaceIndexer.AutoRegisterParsers();
    }

    [Test]
    public async Task AnalyzeAsync_DetectsUnhandledExtensionsXmlDialectsPackagesIssuesAndAnonymizes()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_doctor_test_" + Guid.NewGuid().ToString("N"));
        var dbPath = Path.Combine(tempDir, ".codeexplorer", "graph.db");
        Directory.CreateDirectory(Path.Combine(tempDir, "src", "main", "java", "com", "legacy"));
        Directory.CreateDirectory(Path.Combine(tempDir, "src", "main", "webapp"));
        Directory.CreateDirectory(Path.Combine(tempDir, "src", "main", "resources", "db"));
        Directory.CreateDirectory(Path.Combine(tempDir, ".codeexplorer"));

        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempDir, "pom.xml"), """
                <project xmlns="http://maven.apache.org/POM/4.0.0">
                  <modelVersion>4.0.0</modelVersion>
                  <groupId>com.legacy</groupId>
                  <artifactId>legacy-seam-app</artifactId>
                  <version>1.0.0</version>
                  <dependencies>
                    <dependency>
                      <groupId>org.jboss.seam</groupId>
                      <artifactId>jboss-seam</artifactId>
                      <version>2.3.1.Final</version>
                    </dependency>
                    <dependency>
                      <groupId>org.liquibase</groupId>
                      <artifactId>liquibase-core</artifactId>
                      <version>4.25.0</version>
                    </dependency>
                    <dependency>
                      <groupId>com.google.gwt</groupId>
                      <artifactId>gwt-user</artifactId>
                      <version>2.10.0</version>
                    </dependency>
                    <dependency>
                      <groupId>org.jboss.resteasy</groupId>
                      <artifactId>resteasy-jaxrs</artifactId>
                      <version>3.15.0</version>
                    </dependency>
                  </dependencies>
                </project>
                """);

            await File.WriteAllTextAsync(Path.Combine(tempDir, "src", "main", "java", "com", "legacy", "BrokenBean.java"), """
                package com.legacy;
                public class BrokenBean {
                    public void broken() {
                        if (true) {
                            ...
                        }
                    }
                }
                """);

            await File.WriteAllTextAsync(Path.Combine(tempDir, "src", "main", "webapp", "orders.xhtml"), """
                <ui:composition xmlns="http://www.w3.org/1999/xhtml"
                                xmlns:ui="http://java.sun.com/jsf/facelets">
                  <div />
                </ui:composition>
                """);

            await File.WriteAllTextAsync(Path.Combine(tempDir, "src", "main", "webapp", "components.xml"), """
                <components xmlns="http://jboss.com/products/seam/components">
                  <component name="orderAction" />
                </components>
                """);

            await File.WriteAllTextAsync(Path.Combine(tempDir, "src", "main", "resources", "db", "changelog.xml"), """
                <databaseChangeLog xmlns="http://www.liquibase.org/xml/ns/dbchangelog">
                  <changeSet id="1" author="dev" />
                </databaseChangeLog>
                """);

            await File.WriteAllTextAsync(Path.Combine(tempDir, "src", "main", "resources", "App.gwt.xml"), """
                <module rename-to="app">
                  <inherits name="com.google.gwt.user.User" />
                </module>
                """);

            await using var client = new SqliteGraphClient(dbPath);
            var indexer = new WorkspaceIndexer(client);
            await indexer.IndexAsync(tempDir, clear: true);

            var doctor = new WorkspaceDoctorService();
            var fullReport = await doctor.AnalyzeAsync(client, tempDir, new WorkspaceDoctorOptions
            {
                IncludeIssues = true,
                IncludeGaps = true,
                Anonymize = false,
                MaxSamples = 25
            });

            fullReport.Health.TotalNodes.Should().BeGreaterThan(0);
            fullReport.Health.LanguagesDetected.Should().Contain("java");

            fullReport.Issues.Should().NotBeNull();
            fullReport.Issues!.TotalCount.Should().BeGreaterThan(0);
            fullReport.Issues.Samples.Should().NotBeEmpty();

            fullReport.Gaps.Should().NotBeNull();
            fullReport.Gaps!.UnhandledExtensions.Should().Contain(e => e.Extension == ".xhtml" && e.FileCount == 1);
            fullReport.Gaps.UnhandledExtensions.Should().Contain(e => e.Extension == ".gwt.xml" && e.FileCount == 1);

            fullReport.Gaps.UnhandledXmlDialects.Should().Contain(d =>
                d.RootElement == "databaseChangeLog" &&
                d.NamespaceUri.Contains("liquibase", StringComparison.OrdinalIgnoreCase));
            fullReport.Gaps.UnhandledXmlDialects.Should().Contain(d =>
                d.RootElement == "components" &&
                d.NamespaceUri.Contains("seam", StringComparison.OrdinalIgnoreCase));
            fullReport.Gaps.UnhandledXmlDialects.Should().Contain(d =>
                d.RootElement == "composition");

            fullReport.Gaps.UnmatchedPackages.Should().Contain(p => p.Name.Contains("seam", StringComparison.OrdinalIgnoreCase));
            fullReport.Gaps.UnmatchedPackages.Should().Contain(p => p.Name.Contains("gwt", StringComparison.OrdinalIgnoreCase));
            fullReport.Gaps.CoverageAnomalies.Should().Contain(a => a.Kind == DoctorAnomalyKind.MissingEndpoints);

            var textOutput = DoctorCommandHandler.FormatAsText(fullReport);
            textOutput.Should().Contain("CodeExplorer Doctor");
            textOutput.Should().Contain("databaseChangeLog");

            var anonReport = await doctor.AnalyzeAsync(client, tempDir, new WorkspaceDoctorOptions
            {
                IncludeIssues = true,
                IncludeGaps = true,
                Anonymize = true,
                MaxSamples = 25
            });
            anonReport.Anonymized.Should().BeTrue();
            anonReport.Issues!.Samples.Should().BeEmpty();
            anonReport.Gaps!.CoverageAnomalies.Should().AllSatisfy(a => a.ProjectLabel.Should().StartWith("project#"));

            var mdOutput = DoctorCommandHandler.FormatAsMarkdown(anonReport);
            mdOutput.Should().Contain("Privacy-safe anonymized report");
            mdOutput.Should().NotContain("legacy-seam-app");
            mdOutput.Should().NotContain("BrokenBean.java");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
