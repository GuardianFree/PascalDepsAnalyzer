using System.Text.RegularExpressions;
using DelphiDepsAnalyzer.Models;

namespace DelphiDepsAnalyzer.Parsers;

/// <summary>
/// Парсер Delphi исходников (.pas, .dpr)
/// </summary>
public class DelphiSourceParser
{
    // Регулярные выражения для парсинга
    // ВАЖНО: используем lazy +? чтобы захватить uses секцию до ПЕРВОЙ точки с запятой.
    // В uses секции юниты разделяются запятыми, и только последний заканчивается ";".
    // Greedy "+" захватывал бы всё до последней ";" в секции, включая type/const/var,
    // что приводило к ложным warnings о глубокой вложенности условных директив.
    private static readonly Regex UsesRegex = new(
        @"(?:^|\n)\s*uses\s+([\s\S]+?);",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled
    );

    private static readonly Regex IncludeRegex = new(
        @"\{\s*\$I(?:NCLUDE)?\s+['""]?([^}'""]+)['""]?\s*\}",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    /// <summary>
    /// Парсит Delphi файл и извлекает зависимости
    /// </summary>
    /// <param name="filePath">Путь к файлу</param>
    /// <param name="evaluator">Evaluator для условных директив (null = старое поведение)</param>
    public DelphiUnit Parse(string filePath, Core.ConditionalCompilationEvaluator? evaluator = null)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Файл не найден: {filePath}");
        }

        var unit = new DelphiUnit
        {
            FilePath = Path.GetFullPath(filePath),
            UnitName = Path.GetFileNameWithoutExtension(filePath)
        };

        var content = File.ReadAllText(filePath);

        // Удаляем комментарии перед парсингом
        content = RemoveComments(content);

        // Если есть evaluator, обрабатываем {$DEFINE}/{$UNDEF} директивы
        // ВАЖНО: это должно быть ПЕРЕД разделением на секции
        if (evaluator != null)
        {
            content = evaluator.ProcessDefineDirectives(content);
        }

        // Разделяем на секции interface и implementation
        var (interfaceSection, implementationSection) = SplitSections(content);

        // Парсим uses из interface
        if (!string.IsNullOrEmpty(interfaceSection))
        {
            unit.InterfaceUses = ParseUses(interfaceSection, evaluator);
        }

        // Парсим uses из implementation
        if (!string.IsNullOrEmpty(implementationSection))
        {
            unit.ImplementationUses = ParseUses(implementationSection, evaluator);
        }

        // Парсим include директивы
        unit.IncludeFiles = ParseIncludes(content);

        return unit;
    }

    /// <summary>
    /// Удаляет комментарии из кода
    /// </summary>
    private string RemoveComments(string content)
    {
        // Удаляем // комментарии
        content = Regex.Replace(content, @"//.*?$", "", RegexOptions.Multiline);

        // Удаляем { } комментарии (но не директивы компилятора)
        content = Regex.Replace(content, @"\{(?!\$).*?\}", "", RegexOptions.Singleline);

        // Удаляем (* *) комментарии
        content = Regex.Replace(content, @"\(\*.*?\*\)", "", RegexOptions.Singleline);

        return content;
    }

    /// <summary>
    /// Удаляет условные директивы компиляции из текста uses секции
    /// </summary>
    private string RemoveConditionalDirectives(string content)
    {
        // Удаляем условные директивы: {$IFDEF}, {$IFNDEF}, {$ENDIF}, {$IFEND}, {$ELSE}, {$IFOPT}, {$ELSEIF}, и т.д.
        content = Regex.Replace(content, @"\{\s*\$(?:IFDEF|IFNDEF|IFEND|IFOPT|IF|ELSE|ELSEIF|ENDIF)\b[^}]*\}", "", RegexOptions.IgnoreCase);

        return content;
    }

    /// <summary>
    /// Разделяет код на секции interface и implementation
    /// </summary>
    private (string interfaceSection, string implementationSection) SplitSections(string content)
    {
        var interfaceMatch = Regex.Match(content, @"\binterface\b([\s\S]*?)(?:\bimplementation\b|$)", RegexOptions.IgnoreCase);
        var implementationMatch = Regex.Match(content, @"\bimplementation\b([\s\S]*?)(?:\bend\.|$)", RegexOptions.IgnoreCase);

        var interfaceSection = interfaceMatch.Success ? interfaceMatch.Groups[1].Value : string.Empty;
        var implementationSection = implementationMatch.Success ? implementationMatch.Groups[1].Value : string.Empty;

        // Для .dpr файлов может не быть секций, тогда парсим весь код
        if (string.IsNullOrEmpty(interfaceSection) && string.IsNullOrEmpty(implementationSection))
        {
            interfaceSection = content;
        }

        return (interfaceSection, implementationSection);
    }

    /// <summary>
    /// Извлекает список юнитов из uses секции
    /// </summary>
    /// <param name="section">Секция кода (interface или implementation)</param>
    /// <param name="evaluator">Evaluator для условных директив (null = старое поведение)</param>
    private List<string> ParseUses(string section, Core.ConditionalCompilationEvaluator? evaluator)
    {
        var units = new List<string>();
        var matches = UsesRegex.Matches(section);
        
        foreach (Match match in matches)
        {
            var usesContent = match.Groups[1].Value;

            // Обрабатываем условные директивы (если есть evaluator)
            if (evaluator != null)
            {
                usesContent = evaluator.ProcessConditionalDirectives(usesContent);
            }
            else
            {
                // Старое поведение: просто удаляем директивы
                usesContent = RemoveConditionalDirectives(usesContent);
            }

            // Разделяем по запятым и переносам строк (для поддержки условных директив)
            var unitNames = usesContent.Split(new[] { ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(u => u.Trim())
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .Select(u => {
                    // Убираем "in 'path'" если есть
                    var inMatch = Regex.Match(u, @"^([\w.]+)\s+in\s+", RegexOptions.IgnoreCase);
                    if (inMatch.Success)
                    {
                        return inMatch.Groups[1].Value;
                    }
                    return u;
                })
                .Where(u => !string.IsNullOrWhiteSpace(u));

            units.AddRange(unitNames);
        }

        return units.Distinct().ToList();
    }

    /// <summary>
    /// Извлекает список include файлов
    /// </summary>
    private List<string> ParseIncludes(string content)
    {
        var includes = new List<string>();
        var matches = IncludeRegex.Matches(content);

        foreach (Match match in matches)
        {
            var includePath = match.Groups[1].Value.Trim();
            includes.Add(includePath);
        }

        return includes.Distinct().ToList();
    }
}
