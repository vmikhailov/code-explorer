using System.Text.Json;
using NUnit.Framework;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Mcp;
using CodeExplorer.Core.Parser;
using CodeExplorer.Parser.CSharp;
using CodeExplorer.Parser.Java;
using CodeExplorer.Parser.TypeScript;

namespace CodeExplorer.Tests;

[TestFixture]
[Category("Integration")]
public class SecurityAndProtocolTests
{
    private string _tempWorkspace = null!;
    private string _dbPath = null!;
    private SqliteGraphClient _client = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _tempWorkspace = Path.Combine(Path.GetTempPath(), "codeexplorer_security_test_" + Guid.NewGuid()).Replace('\\', '/');
        Directory.CreateDirectory(_tempWorkspace);

        // 1. C# ASP.NET Core with [Authorize(Roles = "Admin")], [AllowAnonymous] and HotChocolate GraphQL
        var csharpDir = Path.Combine(_tempWorkspace, "DotNetApp").Replace('\\', '/');
        Directory.CreateDirectory(csharpDir);
        await File.WriteAllTextAsync(Path.Combine(csharpDir, "DotNetApp.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");

        var csharpCode = @"
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace DotNetApp.Controllers;

[ApiController]
[Route(""api/[controller]"")]
public class SecureOrdersController : ControllerBase
{
    [Authorize(Roles = ""Admin,Manager"")]
    [HttpPost(""admin-refund"")]
    public IActionResult RefundOrder() => Ok();

    [AllowAnonymous]
    [HttpGet(""public-catalog"")]
    public IActionResult GetCatalog() => Ok();
}
";
        await File.WriteAllTextAsync(Path.Combine(csharpDir, "SecureOrdersController.cs"), csharpCode);

        // 2. Java Spring Boot with @PreAuthorize and @PermitAll / @GetMapping
        var javaDir = Path.Combine(_tempWorkspace, "JavaApp").Replace('\\', '/');
        Directory.CreateDirectory(javaDir);
        await File.WriteAllTextAsync(Path.Combine(javaDir, "pom.xml"), "<project></project>");

        var javaCode = @"
package com.example.demo;

import org.springframework.web.bind.annotation.*;
import org.springframework.security.access.prepost.PreAuthorize;
import jakarta.annotation.security.RolesAllowed;
import jakarta.annotation.security.PermitAll;

@RestController
@RequestMapping(""/api/users"")
public class UserController {

    @RolesAllowed(""ADMIN"")
    @DeleteMapping(""/{id}"")
    public void deleteUser(@PathVariable String id) {}

    @PermitAll
    @GetMapping(""/health"")
    public String health() { return ""OK""; }
}
";
        await File.WriteAllTextAsync(Path.Combine(javaDir, "UserController.java"), javaCode);

        // 3. TypeScript NestJS with @Roles('admin') and @Public()
        var tsDir = Path.Combine(_tempWorkspace, "TsApp").Replace('\\', '/');
        Directory.CreateDirectory(tsDir);
        await File.WriteAllTextAsync(Path.Combine(tsDir, "package.json"), "{}");

        var tsCode = @"
import { Controller, Get, Post } from '@nestjs/common';

@Controller('payments')
export class PaymentsController {
    @Roles('finance-admin')
    @Post('charge')
    charge() {}

    @Public()
    @Get('plans')
    getPlans() {}
}
";
        await File.WriteAllTextAsync(Path.Combine(tsDir, "PaymentsController.ts"), tsCode);

        _dbPath = Path.Combine(_tempWorkspace, "security_graph.db");
        _client = new SqliteGraphClient(_dbPath);

        WorkspaceIndexer.Register(new CSharpParser());
        WorkspaceIndexer.Register(new JavaParser());
        WorkspaceIndexer.Register(new TypeScriptParser());

        var indexer = new WorkspaceIndexer(_client);
        await indexer.IndexAsync(_tempWorkspace, _tempWorkspace, clear: true);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _client.DisposeAsync();
        if (Directory.Exists(_tempWorkspace))
        {
            try { Directory.Delete(_tempWorkspace, true); } catch { }
        }
    }

