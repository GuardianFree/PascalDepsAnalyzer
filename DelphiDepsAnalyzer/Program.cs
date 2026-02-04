using DelphiDepsAnalyzer.Core;
using DelphiDepsAnalyzer.Output;
using DelphiDepsAnalyzer.Parsers;

namespace DelphiDepsAnalyzer;

class Program
{
    static int Main(string[] args)
    {
        Console.WriteLine("Delphi Dependency Analyzer v1.1 (Phase 2)");
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

            // Парсинг проекта
            Console.WriteLine($"\n1. Парсинг проекта: {Path.GetFileName(dprojPath)}");
            var projectParser = new DelphiProjectParser();
            var project = metrics.MeasureOperation("Parse .dproj", () =>
                projectParser.Parse(dprojPath));

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
        Console.WriteLine("\nПример:");
        Console.WriteLine("  DelphiDepsAnalyzer.exe C:\\Projects\\MyApp\\MyApp.dproj");
        Console.WriteLine("  DelphiDepsAnalyzer.exe C:\\Projects\\MyApp\\MyApp.dproj --performance");
        Console.WriteLine("\nОпции:");
        Console.WriteLine("  --help, -h           Показать это сообщение");
        Console.WriteLine("  --performance, -p    Показать детальные метрики производительности");
        Console.WriteLine("\nРезультат:");
        Console.WriteLine("  Создаётся файл <проект>.deps.json рядом с .dproj файлом");
    }
}
