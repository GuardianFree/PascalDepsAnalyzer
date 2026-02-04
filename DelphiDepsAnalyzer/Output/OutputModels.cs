using System.Text.Json.Serialization;
using DelphiDepsAnalyzer.Core;

namespace DelphiDepsAnalyzer.Output;

/// <summary>
/// JSON serialization context для Native AOT
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(AnalysisOutput))]
[JsonSerializable(typeof(ProjectInfo))]
[JsonSerializable(typeof(UnitInfo))]
[JsonSerializable(typeof(DependenciesInfo))]
[JsonSerializable(typeof(GraphInfo))]
[JsonSerializable(typeof(NodeInfo))]
[JsonSerializable(typeof(EdgeInfo))]
[JsonSerializable(typeof(StatisticsInfo))]
[JsonSerializable(typeof(Dictionary<string, CachedUnit>))]
[JsonSerializable(typeof(CachedUnit))]
public partial class AnalysisOutputContext : JsonSerializerContext
{
}

/// <summary>
/// Корневая модель для JSON вывода
/// </summary>
public class AnalysisOutput
{
    public ProjectInfo Project { get; set; } = new();
    public List<UnitInfo> Units { get; set; } = new();
    public List<string> Includes { get; set; } = new();
    public GraphInfo Graph { get; set; } = new();
    public StatisticsInfo Statistics { get; set; } = new();
}

public class ProjectInfo
{
    public string Path { get; set; } = string.Empty;
    public string MainSource { get; set; } = string.Empty;
    public List<string> SearchPaths { get; set; } = new();
}

public class UnitInfo
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public DependenciesInfo Dependencies { get; set; } = new();
    public List<string> Includes { get; set; } = new();
}

public class DependenciesInfo
{
    [JsonPropertyName("interface")]
    public List<string> Interface { get; set; } = new();
    public List<string> Implementation { get; set; } = new();
}

public class GraphInfo
{
    public List<NodeInfo> Nodes { get; set; } = new();
    public List<EdgeInfo> Edges { get; set; } = new();
}

public class NodeInfo
{
    public string Unit { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
}

public class EdgeInfo
{
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
}

public class StatisticsInfo
{
    public int TotalUnits { get; set; }
    public int TotalIncludes { get; set; }
    public int TotalDependencies { get; set; }
    public DateTime AnalyzedAt { get; set; }
}
