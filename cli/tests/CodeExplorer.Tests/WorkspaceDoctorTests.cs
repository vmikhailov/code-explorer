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
                      <groupId>org.apache.struts</groupId>
                      <artifactId>struts2-core</artifactId>
                      <version>2.5.33</version>
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

            await File.WriteAllTextAsync(Path.Combine(tempDir, "src", "main", "webapp", "orders.jsp"), """
                <%@ page contentType="text/html;charset=UTF-8" %>
                <html><body>Orders</body></html>
                """);

            await File.WriteAllTextAsync(Path.Combine(tempDir, "src", "main", "resources", "struts.xml"), """
                <struts xmlns="http://struts.apache.org/dtds/struts-2.5.dtd">
                  <package name="default" extends="struts-default" />
                </struts>
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
            fullReport.Gaps!.UnhandledExtensions.Should().Contain(e => e.Extension == ".jsp" && e.FileCount == 1);
            fullReport.Gaps.UnhandledExtensions.Should().Contain(e => e.Extension == ".gwt.xml" && e.FileCount == 1);

            fullReport.Gaps.UnhandledXmlDialects.Should().Contain(d =>
                d.RootElement == "struts" &&
                d.NamespaceUri.Contains("struts", StringComparison.OrdinalIgnoreCase));

            fullReport.Gaps.UnmatchedPackages.Should().Contain(p => p.Name.Contains("struts", StringComparison.OrdinalIgnoreCase));
            fullReport.Gaps.UnmatchedPackages.Should().Contain(p => p.Name.Contains("gwt", StringComparison.OrdinalIgnoreCase));
            fullReport.Gaps.CoverageAnomalies.Should().Contain(a => a.Kind == DoctorAnomalyKind.MissingEndpoints);

            var textOutput = DoctorCommandHandler.FormatAsText(fullReport);
            textOutput.Should().Contain("CodeExplorer Doctor");
            textOutput.Should().Contain("struts");

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
