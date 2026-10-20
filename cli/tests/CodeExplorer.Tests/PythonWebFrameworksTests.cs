using System.Threading.Channels;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Layers;
using CodeExplorer.Parser.Python;
using NUnit.Framework;

namespace CodeExplorer.Tests;

public class PythonWebFrameworksTests
{
    private static List<EndpointNode> FindEndpointNodes(IEnumerable<IOntologyNode> nodes)
    {
        var result = new List<EndpointNode>();
        foreach (var node in nodes)
        {
            if (node is EndpointNode ep)
            {
                result.Add(ep);
            }
            result.AddRange(FindEndpointNodes(node.Children));
        }
        return result;
    }

    [Test]
    public async Task Flask_RoutesAndBlueprints_ExtractsEndpointsAndHandlers()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce-test-flask-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, "app.py");

        var code = @"from flask import Flask, Blueprint

app = Flask(__name__)
bp = Blueprint('api_v1', __name__, url_prefix='/api/v1')

@app.route('/health')
def health_check():
    return {'status': 'healthy'}

@bp.route('/items', methods=['POST'])
def create_item():
    return {'id': 1}

@app.get('/users/<int:user_id>')
def get_user(user_id):
    return {'id': user_id}
";
        await File.WriteAllTextAsync(filePath, code);

        try
        {
            var channel = Channel.CreateUnbounded<Func<Task>>();
            await using var client = new InMemoryGraphClient();
            var ctx = new ParsingContext(tempDir, tempDir, client, channel);
            var parser = new PythonParser();

            using var syntaxTree = await parser.ParseAsync(filePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

            var fileNode = syntaxTree.FileNode;
            fileNode.Should().NotBeNull();

            var endpoints = FindEndpointNodes(fileNode.Children);
            endpoints.Count.Should().BeGreaterThanOrEqualTo(3);

            var health = endpoints.FirstOrDefault(e => e.Name == "GET:/health");
            health.Should().NotBeNull();
            health!.RouteTemplate.Should().Be("/health");
            health.HttpMethod.Should().Be("GET");
            health.References.Any(r => r.TargetName == "health_check").Should().BeTrue();

            var items = endpoints.FirstOrDefault(e => e.Name == "POST:/api/v1/items");
            items.Should().NotBeNull();
            items!.RouteTemplate.Should().Be("/api/v1/items");
            items.HttpMethod.Should().Be("POST");
            items.References.Any(r => r.TargetName == "create_item").Should().BeTrue();

            var users = endpoints.FirstOrDefault(e => e.Name == "GET:/users/<int:user_id>");
            users.Should().NotBeNull();
            users!.HttpMethod.Should().Be("GET");
            users.References.Any(r => r.TargetName == "get_user").Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task Django_UrlPatterns_ExtractsEndpointsAndHandlers()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce-test-django-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, "urls.py");

        var code = @"from django.urls import path, re_path

def order_list(request):
    pass

class UserDetailView:
    @classmethod
    def as_view(cls):
        pass

urlpatterns = [
    path('api/v1/orders/', order_list),
    path('users/<int:id>/', UserDetailView.as_view()),
    re_path(r'^items/(?P<id>[0-9]+)/$', order_list),
]
";
        await File.WriteAllTextAsync(filePath, code);

        try
        {
            var channel = Channel.CreateUnbounded<Func<Task>>();
            await using var client = new InMemoryGraphClient();
            var ctx = new ParsingContext(tempDir, tempDir, client, channel);
            var parser = new PythonParser();

            using var syntaxTree = await parser.ParseAsync(filePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

            var fileNode = syntaxTree.FileNode;
            fileNode.Should().NotBeNull();

            var endpoints = FindEndpointNodes(fileNode.Children);
            endpoints.Count.Should().BeGreaterThanOrEqualTo(3);

            var orders = endpoints.FirstOrDefault(e => e.Name == "GET:/api/v1/orders/");
            orders.Should().NotBeNull();
            orders!.References.Any(r => r.TargetName == "order_list").Should().BeTrue();

            var users = endpoints.FirstOrDefault(e => e.Name == "GET:/users/<int:id>/");
            users.Should().NotBeNull();
            users!.References.Any(r => r.TargetName.Contains("UserDetailView")).Should().BeTrue();

            var items = endpoints.FirstOrDefault(e => e.Name.Contains("items/"));
            items.Should().NotBeNull();
            items!.References.Any(r => r.TargetName == "order_list").Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}
