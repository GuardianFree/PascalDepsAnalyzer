using System.Text.Json;
using System.Text.Json.Serialization;
using DelphiDepsAnalyzer.Models;

namespace DelphiDepsAnalyzer.Output;

/// <summary>
/// Форматирует результаты анализа в JSON
/// </summary>
public class JsonOutputFormatter
{
    /// <summary>
    /// Сохраняет результаты анализа в JSON файл
    /// </summary>
    public void SaveToFile(DelphiProject project, string outputPath)
    {
        var output = CreateOutputModel(project);
        var json = JsonSerializer.Serialize(output, AnalysisOutputContext.Default.AnalysisOutput);

        File.WriteAllText(outputPath, json);
        Console.WriteLine($"\nРезультаты сохранены в: {outputPath}");
    }

    /// <summary>
    /// Выводит краткую сводку в консоль
    /// </summary>
    public void PrintSummary(DelphiProject project)
    {
        Console.WriteLine("\n" + new string('=', 60));
        Console.WriteLine("СВОДКА АНАЛИЗА ЗАВИСИМОСТЕЙ");
        Console.WriteLine(new string('=', 60));
        Console.WriteLine($"Проект:           {Path.GetFileName(project.ProjectFilePath)}");
        Console.WriteLine($"Главный файл:     {Path.GetFileName(project.MainSourcePath)}");
        Console.WriteLine($"Всего юнитов:     {project.Units.Count}");
        Console.WriteLine($"Include файлов:   {project.IncludeFiles.Count}");
        Console.WriteLine($"Зависимостей:     {project.DependencyGraph.Edges.Count}");
        Console.WriteLine(new string('=', 60));

        Console.WriteLine("\nЮНИТЫ:");
        foreach (var unit in project.Units.OrderBy(u => u.UnitName))
        {
            var depCount = unit.AllDependencies.Count();
            Console.WriteLine($"  {unit.UnitName,-30} ({depCount} зависимостей)");
        }

        if (project.IncludeFiles.Any())
        {
            Console.WriteLine("\nINCLUDE ФАЙЛЫ:");
            foreach (var include in project.IncludeFiles)
            {
                Console.WriteLine($"  {Path.GetFileName(include)}");
            }
        }
    }

    /// <summary>
    /// Создаёт модель для JSON сериализации
    /// </summary>
    private AnalysisOutput CreateOutputModel(DelphiProject project)
    {
        return new AnalysisOutput
        {
            Project = new ProjectInfo
            {
                Path = project.ProjectFilePath,
                MainSource = project.MainSourcePath,
                SearchPaths = project.SearchPaths
            },
            Units = project.Units.Select(u => new UnitInfo
            {
                Name = u.UnitName,
                Path = u.FilePath,
                Dependencies = new DependenciesInfo
                {
                    Interface = u.InterfaceUses,
                    Implementation = u.ImplementationUses
                },
                Includes = u.IncludeFiles
            }).ToList(),
            Includes = project.IncludeFiles.ToList(),
            Graph = new GraphInfo
            {
                Nodes = project.DependencyGraph.Nodes.Select(n => new NodeInfo
                {
                    Unit = n.UnitName,
                    Path = n.FilePath
                }).ToList(),
                Edges = project.DependencyGraph.Edges.Select(e => new EdgeInfo
                {
                    From = e.From,
                    To = e.To
                }).ToList()
            },
            Statistics = new StatisticsInfo
            {
                TotalUnits = project.Units.Count,
                TotalIncludes = project.IncludeFiles.Count,
                TotalDependencies = project.DependencyGraph.Edges.Count,
                AnalyzedAt = DateTime.UtcNow
            }
        };
    }
}