    [Test]
    public async Task Test_CSharp_SecurityBoundaryExtraction()
    {
        // Query endpoints with roles
        var query = "MATCH (e:Endpoint) WHERE e.name CONTAINS 'admin-refund' RETURN e.name AS name, e.required_roles AS roles, e.protocol AS protocol";
        var res = await _client.ExecuteQueryAsync(query);
        using var doc = JsonDocument.Parse(res);
        var array = doc.RootElement.EnumerateArray().ToList();

        Assert.That(array.Count, Is.GreaterThan(0), "No endpoint with required_roles found for C# admin-refund!");
        var roles = array[0].GetProperty("roles").GetString();
        Assert.That(roles, Does.Contain("Admin"));
        var protocol = array[0].GetProperty("protocol").GetString();
        Assert.That(protocol, Is.EqualTo("REST"));

        // Query anonymous endpoints
        var anonQuery = "MATCH (e:Endpoint {is_anonymous: true}) WHERE e.name CONTAINS 'public-catalog' RETURN e.name AS name";
        var anonRes = await _client.ExecuteQueryAsync(anonQuery);
        using var anonDoc = JsonDocument.Parse(anonRes);
        Assert.That(anonDoc.RootElement.GetArrayLength(), Is.GreaterThan(0), "Anonymous endpoint not flagged as is_anonymous: true in C#!");
    }

    [Test]
    public async Task Test_Java_SecurityBoundaryExtraction()
    {
        // Query role-guarded endpoint in Java
        var query = "MATCH (e:Endpoint) WHERE e.name CONTAINS 'deleteUser' OR e.route_template CONTAINS '{id}' RETURN e.name AS name, e.required_roles AS roles";
        var res = await _client.ExecuteQueryAsync(query);
        using var doc = JsonDocument.Parse(res);
        var array = doc.RootElement.EnumerateArray().ToList();

        Assert.That(array.Count, Is.GreaterThan(0), "No endpoint found in Java controller!");
        var roles = array[0].GetProperty("roles").GetString();
        Assert.That(roles, Does.Contain("ADMIN"));

        // Query anonymous/permit-all endpoint in Java
        var anonQuery = "MATCH (e:Endpoint {is_anonymous: true}) WHERE e.name CONTAINS 'health' OR e.route_template CONTAINS 'health' RETURN e.name AS name";
        var anonRes = await _client.ExecuteQueryAsync(anonQuery);
        using var anonDoc = JsonDocument.Parse(anonRes);
        Assert.That(anonDoc.RootElement.GetArrayLength(), Is.GreaterThan(0), "Java PermitAll endpoint not flagged as is_anonymous: true!");
    }

    [Test]
    public async Task Test_TypeScript_SecurityBoundaryExtraction()
    {
        // Query role-guarded endpoint in NestJS
        var query = "MATCH (e:Endpoint) WHERE e.required_roles CONTAINS 'finance-admin' RETURN e.name AS name, e.required_roles AS roles";
        var res = await _client.ExecuteQueryAsync(query);
        using var doc = JsonDocument.Parse(res);
        var array = doc.RootElement.EnumerateArray().ToList();

        Assert.That(array.Count, Is.GreaterThan(0), "No endpoint with required_roles found in NestJS controller!");

        // Query public endpoint in NestJS
        var anonQuery = "MATCH (e:Endpoint {is_anonymous: true}) WHERE e.name CONTAINS 'plans' RETURN e.name AS name";
        var anonRes = await _client.ExecuteQueryAsync(anonQuery);
        using var anonDoc = JsonDocument.Parse(anonRes);
        Assert.That(anonDoc.RootElement.GetArrayLength(), Is.GreaterThan(0), "NestJS @Public() endpoint not flagged as is_anonymous: true!");
    }

    [Test]
    public async Task Test_SecurityAuditing_CypherQuery()
    {
        // Architect audit query: Find all endpoints guarded by roles or unauthenticated
        var query = "MATCH (e:Endpoint) RETURN e.name AS name, coalesce(e.protocol, 'REST') AS protocol, e.is_anonymous AS isAnonymous, e.required_roles AS roles";
        var res = await _client.ExecuteQueryAsync(query);
        using var doc = JsonDocument.Parse(res);
        var array = doc.RootElement.EnumerateArray().ToList();

        Assert.That(array.Count, Is.GreaterThanOrEqualTo(3), "Expected at least 3 endpoints from C#, Java, and NestJS");
        var hasAnonymous = array.Any(x => x.TryGetProperty("isAnonymous", out var prop) && (prop.ValueKind == JsonValueKind.True || (prop.ValueKind == JsonValueKind.Number && prop.GetInt32() != 0)));
        var hasRoles = array.Any(x => x.TryGetProperty("roles", out var prop) && prop.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(prop.GetString()));

        Assert.That(hasAnonymous, Is.True, "Security audit query should identify anonymous endpoints");
        Assert.That(hasRoles, Is.True, "Security audit query should identify role-guarded endpoints");
    }
}
