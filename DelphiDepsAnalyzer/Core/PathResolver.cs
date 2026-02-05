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
    private readonly Dictionary<string, List<string>> _fileIndex; // Индекс всех .pas файлов (unit name -> list of paths)

    public PathResolver(List<string> searchPaths, string projectFilePath)
    {
        _searchPaths = searchPaths ?? new List<string>();
        _pathCache = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        _fileIndex = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

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

        // Предварительная индексация всех .pas файлов для быстрого поиска
        BuildFileIndex();
    }

    /// <summary>
    /// Строит индекс всех .pas файлов в search paths для быстрого поиска
    /// </summary>
    private void BuildFileIndex()
    {
        var startTime = DateTime.Now;
        var indexedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var searchPath in _searchPaths)
        {
            try
            {
                if (!Directory.Exists(searchPath) || !IsPathAllowed(searchPath))
                {
                    continue;
                }

                // Рекурсивно индексируем все .pas файлы (до 5 уровней)
                IndexDirectory(searchPath, depth: 0, indexedPaths);
            }
            catch (Exception)
            {
                // Пропускаем недоступные пути
            }
        }

        var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
        Console.WriteLine($"Проиндексировано файлов: {indexedPaths.Count} за {elapsed:F0}мс");
    }

    /// <summary>
    /// Рекурсивно индексирует .pas файлы в директории
    /// </summary>
    private void IndexDirectory(string directory, int depth, HashSet<string> indexedPaths)
    {
        if (depth > 5 || !IsPathAllowed(directory))
        {
            return;
        }

        try
        {
            // Индексируем все .pas файлы в текущей директории
            var enumerationOptions = new EnumerationOptions
            {
                RecurseSubdirectories = false,
                MatchCasing = MatchCasing.CaseInsensitive,
                AttributesToSkip = FileAttributes.System | FileAttributes.Hidden
            };

            foreach (var filePath in Directory.EnumerateFiles(directory, "*.pas", enumerationOptions))
            {
                var fullPath = Path.GetFullPath(filePath);

                // Избегаем дублирования (если файл уже проиндексирован из другого search path)
                if (indexedPaths.Contains(fullPath))
                {
                    continue;
                }

                indexedPaths.Add(fullPath);

                var fileName = Path.GetFileNameWithoutExtension(filePath);

                if (!_fileIndex.ContainsKey(fileName))
                {
                    _fileIndex[fileName] = new List<string>();
                }

                _fileIndex[fileName].Add(fullPath);

                // Также индексируем qualified names (например, Vcl.Forms для Vcl\Forms.pas)
                var relativePath = GetRelativePathFromSearchPath(fullPath);
                if (!string.IsNullOrEmpty(relativePath))
                {
                    var qualifiedName = relativePath
                        .Replace(Path.DirectorySeparatorChar, '.')
                        .Replace(".pas", "", StringComparison.OrdinalIgnoreCase);

                    if (!_fileIndex.ContainsKey(qualifiedName))
                    {
                        _fileIndex[qualifiedName] = new List<string>();
                    }

                    if (!_fileIndex[qualifiedName].Contains(fullPath))
                    {
                        _fileIndex[qualifiedName].Add(fullPath);
                    }
                }
            }

            // Рекурсивно обрабатываем поддиректории
            foreach (var subDir in Directory.EnumerateDirectories(directory, "*", enumerationOptions))
            {
                IndexDirectory(subDir, depth + 1, indexedPaths);
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Пропускаем папки без доступа
        }
        catch (Exception)
        {
            // Пропускаем другие ошибки
        }
    }

    /// <summary>
    /// Получает относительный путь файла от search path
    /// </summary>
    private string GetRelativePathFromSearchPath(string fullPath)
    {
        foreach (var searchPath in _searchPaths)
        {
            var fullSearchPath = Path.GetFullPath(searchPath);
            if (fullPath.StartsWith(fullSearchPath, StringComparison.OrdinalIgnoreCase))
            {
                return fullPath.Substring(fullSearchPath.Length).TrimStart(Path.DirectorySeparatorChar);
            }
        }

        return string.Empty;
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

        // Сначала проверяем в индексе (быстро)
        if (_fileIndex.TryGetValue(unitName, out var indexedPaths) && indexedPaths.Count > 0)
        {
            // Если несколько вариантов, берём первый (можно улучшить приоритизацией)
            var result = indexedPaths[0];
            _pathCache[unitName] = result;
            return result;
        }

        // Если не нашли в индексе, пробуем прямой поиск (fallback для динамически созданных файлов)
        var relativePath = unitName.Replace('.', Path.DirectorySeparatorChar);

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
        }

        // Кэшируем null результат
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
    /// Проверяет, является ли юнит системным (RTL/VCL/FMX)
    /// </summary>
    public bool IsSystemUnit(string unitName)
    {
        var systemPrefixes = new[] { "System.", "Vcl.", "FMX.", "Data.", "Xml.", "Soap.", "Web." };
        return systemPrefixes.Any(prefix => unitName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }
}
