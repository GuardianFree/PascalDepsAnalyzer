using DelphiDepsAnalyzer.Core;
using DelphiDepsAnalyzer.Output;
using DelphiDepsAnalyzer.Parsers;

namespace DelphiDepsAnalyzer;

class Program
{
    static int Main(string[] args)
    {
        Console.WriteLine("Delphi Dependency Analyzer v1.3.0 (Phase 3 - CI Integration)");
        Console.WriteLine(new string('=', 60));

        var metrics = new PerformanceMetrics();
        var showPerformance = args.Contains("--performance") || args.Contains("-p");

        try
        {
            metrics.Start();

            // Парсинг аргументов
            var filteredArgs = args.Where(a => !a.StartsWith("-")).ToArray();
            if (filteredArgs.Length == 0 || args.Contains("--help") || args.Contains("-h"))
            {
                PrintUsage();
                return 0;
            }

            // Проверяем, какая команда запрошена
            var command = filteredArgs[0].ToLowerInvariant();

            if (command == "list-files")
            {
                return ExecuteListFilesCommand(args, filteredArgs, metrics);
            }

            if (command == "check-changes")
            {
                return ExecuteCheckChangesCommand(args, filteredArgs, metrics);
            }
            
            // Если первый аргумент - это путь к файлу, выполняем стандартный анализ
            var dprojPath = filteredArgs[0];

            if (!File.Exists(dprojPath))
            {
                Console.WriteLine($"ОШИБКА: Файл не найден: {dprojPath}");
                return 1;
            }

            if (!dprojPath.EndsWith(".dproj", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("ОШИБКА: Ожидается файл .dproj");
                return 1;
            }

            // Парсим новые параметры условной компиляции
            string configuration = "Debug";
            string platform = "Win32";
            bool ignoreConditionals = args.Contains("--ignore-conditionals");

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--config" && i + 1 < args.Length)
                    configuration = args[i + 1];
                else if (args[i] == "--platform" && i + 1 < args.Length)
                    platform = args[i + 1];
            }

            // Парсинг проекта
            Console.WriteLine($"\n1. Парсинг проекта: {Path.GetFileName(dprojPath)}");
            var projectParser = new DelphiProjectParser();
            var project = metrics.MeasureOperation("Parse .dproj", () =>
                projectParser.Parse(dprojPath, configuration, platform, !ignoreConditionals));

            // Инициализация кэша
            var projectDir = Path.GetDirectoryName(dprojPath) ?? Directory.GetCurrentDirectory();
            var cache = new DependencyCache(projectDir);

            // Анализ зависимостей
            Console.WriteLine("\n2. Анализ зависимостей:");
            var analyzer = new DependencyAnalyzer(project, metrics, cache);
            metrics.MeasureOperation("Analyze Dependencies", () =>
                analyzer.Analyze());

            // Сохранение кэша
            metrics.MeasureOperation("Save Cache", () =>
                cache.SaveCache());

            // Вывод результатов
            Console.WriteLine("\n3. Формирование отчета:");
            var formatter = new JsonOutputFormatter();

            // Вывод сводки в консоль
            formatter.PrintSummary(project);

            // Сохранение JSON
            var outputPath = Path.ChangeExtension(dprojPath, ".deps.json");
            metrics.MeasureOperation("Save JSON", () =>
                formatter.SaveToFile(project, outputPath));

            metrics.Stop();

            Console.WriteLine("\n✓ Анализ успешно завершён!");
            Console.WriteLine($"Общее время: {metrics.TotalTime.TotalSeconds:F2}с");

            // Показываем детальные метрики если запрошено
            if (showPerformance)
            {
                metrics.PrintReport();

                // Статистика кэша
                var cacheStats = cache.GetStatistics();
                Console.WriteLine("\nСТАТИСТИКА КЭША");
                Console.WriteLine(new string('=', 60));
                Console.WriteLine($"Записей в кэше:   {cacheStats.TotalEntries}");
                Console.WriteLine($"Cache Hits:       {cacheStats.CacheHits}");
                Console.WriteLine($"Cache Misses:     {cacheStats.CacheMisses}");
                Console.WriteLine($"Hit Rate:         {cacheStats.HitRate:F1}%");
                Console.WriteLine(new string('=', 60));
            }

            return 0;
        }
        catch (Exception ex)
        {
            metrics.Stop();
            Console.WriteLine($"\n✗ ОШИБКА: {ex.Message}");
            Console.WriteLine($"\nStack trace:\n{ex.StackTrace}");
            return 1;
        }
    }

