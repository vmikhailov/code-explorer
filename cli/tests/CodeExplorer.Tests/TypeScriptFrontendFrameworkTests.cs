using System.Threading.Channels;
using CodeExplorer.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Layers;
using CodeExplorer.Parser.TypeScript;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
[NonParallelizable]
public class TypeScriptFrontendFrameworkTests
{
    private sealed class TestWorkspace : IDisposable
    {
        public string DirectoryPath { get; }
        public string FilePath { get; }
        public SyntaxTree SyntaxTree { get; }
        public FileNode FileNode => SyntaxTree.FileNode;
        public ParsingContext Context { get; }

        private TestWorkspace(string dir, string file, SyntaxTree tree, ParsingContext ctx)
        {
            DirectoryPath = dir;
            FilePath = file;
            SyntaxTree = tree;
            Context = ctx;
        }

        public static async Task<TestWorkspace> CreateAsync(string code, string fileName = "app.ts")
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "ts_frontend_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var filePath = Path.Combine(tempDir, fileName);
            await File.WriteAllTextAsync(filePath, code);

            var parser = new TypeScriptParser();
            var channel = Channel.CreateUnbounded<Func<Task>>();
            var client = new InMemoryGraphClient();
            var ctx = new ParsingContext(tempDir, tempDir, client, channel);

            var syntaxTree = await parser.ParseAsync(filePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath, ctx);
            return new TestWorkspace(tempDir, filePath, syntaxTree, ctx);
        }

