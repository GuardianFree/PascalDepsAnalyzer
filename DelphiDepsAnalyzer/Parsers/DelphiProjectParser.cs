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
    /// <param name="dprojFilePath">Путь к .dproj файлу</param>
    /// <param name="configuration">Конфигурация сборки (Debug, Release)</param>
    /// <param name="platform">Целевая платформа (Win32, Win64, OSX32, Android, iOS)</param>
    /// <param name="parseConditionals">Парсить ли условные директивы компиляции</param>
    public DelphiProject Parse(string dprojFilePath, string configuration = "Debug",
                              string platform = "Win32", bool parseConditionals = true)
    {
        if (!File.Exists(dprojFilePath))
        {
            throw new FileNotFoundException($"Файл проекта не найден: {dprojFilePath}");
        }

        var project = new DelphiProject
        {
            ProjectFilePath = Path.GetFullPath(dprojFilePath),
            Configuration = configuration,
            Platform = platform
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

        // Парсинг дефайнов (если требуется)
        if (parseConditionals)
        {
            project.CompilationDefines = ParseCompilationDefines(doc, configuration, platform, project);
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

    /// <summary>
    /// Извлекает DCC_Define для указанной конфигурации и платформы
    /// Обрабатывает:
    /// - Condition атрибуты на PropertyGroup
    /// - Наследование через CfgParent/Base
    /// - Макросы $(Config), $(Platform)
    /// </summary>
    private HashSet<string> ParseCompilationDefines(XDocument doc, string configuration, string platform, DelphiProject project)
    {
        var defines = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ns = doc.Root?.Name.Namespace ?? XNamespace.None;

        // 1. Собираем defines из Base конфигурации
        var baseGroups = doc.Descendants(ns + "PropertyGroup")
            .Where(pg => EvaluateCondition(pg.Attribute("Condition")?.Value, "Base", platform));

        foreach (var group in baseGroups)
        {
            var dccDefine = group.Element(ns + "DCC_Define")?.Value;
            if (!string.IsNullOrEmpty(dccDefine))
            {
                AddDefines(defines, dccDefine);
            }
        }

        // 2. Собираем defines из целевой конфигурации (Debug/Release)
        var configGroups = doc.Descendants(ns + "PropertyGroup")
            .Where(pg => EvaluateCondition(pg.Attribute("Condition")?.Value, configuration, platform));

        foreach (var group in configGroups)
        {
            var dccDefine = group.Element(ns + "DCC_Define")?.Value;
            if (!string.IsNullOrEmpty(dccDefine))
            {
                AddDefines(defines, dccDefine);
            }
        }

        // 3. Добавляем предопределённые символы для платформы
        AddPlatformDefines(defines, platform);

        // 4. Добавляем предопределённые переменные компилятора
        AddCompilerVariables(project);

        return defines;
    }

    /// <summary>
    /// Вычисляет Condition атрибут из PropertyGroup
    /// Поддерживает: '$(Config)'=='Debug', '$(Platform)'=='Win32', 'and', 'or'
    /// </summary>
    private bool EvaluateCondition(string? condition, string config, string platform)
    {
        if (string.IsNullOrEmpty(condition))
            return false;

        // Заменяем макросы на реальные значения
        condition = condition
            .Replace("$(Config)", $"'{config}'", StringComparison.OrdinalIgnoreCase)
            .Replace("$(Configuration)", $"'{config}'", StringComparison.OrdinalIgnoreCase)
            .Replace("$(Platform)", $"'{platform}'", StringComparison.OrdinalIgnoreCase)
            .Replace("$(Base)", "'Base'", StringComparison.OrdinalIgnoreCase);

        // Вычисляем простые условия вида 'value1'=='value2' или 'value'!=''
        try
        {
            // Обрабатываем OR
            if (condition.Contains(" or ", StringComparison.OrdinalIgnoreCase))
            {
                var parts = condition.Split(new[] { " or " }, StringSplitOptions.RemoveEmptyEntries);
                // Для OR достаточно одного истинного условия, НО игнорируем условия с неопределенными макросами
                return parts.Any(p => {
                    var trimmed = p.Trim();
                    // Игнорируем условия с неопределенными макросами вида '$(Cfg_1)'!=''
                    if (trimmed.Contains("$("))
                        return false;
                    return EvaluateSimpleCondition(trimmed);
                });
            }

            // Обрабатываем AND
            if (condition.Contains(" and ", StringComparison.OrdinalIgnoreCase))
            {
                var parts = condition.Split(new[] { " and " }, StringSplitOptions.RemoveEmptyEntries);
                return parts.All(p => {
                    var trimmed = p.Trim();
                    // Условия с неопределенными макросами считаем false
                    if (trimmed.Contains("$("))
                        return false;
                    return EvaluateSimpleCondition(trimmed);
                });
            }

            // Простое условие без OR/AND
            // Игнорируем условия с неопределенными макросами
            if (condition.Contains("$("))
                return false;
            return EvaluateSimpleCondition(condition.Trim());
        }
        catch
        {
            // В случае ошибки парсинга возвращаем false
            return false;
        }
    }

    /// <summary>
    /// Вычисляет простое условие вида 'value1'=='value2' или 'value'!=''
    /// </summary>
    private bool EvaluateSimpleCondition(string condition)
    {
        // Обрабатываем == и !=
        if (condition.Contains("=="))
        {
            var parts = condition.Split(new[] { "==" }, 2, StringSplitOptions.None);
            if (parts.Length == 2)
            {
                var left = parts[0].Trim().Trim('\'');
                var right = parts[1].Trim().Trim('\'');
                return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
            }
        }
        else if (condition.Contains("!="))
        {
            var parts = condition.Split(new[] { "!=" }, 2, StringSplitOptions.None);
            if (parts.Length == 2)
            {
                var left = parts[0].Trim().Trim('\'');
                var right = parts[1].Trim().Trim('\'');
                return !string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
            }
        }

        return false;
    }

    /// <summary>
    /// Добавляет defines из строки DCC_Define (разделённой точкой с запятой)
    /// </summary>
    private void AddDefines(HashSet<string> defines, string dccDefineString)
    {
        // Удаляем макросы $(DCC_Define) из строки
        dccDefineString = dccDefineString.Replace("$(DCC_Define)", "", StringComparison.OrdinalIgnoreCase);

        var symbols = dccDefineString.Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (var symbol in symbols)
        {
            var trimmed = symbol.Trim();
            if (!string.IsNullOrEmpty(trimmed) && !trimmed.StartsWith("$("))
            {
                defines.Add(trimmed);
            }
        }
    }

    /// <summary>
    /// Добавляет предопределённые символы для платформы
    /// </summary>
    private void AddPlatformDefines(HashSet<string> defines, string platform)
    {
        // Предопределённые символы Delphi для платформ
        switch (platform.ToUpperInvariant())
        {
            case "WIN32":
                defines.Add("MSWINDOWS");
                defines.Add("WIN32");
                break;
            case "WIN64":
                defines.Add("MSWINDOWS");
                defines.Add("WIN64");
                defines.Add("CPU64BITS");
                break;
            case "OSX32":
            case "OSX64":
                defines.Add("MACOS");
                defines.Add("POSIX");
                break;
            case "ANDROID":
            case "ANDROID64":
                defines.Add("ANDROID");
                defines.Add("POSIX");
                break;
            case "IOS32":
            case "IOS64":
            case "IOSSIMULATOR":
                defines.Add("IOS");
                defines.Add("POSIX");
                break;
            case "LINUX64":
                defines.Add("LINUX");
                defines.Add("POSIX");
                break;
        }

        // Универсальные предопределённые символы
        defines.Add("CONDITIONALEXPRESSIONS");

        // Версионный символ Delphi 12 Athens
        defines.Add("VER360");
    }

    /// <summary>
    /// Добавляет предопределённые переменные компилятора Delphi 12 Athens
    /// Используются для вычисления выражений вида {$IF CompilerVersion >= 31}
    /// </summary>
    private void AddCompilerVariables(DelphiProject project)
    {
        project.CompilerVariables["CompilerVersion"] = 36.0;
        project.CompilerVariables["RTLVersion"] = 36.0;
    }
}
