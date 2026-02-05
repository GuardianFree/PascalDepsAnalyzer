namespace DelphiDepsAnalyzer.Models;

/// <summary>
/// Представляет запись о проекте из списка проектов для CI-анализа
/// </summary>
public record ProjectEntry
{
    /// <summary>
    /// Абсолютный путь к файлу проекта (.dproj)
    /// </summary>
    public required string ProjectPath { get; init; }

    /// <summary>
    /// Относительный путь от корня репозитория (для вывода)
    /// </summary>
    public required string RelativePath { get; init; }

    /// <summary>
    /// Директивы компиляции (опционально, например "FASTREPORT")
    /// </summary>
    public string? Directives { get; init; }

    /// <summary>
    /// Возвращает строку для записи в выходной файл
    /// </summary>
    public string GetOutputLine()
    {
        return string.IsNullOrEmpty(Directives)
            ? RelativePath
            : $"{RelativePath};{Directives}";
    }
}
