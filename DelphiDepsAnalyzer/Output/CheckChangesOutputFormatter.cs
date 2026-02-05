using DelphiDepsAnalyzer.Models;

namespace DelphiDepsAnalyzer.Output;

/// <summary>
/// Форматирует и сохраняет результаты анализа затронутых проектов
/// </summary>
public class CheckChangesOutputFormatter
{
    /// <summary>
    /// Сохранить список затронутых проектов в файл
    /// </summary>
    public void SaveAffectedProjects(List<ProjectEntry> affectedProjects, string outputPath)
    {
        // Сортируем по алфавиту для консистентности
        var sorted = affectedProjects.OrderBy(p => p.RelativePath).ToList();

        // Формируем строки в исходном формате: путь[;директивы]
        var lines = sorted.Select(p => p.GetOutputLine()).ToArray();

        File.WriteAllLines(outputPath, lines);
        Console.WriteLine($"\nСписок затронутых проектов сохранён в: {outputPath}");
    }

    /// <summary>
    /// Вывести сводку в консоль
    /// </summary>
    public void PrintSummary(int totalProjects, int affectedCount, List<ProjectEntry> affectedProjects)
    {
        Console.WriteLine("\n4. Результаты:");
        Console.WriteLine($"   Проанализировано проектов: {totalProjects}");
        Console.WriteLine($"   Затронуто изменениями: {affectedCount}");

        if (affectedCount > 0)
        {
            Console.WriteLine("\n   Затронутые проекты:");
            var sorted = affectedProjects.OrderBy(p => p.RelativePath).ToList();
            foreach (var project in sorted)
            {
                var directives = string.IsNullOrEmpty(project.Directives)
                    ? ""
                    : $" ({project.Directives})";
                Console.WriteLine($"     - {project.RelativePath}{directives}");
            }
        }

        Console.WriteLine(new string('=', 60));
    }
}
