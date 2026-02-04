using System.Xml.Linq;
using DelphiDepsAnalyzer.Models;

namespace DelphiDepsAnalyzer.Parsers;

/// <summary>
/// Парсер .dproj файлов (Delphi Project XML)
/// </summary>
public class DelphiProjectParser
{
    /// <summary>
    /// Парсит .dproj файл и извлекает конфигурацию проекта
    /// </summary>
    public DelphiProject Parse(string dprojFilePath)
    {
        if (!File.Exists(dprojFilePath))
        {
            throw new FileNotFoundException($"Файл проекта не найден: {dprojFilePath}");
        }

        var project = new DelphiProject
        {
            ProjectFilePath = Path.GetFullPath(dprojFilePath)
        };

        var doc = XDocument.Load(dprojFilePath);
        var ns = doc.Root?.Name.Namespace ?? XNamespace.None;

        // Извлечение главного .dpr файла
        var mainSource = doc.Descendants(ns + "MainSource")
            .FirstOrDefault()?.Value;

        if (!string.IsNullOrEmpty(mainSource))
        {
            var projectDir = Path.GetDirectoryName(dprojFilePath) ?? string.Empty;
            project.MainSourcePath = Path.GetFullPath(Path.Combine(projectDir, mainSource));
        }

        // Извлечение Search Paths
        var searchPaths = doc.Descendants(ns + "DCC_UnitSearchPath")
            .FirstOrDefault()?.Value;

        if (!string.IsNullOrEmpty(searchPaths))
        {
            project.SearchPaths = ParsePaths(searchPaths, dprojFilePath);
        }

        // Извлечение Include Paths
        var includePaths = doc.Descendants(ns + "DCC_IncludePath")
            .FirstOrDefault()?.Value;

        if (!string.IsNullOrEmpty(includePaths))
        {
            project.IncludePaths = ParsePaths(includePaths, dprojFilePath);
        }

        // Добавляем директорию проекта в search paths по умолчанию
        var projectDir2 = Path.GetDirectoryName(dprojFilePath);
        if (!string.IsNullOrEmpty(projectDir2) && !project.SearchPaths.Contains(projectDir2))
        {
            project.SearchPaths.Insert(0, projectDir2);
        }

        return project;
    }

    /// <summary>
    /// Парсит строку с путями (разделённые точкой с запятой)
    /// </summary>
    private List<string> ParsePaths(string pathsString, string dprojFilePath)
    {
        var projectDir = Path.GetDirectoryName(dprojFilePath) ?? string.Empty;
        var paths = new List<string>();

        foreach (var path in pathsString.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmedPath = path.Trim();

            // Пропускаем макросы, которые мы не можем разрешить на этом этапе
            if (trimmedPath.Contains("$(") && trimmedPath.Contains(")"))
            {
                // TODO: Добавить поддержку макросов $(Platform), $(Config) и т.д.
                Console.WriteLine($"Предупреждение: Пропущен путь с макросами: {trimmedPath}");
                continue;
            }

            // Разрешаем относительные пути
            string fullPath;
            if (Path.IsPathRooted(trimmedPath))
            {
                fullPath = trimmedPath;
            }
            else
            {
                fullPath = Path.GetFullPath(Path.Combine(projectDir, trimmedPath));
            }

            if (Directory.Exists(fullPath))
            {
                paths.Add(fullPath);
            }
            else
            {
                Console.WriteLine($"Предупреждение: Путь не существует: {fullPath}");
            }
        }

        return paths;
    }
}