    static void PrintUsage()
    {
        Console.WriteLine("\nИспользование:");
        Console.WriteLine("  DelphiDepsAnalyzer.exe <путь к .dproj файлу> [опции]");
        Console.WriteLine("  DelphiDepsAnalyzer.exe list-files <путь к .dproj файлу> [опции]");
        Console.WriteLine("  DelphiDepsAnalyzer.exe check-changes <файл_списка_проектов> [опции]");
        Console.WriteLine("\nКоманды:");
        Console.WriteLine("  (по умолчанию)       Полный анализ зависимостей с выводом в JSON");
        Console.WriteLine("  list-files           Вывод списка файлов с путями относительно корня репозитория");
        Console.WriteLine("  check-changes        Определить проекты, затронутые изменениями между коммитами");
        Console.WriteLine("\nПримеры:");
        Console.WriteLine("  DelphiDepsAnalyzer.exe C:\\Projects\\MyApp\\MyApp.dproj");
        Console.WriteLine("  DelphiDepsAnalyzer.exe C:\\Projects\\MyApp\\MyApp.dproj --performance");
        Console.WriteLine("  DelphiDepsAnalyzer.exe list-files C:\\Projects\\MyApp\\MyApp.dproj");
        Console.WriteLine("  DelphiDepsAnalyzer.exe list-files C:\\Projects\\MyApp\\MyApp.dproj --repo-root C:\\Projects");
        Console.WriteLine("  DelphiDepsAnalyzer.exe check-changes Projects.txt --from abc123 --to def456 --output affected.txt");
        Console.WriteLine("\nОпции:");
        Console.WriteLine("  --help, -h               Показать это сообщение");
        Console.WriteLine("  --performance, -p        Показать детальные метрики производительности");
        Console.WriteLine("  --config <name>          Конфигурация сборки (Debug/Release, по умолчанию: Debug)");
        Console.WriteLine("  --platform <name>        Платформа (Win32/Win64/OSX32/Android/iOS, по умолчанию: Win32)");
        Console.WriteLine("  --ignore-conditionals    Игнорировать условные директивы (старое поведение)");
        Console.WriteLine("  --repo-root <путь>       Указать корень репозитория");
        Console.WriteLine("  --output <путь>          Сохранить результат в файл");
        Console.WriteLine("  --from <commit>          Начальный коммит (для команды check-changes)");
        Console.WriteLine("  --to <commit>            Конечный коммит (для команды check-changes)");
        Console.WriteLine("\nРезультат:");
        Console.WriteLine("  По умолчанию: создаётся файл <проект>.deps.json рядом с .dproj файлом");
        Console.WriteLine("  list-files: список файлов выводится в консоль или в указанный файл");
        Console.WriteLine("  check-changes: список затронутых проектов сохраняется в указанный файл");
        Console.WriteLine("              Если --repo-root не указан, ищется папка .git автоматически");
    }

