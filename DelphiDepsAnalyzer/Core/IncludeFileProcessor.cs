using System.Text.RegularExpressions;

namespace DelphiDepsAnalyzer.Core;

/// <summary>
/// Обрабатывает include файлы и извлекает {$DEFINE} директивы
/// </summary>
public class IncludeFileProcessor
{
    private readonly PathResolver _pathResolver;
    private readonly ConditionalCompilationEvaluator _evaluator;
    private readonly HashSet<string> _processedFiles;

    public IncludeFileProcessor(PathResolver pathResolver, ConditionalCompilationEvaluator evaluator)
    {
        _pathResolver = pathResolver;
        _evaluator = evaluator;
        _processedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Обрабатывает include файл и обновляет evaluator дефайнами из него
    /// Предотвращает циклические includes через HashSet посещённых файлов
    /// </summary>
    public void ProcessIncludeFile(string includePath, string currentFilePath)
    {
        var resolvedPath = _pathResolver.ResolveIncludePath(includePath, currentFilePath);
        if (resolvedPath == null || !File.Exists(resolvedPath))
        {
            return;
        }

        // Предотвращение циклических includes
        var normalizedPath = Path.GetFullPath(resolvedPath);
        if (_processedFiles.Contains(normalizedPath))
        {
            return;
        }

        _processedFiles.Add(normalizedPath);

        try
        {
            var content = File.ReadAllText(resolvedPath);
            
            // Обрабатываем {$DEFINE}/{$UNDEF} с учётом условных блоков
            // ProcessDefineDirectives теперь учитывает {$IFDEF} и применяет
            // {$DEFINE}/{$UNDEF} только в активных блоках
            _evaluator.ProcessDefineDirectives(content);

            // Получаем только активный код для поиска вложенных {$I}
            var activeContent = _evaluator.ProcessConditionalDirectives(content);

            // Рекурсивно обрабатываем вложенные {$I} директивы только из активного кода
            var includeMatches = Regex.Matches(activeContent, @"\{\s*\$I(?:NCLUDE)?\s+['""]?([^}'""]+)['""]?\s*\}",
                                              RegexOptions.IgnoreCase);

            foreach (Match match in includeMatches)
            {
                var nestedIncludePath = match.Groups[1].Value.Trim();
                ProcessIncludeFile(nestedIncludePath, resolvedPath);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [WARNING] Ошибка обработки include файла {includePath}: {ex.Message}");
        }
    }
}
