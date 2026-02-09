using System.Collections.Concurrent;
using DelphiDepsAnalyzer.Models;
using DelphiDepsAnalyzer.Parsers;

namespace DelphiDepsAnalyzer.Core;

/// <summary>
/// Анализирует зависимости Delphi проекта и строит граф
/// </summary>
public class DependencyAnalyzer
{
    private readonly DelphiProject _project;
    private readonly DelphiSourceParser _sourceParser;
    private readonly PathResolver _pathResolver;
    private readonly ConcurrentDictionary<string, byte> _processedUnits; // Для предотвращения циклов
    private readonly ConcurrentDictionary<string, DelphiUnit> _unitCache; // Кэш распарсенных юнитов
    private readonly PerformanceMetrics _metrics;
    private readonly DependencyCache? _cache;
    private readonly object _graphLock = new(); // Синхронизация для графа
    private readonly ConditionalCompilationEvaluator? _evaluator;
    private readonly IncludeFileProcessor? _includeProcessor;
    private readonly bool _useConditionals;
    private readonly HashSet<string>? _activeDefines;

    public DependencyAnalyzer(DelphiProject project, PerformanceMetrics? metrics = null,
                             DependencyCache? cache = null, bool useConditionals = true)
    {
        _project = project;
        _sourceParser = new DelphiSourceParser();
        _pathResolver = new PathResolver(project.SearchPaths, project.ProjectFilePath);
        _processedUnits = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        _unitCache = new ConcurrentDictionary<string, DelphiUnit>(StringComparer.OrdinalIgnoreCase);
        _metrics = metrics ?? new PerformanceMetrics();
        _cache = cache;
        _useConditionals = useConditionals;

        // Инициализируем evaluator только если используем условную компиляцию
        if (_useConditionals && project.CompilationDefines.Count > 0)
        {
            _evaluator = new ConditionalCompilationEvaluator(project.CompilationDefines,
                project.CompilerVariables.Count > 0 ? project.CompilerVariables : null);
            _includeProcessor = new IncludeFileProcessor(_pathResolver, _evaluator);
            _activeDefines = project.CompilationDefines;
        }
    }

    /// <summary>
    /// Выполняет полный анализ зависимостей проекта
    /// </summary>
    public void Analyze()
    {
        Console.WriteLine($"Анализ проекта: {_project.ProjectFilePath}");
        Console.WriteLine($"Главный файл: {_project.MainSourcePath}");
        Console.WriteLine($"Search paths: {_project.SearchPaths.Count}");

        if (_useConditionals && _evaluator != null)
        {
            Console.WriteLine($"Условная компиляция: включена");
            Console.WriteLine($"Конфигурация: {_project.Configuration}, Платформа: {_project.Platform}");
            Console.WriteLine($"Активные символы: {string.Join(", ", _project.CompilationDefines)}");
        }
        else if (!_useConditionals)
        {
            Console.WriteLine($"Условная компиляция: отключена (--ignore-conditionals)");
        }

        if (!File.Exists(_project.MainSourcePath))
        {
            throw new FileNotFoundException($"Главный файл проекта не найден: {_project.MainSourcePath}");
        }

        // Обрабатываем include файлы в главном .dpr файле ПЕРЕД его парсингом
        if (_includeProcessor != null)
        {
            ProcessIncludesForFile(_project.MainSourcePath);
        }

        // Парсим главный файл
        Console.WriteLine("\nПарсинг главного файла...");
        DelphiUnit mainUnit;

        // Проверяем кэш для главного файла
        if (_cache != null && _cache.TryGetCachedUnit(_project.MainSourcePath, _activeDefines, out var cachedMainUnit) && cachedMainUnit != null)
        {
            Console.WriteLine("[CACHE HIT] Главный файл загружен из кэша");
            _cache.RecordHit();
            mainUnit = cachedMainUnit;
        }
        else
        {
            if (_cache != null)
            {
                Console.WriteLine("[CACHE MISS] Парсинг главного файла");
                _cache.RecordMiss();
            }

            mainUnit = _metrics.MeasureOperation("Parse Main File", () =>
                _sourceParser.Parse(_project.MainSourcePath, _evaluator?.Clone()));

            // Сохраняем в кэш
            _cache?.CacheUnit(_project.MainSourcePath, _activeDefines, mainUnit);
        }

        _project.Units.Add(mainUnit); // ConcurrentBag - потокобезопасен

        lock (_graphLock)
        {
            _project.DependencyGraph.AddNode(mainUnit.UnitName, mainUnit.FilePath);
        }
        _unitCache[mainUnit.UnitName] = mainUnit;

        // Рекурсивно анализируем зависимости
        Console.WriteLine("\nРекурсивный анализ зависимостей...");
        AnalyzeDependencies(mainUnit);

        // Обрабатываем include файлы
        ProcessIncludes();

        Console.WriteLine($"\nАнализ завершён:");
        Console.WriteLine($"  Найдено юнитов: {_project.Units.Count}");
        Console.WriteLine($"  Найдено include файлов: {_project.IncludeFiles.Count}");
        Console.WriteLine($"  Рёбер в графе: {_project.DependencyGraph.Edges.Count}");
    }

