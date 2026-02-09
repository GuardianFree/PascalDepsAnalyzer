using System.Collections.Concurrent;
using DelphiDepsAnalyzer.Models;
using DelphiDepsAnalyzer.Parsers;

namespace DelphiDepsAnalyzer.Core;

/// <summary>
/// Анализатор затронутых проектов с early exit оптимизацией
/// </summary>
public class AffectedProjectsAnalyzer
{
    private readonly PerformanceMetrics _metrics;
    private readonly DependencyCache _cache;
    private readonly string _configuration;
    private readonly string _platform;

    public AffectedProjectsAnalyzer(PerformanceMetrics metrics, DependencyCache cache,
                                     string configuration = "Debug", string platform = "Win32")
    {
        _metrics = metrics;
        _cache = cache;
        _configuration = configuration;
        _platform = platform;
    }

    /// <summary>
    /// Анализирует все проекты параллельно и возвращает затронутые
    /// </summary>
    public List<ProjectEntry> AnalyzeAffectedProjects(
        List<ProjectEntry> allProjects,
        HashSet<string> changedFiles,
        string repoRoot)
    {
        var affected = new ConcurrentBag<ProjectEntry>();
        var projectCounter = 0;
        var lockObj = new object();

        Parallel.ForEach(allProjects, project =>
        {
            var currentIndex = Interlocked.Increment(ref projectCounter);

            try
            {
                var isAffected = IsProjectAffected(
                    project.ProjectPath,
                    project.Directives,
                    changedFiles,
                    repoRoot,
                    out var matchedFile);

                lock (lockObj)
                {
                    if (isAffected)
                    {
                        Console.WriteLine($"  [{currentIndex}/{allProjects.Count}] {project.RelativePath}... ✓ AFFECTED ({Path.GetFileName(matchedFile)})");
                        affected.Add(project);
                    }
                    else
                    {
                        Console.WriteLine($"  [{currentIndex}/{allProjects.Count}] {project.RelativePath}... - not affected");
                    }
                }
            }
            catch (Exception ex)
            {
                lock (lockObj)
                {
                    Console.WriteLine($"  [{currentIndex}/{allProjects.Count}] {project.RelativePath}... [ERROR] {ex.Message}");
                }
            }
        });

        return affected.ToList();
    }

    /// <summary>
    /// Проверяет, затронут ли конкретный проект (с early exit)
    /// </summary>
    private bool IsProjectAffected(
        string projectPath,
        string? additionalDirectives,
        HashSet<string> changedFiles,
        string repoRoot,
        out string? firstMatchedFile)
    {
        firstMatchedFile = null;

        // 1. Сначала проверяем сам .dproj файл
        var projectRelativePath = NormalizePath(projectPath, repoRoot);
        if (changedFiles.Contains(projectRelativePath))
        {
            firstMatchedFile = projectRelativePath;
            return true;
        }

        // 2. Парсим проект
        var projectParser = new DelphiProjectParser();
        DelphiProject project;

        try
        {
            project = _metrics.MeasureOperation($"Parse {Path.GetFileName(projectPath)}", () =>
                projectParser.Parse(projectPath, _configuration, _platform));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Не удалось распарсить проект: {ex.Message}", ex);
        }

        // 2.1. Добавляем директивы из файла списка проектов (активны в любой конфигурации)
        if (!string.IsNullOrEmpty(additionalDirectives))
        {
            foreach (var define in additionalDirectives.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = define.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    project.CompilationDefines.Add(trimmed);
                }
            }
        }

        // 3. Анализируем зависимости
        var analyzer = new DependencyAnalyzer(project, _metrics, _cache);

        try
        {
            _metrics.MeasureOperation($"Analyze {Path.GetFileName(projectPath)}", () =>
                analyzer.Analyze());
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Не удалось проанализировать зависимости: {ex.Message}", ex);
        }

        // 4. Собираем все файлы проекта
        var projectFiles = new List<string>();

        // Главный .dpr файл
        if (!string.IsNullOrEmpty(project.MainSourcePath))
        {
            projectFiles.Add(project.MainSourcePath);
        }

        // Все .pas файлы из юнитов
        foreach (var unit in project.Units)
        {
            if (!string.IsNullOrEmpty(unit.FilePath))
            {
                projectFiles.Add(unit.FilePath);
            }
        }

        // Все .inc файлы
        foreach (var includeFile in project.IncludeFiles)
        {
            if (!string.IsNullOrEmpty(includeFile))
            {
                projectFiles.Add(includeFile);
            }
        }

        // 5. EARLY EXIT: проверяем каждый файл на совпадение
        foreach (var file in projectFiles)
        {
            var normalizedFile = NormalizePath(file, repoRoot);
            if (changedFiles.Contains(normalizedFile))
            {
                firstMatchedFile = normalizedFile;
                return true; // Прерываем анализ!
            }
        }

        return false;
    }

    /// <summary>
    /// Нормализовать путь для сравнения: lowercase, forward slashes, относительный от корня репо
    /// </summary>
    private string NormalizePath(string path, string repoRoot)
    {
        var fullPath = Path.GetFullPath(path);
        var repoRootFull = Path.GetFullPath(repoRoot);

        var relativePath = Path.GetRelativePath(repoRootFull, fullPath);
        return relativePath.Replace('\\', '/').ToLowerInvariant();
    }
}
