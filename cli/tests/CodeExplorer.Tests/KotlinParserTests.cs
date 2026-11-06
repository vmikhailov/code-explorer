using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Parser.Android;
using CodeExplorer.Parser.Kotlin;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class KotlinParserTests
{
    private const string SampleAndroidManifest = """
        <?xml version="1.0" encoding="utf-8"?>
        <manifest xmlns:android="http://schemas.android.com/apk/res/android">
            <uses-permission android:name="android.permission.INTERNET"/>
            <uses-permission android:name="android.permission.RECORD_AUDIO"/>
            <uses-permission android:name="android.permission.POST_NOTIFICATIONS"/>

            <application
                android:label="HearAI"
                android:icon="@mipmap/ic_launcher">
                <activity
                    android:name=".MainActivity"
                    android:exported="true">
                    <intent-filter>
                        <action android:name="android.intent.action.MAIN"/>
                        <category android:name="android.intent.category.LAUNCHER"/>
                    </intent-filter>
                    <intent-filter>
                        <action android:name="android.intent.action.VIEW"/>
                        <category android:name="android.intent.category.DEFAULT"/>
                        <category android:name="android.intent.category.BROWSABLE"/>
                        <data android:scheme="hearai"/>
                    </intent-filter>
                </activity>
                <provider
                    android:name="androidx.core.content.FileProvider"
                    android:authorities="com.zenaptica.hearai.apk_provider"
                    android:exported="false">
                </provider>
            </application>
        </manifest>
        """;

    private const string SampleMainActivityKt = """
        package com.zenaptica.hearai

        import android.content.Intent
        import android.os.Bundle
        import io.flutter.embedding.android.FlutterActivity
        import io.flutter.embedding.engine.FlutterEngine
        import io.flutter.plugin.common.MethodChannel

        class MainActivity : FlutterActivity() {
            private val installChannel = "hearai/apk_install"
            private val deepLinkChannel = "hearai/deep_link"

            override fun onCreate(savedInstanceState: Bundle?) {
                super.onCreate(savedInstanceState)
            }

            override fun onNewIntent(intent: Intent) {
                super.onNewIntent(intent)
            }

            fun installApk(path: String) {
            }
        }
        """;

    [Test]
    public void Test_AndroidManifestParser_ExtractsMetadataAndDeepLinks()
    {
        var manifest = AndroidManifestParser.ParseContent(SampleAndroidManifest);
        manifest.Should().NotBeNull();
        manifest!.AppLabel.Should().Be("HearAI");
        manifest.Permissions.Should().Contain("android.permission.INTERNET");
        manifest.Permissions.Should().Contain("android.permission.RECORD_AUDIO");
        manifest.Permissions.Should().Contain("android.permission.POST_NOTIFICATIONS");

        // Activities
        manifest.Activities.Count.Should().Be(1);
        var act = manifest.Activities[0];
        act.Name.Should().Be(".MainActivity");
        act.IsLauncher.Should().BeTrue();
        act.Exported.Should().BeTrue();

        // Deep links
        manifest.DeepLinks.Count.Should().Be(1);
        var dl = manifest.DeepLinks[0];
        dl.Scheme.Should().Be("hearai");
        dl.ActivityName.Should().Be(".MainActivity");

        // Providers
        manifest.Providers.Should().Contain("androidx.core.content.FileProvider");
    }

    [Test]
    public async Task Test_AndroidManifestFileParser_ProducesSemanticNodes()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_android_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var manifestPath = Path.Combine(tempDir, "AndroidManifest.xml");
            await File.WriteAllTextAsync(manifestPath, SampleAndroidManifest);

            var parser = new AndroidManifestFileParser();
            parser.CanParseFile("AndroidManifest.xml").Should().BeTrue();
            parser.CanParseFile("strings.xml").Should().BeFalse();

            var syntaxTree = await parser.ParseAsync(manifestPath, "parent", "test-ws", tempDir);
            syntaxTree.FileNode.Should().NotBeNull();

            var entryPoints = syntaxTree.FileNode.Children.OfType<EntryPointNode>().ToList();
            var endpoints = syntaxTree.FileNode.Children.OfType<EndpointNode>().ToList();

            (entryPoints.Any(ep => ep.EntryType == EntryPointType.Activity && ep.Name.Contains("MainActivity"))).Should().BeTrue();
            (entryPoints.Any(ep => ep.EntryType == EntryPointType.Provider && ep.Name.Contains("FileProvider"))).Should().BeTrue();

            endpoints.Count.Should().Be(1);
            var deepLinkEp = endpoints[0];
            deepLinkEp.Protocol.Should().Be("DeepLink");
            deepLinkEp.RouteTemplate.Should().Contain("hearai://");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task Test_KotlinFileParser_ExtractsClassesMethodsAndMethodChannels()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_kt_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var ktPath = Path.Combine(tempDir, "MainActivity.kt");
            await File.WriteAllTextAsync(ktPath, SampleMainActivityKt);

            var parser = new KotlinFileParser();
            parser.CanParse(".kt").Should().BeTrue();
            parser.CanParse(".kts").Should().BeTrue();
            parser.CanParse(".java").Should().BeFalse();

            var syntaxTree = await parser.ParseAsync(ktPath, "parent", "test-ws", tempDir);
            syntaxTree.FileNode.Should().NotBeNull();

            // Classes
            var typeNodes = syntaxTree.FileNode.Children.OfType<TypeNode>().ToList();
            typeNodes.Count.Should().Be(1);
            var mainActivityClass = typeNodes[0];
            mainActivityClass.Name.Should().Be("MainActivity");
            mainActivityClass.Extends.Should().Be("FlutterActivity");

            // Methods
            var methods = mainActivityClass.Children.OfType<FunctionNode>().Select(f => f.Name).ToList();
            methods.Should().Contain("onCreate");
            methods.Should().Contain("onNewIntent");
            methods.Should().Contain("installApk");

            // MethodChannels
            var channels = syntaxTree.FileNode.Children.OfType<EntryPointNode>().Where(ep => ep.EntryType == EntryPointType.Channel).ToList();
            var channelNames = channels.Select(c => c.Name).ToList();
            channelNames.Should().Contain("MethodChannel hearai/apk_install");
            channelNames.Should().Contain("MethodChannel hearai/deep_link");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public void Test_GradleMultiProjectContainer_Deduplication()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_gradle_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            // Root multi-project container with settings.gradle.kts and build.gradle.kts (no applied project plugins)
            var rootSettings = """
                pluginManagement {
                    repositories { google(); mavenCentral() }
                }
                plugins {
                    id("com.android.application") version "9.0.1" apply false
                    id("org.jetbrains.kotlin.android") version "2.3.20" apply false
                }
                include(":app")
                """;

            var rootBuild = """
                allprojects {
                    repositories { google(); mavenCentral() }
                }
                subprojects {
                    project.evaluationDependsOn(":app")
                }
                """;

            File.WriteAllText(Path.Combine(tempDir, "settings.gradle.kts"), rootSettings);
            File.WriteAllText(Path.Combine(tempDir, "build.gradle.kts"), rootBuild);

            // Subproject :app
            var appDir = Path.Combine(tempDir, "app");
            Directory.CreateDirectory(appDir);
            var appBuild = """
                plugins {
                    id("com.android.application")
                    id("dev.flutter.flutter-gradle-plugin")
                }
                android {
                    namespace = "com.zenaptica.hearai"
                    defaultConfig {
                        applicationId = "com.zenaptica.hearai"
                        minSdk = 26
                        targetSdk = 35
                    }
                }
                """;
            File.WriteAllText(Path.Combine(appDir, "build.gradle.kts"), appBuild);

            var appSrcMain = Path.Combine(appDir, "src", "main");
            Directory.CreateDirectory(appSrcMain);
            File.WriteAllText(Path.Combine(appSrcMain, "AndroidManifest.xml"), SampleAndroidManifest);

            var parser = new KotlinProjectParser();

            // Root container should NOT be detected as project
            var rootFiles = Directory.GetFiles(tempDir);
            parser.IsProjectDirectory(tempDir, rootFiles).Should().BeFalse("Root Gradle multi-project container should not be detected as project node");

            // Subproject :app SHOULD be detected as project
            var appFiles = Directory.GetFiles(appDir);
            parser.IsProjectDirectory(appDir, appFiles).Should().BeTrue(":app subproject should be detected as project");

            // Project name should be HearAI (Android)
            var projName = parser.GetProjectName(appDir, appFiles);
            projName.Should().Be("HearAI (Android)");

            // Manifest properties
            var props = parser.ExtractManifestProperties(appDir, appFiles);
            props["manifest_type"].Should().Be("mobile");
            props["framework"].Should().Be("Flutter Android");
            props["sdk"].Should().Be("Android");
            props["namespace"].Should().Be("com.zenaptica.hearai");
            props["application_id"].Should().Be("com.zenaptica.hearai");
            props["min_sdk"].Should().Be("26");
            props["target_sdk"].Should().Be("35");
            props["has_deep_links"].Should().Be("true");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task Test_KotlinParser_ParsesKtorRoutingAndExposedOrmAndHttpClient()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "kotlin_ktor_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var routesCode = """
                package org.jetbrains.kotlinconf.backend.routes

                import io.ktor.server.routing.*
                import io.ktor.server.response.*
                import io.ktor.http.*

                fun Route.votingRoutes() {
                    route("vote") {
                        get {
                            call.respond("ok")
                        }
                        post {
                            call.respond("voted")
                        }
                        get("all") {
                            call.respond("all-votes")
                        }
                    }
                    route("feedback") {
                        post {
                            call.respond("feedback")
                        }
                    }
                }

                fun Application.mainModule() {
                    routing {
                        get("/healthz") {
                            call.respond(HttpStatusCode.OK)
                        }
                        staticResources("/admin", "admin")
                        votingRoutes()
                    }
                }
                """;

            var exposedCode = """
                package org.jetbrains.kotlinconf.backend.schema

                import org.jetbrains.exposed.v1.core.Table
                import org.jetbrains.exposed.v1.core.Column
                import org.jetbrains.exposed.v1.jdbc.selectAll
                import org.jetbrains.exposed.v1.jdbc.insert
                import org.jetbrains.exposed.v1.jdbc.update
                import org.jetbrains.exposed.v1.jdbc.deleteWhere
                import org.jetbrains.exposed.v1.jdbc.transactions.suspendTransaction

                internal object Users : Table("users") {
                    val userId: Column<String> = varchar("uuid", 50)
                }

                internal object Votes : Table() {
                    val userId: Column<String> = varchar("uuid", 50)
                }

                internal class Repository {
                    suspend fun getUser(id: String) = suspendTransaction {
                        Users.selectAll()
                    }

                    suspend fun saveUser(id: String) = suspendTransaction {
                        Users.insert { it[userId] = id }
                    }

                    suspend fun removeVote(id: String) = suspendTransaction {
                        Votes.deleteWhere { Votes.userId eq id }
                    }

                    fun runRaw() {
                        exec("SELECT COUNT(*) FROM Users")
                    }
                }
                """;

            var clientCode = """
                package org.jetbrains.kotlinconf.network

                import io.ktor.client.HttpClient
                import io.ktor.client.request.get
                import io.ktor.client.request.post

                class YearlyApi(private val client: HttpClient) {
                    suspend fun getConference() {
                        client.get { apiUrl("conference") }
                    }

                    suspend fun submitVote() {
                        client.post { apiUrl("vote") }
                    }

                    suspend fun fetchExternal(url: String) {
                        client.get("https://sessionize.com/api/v2/view")
                    }
                }
                """;

            var routesPath = Path.Combine(tempDir, "Routes.kt");
            var exposedPath = Path.Combine(tempDir, "Schema.kt");
            var clientPath = Path.Combine(tempDir, "Client.kt");

            File.WriteAllText(routesPath, routesCode);
            File.WriteAllText(exposedPath, exposedCode);
            File.WriteAllText(clientPath, clientCode);

            var parser = new KotlinFileParser();

            // 1. Verify Ktor Routing
            var routesTree = await parser.ParseAsync(routesPath, "ws", "ws", tempDir);
            var endpoints = routesTree.FileNode.Children.OfType<EndpointNode>().ToList();
            endpoints.Should().NotBeEmpty();

            // Check /vote routes
            endpoints.Should().Contain(e => e.RouteTemplate == "/vote" && e.HttpMethod == "GET");
            endpoints.Should().Contain(e => e.RouteTemplate == "/vote" && e.HttpMethod == "POST");
            endpoints.Should().Contain(e => e.RouteTemplate == "/vote/all" && e.HttpMethod == "GET");
            endpoints.Should().Contain(e => e.RouteTemplate == "/feedback" && e.HttpMethod == "POST");
            endpoints.Should().Contain(e => e.RouteTemplate == "/healthz" && e.HttpMethod == "GET");
            endpoints.Should().Contain(e => e.RouteTemplate == "/admin" && e.HttpMethod == "GET");

            // Verify EXPOSED_BY reference
            var exposedByRels = routesTree.FileNode.References.Where(r => r.Kind == "EXPOSED_BY").ToList();
            exposedByRels.Should().NotBeEmpty();

            // 2. Verify Exposed ORM Table definitions and operations
            var exposedTree = await parser.ParseAsync(exposedPath, "ws", "ws", tempDir);
            var tables = exposedTree.FileNode.Children.OfType<TableNode>().ToList();
            tables.Should().Contain(t => t.Name == "users");
            tables.Should().Contain(t => t.Name == "Votes");

            var queries = exposedTree.FileNode.Children.OfType<QueryNode>().ToList();
            queries.Should().Contain(q => q.Operation == "SELECT" && q.Name.Contains("Users"));
            queries.Should().Contain(q => q.Operation == "INSERT" && q.Name.Contains("Users"));
            queries.Should().Contain(q => q.Operation == "DELETE" && q.Name.Contains("Votes"));
            queries.Should().Contain(q => q.Name.Contains("SQL Query"));

            // 3. Verify Ktor HTTP Client calls
            var clientTree = await parser.ParseAsync(clientPath, "ws", "ws", tempDir);
            var extServices = clientTree.FileNode.Children.OfType<ExternalServiceNode>().ToList();
            extServices.Should().NotBeEmpty();
            extServices.Should().Contain(e => (e.Url == "/conference" || e.Path == "/conference") && e.Method == "GET");
            extServices.Should().Contain(e => (e.Url == "/vote" || e.Path == "/vote") && e.Method == "POST");
            extServices.Should().Contain(e => (e.Url != null && e.Url.Contains("sessionize.com")) || e.DomainOrService == "sessionize.com");

            var callRels = clientTree.FileNode.References.Where(r => r.Kind == "CALLS").ToList();
            callRels.Should().NotBeEmpty();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task Test_KotlinParser_RoomOrm_And_Retrofit()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "kotlin_room_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var entityPath = Path.Combine(tempDir, "TopicEntity.kt");
            var daoPath = Path.Combine(tempDir, "TopicDao.kt");
            var dbPath = Path.Combine(tempDir, "AppDatabase.kt");
            var retrofitPath = Path.Combine(tempDir, "NetworkApi.kt");

            await File.WriteAllTextAsync(entityPath, """
                package com.example.db
                import androidx.room.Entity
                import androidx.room.PrimaryKey

                @Entity(tableName = "topics")
                data class TopicEntity(
                    @PrimaryKey val id: String,
                    val name: String
                )
                """);

            await File.WriteAllTextAsync(daoPath, """"
                package com.example.db
                import androidx.room.Dao
                import androidx.room.Query
                import androidx.room.Insert
                import androidx.room.Upsert
                import kotlinx.coroutines.flow.Flow

                @Dao
                interface TopicDao {
                    @Query(value = """
                        SELECT * FROM topics
                        WHERE id = :topicId
                    """)
                    fun getTopic(topicId: String): Flow<TopicEntity>

                    @Query("DELETE FROM topics WHERE id IN (:ids)")
                    suspend fun deleteTopics(ids: List<String>)

                    @Insert
                    suspend fun insertTopics(entities: List<TopicEntity>)

                    @Upsert
                    suspend fun upsertTopics(entities: List<TopicEntity>)
                }
                """");

            await File.WriteAllTextAsync(dbPath, """
                package com.example.db
                import androidx.room.Database
                import androidx.room.RoomDatabase

                @Database(
                    entities = [
                        TopicEntity::class
                    ],
                    version = 1
                )
                abstract class AppDatabase : RoomDatabase() {
                    abstract fun topicDao(): TopicDao
                }
                """);

            await File.WriteAllTextAsync(retrofitPath, """
                package com.example.network
                import retrofit2.http.GET
                import retrofit2.http.POST
                import retrofit2.http.Query

                interface ApiClient {
                    @GET(value = "topics")
                    suspend fun getTopics(@Query("id") id: String?): List<String>

                    @POST("topics/sync")
                    suspend fun syncTopics(): Boolean
                }
                """);

            var parser = new KotlinFileParser();

            // 1. Verify Entity table extraction
            var entityTree = await parser.ParseAsync(entityPath, "ws", "ws", tempDir);
            var tables = entityTree.FileNode.Children.OfType<TableNode>().ToList();
            tables.Should().Contain(t => t.Name == "topics");

            // 2. Verify Room DAO queries and CRUD methods
            var daoTree = await parser.ParseAsync(daoPath, "ws", "ws", tempDir);
            var queries = daoTree.FileNode.Children.OfType<QueryNode>().ToList();
            queries.Should().HaveCountGreaterThanOrEqualTo(4);
            queries.Should().Contain(q => q.Operation == "SELECT" && q.QueryText.Contains("topics"));
            queries.Should().Contain(q => q.Operation == "DELETE" && q.QueryText.Contains("topics"));
            queries.Should().Contain(q => q.Operation == "INSERT");
            queries.Should().Contain(q => q.Operation == "UPSERT");

            // Verify READS_FROM and WRITES_TO
            var readsFrom = daoTree.FileNode.References.Where(r => r.Kind == "READS_FROM").ToList();
            readsFrom.Should().NotBeEmpty();
            var writesTo = daoTree.FileNode.References.Where(r => r.Kind == "WRITES_TO").ToList();
            writesTo.Should().NotBeEmpty();

            // Verify ACCESSED_BY
            var accessedBy = daoTree.FileNode.References.Where(r => r.Kind == "ACCESSED_BY").ToList();
            accessedBy.Should().NotBeEmpty();

            // 3. Verify Room Database detection
            var dbTree = await parser.ParseAsync(dbPath, "ws", "ws", tempDir);
            var dbs = dbTree.FileNode.Children.OfType<DatabaseNode>().ToList();
            dbs.Should().Contain(d => d.Engine == "SQLite");

            // 4. Verify Retrofit Client calls (ExternalService)
            var retroTree = await parser.ParseAsync(retrofitPath, "ws", "ws", tempDir);
            var extServices = retroTree.FileNode.Children.OfType<ExternalServiceNode>().ToList();
            extServices.Should().HaveCount(2);
            extServices.Should().Contain(e => e.Method == "GET" && (e.Url == "/topics" || e.Path == "/topics"));
            extServices.Should().Contain(e => e.Method == "POST" && (e.Url == "/topics/sync" || e.Path == "/topics/sync"));
            var retroCalls = retroTree.FileNode.References.Where(r => r.Kind == "CALLS").ToList();
            retroCalls.Should().HaveCount(2);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}