    /// <summary>
    /// Рекурсивно анализирует зависимости юнита (с параллельной обработкой)
    /// </summary>
    private void AnalyzeDependencies(DelphiUnit unit)
    {
        // Обрабатываем зависимости параллельно для ускорения на больших проектах
        Parallel.ForEach(unit.AllDependencies, dependency =>
        {
            // Пропускаем уже обработанные юниты (защита от циклов)
            if (_processedUnits.ContainsKey(dependency))
            {
                // Но всё равно добавляем ребро в граф
                lock (_graphLock)
                {
                    _project.DependencyGraph.AddEdge(unit.UnitName, dependency);
                }
                return; // В Parallel.ForEach используем return вместо continue
            }

            // Пропускаем внешние юниты (системные, библиотечные)
            if (_pathResolver.IsExternalUnit(dependency))
            {
                lock (_graphLock)
                {
                    _project.DependencyGraph.AddNode(dependency, "[External]");
                    _project.DependencyGraph.AddEdge(unit.UnitName, dependency);
                }
                _processedUnits.TryAdd(dependency, 0);
                return;
            }

            // Пытаемся найти файл юнита
            var unitPath = _metrics.MeasureOperation("Resolve Unit Path", () =>
                _pathResolver.ResolveUnitPath(dependency));
            if (unitPath == null)
            {
                lock (_graphLock)
                {
                    _project.DependencyGraph.AddNode(dependency, "[Not Found]");
                    _project.DependencyGraph.AddEdge(unit.UnitName, dependency);
                }
                _processedUnits.TryAdd(dependency, 0);
                return;
            }

            // Помечаем как обработанный
            _processedUnits.TryAdd(dependency, 0);

            // Парсим найденный юнит (с использованием кэша, если доступен)
            DelphiUnit dependencyUnit;
            try
            {
                // Проверяем кэш
                if (_cache != null && _cache.TryGetCachedUnit(unitPath, _activeDefines, out var cachedUnit) && cachedUnit != null)
                {
                    _cache.RecordHit();
                    dependencyUnit = cachedUnit;
                }
                else
                {
                    _cache?.RecordMiss();

                    // Обрабатываем include файлы ПЕРЕД парсингом
                    if (_includeProcessor != null)
                    {
                        ProcessIncludesForFile(unitPath);
                    }

                    dependencyUnit = _metrics.MeasureOperation($"Parse Unit: {dependency}", () =>
                        _sourceParser.Parse(unitPath, _evaluator?.Clone()));

                    // Сохраняем в кэш
                    _cache?.CacheUnit(unitPath, _activeDefines, dependencyUnit);
                }

                _project.Units.Add(dependencyUnit); // ConcurrentBag - потокобезопасен
                _unitCache[dependencyUnit.UnitName] = dependencyUnit;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [ERROR] Ошибка парсинга {unitPath}: {ex.Message}");
                return;
            }

            // Добавляем в граф
            lock (_graphLock)
            {
                _project.DependencyGraph.AddNode(dependencyUnit.UnitName, dependencyUnit.FilePath);
                _project.DependencyGraph.AddEdge(unit.UnitName, dependencyUnit.UnitName);
            }

            // Рекурсивно обрабатываем зависимости найденного юнита
            AnalyzeDependencies(dependencyUnit);
        }); // Закрываем Parallel.ForEach
    }

    /// <summary>
    /// Обрабатывает все include файлы
    /// </summary>
    private void ProcessIncludes()
    {
        // Используем ConcurrentDictionary для быстрой проверки дубликатов
        var processedIncludes = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

        foreach (var unit in _project.Units)
        {
            foreach (var includePath in unit.IncludeFiles)
            {
                var resolvedPath = _pathResolver.ResolveIncludePath(includePath, unit.FilePath);
                if (resolvedPath != null && processedIncludes.TryAdd(resolvedPath, 0))
                {
                    _project.IncludeFiles.Add(resolvedPath);
                    Console.WriteLine($"  [INCLUDE] {includePath} -> {resolvedPath}");
                }
                else if (resolvedPath == null)
                {
                    Console.WriteLine($"  [WARNING] Include файл не найден: {includePath}");
                }
            }
        }
    }

    /// <summary>
    /// Предварительно обрабатывает include файлы для извлечения дефайнов
    /// </summary>
    private void ProcessIncludesForFile(string filePath)
    {
        if (_includeProcessor == null)
            return;

        try
        {
            var content = File.ReadAllText(filePath);
            var includeMatches = System.Text.RegularExpressions.Regex.Matches(content,
                @"\{\s*\$I(?:NCLUDE)?\s+['""]?([^}'""]+)['""]?\s*\}",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            foreach (System.Text.RegularExpressions.Match match in includeMatches)
            {
                var includePath = match.Groups[1].Value.Trim();
                _includeProcessor.ProcessIncludeFile(includePath, filePath);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [WARNING] Ошибка обработки includes для {filePath}: {ex.Message}");
        }
    }
}
