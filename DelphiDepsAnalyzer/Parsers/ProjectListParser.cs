using DelphiDepsAnalyzer.Models;

namespace DelphiDepsAnalyzer.Parsers;

/// <summary>
/// Парсер файла со списком проектов для CI-анализа
/// </summary>
public class ProjectListParser
{
    /// <summary>
    /// Парсит файл со списком проектов
    /// Формат: путь_к_проекту[;директивы_компиляции]
    /// Пример: Bazis.dproj;FASTREPORT
    /// </summary>
    public List<ProjectEntry> Parse(string projectListPath, string repoRoot)
    {
        if (!File.Exists(projectListPath))
        {
            throw new FileNotFoundException($"Файл списка проектов не найден: {projectListPath}");
        }

        var repoRootFull = Path.GetFullPath(repoRoot);
        var projectListDir = Path.GetDirectoryName(Path.GetFullPath(projectListPath)) ?? repoRootFull;
        var result = new List<ProjectEntry>();
        var lineNumber = 0;

        foreach (var line in File.ReadLines(projectListPath))
        {
            lineNumber++;

            // Игнорируем пустые строки и комментарии
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#") || trimmed.StartsWith("//"))
            {
                continue;
            }

            // Разбираем строку: путь[;директивы]
            var parts = trimmed.Split(';', 2);
            var projectPath = parts[0].Trim();
            var directives = parts.Length > 1 ? parts[1].Trim() : null;

            if (string.IsNullOrWhiteSpace(projectPath))
            {
                Console.WriteLine($"  [WARNING] Строка {lineNumber}: пустой путь к проекту, пропускаем");
                continue;
            }

            // Конвертируем относительный путь в абсолютный
            // Сначала пробуем относительно файла со списком проектов
            string absolutePath;
            if (Path.IsPathRooted(projectPath))
            {
                absolutePath = projectPath;
            }
            else
            {
                // Сначала пробуем относительно директории списка проектов
                absolutePath = Path.GetFullPath(Path.Combine(projectListDir, projectPath));

                // Если не найден, пробуем относительно корня репо
                if (!File.Exists(absolutePath))
                {
                    absolutePath = Path.GetFullPath(Path.Combine(repoRootFull, projectPath));
                }
            }

            // Валидация существования проекта
            if (!File.Exists(absolutePath))
            {
                Console.WriteLine($"  [WARNING] Строка {lineNumber}: файл проекта не найден: {projectPath}");
                Console.WriteLine($"            Проверены пути: {absolutePath}");
                continue;
            }

            if (!absolutePath.EndsWith(".dproj", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"  [WARNING] Строка {lineNumber}: ожидается файл .dproj: {projectPath}");
                continue;
            }

            // Вычисляем относительный путь от корня репо для вывода
            var relativePath = Path.GetRelativePath(repoRootFull, absolutePath);

            result.Add(new ProjectEntry
            {
                ProjectPath = absolutePath,
                RelativePath = relativePath,
                Directives = string.IsNullOrEmpty(directives) ? null : directives
            });
        }

        if (result.Count == 0)
        {
            throw new InvalidOperationException($"Файл {projectListPath} не содержит валидных проектов");
        }

        return result;
    }
}
