using DelphiDepsAnalyzer.Models;

namespace DelphiDepsAnalyzer.Output;

/// <summary>
/// Форматирует вывод файлов с путями относительно корня репозитория
/// </summary>
public class RelativePathOutputFormatter
{
    private readonly string _repositoryRoot;

    public RelativePathOutputFormatter(string repositoryRoot)
    {
        _repositoryRoot = Path.GetFullPath(repositoryRoot);
    }

    /// <summary>
    /// Конвертирует абсолютный путь в относительный от корня репозитория
    /// </summary>
    private string GetRelativePath(string absolutePath)
    {
        var fullPath = Path.GetFullPath(absolutePath);
        
        // Используем Path.GetRelativePath для кросс-платформенной совместимости
        var relativePath = Path.GetRelativePath(_repositoryRoot, fullPath);
        
        // Нормализуем разделители для единообразия (используем /)
        return relativePath.Replace('\\', '/');
    }

    /// <summary>
    /// Выводит список всех файлов проекта в консоль
    /// </summary>
    public void PrintFileList(DelphiProject project)
    {
        Console.WriteLine("Файлы проекта (относительно корня репозитория):");
        Console.WriteLine(new string('=', 60));
        
        var allFiles = CollectAllFiles(project);
        
        foreach (var file in allFiles.OrderBy(f => f))
        {
            Console.WriteLine(file);
        }
        
        Console.WriteLine(new string('=', 60));
        Console.WriteLine($"Всего файлов: {allFiles.Count}");
    }

    /// <summary>
    /// Собирает все файлы проекта и конвертирует их в относительные пути
    /// </summary>
    public List<string> CollectAllFiles(DelphiProject project)
    {
        var files = new HashSet<string>();
        
        // Добавляем .dproj файл
        if (!string.IsNullOrEmpty(project.ProjectFilePath))
        {
            files.Add(GetRelativePath(project.ProjectFilePath));
        }
        
        // Добавляем главный .dpr файл
        if (!string.IsNullOrEmpty(project.MainSourcePath))
        {
            files.Add(GetRelativePath(project.MainSourcePath));
        }
        
        // Добавляем все .pas файлы
        foreach (var unit in project.Units)
        {
            if (!string.IsNullOrEmpty(unit.FilePath))
            {
                files.Add(GetRelativePath(unit.FilePath));
            }
        }
        
        // Добавляем все .inc файлы
        foreach (var includeFile in project.IncludeFiles)
        {
            if (!string.IsNullOrEmpty(includeFile))
            {
                files.Add(GetRelativePath(includeFile));
            }
        }
        
        return files.ToList();
    }

    /// <summary>
    /// Сохраняет список файлов в текстовый файл
    /// </summary>
    public void SaveToFile(DelphiProject project, string outputPath)
    {
        var files = CollectAllFiles(project);
        File.WriteAllLines(outputPath, files.OrderBy(f => f));
        Console.WriteLine($"\nСписок файлов сохранён в: {outputPath}");
    }
}
