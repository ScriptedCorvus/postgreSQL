using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DatabaseClient.Core.Models;

namespace DatabaseClient.Core.Services;

/// <summary>
/// Generates EXPLAIN SQL and parses query plan output for each database provider.
/// </summary>
public static partial class QueryPlanParser
{
    /// <summary>
    /// Generates the EXPLAIN SQL command for the given query and database type.
    /// </summary>
    public static string GenerateExplainSql(string sql, DatabaseType databaseType, bool analyze = false)
    {
        // Remove trailing semicolons
        sql = sql.TrimEnd().TrimEnd(';');

        return databaseType switch
        {
            DatabaseType.PostgreSQL => analyze
                ? $"EXPLAIN (ANALYZE, BUFFERS, FORMAT TEXT) {sql}"
                : $"EXPLAIN (FORMAT TEXT) {sql}",
            DatabaseType.MySQL or DatabaseType.MariaDB => analyze
                ? $"EXPLAIN ANALYZE {sql}"
                : $"EXPLAIN {sql}",
            DatabaseType.SQLite => $"EXPLAIN QUERY PLAN {sql}",
            _ => $"EXPLAIN {sql}"
        };
    }

    /// <summary>
    /// Parses the text-based EXPLAIN output into a QueryPlan model.
    /// </summary>
    public static QueryPlan ParseTextPlan(string explainOutput, DatabaseType databaseType)
    {
        return databaseType switch
        {
            DatabaseType.PostgreSQL => ParsePostgreSqlTextPlan(explainOutput),
            DatabaseType.MySQL or DatabaseType.MariaDB => ParseMySqlPlan(explainOutput),
            DatabaseType.SQLite => ParseSqlitePlan(explainOutput),
            _ => new QueryPlan { RawText = explainOutput }
        };
    }

    /// <summary>
    /// Parses PostgreSQL text-format EXPLAIN output.
    /// Example: "Seq Scan on users  (cost=0.00..35.50 rows=2550 width=36)"
    /// </summary>
    private static QueryPlan ParsePostgreSqlTextPlan(string output)
    {
        var plan = new QueryPlan { RawText = output };
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var nodeStack = new Stack<(QueryPlanNode Node, int Indent)>();

        foreach (var line in lines)
        {
            // Check for timing lines
            if (line.TrimStart().StartsWith("Planning Time:", StringComparison.OrdinalIgnoreCase))
            {
                var match = TimingRegex().Match(line);
                if (match.Success && double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var time))
                    plan.PlanningTime = time;
                continue;
            }
            if (line.TrimStart().StartsWith("Execution Time:", StringComparison.OrdinalIgnoreCase))
            {
                var match = TimingRegex().Match(line);
                if (match.Success && double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var time))
                    plan.ExecutionTime = time;
                continue;
            }