    static int ExecuteCheckChangesCommand(string[] args, string[] filteredArgs, PerformanceMetrics metrics)
    {
        var showPerformance = args.Contains("--performance") || args.Contains("-p");

        // Нужен путь к файлу списка проектов после команды check-changes
        if (filteredArgs.Length < 2)
        {
            Console.WriteLine("ОШИБКА: Укажите путь к файлу списка проектов после команды check-changes");
            Console.WriteLine("Пример: DelphiDepsAnalyzer.exe check-changes Projects.txt --from abc123 --to def456 --output affected.txt");
            return 1;
        }

        var projectListPath = filteredArgs[1];

        if (!File.Exists(projectListPath))
        {
            Console.WriteLine($"ОШИБКА: Файл списка проектов не найден: {projectListPath}");
            return 1;
        }

        // Парсим опции: --from, --to, --output, --repo-root, --config, --platform
        string? fromCommit = null;
        string? toCommit = null;
        string? outputPath = null;
        string? repoRoot = null;
        string configuration = "Debug";
        string platform = "Win32";

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--from" && i + 1 < args.Length)
                fromCommit = args[i + 1];
            else if (args[i] == "--to" && i + 1 < args.Length)
                toCommit = args[i + 1];
            else if (args[i] == "--output" && i + 1 < args.Length)
                outputPath = args[i + 1];
            else if (args[i] == "--repo-root" && i + 1 < args.Length)
                repoRoot = args[i + 1];
            else if (args[i] == "--config" && i + 1 < args.Length)
                configuration = args[i + 1];
            else if (args[i] == "--platform" && i + 1 < args.Length)
                platform = args[i + 1];
        }

        // Валидация обязательных параметров
        if (string.IsNullOrEmpty(fromCommit) || string.IsNullOrEmpty(toCommit) || string.IsNullOrEmpty(outputPath))
        {
            Console.WriteLine("ОШИБКА: Требуются параметры --from, --to, --output");
            Console.WriteLine("Пример: DelphiDepsAnalyzer.exe check-changes Projects.txt --from abc123 --to def456 --output affected.txt");
            return 1;
        }

        // Определение корня репозитория
        var projectDir = Path.GetDirectoryName(Path.GetFullPath(projectListPath)) ?? Directory.GetCurrentDirectory();
        var repositoryRoot = RepositoryRootFinder.GetRepositoryRoot(repoRoot, projectDir);
        Console.WriteLine($"Корень репозитория: {repositoryRoot}");

        // Получение измененных файлов через git
        Console.WriteLine($"\n1. Получение изменений между {fromCommit}..{toCommit}:");
        var gitAnalyzer = new GitChangesAnalyzer(repositoryRoot);
        var changedFiles = metrics.MeasureOperation("Git Diff", () =>
            gitAnalyzer.GetChangedFiles(fromCommit, toCommit));
        Console.WriteLine($"   Изменено файлов: {changedFiles.Count}");

        if (changedFiles.Count == 0)
        {
            Console.WriteLine("\n✓ Нет измененных файлов между указанными коммитами.");
            File.WriteAllText(outputPath, string.Empty);
            Console.WriteLine($"Создан пустой файл: {outputPath}");
            return 0;
        }

        // Парсинг списка проектов
        Console.WriteLine($"\n2. Парсинг списка проектов:");
        var projectParser = new ProjectListParser();
        var allProjects = metrics.MeasureOperation("Parse Project List", () =>
            projectParser.Parse(projectListPath, repositoryRoot));
        Console.WriteLine($"   Прочитано проектов: {allProjects.Count}");

        // Инициализация единого кэша для всех проектов
        var cache = new DependencyCache(repositoryRoot);

        // Анализ затронутых проектов
        Console.WriteLine($"\n3. Анализ затронутых проектов:");
        var analyzer = new AffectedProjectsAnalyzer(metrics, cache, configuration, platform);
        var affectedProjects = metrics.MeasureOperation("Analyze Affected Projects", () =>
            analyzer.AnalyzeAffectedProjects(allProjects, changedFiles, repositoryRoot));

        // Сохранение кэша
        metrics.MeasureOperation("Save Cache", () =>
            cache.SaveCache());

        // Сохранение результатов
        var formatter = new CheckChangesOutputFormatter();
        formatter.SaveAffectedProjects(affectedProjects, outputPath);
        formatter.PrintSummary(allProjects.Count, affectedProjects.Count, affectedProjects);

        metrics.Stop();

        Console.WriteLine($"\n✓ Анализ успешно завершён!");
        Console.WriteLine($"Общее время: {metrics.TotalTime.TotalSeconds:F2}с");

