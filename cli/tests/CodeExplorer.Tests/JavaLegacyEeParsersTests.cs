using CodeExplorer.Parsers.Java;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class JavaLegacyEeParsersTests
{
    [SetUp]
    public void SetUp()
    {
        WorkspaceIndexer.AutoRegisterParsers();
    }

    [Test]
    public async Task LegacyJavaEeStack_ExtractsLiquibaseTablesSeamEndpointsAndFaceletsElBindings()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_legacy_ee_test_" + Guid.NewGuid().ToString("N"));
        var dbPath = Path.Combine(tempDir, ".codeexplorer", "graph.db");
        Directory.CreateDirectory(Path.Combine(tempDir, "src", "main", "java", "com", "legacy"));
        Directory.CreateDirectory(Path.Combine(tempDir, "src", "main", "webapp", "WEB-INF"));
        Directory.CreateDirectory(Path.Combine(tempDir, "src", "main", "resources", "db", "changelogs"));
        Directory.CreateDirectory(Path.Combine(tempDir, ".codeexplorer"));

        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempDir, "pom.xml"), """
                <project xmlns="http://maven.apache.org/POM/4.0.0">
                  <modelVersion>4.0.0</modelVersion>
                  <groupId>com.legacy</groupId>
                  <artifactId>legacy-ee-app</artifactId>
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
                      <groupId>org.hibernate</groupId>
                      <artifactId>hibernate-core</artifactId>
                      <version>4.2.0.Final</version>
                    </dependency>
                    <dependency>
                      <groupId>com.google.gwt</groupId>
                      <artifactId>gwt-user</artifactId>
                      <version>2.8.2</version>
                    </dependency>
                  </dependencies>
                </project>
                """);

            await File.WriteAllTextAsync(Path.Combine(tempDir, "src", "main", "java", "com", "legacy", "HDocument.java"), """
                package com.legacy;
                import javax.persistence.Entity;

                @Entity
                public class HDocument {
                    private Long id;
                }
                """);

            await File.WriteAllTextAsync(Path.Combine(tempDir, "src", "main", "java", "com", "legacy", "DocumentDAO.java"), """
                package com.legacy;

                public class DocumentDAO extends AbstractDAOImpl<HDocument, Long> {
                }
                """);

            await File.WriteAllTextAsync(Path.Combine(tempDir, "src", "main", "java", "com", "legacy", "DocumentServiceImpl.java"), """
                package com.legacy;
                import org.jboss.seam.annotations.In;
                import org.jboss.seam.annotations.Name;

                @Name("documentService")
                public class DocumentServiceImpl {
                    @In
                    private DocumentDAO documentDAO;

                    public HDocument saveDocument(HDocument document) {
                        return documentDAO.makePersistent(document);
                    }
                }
                """);

            await File.WriteAllTextAsync(Path.Combine(tempDir, "src", "main", "java", "com", "legacy", "HomePageAction.java"), """
                package com.legacy;
                import org.jboss.seam.annotations.In;
                import org.jboss.seam.annotations.Name;
                import org.jboss.seam.annotations.Observer;
                import org.jboss.seam.annotations.RaiseEvent;
                import org.jboss.seam.core.Events;

                @Name("homePage")
                public class HomePageAction {
                    @In
                    private UserSession userSession;

                    @Observer("user.loggedIn")
                    public void onLogin() {
                        Events.instance().raiseEvent("home.refreshed");
                    }

                    @RaiseEvent("home.updated")
                    public String getHtml() {
                        return "<p>Hello</p>";
                    }

                    public int getTotalDocuments() {
                        return 42;
                    }
                }
                """);

            await File.WriteAllTextAsync(Path.Combine(tempDir, "src", "main", "resources", "db", "changelogs", "db.changelog-1.0.xml"), """
                <?xml version="1.0" encoding="UTF-8"?>
                <databaseChangeLog xmlns="http://www.liquibase.org/xml/ns/dbchangelog">
                  <changeSet id="1" author="dev">
                    <createTable tableName="HAccount">
                      <column name="id" type="bigint" />
                    </createTable>
                    <addColumn tableName="HProjectIteration">
                      <column name="requireReview" type="boolean" />
                    </addColumn>
                    <addForeignKeyConstraint baseTableName="HProjectIteration" referencedTableName="HAccount" />
                    <sql>UPDATE HTextFlowTarget SET translated_by_id = 1;</sql>
                  </changeSet>
                </databaseChangeLog>
                """);

            await File.WriteAllTextAsync(Path.Combine(tempDir, "src", "main", "webapp", "WEB-INF", "pages.xml"), """
                <pages xmlns="http://jboss.org/schema/seam/pages">
                  <page view-id="/home.xhtml" action="#{homePage.getHtml}">
                    <navigation>
                      <redirect view-id="/dashboard.xhtml" />
                    </navigation>
                  </page>
                </pages>
                """);

            await File.WriteAllTextAsync(Path.Combine(tempDir, "src", "main", "webapp", "WEB-INF", "urlrewrite.xml"), """
                <urlrewrite>
                  <rule>
                    <from>^/project/view/([^/]+)$</from>
                    <to>/project/project.seam?slug=$1</to>
                  </rule>
                </urlrewrite>
                """);

            await File.WriteAllTextAsync(Path.Combine(tempDir, "src", "main", "webapp", "WEB-INF", "components.xml"), """
                <components xmlns="http://jboss.org/schema/seam/components">
                  <component name="homePage" class="com.legacy.HomePageAction" />
                </components>
                """);

            await File.WriteAllTextAsync(Path.Combine(tempDir, "src", "main", "webapp", "home.xhtml"), """
                <ui:composition xmlns="http://www.w3.org/1999/xhtml"
                  xmlns:ui="http://java.sun.com/jsf/facelets"
                  xmlns:h="http://java.sun.com/jsf/html"
                  template="/WEB-INF/template/layout.xhtml">
                  <h:outputText value="#{homePage.getHtml}" />
                  <span class="badge">#{homePage.totalDocuments}</span>
                  <h:link value="#{msgs['jsf.Edit']}" outcome="/edit_home.xhtml" />
                </ui:composition>
                """);

            await File.WriteAllTextAsync(Path.Combine(tempDir, "src", "main", "java", "com", "legacy", "App.gwt.xml"), """
                <module rename-to="legacy">
                  <inherits name="com.google.gwt.user.User" />
                  <entry-point class="com.legacy.HomePageAction" />
                </module>
                """);

            await File.WriteAllTextAsync(Path.Combine(tempDir, "src", "main", "java", "com", "legacy", "HomePageAction.ui.xml"), """
                <ui:UiBinder xmlns:ui="urn:ui:com.google.gwt.uibinder"
                  xmlns:g="urn:import:com.google.gwt.user.client.ui">
                  <g:HTMLPanel />
                </ui:UiBinder>
                """);

            await File.WriteAllTextAsync(Path.Combine(tempDir, "src", "main", "java", "com", "legacy", "SetupScript.groovy"), """
                import com.legacy.HomePageAction

                class SetupHelper {
                    def runSetup() {
                        println("ok")
                    }
                }
                """);

            await using var client = new SqliteGraphClient(dbPath);
            var indexer = new WorkspaceIndexer(client);
            await indexer.IndexAsync(tempDir, clear: true);

            // 1. Liquibase tables & queries extracted
            var tables = await client.ExecuteCypherAsync("MATCH (t:Table) RETURN t.name AS name");
            var tableNames = tables.Rows.Select(r => r.GetStringProp("name")).ToList();
            tableNames.Should().Contain("HAccount");
            tableNames.Should().Contain("HProjectIteration");
            tableNames.Should().Contain("HTextFlowTarget");

            // 2. Seam pages.xml + urlrewrite.xml + Facelets .xhtml endpoints extracted
            var endpoints = await client.ExecuteCypherAsync("MATCH (e:Endpoint) RETURN e.name AS name");
            var epNames = endpoints.Rows.Select(r => r.GetStringProp("name")).ToList();
            epNames.Should().Contain("GET:/home.xhtml");
            epNames.Should().Contain(e => e.Contains("/project/view/", StringComparison.Ordinal));

            // 3. Seam events (Topic) extracted from @Observer / @RaiseEvent / Events.raiseEvent
            var topics = await client.ExecuteCypherAsync("MATCH (t:Topic) RETURN t.name AS name");
            var topicNames = topics.Rows.Select(r => r.GetStringProp("name")).ToList();
            topicNames.Should().Contain(t => t.EndsWith("user.loggedIn", StringComparison.Ordinal));
            topicNames.Should().Contain(t => t.EndsWith("home.updated", StringComparison.Ordinal));
            topicNames.Should().Contain(t => t.EndsWith("home.refreshed", StringComparison.Ordinal));

            // 4. Facelets .xhtml links to Seam @Name bean and resolves JavaBean property #{homePage.totalDocuments} -> getTotalDocuments
            var xhtmlCallers = await client.ExecuteCypherAsync(
                "MATCH (f:File)-[:CALLS]->(fn:Function) WHERE f.name = 'home.xhtml' RETURN fn.name AS name");
            xhtmlCallers.Rows.Select(r => r.GetStringProp("name")).Should().Contain(n => n.EndsWith("getTotalDocuments", StringComparison.Ordinal));

            var xhtmlBeanLinks = await client.ExecuteCypherAsync(
                "MATCH (f:File)-[:USES_TYPE]->(t:Type) WHERE f.name = 'home.xhtml' RETURN t.name AS name");
            xhtmlBeanLinks.Rows.Select(r => r.GetStringProp("name")).Should().Contain("HomePageAction");

            // 5. components.xml links to component class
            var compEdges = await client.ExecuteCypherAsync(
                "MATCH (f:File)-[:USES_TYPE]->(t:Type) WHERE f.name = 'components.xml' RETURN t.name AS name");
            compEdges.Rows.Select(r => r.GetStringProp("name")).Should().Contain("HomePageAction");

            // 6. Hibernate GenericDAO write lineage (documentDAO.makePersistent -> WRITES_TO HDocument)
            var docWriters = await client.ExecuteCypherAsync(
                "MATCH (w)-[:WRITES_TO]->(t:Table) WHERE t.name = 'HDocument' RETURN w.name AS name");
            docWriters.Rows.Select(r => r.GetStringProp("name")).Should().Contain(n =>
                n.Contains("saveDocument", StringComparison.Ordinal) || n.Contains("DocumentDAO", StringComparison.Ordinal));

            // 7. GWT (.gwt.xml + .ui.xml) and Groovy (.groovy) parsed and linked
            var uiBinderLinks = await client.ExecuteCypherAsync(
                "MATCH (f:File)-[:USES_TYPE]->(t:Type) WHERE f.name = 'HomePageAction.ui.xml' RETURN t.name AS name");
            uiBinderLinks.Rows.Select(r => r.GetStringProp("name")).Should().Contain("HomePageAction");

            var groovyTypes = await client.ExecuteCypherAsync(
                "MATCH (t:Type) WHERE t.name = 'SetupHelper' RETURN t.name AS name");
            groovyTypes.Rows.Should().NotBeEmpty();

            var faceletsParser = new FaceletsXhtmlParser();
            var xhtmlPath = Path.Combine(tempDir, "src", "main", "webapp", "home.xhtml");
            var tree = await faceletsParser.ParseAsync(xhtmlPath, "p1", "ws", tempDir);
            tree.FileNode.References.Should().Contain(r => r.TargetName == "homePage.getTotalDocuments");
            tree.FileNode.References.Should().NotContain(r => r.TargetName.StartsWith("msgs", StringComparison.OrdinalIgnoreCase));
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