            // Skip lines that are just info (Filter, Index Cond, etc.)
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("Filter:") || trimmed.StartsWith("Index Cond:") ||
                trimmed.StartsWith("Sort Key:") || trimmed.StartsWith("Join Filter:") ||
                trimmed.StartsWith("Rows Removed") || trimmed.StartsWith("Buffers:") ||
                trimmed.StartsWith("->") == false && !PgNodeRegex().IsMatch(trimmed) && nodeStack.Count > 0)
            {
                // Annotate the last node with extra info
                if (nodeStack.TryPeek(out var last))
                {
                    if (trimmed.StartsWith("Filter:"))
                        last.Node.Filter = trimmed["Filter:".Length..].Trim();
                    else if (trimmed.StartsWith("Index Cond:"))
                        last.Node.IndexCondition = trimmed["Index Cond:".Length..].Trim();
                    else if (trimmed.StartsWith("Sort Key:"))
                        last.Node.SortKey = trimmed["Sort Key:".Length..].Trim();
                    else if (trimmed.StartsWith("Join Filter:"))
                        last.Node.JoinFilter = trimmed["Join Filter:".Length..].Trim();
                    else
                        last.Node.Extra = (last.Node.Extra ?? "") + trimmed + "\n";
                }
                continue;
            }

            // Parse a plan node line
            var indent = line.Length - line.TrimStart(' ').Length;
            // Remove arrow prefix
            var nodeText = trimmed.StartsWith("->") ? trimmed[2..].TrimStart() : trimmed;

            var node = ParsePostgreSqlNode(nodeText);
            if (node is null) continue;

            // Pop from stack any nodes with same or higher indent
            while (nodeStack.Count > 0 && nodeStack.Peek().Indent >= indent)
                nodeStack.Pop();

            if (nodeStack.Count > 0)
                nodeStack.Peek().Node.Children.Add(node);
            else
                plan.Nodes.Add(node);

            nodeStack.Push((node, indent));
        }

        return plan;
    }

    private static QueryPlanNode? ParsePostgreSqlNode(string text)
    {
        var match = PgNodeRegex().Match(text);
        if (!match.Success) return null;

        var node = new QueryPlanNode
        {
            NodeType = match.Groups[1].Value.Trim()
        };

        // Parse "on tablename" if present
        var onMatch = PgOnRegex().Match(text);
        if (onMatch.Success)
            node.RelationName = onMatch.Groups[1].Value;

        // Parse "using indexname"
        var usingMatch = PgUsingRegex().Match(text);
        if (usingMatch.Success)
            node.IndexName = usingMatch.Groups[1].Value;

        // Parse cost
        var costMatch = PgCostRegex().Match(text);
        if (costMatch.Success)
        {
            double.TryParse(costMatch.Groups[1].Value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var startup);
            double.TryParse(costMatch.Groups[2].Value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var total);
            double.TryParse(costMatch.Groups[3].Value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var rows);
            node.StartupCost = startup;
            node.TotalCost = total;
            node.PlanRows = rows;
        }

        // Parse actual time (from EXPLAIN ANALYZE)
        var actualMatch = PgActualRegex().Match(text);
        if (actualMatch.Success)
        {
            double.TryParse(actualMatch.Groups[2].Value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var actualTime);
            long.TryParse(actualMatch.Groups[3].Value, out var actualRows);
            long.TryParse(actualMatch.Groups[4].Value, out var loops);
            node.ActualTime = actualTime;
            node.ActualRows = actualRows;
            node.ActualLoops = loops;
        }

        return node;
    }

    /// <summary>
    /// Parses MySQL/MariaDB EXPLAIN tabular output.
    /// </summary>
    private static QueryPlan ParseMySqlPlan(string output)
    {
        var plan = new QueryPlan { RawText = output };

        // MySQL EXPLAIN returns a tabular result. Parse each line
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('+') || line.StartsWith('|') == false)
                continue;

            // Parse pipe-delimited columns: | id | select_type | table | type | possible_keys | key | key_len | ref | rows | Extra |
            var parts = line.Split('|', StringSplitOptions.TrimEntries)
                .Where(p => !string.IsNullOrEmpty(p)).ToArray();

            if (parts.Length < 2 || parts[0] == "id") continue; // Skip header

            var node = new QueryPlanNode
            {
                NodeType = parts.Length > 1 ? parts[1] : "SELECT",
                RelationName = parts.Length > 2 ? parts[2] : null,
                IndexName = parts.Length > 5 ? (parts[5] == "NULL" ? null : parts[5]) : null,
                Extra = parts.Length > 9 ? parts[^1] : null
            };

            if (parts.Length > 8 && double.TryParse(parts[8], out var rows))
                node.PlanRows = rows;

            // Determine cost-like info from access type
            if (parts.Length > 3)
            {
                node.NodeType = parts[3] switch
                {
                    "ALL" => "Full Table Scan",
                    "index" => "Full Index Scan",
                    "range" => "Index Range Scan",
                    "ref" => "Index Lookup",
                    "eq_ref" => "Unique Index Lookup",
                    "const" => "Constant Lookup",
                    "system" => "System Table",
                    _ => parts[3]
                } + (node.RelationName is not null ? $" on {node.RelationName}" : "");
            }

            plan.Nodes.Add(node);
        }

        // If no tabular format detected, just create a single text node
        if (plan.Nodes.Count == 0)
        {
            plan.Nodes.Add(new QueryPlanNode
            {
                NodeType = "Query Plan",
                Extra = output
            });
        }

        return plan;
    }

    /// <summary>
    /// Parses SQLite EXPLAIN QUERY PLAN output.
    /// Format: |--SCAN table_name | |--SEARCH table_name USING INDEX...
    /// </summary>
    private static QueryPlan ParseSqlitePlan(string output)
    {
        var plan = new QueryPlan { RawText = output };
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            // SQLite EXPLAIN QUERY PLAN: "id|parent|notused|detail"
            // Or: "|--SCAN table", "|--SEARCH table USING INDEX idx"
            var detailMatch = SqlitePlanRegex().Match(trimmed);
            var detail = detailMatch.Success ? detailMatch.Groups[1].Value.Trim() : trimmed;

            var node = new QueryPlanNode();

            if (detail.StartsWith("SCAN", StringComparison.OrdinalIgnoreCase))
            {
                node.NodeType = "Full Table Scan";
                var tableName = detail[4..].Trim().Split(' ')[0];
                node.RelationName = tableName;
            }
            else if (detail.StartsWith("SEARCH", StringComparison.OrdinalIgnoreCase))
            {
                node.NodeType = "Index Search";
                var rest = detail[6..].Trim();
                var parts = rest.Split(new[] { " USING " }, StringSplitOptions.None);
                node.RelationName = parts[0].Trim().Split(' ')[0];
                if (parts.Length > 1)
                {
                    var idxPart = parts[1].Trim();
                    if (idxPart.StartsWith("INDEX", StringComparison.OrdinalIgnoreCase))
                        node.IndexName = idxPart[5..].Trim().Split(' ')[0];
                    else if (idxPart.StartsWith("COVERING INDEX", StringComparison.OrdinalIgnoreCase))
                        node.IndexName = idxPart[14..].Trim().Split(' ')[0];
                }
            }
            else
            {
                node.NodeType = detail;
            }

            plan.Nodes.Add(node);
        }

        return plan;
    }

    // ── Regex patterns ────────────────────────────────────────────────────

    [GeneratedRegex(@"^(\w[\w\s]+?)(?:\s+on\s|\s+using\s|\s*\()", RegexOptions.IgnoreCase)]
    private static partial Regex PgNodeRegex();

    [GeneratedRegex(@"\bon\s+(\w+)", RegexOptions.IgnoreCase)]
    private static partial Regex PgOnRegex();

    [GeneratedRegex(@"\busing\s+(\w+)", RegexOptions.IgnoreCase)]
    private static partial Regex PgUsingRegex();

    [GeneratedRegex(@"\(cost=([\d.]+)\.\.([\d.]+)\s+rows=([\d.]+)", RegexOptions.IgnoreCase)]
    private static partial Regex PgCostRegex();

    [GeneratedRegex(@"\(actual time=([\d.]+)\.\.([\d.]+)\s+rows=(\d+)\s+loops=(\d+)\)", RegexOptions.IgnoreCase)]
    private static partial Regex PgActualRegex();

    [GeneratedRegex(@"([\d.]+)\s*ms")]
    private static partial Regex TimingRegex();

    [GeneratedRegex(@"(?:\|--)?(.+)")]
    private static partial Regex SqlitePlanRegex();
}