        public void Dispose()
        {
            SyntaxTree.Dispose();
            try { Directory.Delete(DirectoryPath, true); } catch { }
        }
    }

    private static List<T> FindNodes<T>(IEnumerable<IOntologyNode> nodes) where T : IOntologyNode
    {
        var result = new List<T>();
        foreach (var node in nodes)
        {
            if (node is T match) result.Add(match);
            result.AddRange(FindNodes<T>(node.Children));
        }
        return result;
    }

    private static List<Reference> FindAllReferences(FileNode fileNode)
    {
        var result = new List<Reference>(fileNode.References);
        result.AddRange(FindReferences(fileNode.Children));
        return result;
    }

    private static List<Reference> FindReferences(IEnumerable<IOntologyNode> nodes)
    {
        var result = new List<Reference>();
        foreach (var node in nodes)
        {
            result.AddRange(node.References);
            result.AddRange(FindReferences(node.Children));
        }
        return result;
    }

    [Test]
    public async Task Test_React_RootMount_And_LazyImport()
    {
        var code = """
import React, { lazy } from 'react';
import { createRoot } from 'react-dom/client';

const UserModal = lazy(() => import('./UserModal'));

function App() {
  return null;
}

const container = { id: 'root' };
const root = createRoot(container as any);
root.render(React.createElement(App, null));
""";
        using var ws = await TestWorkspace.CreateAsync(code, "index.tsx");

        var entryPoints = FindNodes<EntryPointNode>(ws.FileNode.Children);
        var reactRoot = entryPoints.FirstOrDefault(e => e.EntryType == "React" && e.Name == "Root");
        reactRoot.Should().NotBeNull("Expected React:Root EntryPoint to be detected");

        // App should be referenced by the root mount
        reactRoot!.References.Any(r => r.TargetName == "App" && r.Kind == "DEPENDS_ON").Should().BeTrue();

        // Lazy import should produce a reference to ./UserModal
        var allRefs = FindAllReferences(ws.FileNode);
        allRefs.Any(r => r.TargetName == "./UserModal" && r.Kind == "DEPENDS_ON").Should().BeTrue();
    }

    [Test]
    public async Task Test_Vue_CreateApp_Mount_And_AsyncComponent()
    {
        var code = """
import { createApp, defineAsyncComponent } from 'vue';

const UserProfile = defineAsyncComponent(() => import('./UserProfile'));

const App = {
  template: '<h1>Hello Vue</h1>'
};

createApp(App).mount('#app');
""";
        using var ws = await TestWorkspace.CreateAsync(code, "main.ts");

        var entryPoints = FindNodes<EntryPointNode>(ws.FileNode.Children);
        var vueRoot = entryPoints.FirstOrDefault(e => e.EntryType == "Vue" && e.Name == "Root");
        vueRoot.Should().NotBeNull("Expected Vue:Root EntryPoint to be detected");

        vueRoot!.References.Any(r => r.TargetName == "App" && r.Kind == "DEPENDS_ON").Should().BeTrue();

        var allRefs = FindAllReferences(ws.FileNode);
        allRefs.Any(r => r.TargetName == "./UserProfile" && r.Kind == "DEPENDS_ON").Should().BeTrue();
    }

    [Test]
    public async Task Test_Angular_BootstrapApplication_And_ComponentDecorator()
    {
        var code = """
import { Component, bootstrapApplication } from '@angular/core';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html'
})
export class AppComponent {}

bootstrapApplication(AppComponent);
""";
        using var ws = await TestWorkspace.CreateAsync(code, "main.ts");

        var entryPoints = FindNodes<EntryPointNode>(ws.FileNode.Children);
        var angularBootstrap = entryPoints.FirstOrDefault(e => e.EntryType == "Angular" && e.Name == "Bootstrap");
        angularBootstrap.Should().NotBeNull("Expected Angular:Bootstrap EntryPoint");
        angularBootstrap!.References.Any(r => r.TargetName == "AppComponent" && r.Kind == "DEPENDS_ON").Should().BeTrue();

        var compDec = entryPoints.FirstOrDefault(e => e.EntryType == "Component" && e.Name == "app-root");
        compDec.Should().NotBeNull("Expected Component:app-root EntryPoint");
        compDec!.References.Any(r => r.TargetName == "./app.component.html" && r.Kind == "DEPENDS_ON").Should().BeTrue();
    }

    [Test]
    public async Task Test_Svelte_Mount_And_New()
    {
        var code = """
import { mount } from 'svelte';

class AppRoot {}

mount(AppRoot, {
  target: '#app'
});
""";
        using var ws = await TestWorkspace.CreateAsync(code, "main.ts");

        var entryPoints = FindNodes<EntryPointNode>(ws.FileNode.Children);
        var svelteMount = entryPoints.FirstOrDefault(e => e.EntryType == "Svelte" && e.Name == "Mount");
        svelteMount.Should().NotBeNull("Expected Svelte:Mount EntryPoint");
        svelteMount!.References.Any(r => r.TargetName == "AppRoot" && r.Kind == "DEPENDS_ON").Should().BeTrue();
    }

    [Test]
    public async Task Test_Vite_DefineConfig()
    {
        var code = """
import { defineConfig } from 'vite';

function reactPlugin() { return {}; }
function vuePlugin() { return {}; }

export default defineConfig({
  plugins: [reactPlugin(), vuePlugin()]
});
""";
        using var ws = await TestWorkspace.CreateAsync(code, "vite.config.ts");

        var entryPoints = FindNodes<EntryPointNode>(ws.FileNode.Children);
        var viteConfig = entryPoints.FirstOrDefault(e => e.EntryType == "Vite" && e.Name == "Config");
        viteConfig.Should().NotBeNull("Expected Vite:Config EntryPoint");
        viteConfig!.References.Any(r => r.TargetName == "reactPlugin" && r.Kind == "DEPENDS_ON").Should().BeTrue();
        viteConfig!.References.Any(r => r.TargetName == "vuePlugin" && r.Kind == "DEPENDS_ON").Should().BeTrue();
    }

    [Test]
    public async Task Test_Angular_Route_Data_Services_Extracted_As_ExternalService()
    {
        using var _ = await TestRegistryLock.AcquireAsync();
        try
        {
            var envCode = """
export const environment = {
  bff_service: '/bff_service',
  domain_service: '/domain_service'
};
""";
            ConstantRegistry.Clear();
            ConstantRegistry.ScanAndRegister("src/environments/environment.ts", envCode, "fe");

            var routeCode = """
import { Routes } from '@angular/router';
import { environment } from 'src/environments/environment';

const routes: Routes = [
  {
    path: 'custom',
    data: {
      services: [
        environment.bff_service,
        environment.domain_service
      ]
    }
  }
];
""";
            using var ws = await TestWorkspace.CreateAsync(routeCode, "routing.module.ts");

            var extServices = FindNodes<ExternalServiceNode>(ws.FileNode.Children);
            extServices.Should().HaveCount(2);
            extServices.Any(s => s.DomainOrService == "bff_service").Should().BeTrue();
            extServices.Any(s => s.DomainOrService == "domain_service").Should().BeTrue();
        }
        finally
        {
            ConstantRegistry.Clear();
        }
    }
}
