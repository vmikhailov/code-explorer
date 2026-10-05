using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser.Android;
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
        Assert.That(manifest, Is.Not.Null);
        Assert.That(manifest!.AppLabel, Is.EqualTo("HearAI"));
        Assert.That(manifest.Permissions, Contains.Item("android.permission.INTERNET"));
        Assert.That(manifest.Permissions, Contains.Item("android.permission.RECORD_AUDIO"));
        Assert.That(manifest.Permissions, Contains.Item("android.permission.POST_NOTIFICATIONS"));

        // Activities
        Assert.That(manifest.Activities.Count, Is.EqualTo(1));
        var act = manifest.Activities[0];
        Assert.That(act.Name, Is.EqualTo(".MainActivity"));
        Assert.That(act.IsLauncher, Is.True);
        Assert.That(act.Exported, Is.True);

        // Deep links
        Assert.That(manifest.DeepLinks.Count, Is.EqualTo(1));
        var dl = manifest.DeepLinks[0];
        Assert.That(dl.Scheme, Is.EqualTo("hearai"));
        Assert.That(dl.ActivityName, Is.EqualTo(".MainActivity"));

        // Providers
        Assert.That(manifest.Providers, Contains.Item("androidx.core.content.FileProvider"));
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
            Assert.That(parser.CanParseFile("AndroidManifest.xml"), Is.True);
            Assert.That(parser.CanParseFile("strings.xml"), Is.False);

            var syntaxTree = await parser.ParseAsync(manifestPath, "parent", "test-ws", tempDir);
            Assert.That(syntaxTree.FileNode, Is.Not.Null);

            var entryPoints = syntaxTree.FileNode.Children.OfType<EntryPointNode>().ToList();
            var endpoints = syntaxTree.FileNode.Children.OfType<EndpointNode>().ToList();

            Assert.That(entryPoints.Any(ep => ep.EntryType == "activity" && ep.Name.Contains("MainActivity")), Is.True);
            Assert.That(entryPoints.Any(ep => ep.EntryType == "provider" && ep.Name.Contains("FileProvider")), Is.True);

            Assert.That(endpoints.Count, Is.EqualTo(1));
            var deepLinkEp = endpoints[0];
            Assert.That(deepLinkEp.Protocol, Is.EqualTo("DeepLink"));
            Assert.That(deepLinkEp.RouteTemplate, Contains.Substring("hearai://"));
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
            Assert.That(parser.CanParse(".kt"), Is.True);
            Assert.That(parser.CanParse(".kts"), Is.True);
            Assert.That(parser.CanParse(".java"), Is.False);

            var syntaxTree = await parser.ParseAsync(ktPath, "parent", "test-ws", tempDir);
            Assert.That(syntaxTree.FileNode, Is.Not.Null);

            // Classes
            var typeNodes = syntaxTree.FileNode.Children.OfType<TypeNode>().ToList();
            Assert.That(typeNodes.Count, Is.EqualTo(1));
            var mainActivityClass = typeNodes[0];
            Assert.That(mainActivityClass.Name, Is.EqualTo("MainActivity"));
            Assert.That(mainActivityClass.Extensions?["extends"], Is.EqualTo("FlutterActivity"));

            // Methods
            var methods = mainActivityClass.Children.OfType<FunctionNode>().Select(f => f.Name).ToList();
            Assert.That(methods, Contains.Item("onCreate"));
            Assert.That(methods, Contains.Item("onNewIntent"));
            Assert.That(methods, Contains.Item("installApk"));

            // MethodChannels
            var channels = syntaxTree.FileNode.Children.OfType<EntryPointNode>().Where(ep => ep.EntryType == "channel").ToList();
            var channelNames = channels.Select(c => c.Name).ToList();
            Assert.That(channelNames, Contains.Item("MethodChannel hearai/apk_install"));
            Assert.That(channelNames, Contains.Item("MethodChannel hearai/deep_link"));
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
            Assert.That(parser.IsProjectDirectory(tempDir, rootFiles), Is.False, "Root Gradle multi-project container should not be detected as project node");

            // Subproject :app SHOULD be detected as project
            var appFiles = Directory.GetFiles(appDir);
            Assert.That(parser.IsProjectDirectory(appDir, appFiles), Is.True, ":app subproject should be detected as project");

            // Project name should be HearAI (Android)
            var projName = parser.GetProjectName(appDir, appFiles);
            Assert.That(projName, Is.EqualTo("HearAI (Android)"));

            // Manifest properties
            var props = parser.ExtractManifestProperties(appDir, appFiles);
            Assert.That(props["manifest_type"], Is.EqualTo("mobile"));
            Assert.That(props["framework"], Is.EqualTo("Flutter Android"));
            Assert.That(props["sdk"], Is.EqualTo("Android"));
            Assert.That(props["namespace"], Is.EqualTo("com.zenaptica.hearai"));
            Assert.That(props["application_id"], Is.EqualTo("com.zenaptica.hearai"));
            Assert.That(props["min_sdk"], Is.EqualTo("26"));
            Assert.That(props["target_sdk"], Is.EqualTo("35"));
            Assert.That(props["has_deep_links"], Is.EqualTo("true"));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}