        // Показываем детальные метрики если запрошено
        if (showPerformance)
        {
            metrics.PrintReport();

            // Статистика кэша
            var cacheStats = cache.GetStatistics();
            Console.WriteLine("\nСТАТИСТИКА КЭША");
            Console.WriteLine(new string('=', 60));
            Console.WriteLine($"Записей в кэше:   {cacheStats.TotalEntries}");
            Console.WriteLine($"Cache Hits:       {cacheStats.CacheHits}");
            Console.WriteLine($"Cache Misses:     {cacheStats.CacheMisses}");
            Console.WriteLine($"Hit Rate:         {cacheStats.HitRate:F1}%");
            Console.WriteLine(new string('=', 60));
        }

        return 0;
    }

    static int ExecuteListFilesCommand(string[] args, string[] filteredArgs, PerformanceMetrics metrics)
    {
        // Нужен путь к .dproj файлу после команды list-files
        if (filteredArgs.Length < 2)
        {
            Console.WriteLine("ОШИБКА: Укажите путь к .dproj файлу после команды list-files");
            Console.WriteLine("Пример: DelphiDepsAnalyzer.exe list-files MyProject.dproj");
            return 1;
        }

        var dprojPath = filteredArgs[1];

        if (!File.Exists(dprojPath))
        {
            Console.WriteLine($"ОШИБКА: Файл не найден: {dprojPath}");
            return 1;
        }

        if (!dprojPath.EndsWith(".dproj", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("ОШИБКА: Ожидается файл .dproj");
            return 1;
        }

        // Парсим опции
        string? repoRoot = null;
        string? outputFile = null;
        string configuration = "Debug";
        string platform = "Win32";
        bool ignoreConditionals = args.Contains("--ignore-conditionals");

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--repo-root" && i + 1 < args.Length)
            {
                repoRoot = args[i + 1];
            }
            else if (args[i] == "--output" && i + 1 < args.Length)
            {
                outputFile = args[i + 1];
            }
            else if (args[i] == "--config" && i + 1 < args.Length)
            {
                configuration = args[i + 1];
            }
            else if (args[i] == "--platform" && i + 1 < args.Length)
            {
                platform = args[i + 1];
            }
        }

        // Парсинг проекта
        Console.WriteLine($"\n1. Парсинг проекта: {Path.GetFileName(dprojPath)}");
        var projectParser = new DelphiProjectParser();
        var project = metrics.MeasureOperation("Parse .dproj", () =>
            projectParser.Parse(dprojPath, configuration, platform, !ignoreConditionals));

        // Инициализация кэша
        var projectDir = Path.GetDirectoryName(dprojPath) ?? Directory.GetCurrentDirectory();
        var cache = new DependencyCache(projectDir);

        // Анализ зависимостей
        Console.WriteLine("\n2. Анализ зависимостей:");
        var analyzer = new DependencyAnalyzer(project, metrics, cache);
        metrics.MeasureOperation("Analyze Dependencies", () =>
            analyzer.Analyze());

        // Сохранение кэша
        metrics.MeasureOperation("Save Cache", () =>
            cache.SaveCache());

        // Определяем корень репозитория
        Console.WriteLine("\n3. Определение корня репозитория:");
        var repositoryRoot = RepositoryRootFinder.GetRepositoryRoot(repoRoot, projectDir);
        Console.WriteLine($"Корень репозитория: {repositoryRoot}");

        // Формируем список файлов
        Console.WriteLine("\n4. Формирование списка файлов:");
        var formatter = new RelativePathOutputFormatter(repositoryRoot);

        if (!string.IsNullOrEmpty(outputFile))
        {
            formatter.SaveToFile(project, outputFile);
        }
        else
        {
            formatter.PrintFileList(project);
        }

        metrics.Stop();

        Console.WriteLine("\n✓ Команда list-files успешно выполнена!");
        Console.WriteLine($"Общее время: {metrics.TotalTime.TotalSeconds:F2}с");

        return 0;
    }
}
