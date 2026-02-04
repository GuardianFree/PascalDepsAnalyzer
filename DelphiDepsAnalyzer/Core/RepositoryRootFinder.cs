namespace DelphiDepsAnalyzer.Core;

/// <summary>
/// Находит корень репозитория (папку с .git)
/// </summary>
public class RepositoryRootFinder
{
    /// <summary>
    /// Найти корень репозитория, начиная с указанной директории
    /// </summary>
    /// <param name="startPath">Начальная директория</param>
    /// <returns>Путь к корню репозитория или null если не найден</returns>
    public static string? FindRepositoryRoot(string startPath)
    {
        var directory = new DirectoryInfo(startPath);
        
        while (directory != null)
        {
            // Проверяем наличие папки .git
            var gitPath = Path.Combine(directory.FullName, ".git");
            if (Directory.Exists(gitPath) || File.Exists(gitPath)) // .git может быть файлом в submodules
            {
                return directory.FullName;
            }
            
            directory = directory.Parent;
        }
        
        return null;
    }
    
    /// <summary>
    /// Получить корень репозитория из переданного пути или автоматически найти
    /// </summary>
    /// <param name="repoRoot">Явно указанный корень репозитория (может быть null)</param>
    /// <param name="fallbackPath">Путь для поиска если repoRoot не указан</param>
    /// <returns>Путь к корню репозитория</returns>
    public static string GetRepositoryRoot(string? repoRoot, string fallbackPath)
    {
        // Если корень указан явно, используем его
        if (!string.IsNullOrEmpty(repoRoot))
        {
            if (!Directory.Exists(repoRoot))
            {
                throw new DirectoryNotFoundException($"Указанная директория не существует: {repoRoot}");
            }
            return Path.GetFullPath(repoRoot);
        }
        
        // Автоматический поиск .git папки
        var found = FindRepositoryRoot(fallbackPath);
        if (found != null)
        {
            return found;
        }
        
        throw new InvalidOperationException(
            "Не удалось найти корень репозитория (.git папку). " +
            "Укажите путь явно с помощью параметра --repo-root");
    }
}
