namespace DelphiDepsAnalyzer.Models;

/// <summary>
/// Представляет граф зависимостей между юнитами
/// </summary>
public class DependencyGraph
{
    /// <summary>
    /// Узлы графа (юниты)
    /// </summary>
    public List<GraphNode> Nodes { get; set; } = new();

    /// <summary>
    /// Рёбра графа (зависимости)
    /// </summary>
    public List<GraphEdge> Edges { get; set; } = new();

    /// <summary>
    /// Добавить узел в граф
    /// </summary>
    public void AddNode(string unitName, string filePath)
    {
        if (!Nodes.Any(n => n.UnitName == unitName))
        {
            Nodes.Add(new GraphNode
            {
                UnitName = unitName,
                FilePath = filePath
            });
        }
    }

    /// <summary>
    /// Добавить ребро (зависимость)
    /// </summary>
    public void AddEdge(string fromUnit, string toUnit)
    {
        if (!Edges.Any(e => e.From == fromUnit && e.To == toUnit))
        {
            Edges.Add(new GraphEdge
            {
                From = fromUnit,
                To = toUnit
            });
        }
    }
}

/// <summary>
/// Узел графа зависимостей
/// </summary>
public class GraphNode
{
    public string UnitName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
}

/// <summary>
/// Ребро графа зависимостей (from -> to)
/// </summary>
public class GraphEdge
{
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
}
