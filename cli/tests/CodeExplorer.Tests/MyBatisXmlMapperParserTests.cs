using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser;
using CodeExplorer.Parser.Java;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class MyBatisXmlMapperParserTests
{
    [Test]
    public void Test_CanParseFile_IdentifiesMyBatisMapperFiles()
    {
        var parser = new MyBatisXmlMapperParser();

        parser.CanParseFile("PortalOrderDao.xml").Should().BeTrue();
        parser.CanParseFile("PmsProductMapper.xml").Should().BeTrue();
        parser.CanParseFile("C:/work/repo/src/main/resources/dao/OrderDao.xml").Should().BeTrue();
        parser.CanParseFile("C:/work/repo/src/main/resources/com/example/mapper/ItemMapper.xml").Should().BeTrue();
        parser.CanParseFile("C:/work/repo/src/main/resources/mapper/custom.xml").Should().BeTrue();

        // Non-MyBatis files must NOT match
        parser.CanParseFile("pom.xml").Should().BeFalse();
        parser.CanParseFile("AndroidManifest.xml").Should().BeFalse();
        parser.CanParseFile("logback-spring.xml").Should().BeFalse();
        parser.CanParseFile("generatorConfig.xml").Should().BeFalse();
        parser.CanParseFile("User.java").Should().BeFalse();
    }

    [Test]
    public async Task Test_ParseAsync_ExtractsQueriesTablesAndMapperReferences()
    {
        var parser = new MyBatisXmlMapperParser();

        var xmlContent = """
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE mapper PUBLIC "-//mybatis.org//DTD Mapper 3.0//EN" "http://mybatis.org/dtd/mybatis-3-mapper.dtd">
            <mapper namespace="com.macro.mall.portal.dao.PortalOrderDao">
                <sql id="orderColumns">
                    o.id, o.order_sn, o.member_id
                </sql>
                <select id="getDetail" resultType="com.macro.mall.portal.domain.OmsOrderDetail">
                    SELECT
                        <include refid="orderColumns" />,
                        ot.product_name
                    FROM
                        oms_order o
                        LEFT JOIN oms_order_item ot ON o.id = ot.order_id
                    WHERE
                        o.id = #{orderId}
                </select>
                <update id="updateOrderStatus">
                    UPDATE oms_order
                    SET status = #{status}
                    WHERE id = #{orderId}
                </update>
                <delete id="deleteOrder">
                    DELETE FROM oms_order
                    WHERE id = #{orderId}
                </delete>
            </mapper>
            """;

        var tempDir = Path.Combine(Path.GetTempPath(), "mybatis_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var xmlPath = Path.Combine(tempDir, "PortalOrderDao.xml");
        try
        {
            await File.WriteAllTextAsync(xmlPath, xmlContent);

            var syntaxTree = await parser.ParseAsync(xmlPath, "root", "workspace", tempDir);

            syntaxTree.Should().NotBeNull();
            syntaxTree.FileNode.Should().NotBeNull();

            var queries = syntaxTree.FileNode.Children.OfType<QueryNode>().ToList();
            queries.Count.Should().Be(3);

            var selectQuery = queries.FirstOrDefault(q => q.Name.Contains("getDetail"));
            selectQuery.Should().NotBeNull();
            selectQuery!.Verb.Should().Be("SELECT");
            selectQuery.QueryText.Should().Contain("oms_order");

            var tables = new List<TableNode>();
            CollectTables(selectQuery, tables);
            var tableNames = tables.Select(t => t.Name.ToLowerInvariant()).ToList();
            tableNames.Should().Contain("oms_order");

            // Verify references to Java mapper method and interface
            var refs = syntaxTree.FileNode.References;
            refs.Should().Contain(r => r.TargetName == "com.macro.mall.portal.dao.PortalOrderDao.getDetail" && r.Kind == "ACCESSED_BY");
            refs.Should().Contain(r => r.TargetName == "com.macro.mall.portal.dao.PortalOrderDao.getDetail" && r.Kind == "IMPLEMENTED_BY");
            refs.Should().Contain(r => r.TargetName == "com.macro.mall.portal.dao.PortalOrderDao" && r.Kind == "ACCESSED_BY");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    private static void CollectTables(IOntologyNode node, List<TableNode> tables)
    {
        if (node is TableNode tbl)
        {
            tables.Add(tbl);
        }
        foreach (var child in node.Children)
        {
            CollectTables(child, tables);
        }
    }
}
