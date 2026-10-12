using AwesomeAssertions;
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

            (entryPoints.Any(ep => ep.EntryType == "activity" && ep.Name.Contains("MainActivity"))).Should().BeTrue();
            (entryPoints.Any(ep => ep.EntryType == "provider" && ep.Name.Contains("FileProvider"))).Should().BeTrue();

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
            mainActivityClass.Extensions?["extends"].Should().Be("FlutterActivity");

            // Methods
            var methods = mainActivityClass.Children.OfType<FunctionNode>().Select(f => f.Name).ToList();
            methods.Should().Contain("onCreate");
            methods.Should().Contain("onNewIntent");
            methods.Should().Contain("installApk");

            // MethodChannels
            var channels = syntaxTree.FileNode.Children.OfType<EntryPointNode>().Where(ep => ep.EntryType == "channel").ToList();
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
}
