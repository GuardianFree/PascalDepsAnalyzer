namespace DelphiDepsAnalyzer.Core;

/// <summary>
/// Разрешает пути к файлам юнитов на основе search paths
/// </summary>
public class PathResolver
{
    private readonly List<string> _searchPaths;
    private readonly string _projectRoot;
    private readonly HashSet<string> _allowedRoots;
    private readonly Dictionary<string, string?> _pathCache; // Кэш разрешенных путей

    public PathResolver(List<string> searchPaths, string projectFilePath)
    {
        _searchPaths = searchPaths ?? new List<string>();
        _pathCache = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        // Определяем корень проекта (директория .dproj файла)
        _projectRoot = Path.GetDirectoryName(Path.GetFullPath(projectFilePath)) ?? string.Empty;

        // Находим корень репозитория (если есть .git)
        var repoRoot = FindRepositoryRoot(_projectRoot);

        // Разрешенные корневые директории для поиска:
        // 1. Корень репозитория (если найден)
        // 2. Корень проекта
        // 3. Все search paths из конфигурации
        _allowedRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrEmpty(repoRoot))
        {
            _allowedRoots.Add(repoRoot);
        }

        _allowedRoots.Add(_projectRoot);

        foreach (var searchPath in _searchPaths)
        {
            _allowedRoots.Add(Path.GetFullPath(searchPath));
        }
    }

    /// <summary>
    /// Находит корень git-репозитория, поднимаясь вверх по дереву директорий
    /// </summary>
    private string? FindRepositoryRoot(string startPath)
    {
        try
        {
            var currentDir = new DirectoryInfo(startPath);

            while (currentDir != null)
            {
                // Проверяем наличие .git папки
                if (Directory.Exists(Path.Combine(currentDir.FullName, ".git")))
                {
                    return currentDir.FullName;
                }

                currentDir = currentDir.Parent;
            }
        }
        catch
        {
            // Если не удалось найти, возвращаем null
        }

        return null;
    }

    /// <summary>
    /// Проверяет, что путь находится внутри одной из разрешенных директорий
    /// </summary>
    private bool IsPathAllowed(string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);

            foreach (var allowedRoot in _allowedRoots)
            {
                if (fullPath.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    /// <summary>
    /// Находит полный путь к .pas файлу по имени юнита
    /// </summary>
    public string? ResolveUnitPath(string unitName)
    {
        // Проверяем кэш
        if (_pathCache.TryGetValue(unitName, out var cachedPath))
        {
            return cachedPath;
        }

        // Обрабатываем qualified unit names (например, Vcl.Forms -> Vcl\Forms.pas)
        var relativePath = unitName.Replace('.', Path.DirectorySeparatorChar);

        // Ищем в каждом search path
        foreach (var searchPath in _searchPaths)
        {
            // Вариант 1: unitName.pas напрямую в search path
            var simplePath = Path.Combine(searchPath, $"{unitName}.pas");
            if (File.Exists(simplePath) && IsPathAllowed(simplePath))
            {
                var result = Path.GetFullPath(simplePath);
                _pathCache[unitName] = result;
                return result;
            }

            // Вариант 2: qualified name (например, Vcl\Forms.pas)
            var qualifiedPath = Path.Combine(searchPath, $"{relativePath}.pas");
            if (File.Exists(qualifiedPath) && IsPathAllowed(qualifiedPath))
            {
                var result = Path.GetFullPath(qualifiedPath);
                _pathCache[unitName] = result;
                return result;
            }

            // Вариант 3: рекурсивный поиск в подпапках search path (только вниз)
            var foundPath = SearchInDirectory(searchPath, unitName, depth: 0);
            if (foundPath != null)
            {
                _pathCache[unitName] = foundPath;
                return foundPath;
            }
        }

        // Кэшируем null результат, чтобы не искать повторно
        _pathCache[unitName] = null;
        return null;
    }

    /// <summary>
    /// Находит полный путь к include файлу
    /// </summary>
    public string? ResolveIncludePath(string includePath, string currentFilePath)
    {
        var currentDir = Path.GetDirectoryName(currentFilePath) ?? string.Empty;

        // Если путь относительный, сначала проверяем относительно текущего файла
        if (!Path.IsPathRooted(includePath))
        {
            var relativePath = Path.Combine(currentDir, includePath);
            if (File.Exists(relativePath))
            {
                return Path.GetFullPath(relativePath);
            }
        }

        // Проверяем абсолютный путь
        if (File.Exists(includePath))
        {
            return Path.GetFullPath(includePath);
        }

        // Ищем в search paths
        foreach (var searchPath in _searchPaths)
        {
            var fullPath = Path.Combine(searchPath, includePath);
            if (File.Exists(fullPath))
            {
                return Path.GetFullPath(fullPath);
            }
        }

        return null;
    }

    /// <summary>
    /// Рекурсивный поиск файла в директории (только вниз, до 5 уровней)
    /// </summary>
    private string? SearchInDirectory(string directory, string unitName, int depth = 0)
    {
        if (depth > 5) // Ограничиваем глубину поиска для производительности
        {
            return null;
        }

        // Проверяем, что директория находится в разрешенных путях
        if (!IsPathAllowed(directory))
        {
            return null;
        }

        try
        {
            // Проверяем в текущей директории
            var fileName = $"{unitName}.pas";
            var filePath = Path.Combine(directory, fileName);
            if (File.Exists(filePath) && IsPathAllowed(filePath))
            {
                return Path.GetFullPath(filePath);
            }

            // Ищем в поддиректориях
            foreach (var subDir in Directory.GetDirectories(directory))
            {
                var foundPath = SearchInDirectory(subDir, unitName, depth + 1);
                if (foundPath != null)
                {
                    return foundPath;
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Пропускаем папки без доступа
        }
        catch (Exception)
        {
            // Пропускаем другие ошибки доступа к файловой системе
        }

        return null;
    }


    /// <summary>
    /// Проверяет, является ли юнит системным (RTL/VCL/FMX)
    /// </summary>
    public bool IsSystemUnit(string unitName)
    {
        var systemPrefixes = new[] { "System.", "Vcl.", "FMX.", "Data.", "Xml.", "Soap.", "Web." };
        return systemPrefixes.Any(prefix => unitName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }
}
