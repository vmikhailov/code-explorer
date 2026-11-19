using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Mcp;
using CodeExplorer.Core.Parser;
using CodeExplorer.Cypher.Common;
using CodeExplorer.Parsers.Perl;
using CodeExplorer.Parsers.Php;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class PhpAndPerlParserTests
{
    [Test]
    public async Task Test_PhpFileParser_SymfonyDoctrineAndMreg_ExtractsEndpointsTablesAndUpgradeRisks()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "php_symfony_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            const string entityCode = """
                <?php
                namespace AppBundle\Entity;

                use Doctrine\ORM\Mapping as ORM;

                /**
                 * @ORM\Entity
                 * @ORM\Table(name="customer_invoices")
                 */
                class CustomerInvoice
                {
                    private $id;
                    public function getId() { return $this->id; }
                }
                """;

            const string controllerCode = """
                <?php
                namespace AppBundle\Controller;

                use Symfony\Bundle\FrameworkBundle\Controller\Controller;
                use Symfony\Component\Routing\Annotation\Route;
                use Mreg\Request as MregRequest;

                /**
                 * @Route("/billing")
                 */
                class BillingController extends Controller
                {
                    /**
                     * @Route("/pay", name="billing_pay", methods={"POST"})
                     */
                    public function payAction($invoiceItems, $conn, $smarty)
                    {
                        if (count($invoiceItems) > 0) {
                            while (list($k, $v) = each($invoiceItems)) {
                                $amount = round($v['price'], 2);
                            }
                        }

                        $rows = $conn->fetchAll("SELECT id, total FROM customer_invoices WHERE status = 'OPEN' GROUP BY account_id");
                        $conn->update('customer_payments', array('status' => 'PAID'), array('id' => 1));

                        $mreg = new MregRequest();
                        $mreg->sendCommand('PayCustomerInvoice');

                        return $smarty->display('billing/receipt.tpl');
                    }
                }
                """;

            var entityPath = Path.Combine(tempDir, "CustomerInvoice.php");
            var ctrlPath = Path.Combine(tempDir, "BillingController.php");
            await File.WriteAllTextAsync(entityPath, entityCode);
            await File.WriteAllTextAsync(ctrlPath, controllerCode);

            var parser = new PhpFileParser();
            var entityTree = await parser.ParseAsync(entityPath, "ws", "ws", tempDir);
            var ctrlTree = await parser.ParseAsync(ctrlPath, "ws", "ws", tempDir);

            var entityTables = entityTree.FileNode.Children.OfType<TableNode>().ToList();
            entityTables.Should().Contain(t => t.Name == "customer_invoices");

            var endpoints = ctrlTree.FileNode.Children.OfType<EndpointNode>().ToList();
            endpoints.Should().ContainSingle(e => e.HttpMethod == "POST" && e.RouteTemplate == "/billing/pay");

            var ctrlType = ctrlTree.FileNode.Children.OfType<TypeNode>().First(t => t.Name == "BillingController");
            var payFn = ctrlType.Children.OfType<FunctionNode>().First(f => f.Name == "payAction");
            int.Parse(payFn.CustomProperties!["complexity"]).Should().BeGreaterThanOrEqualTo(3);
            payFn.CustomProperties["upgrade_risks"].Should().Contain("count");
            payFn.CustomProperties["upgrade_risks"].Should().Contain("each");
            payFn.CustomProperties["upgrade_risks"].Should().Contain("round");

            var queries = ctrlTree.FileNode.Children.OfType<QueryNode>().ToList();
            queries.Should().Contain(q => q.Operation == "SELECT" && q.CustomProperties != null && q.CustomProperties.ContainsKey("has_group_by_without_order_by"));
            queries.Should().Contain(q => q.Operation == "UPDATE");

            var extServices = ctrlTree.FileNode.Children.OfType<ExternalServiceNode>().ToList();
            extServices.Should().Contain(s => s.Protocol == "MREG" && s.Url == "PayCustomerInvoice");
            ctrlTree.RawImports.Should().Contain(i => i.Path == "billing/receipt.tpl");
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
            catch (IOException)
            {
                // Ignore transient Windows SQLite lock cleanup race in temp folder
            }
        }
    }

    [Test]
    public async Task Test_PhpFileParser_ZendFramework1AndNetteDatabase_ExtractsPsr0AndLineage()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "php_zf1_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            const string zfCode = """
                <?php
                require_once 'Library/Mreg/Request.php';

                class Billsys_Model_DbTable_Domains extends Zend_Db_Table_Abstract
                {
                    protected $_name = 'domains';
                }

                class Billsys_DomainController extends Zend_Controller_Action
                {
                    public function renewAction()
                    {
                        $this->db->table('domain_renewals')->insert(array('domain' => 'example.de'));
                        mysql_query("DELETE FROM expired_locks WHERE domain_id = 42");
                    }
                }
                """;

            var zfPath = Path.Combine(tempDir, "DomainController.php");
            await File.WriteAllTextAsync(zfPath, zfCode);

            var parser = new PhpFileParser();
            var tree = await parser.ParseAsync(zfPath, "ws", "ws", tempDir);

            var tables = tree.FileNode.Children.OfType<TableNode>().Select(t => t.Name).ToList();
            tables.Should().Contain("domains");
            tables.Should().Contain("domain_renewals");
            tables.Should().Contain("expired_locks");

            var endpoints = tree.FileNode.Children.OfType<EndpointNode>().ToList();
            endpoints.Should().ContainSingle(e => e.RouteTemplate == "/billsys/domain/renew");
            tree.RawImports.Should().Contain(i => i.Path == "Library/Mreg/Request.php");
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
            catch (IOException)
            {
                // Ignore transient Windows SQLite lock cleanup race in temp folder
            }
        }
    }

    [Test]
    public async Task Test_PerlFileParser_CronScriptAndPackage_ExtractsDbiSwitchAndPtfXClient()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "perl_dd24_test_" + Guid.NewGuid().ToString("N"));
        var cronDir = Path.Combine(tempDir, "cronjobs");
        Directory.CreateDirectory(cronDir);

        try
        {
            const string perlCronCode = """
                #!/usr/bin/perl
                package DD24::Cron::BillingRenew;

                use strict;
                use warnings;
                use DBI;
                use Switch;
                use PTF::XClient;
                use Mail::Sendmail;

                sub process_due_renewals {
                    my ($dbh, $mode) = @_;
                    switch ($mode) {
                        case 'live' {
                            my $rows = $dbh->selectall_arrayref(<<SQL);
                SELECT subscription_id, customer_id
                FROM billing_subscriptions
                WHERE next_renewal_date <= NOW()
                GROUP BY customer_id
                SQL
                            $dbh->do("UPDATE billing_subscriptions SET renewal_state = 'LOCKED' WHERE renewal_state = 'DUE'");
                            my $xclient = PTF::XClient->new();
                            $xclient->send('RenewDomainCommand');
                            sendmail(To => 'billing@example.com');
                        }
                    }
                }

                1;
                """;

            var scriptPath = Path.Combine(cronDir, "billing_renew.pl");
            await File.WriteAllTextAsync(scriptPath, perlCronCode);

            var parser = new PerlFileParser();
            var tree = await parser.ParseAsync(scriptPath, "ws", "ws", tempDir);

            var endpoints = tree.FileNode.Children.OfType<EndpointNode>().ToList();
            endpoints.Should().ContainSingle(e => e.Protocol == "CRON" && e.RouteTemplate == "/cronjobs/billing_renew.pl");

            var modNode = tree.FileNode.Children.OfType<TypeNode>().First(t => t.Name == "BillingRenew");
            modNode.CustomProperties!["upgrade_risks"].Should().Contain("switch_module");

            var subNode = modNode.Children.OfType<FunctionNode>().First(f => f.Name == "process_due_renewals");
            int.Parse(subNode.CustomProperties!["complexity"]).Should().BeGreaterThanOrEqualTo(2);

            var tables = tree.FileNode.Children.OfType<TableNode>().Select(t => t.Name).ToList();
            tables.Should().Contain("billing_subscriptions");

            var queries = tree.FileNode.Children.OfType<QueryNode>().ToList();
            queries.Should().Contain(q => q.Operation == "SELECT" && q.CustomProperties != null && q.CustomProperties.ContainsKey("has_group_by_without_order_by"));
            queries.Should().Contain(q => q.Operation == "UPDATE");

            var extNodes = tree.FileNode.Children.OfType<ExternalServiceNode>().ToList();
            extNodes.Should().Contain(e => e.Protocol == "MREG" && e.Url == "RenewDomainCommand");
            extNodes.Should().Contain(e => e.Protocol == "SMTP");
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
            catch (IOException)
            {
                // Ignore transient Windows SQLite lock cleanup race in temp folder
            }
        }
    }

    [Test]
    public async Task Test_FullWorkspaceScan_And_ContextPack_OnPolyglotPhpAndPerl()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "dd24_polyglot_ctx_" + Guid.NewGuid().ToString("N"));
        var loginDir = Path.Combine(tempDir, "login");
        var cronDir = Path.Combine(tempDir, "cronjobs");
        Directory.CreateDirectory(loginDir);
        Directory.CreateDirectory(cronDir);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(loginDir, "composer.json"), """
                {
                  "name": "rtldev/dd24-login",
                  "require": {
                    "symfony/symfony": "3.4.49",
                    "doctrine/orm": "2.7.5"
                  }
                }
                """);

            await File.WriteAllTextAsync(Path.Combine(loginDir, "PaymentService.php"), """
                <?php
                namespace AppBundle\Service;

                class PaymentService
                {
                    public function chargeInvoice($db, $invoiceId)
                    {
                        $db->update('customer_invoices', array('status' => 'CHARGED'), array('id' => $invoiceId));
                    }
                }
                """);

            await File.WriteAllTextAsync(Path.Combine(loginDir, "PaymentController.php"), """
                <?php
                namespace AppBundle\Controller;

                use AppBundle\Service\PaymentService;
                use Symfony\Component\Routing\Annotation\Route;

                class PaymentController
                {
                    /**
                     * @Route("/payment/checkout", methods={"POST"})
                     */
                    public function checkoutAction($db, $invoiceId)
                    {
                        $svc = new PaymentService();
                        $svc->chargeInvoice($db, $invoiceId);
                    }
                }
                """);

            await File.WriteAllTextAsync(Path.Combine(cronDir, "settle_invoices.pl"), """
                #!/usr/bin/perl
                package DD24::Cron::SettleInvoices;
                use strict;
                use DBI;

                sub run_settlement {
                    my ($dbh) = @_;
                    my $rows = $dbh->selectall_arrayref("SELECT id FROM customer_invoices WHERE status = 'CHARGED'");
                }
                """);

            WorkspaceLocator.Initialize(tempDir, "dd24-demo");
            var dbPath = Path.Combine(tempDir, ".codeexplorer", "graph.db");
            await using var client = new SqliteGraphClient(dbPath);
            WorkspaceIndexer.AutoRegisterParsers();
            var indexer = new WorkspaceIndexer(client);
            await indexer.IndexAsync(tempDir, tempDir, clear: true);

            var repository = new CodeExplorerRepository(client, defaultWorkspacePath: tempDir);
            var yaml = await repository.GetTaskContextAsync(
                "checkoutAction chargeInvoice customer_invoices",
                maxAnchors: 8,
                includeGates: false,
                workspacePath: tempDir);

            TestContext.Out.WriteLine(yaml);
            yaml.Should().Contain("checkoutAction");
            yaml.Should().Contain("chargeInvoice");
            yaml.Should().Contain("customer_invoices");
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
            catch (IOException)
            {
                // Ignore transient Windows SQLite lock cleanup race in temp folder
            }
        }
    }

    [Test]
    public async Task SampleProjects_PhpAndPerl_FullScan_ExtractsProjectsEndpointsTablesAndMregCalls()
    {
        var currentDir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        string? sampleRoot = null;
        while (currentDir != null)
        {
            var candidate = Path.Combine(currentDir.FullName, "cli", "tests", "SampleProjects");
            if (Directory.Exists(candidate))
            {
                sampleRoot = candidate;
                break;
            }
            var direct = Path.Combine(currentDir.FullName, "SampleProjects");
            if (Directory.Exists(direct))
            {
                sampleRoot = direct;
                break;
            }
            currentDir = currentDir.Parent;
        }

        sampleRoot.Should().NotBeNull();
        WorkspaceIndexer.AutoRegisterParsers();

        await using var phpClient = new InMemoryGraphRepository();
        var phpIndexer = new WorkspaceIndexer(phpClient);
        await phpIndexer.IndexAsync(Path.Combine(sampleRoot!, "php"), clear: false);

        var phpProjects = phpClient.UploadedNodes.Where(n => n.Kind == NodeKind.Project).ToList();
        phpProjects.Should().HaveCount(3);

        var phpEndpoints = phpClient.UploadedNodes.Where(n => n.Kind == NodeKind.Endpoint)
            .Select(e => e.Properties.GetValueOrDefault("name")?.ToString() ?? string.Empty).ToList();
        phpEndpoints.Should().Contain(n => n.Contains("/billing/pay"));
        phpEndpoints.Should().Contain(n => n.Contains("/billsys/domain/renew"));

        var phpTables = phpClient.UploadedNodes.Where(n => n.Kind == NodeKind.Table)
            .Select(t => t.Properties.GetValueOrDefault("name")?.ToString() ?? string.Empty).ToList();
        phpTables.Should().Contain(["customer_invoices", "customer_payments", "domains", "domain_renewals", "expired_locks"]);

        await using var perlClient = new InMemoryGraphRepository();
        var perlIndexer = new WorkspaceIndexer(perlClient);
        await perlIndexer.IndexAsync(Path.Combine(sampleRoot!, "perl"), clear: false);

        var perlProjects = perlClient.UploadedNodes.Where(n => n.Kind == NodeKind.Project).ToList();
        perlProjects.Should().HaveCount(2);

        var perlEndpoints = perlClient.UploadedNodes.Where(n => n.Kind == NodeKind.Endpoint)
            .Select(e => e.Properties.GetValueOrDefault("name")?.ToString() ?? string.Empty).ToList();
        perlEndpoints.Should().Contain(n => n.Contains("billing_renew.pl"));

        var perlTables = perlClient.UploadedNodes.Where(n => n.Kind == NodeKind.Table)
            .Select(t => t.Properties.GetValueOrDefault("name")?.ToString() ?? string.Empty).ToList();
        perlTables.Should().Contain(["billing_subscriptions", "mreg_audit_log"]);
    }

    [Test]
    public async Task DD24MonorepoLayout_CollapsesDeepMvcViewsPerlModulesAndRequirementsSubfolder_AndParsesCrOnlyPython()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ce_dd24_monorepo_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            // 0. Root monorepo files (MANIFEST.md, .gitignore) - must NOT cause tempRoot itself to become a root PHP/Perl project
            await File.WriteAllTextAsync(Path.Combine(tempRoot, "MANIFEST.md"), "# DD24 Repositories\n");
            await File.WriteAllTextAsync(Path.Combine(tempRoot, ".gitignore"), ".DS_Store\n");

            // 1. Zend Framework 1 / MVC service with public/index.php, Oauth/, and deep application/modules/v1/views/scripts/<ctrl>/*.phtml
            var loginRoot = Path.Combine(tempRoot, "rtldev-dd24-login");
            var loginPublic = Path.Combine(loginRoot, "public");
            var loginOauth = Path.Combine(loginRoot, "Oauth");
            var loginViews = Path.Combine(loginRoot, "application", "modules", "v1", "views", "scripts", "dnsrecords");
            Directory.CreateDirectory(loginPublic);
            Directory.CreateDirectory(loginOauth);
            Directory.CreateDirectory(loginViews);
            await File.WriteAllTextAsync(Path.Combine(loginPublic, "index.php"), "<?php echo 'login';");
            await File.WriteAllTextAsync(Path.Combine(loginOauth, "Oauth.php"), "<?php class OauthClient {}");
            await File.WriteAllTextAsync(Path.Combine(loginViews, "index.phtml"), "<?php echo $this->domain;");

            // 2. Manifestless PHP libraries with direct .php files, nested 3rdParty/Retail/composer.json, and jquery-ui-1.12.1.custom/package.json
            var libWlRoot = Path.Combine(tempRoot, "rtldev-dd24-library-wl");
            var retailNested = Path.Combine(libWlRoot, "3rdParty", "Retail");
            Directory.CreateDirectory(retailNested);
            await File.WriteAllTextAsync(Path.Combine(libWlRoot, "WlHelper.php"), "<?php class WlHelper {}");
            await File.WriteAllTextAsync(Path.Combine(retailNested, "composer.json"), """{"name": "dd24/retail-sdk"}""");
            await File.WriteAllTextAsync(Path.Combine(retailNested, "RetailClient.php"), "<?php class RetailClient {}");

            var libWwwRoot = Path.Combine(tempRoot, "rtldev-dd24-library-www");
            var jqueryUiDir = Path.Combine(libWwwRoot, "js", "jquery-ui-1.12.1.custom");
            Directory.CreateDirectory(jqueryUiDir);
            await File.WriteAllTextAsync(Path.Combine(libWwwRoot, "WwwRequest.php"), "<?php class WwwRequest {}");
            await File.WriteAllTextAsync(Path.Combine(jqueryUiDir, "package.json"), """{"name": "jquery-ui", "version": "1.12.1"}""");
            await File.WriteAllTextAsync(Path.Combine(jqueryUiDir, "jquery-ui.js"), "function initUi() {}");

            // 3. Kirby CMS service with public/index.php, site/controllers, and bundled kirby-3.7.1/composer.json
            var kirbyServiceRoot = Path.Combine(tempRoot, "rtldev-dd24-kirby-docker");
            var kirbyPublic = Path.Combine(kirbyServiceRoot, "public");
            var kirbySite = Path.Combine(kirbyServiceRoot, "site", "controllers");
            var kirbyEngine = Path.Combine(kirbyServiceRoot, "kirby-3.7.1");
            Directory.CreateDirectory(kirbyPublic);
            Directory.CreateDirectory(kirbySite);
            Directory.CreateDirectory(kirbyEngine);
            await File.WriteAllTextAsync(Path.Combine(kirbyPublic, "index.php"), "<?php echo 'kirby';");
            await File.WriteAllTextAsync(Path.Combine(kirbySite, "home.php"), "<?php return function () {};");
            await File.WriteAllTextAsync(Path.Combine(kirbyEngine, "composer.json"), """{"name": "getkirby/cms"}""");
            await File.WriteAllTextAsync(Path.Combine(kirbyEngine, "bootstrap.php"), "<?php class KirbyApp {}");

            // 4. Cron service with requirements/requirements.txt (0 .py files inside requirements/),
            //    CR-only (\r) legacy Python file, and Perl module directories db/, billing/
            var cronRoot = Path.Combine(tempRoot, "rtldev-dd24-cron-docker");
            var cronReq = Path.Combine(cronRoot, "requirements");
            var cronOldBilling = Path.Combine(cronRoot, "cronjobs_old", "billing");
            var cronPerlDb = Path.Combine(cronRoot, "db");
            var cronPerlBilling = Path.Combine(cronRoot, "billing");
            Directory.CreateDirectory(cronReq);
            Directory.CreateDirectory(cronOldBilling);
            Directory.CreateDirectory(cronPerlDb);
            Directory.CreateDirectory(cronPerlBilling);
            await File.WriteAllTextAsync(Path.Combine(cronReq, "requirements.txt"), "requests==2.31.0\n");
            await File.WriteAllTextAsync(
                Path.Combine(cronOldBilling, "maintain_pos_ek.py"),
                "# -*- coding: ISO-8859-1 -*-\rfrom common.CMailHandler import CMailHandler\rfrom db.CDBHandle import CDBHandle\rdef run_pos_ek():\r    return 1\r");
            await File.WriteAllTextAsync(Path.Combine(cronPerlDb, "CDBHandle.pm"), "package db::CDBHandle;\n1;\n");
            await File.WriteAllTextAsync(Path.Combine(cronPerlBilling, "CBilling.pm"), "package billing::CBilling;\n1;\n");

            // 5. Multi-folder Python backend without root manifest (only Dockerfile + Python package & Perl libs subdirectories)
            var backendRoot = Path.Combine(tempRoot, "rtldev-dd24-backend");
            var backendApi = Path.Combine(backendRoot, "api");
            var backendWsb = Path.Combine(backendRoot, "wsb");
            var backendOrder = Path.Combine(backendRoot, "order");
            var backendPerlDb = Path.Combine(backendRoot, "libs", "db");
            Directory.CreateDirectory(backendApi);
            Directory.CreateDirectory(backendWsb);
            Directory.CreateDirectory(backendOrder);
            Directory.CreateDirectory(backendPerlDb);
            await File.WriteAllTextAsync(Path.Combine(backendRoot, "Dockerfile"), "FROM python:3.11\n");
            await File.WriteAllTextAsync(Path.Combine(backendApi, "handler.py"), "def handle_backend_api():\n    return True\n");
            await File.WriteAllTextAsync(Path.Combine(backendWsb, "worker.py"), "def work():\n    return True\n");
            await File.WriteAllTextAsync(Path.Combine(backendOrder, "process.py"), "def process():\n    return True\n");
            await File.WriteAllTextAsync(Path.Combine(backendPerlDb, "BackendDb.pm"), "package db::BackendDb;\n1;\n");

            WorkspaceIndexer.AutoRegisterParsers();
            await using var client = new InMemoryGraphRepository();
            var indexer = new WorkspaceIndexer(client);
            await indexer.IndexAsync(tempRoot, clear: false);

            var projectNames = client.UploadedNodes
                .Where(n => n.Kind == NodeKind.Project)
                .Select(n => n.Properties.GetValueOrDefault("name")?.ToString() ?? string.Empty)
                .ToList();

            var rootFolderName = Path.GetFileName(tempRoot);
            projectNames.Should().NotContain(rootFolderName);
            projectNames.Should().Contain([
                "rtldev-dd24-login",
                "rtldev-dd24-library-wl",
                "rtldev-dd24-library-www",
                "rtldev-dd24-kirby-docker",
                "rtldev-dd24-cron-docker",
                "rtldev-dd24-backend"
            ]);

            projectNames.Should().NotContain([
                "Oauth", "public", "dnsrecords", "requirements", "db", "billing",
                "Retail", "dd24/retail-sdk", "kirby-3.7.1", "getkirby/cms", "site",
                "jquery-ui-1.12.1.custom", "jquery-ui",
                "api.Scripts", "wsb.Scripts", "order.Scripts"
            ]);

            var functionNames = client.UploadedNodes
                .Where(n => n.Kind == NodeKind.Function)
                .Select(n => n.Properties.GetValueOrDefault("name")?.ToString() ?? string.Empty)
                .ToList();
            functionNames.Should().Contain(["run_pos_ek", "handle_backend_api"]);

            var syntaxErrors = client.UploadedNodes
                .Where(n => n.Kind == NodeKind.ScanIssue &&
                            string.Equals(n.Properties.GetValueOrDefault("category")?.ToString(), "syntax_error", StringComparison.Ordinal))
                .ToList();
            syntaxErrors.Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }
}
