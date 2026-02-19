namespace DatabaseClient.Core.Models;

/// <summary>
/// Represents a node in a query execution plan tree.
/// </summary>
public class QueryPlanNode
{
    /// <summary>The operation type (e.g., "Seq Scan", "Index Scan", "Hash Join").</summary>
    public string NodeType { get; set; } = string.Empty;

    /// <summary>Relation/table name involved, if any.</summary>
    public string? RelationName { get; set; }

    /// <summary>Alias of the relation, if any.</summary>
    public string? Alias { get; set; }

    /// <summary>Total estimated startup cost.</summary>
    public double StartupCost { get; set; }

    /// <summary>Total estimated total cost.</summary>
    public double TotalCost { get; set; }

    /// <summary>Estimated number of output rows.</summary>
    public double PlanRows { get; set; }

    /// <summary>Actual time in ms (only with ANALYZE).</summary>
    public double? ActualTime { get; set; }

    /// <summary>Actual number of rows (only with ANALYZE).</summary>
    public long? ActualRows { get; set; }

    /// <summary>Number of loops (only with ANALYZE).</summary>
    public long? ActualLoops { get; set; }

    /// <summary>Filter condition applied.</summary>
    public string? Filter { get; set; }

    /// <summary>Join condition.</summary>
    public string? JoinFilter { get; set; }

    /// <summary>Index name used, if any.</summary>
    public string? IndexName { get; set; }

    /// <summary>Index condition.</summary>
    public string? IndexCondition { get; set; }

    /// <summary>Sort keys (ORDER BY).</summary>
    public string? SortKey { get; set; }

    /// <summary>Additional info or raw text for this node.</summary>
    public string? Extra { get; set; }

    /// <summary>Child plan nodes.</summary>
    public List<QueryPlanNode> Children { get; set; } = [];

    /// <summary>Nesting depth for display.</summary>
    public int Depth { get; set; }

    /// <summary>
    /// Returns a display label for this plan node.
    /// </summary>
    public string DisplayLabel
    {
        get
        {
            var label = NodeType;
            if (!string.IsNullOrEmpty(RelationName))
                label += $" on {RelationName}";
            if (!string.IsNullOrEmpty(Alias) && Alias != RelationName)
                label += $" ({Alias})";
            if (!string.IsNullOrEmpty(IndexName))
                label += $" using {IndexName}";
            return label;
        }
    }

    /// <summary>
    /// Returns a cost summary string.
    /// </summary>
    public string CostSummary
    {
        get
        {
            var parts = new List<string>();
            parts.Add($"cost={StartupCost:F2}..{TotalCost:F2}");
            parts.Add($"rows={PlanRows:F0}");
            if (ActualTime.HasValue)
                parts.Add($"actual={ActualTime:F3}ms");
            if (ActualRows.HasValue)
                parts.Add($"actual_rows={ActualRows}");
            return string.Join(", ", parts);
        }
    }

    /// <summary>
    /// Returns a relative cost indicator (0.0 to 1.0) for coloring.
    /// Based on TotalCost relative to a given maxCost.
    /// </summary>
    public double GetRelativeCost(double maxCost)
    {
        if (maxCost <= 0) return 0;
        return Math.Min(1.0, TotalCost / maxCost);
    }
}

/// <summary>
/// Full query execution plan result.
/// </summary>
public class QueryPlan
{
    /// <summary>The root node(s) of the plan.</summary>
    public List<QueryPlanNode> Nodes { get; set; } = [];

    /// <summary>Total planning time (if available, from EXPLAIN ANALYZE).</summary>
    public double? PlanningTime { get; set; }

    /// <summary>Total execution time (if available, from EXPLAIN ANALYZE).</summary>
    public double? ExecutionTime { get; set; }

    /// <summary>Raw EXPLAIN text output.</summary>
    public string RawText { get; set; } = string.Empty;

    /// <summary>
    /// Gets the maximum TotalCost across all nodes (for relative coloring).
    /// </summary>
    public double MaxCost => GetMaxCost(Nodes);

    private static double GetMaxCost(List<QueryPlanNode> nodes)
    {
        double max = 0;
        foreach (var node in nodes)
        {
            max = Math.Max(max, node.TotalCost);
            max = Math.Max(max, GetMaxCost(node.Children));
        }
        return max;
    }

    /// <summary>
    /// Flattens all nodes into a list (for TreeView binding).
    /// </summary>
    public List<QueryPlanNode> FlattenNodes()
    {
        var result = new List<QueryPlanNode>();
        FlattenRecursive(Nodes, result, 0);
        return result;
    }

    private static void FlattenRecursive(List<QueryPlanNode> nodes, List<QueryPlanNode> result, int depth)
    {
        foreach (var node in nodes)
        {
            node.Depth = depth;
            result.Add(node);
            FlattenRecursive(node.Children, result, depth + 1);
        }
    }
}
