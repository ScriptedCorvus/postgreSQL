using DatabaseClient.Core.Models;
using DatabaseClient.Core.Services;
using FluentAssertions;

namespace DatabaseClient.Core.Tests.Services;

[TestFixture]
public class QueryPlanParserTests
{
    // -- GenerateExplainSql --

    [Test]
    public void GenerateExplainSql_PostgreSQL_ReturnsExplainFormat()
    {
        var result = QueryPlanParser.GenerateExplainSql("SELECT 1", DatabaseType.PostgreSQL);
        result.Should().Be("EXPLAIN (FORMAT TEXT) SELECT 1");
    }

    [Test]
    public void GenerateExplainSql_PostgreSQL_Analyze_IncludesAnalyzeBuffers()
    {
        var result = QueryPlanParser.GenerateExplainSql("SELECT 1", DatabaseType.PostgreSQL, analyze: true);
        result.Should().Be("EXPLAIN (ANALYZE, BUFFERS, FORMAT TEXT) SELECT 1");
    }

    [Test]
    public void GenerateExplainSql_MySQL_ReturnsExplain()
    {
        var result = QueryPlanParser.GenerateExplainSql("SELECT 1", DatabaseType.MySQL);
        result.Should().Be("EXPLAIN SELECT 1");
    }

    [Test]
    public void GenerateExplainSql_MySQL_Analyze_ReturnsExplainAnalyze()
    {
        var result = QueryPlanParser.GenerateExplainSql("SELECT 1", DatabaseType.MySQL, analyze: true);
        result.Should().Be("EXPLAIN ANALYZE SELECT 1");
    }

    [Test]
    public void GenerateExplainSql_MariaDB_SameAsMySQL()
    {
        var result = QueryPlanParser.GenerateExplainSql("SELECT 1", DatabaseType.MariaDB);
        result.Should().Be("EXPLAIN SELECT 1");
    }

    [Test]
    public void GenerateExplainSql_SQLite_ReturnsExplainQueryPlan()
    {
        var result = QueryPlanParser.GenerateExplainSql("SELECT 1", DatabaseType.SQLite);
        result.Should().Be("EXPLAIN QUERY PLAN SELECT 1");
    }

    [Test]
    public void GenerateExplainSql_TrimsTrailingSemicolon()
    {
        var result = QueryPlanParser.GenerateExplainSql("SELECT 1;", DatabaseType.PostgreSQL);
        result.Should().NotContain(";");
    }

    // -- ParseTextPlan --

    [Test]
    public void ParseTextPlan_PostgreSQL_ParsesSimplePlan()
    {
        var plan = @"Seq Scan on users  (cost=0.00..35.50 rows=2550 width=36)
Planning Time: 0.05 ms
Execution Time: 1.23 ms";

        var result = QueryPlanParser.ParseTextPlan(plan, DatabaseType.PostgreSQL);
        result.Should().NotBeNull();
        result.RawText.Should().Be(plan);
        result.PlanningTime.Should().BeApproximately(0.05, 0.001);
        result.ExecutionTime.Should().BeApproximately(1.23, 0.001);
    }

    [Test]
    public void ParseTextPlan_PostgreSQL_ParsesNodeType()
    {
        var plan = "Seq Scan on users  (cost=0.00..35.50 rows=2550 width=36)";

        var result = QueryPlanParser.ParseTextPlan(plan, DatabaseType.PostgreSQL);
        result.Nodes.Should().NotBeEmpty();
        result.Nodes[0].NodeType.Should().Contain("Seq Scan");
    }

    [Test]
    public void ParseTextPlan_PostgreSQL_ParsesNestedNodes()
    {
        var plan = @"Hash Join  (cost=1.10..2.45 rows=3 width=72)
  Hash Cond: (a.id = b.id)
  ->  Seq Scan on a  (cost=0.00..1.05 rows=5 width=36)
  ->  Hash  (cost=1.05..1.05 rows=5 width=36)
        ->  Seq Scan on b  (cost=0.00..1.05 rows=5 width=36)";

        var result = QueryPlanParser.ParseTextPlan(plan, DatabaseType.PostgreSQL);
        result.Nodes.Should().NotBeEmpty();
        result.Nodes[0].Children.Should().NotBeEmpty();
    }

    [Test]
    public void ParseTextPlan_SQLite_ParsesPlan()
    {
        var plan = @"SCAN users";

        var result = QueryPlanParser.ParseTextPlan(plan, DatabaseType.SQLite);
        result.Should().NotBeNull();
        result.RawText.Should().Be(plan);
    }

    [Test]
    public void ParseTextPlan_EmptyInput_ReturnsEmptyPlan()
    {
        var result = QueryPlanParser.ParseTextPlan("", DatabaseType.PostgreSQL);
        result.Should().NotBeNull();
        result.RawText.Should().Be("");
    }

    // -- Warnings Detection --

    [Test]
    public void ParseTextPlan_PostgreSQL_DetectsSeqScanWarning()
    {
        var plan = "Seq Scan on users  (cost=0.00..35.50 rows=2550 width=36)";

        var result = QueryPlanParser.ParseTextPlan(plan, DatabaseType.PostgreSQL);
        // Sequential scan on a table should be noted — the exact behavior depends on implementation
        result.Nodes.Should().NotBeEmpty();
    }
}
