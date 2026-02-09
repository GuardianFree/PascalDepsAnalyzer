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

    /// <summary>
    /// Конфигурация проекта (Debug, Release)
    /// </summary>
    public string Configuration { get; set; } = "Debug";

    /// <summary>
    /// Целевая платформа (Win32, Win64, OSX32, Android, iOS)
    /// </summary>
    public string Platform { get; set; } = "Win32";

    /// <summary>
    /// Символы условной компиляции, определённые в проекте
    /// </summary>
    public HashSet<string> CompilationDefines { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Предопределённые переменные компилятора (CompilerVersion, RTLVersion и т.д.)
    /// Используются для вычисления выражений вида {$IF CompilerVersion >= 31}
    /// </summary>
    public Dictionary<string, double> CompilerVariables { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
