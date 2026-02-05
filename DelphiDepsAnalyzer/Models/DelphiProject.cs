using System.Collections.Concurrent;

namespace DelphiDepsAnalyzer.Models;

/// <summary>
/// Представляет Delphi проект (.dproj)
/// </summary>
public class DelphiProject
{
    /// <summary>
    /// Путь к файлу .dproj
    /// </summary>
    public string ProjectFilePath { get; set; } = string.Empty;

    /// <summary>
    /// Путь к главному .dpr файлу
    /// </summary>
    public string MainSourcePath { get; set; } = string.Empty;

    /// <summary>
    /// Пути поиска юнитов (Unit Search Paths)
    /// </summary>
    public List<string> SearchPaths { get; set; } = new();

    /// <summary>
    /// Пути для include файлов
    /// </summary>
    public List<string> IncludePaths { get; set; } = new();

    /// <summary>
    /// Все юниты проекта (потокобезопасная коллекция для параллельного анализа)
    /// </summary>
    public ConcurrentBag<DelphiUnit> Units { get; set; } = new();

    /// <summary>
    /// Все include файлы проекта (потокобезопасная коллекция)
    /// </summary>
    public ConcurrentBag<string> IncludeFiles { get; set; } = new();

    /// <summary>
    /// Граф зависимостей
    /// </summary>
    public DependencyGraph DependencyGraph { get; set; } = new();
}
