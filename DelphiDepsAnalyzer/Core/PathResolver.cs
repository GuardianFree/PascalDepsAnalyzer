using System.Collections.Concurrent;

namespace DelphiDepsAnalyzer.Core;

/// <summary>
/// Разрешает пути к файлам юнитов на основе search paths
/// </summary>
public class PathResolver
{
    private readonly List<string> _searchPaths;
    private readonly string _projectRoot;
    private readonly HashSet<string> _allowedRoots;
    private readonly ConcurrentDictionary<string, string?> _pathCache; // Кэш разрешенных путей
    private readonly ConcurrentDictionary<string, List<string>> _fileIndex; // Индекс всех .pas файлов (unit name -> list of paths)
    private readonly ExternalUnitsConfig _externalUnitsConfig; // Конфигурация внешних юнитов
    private readonly object _indexLock = new(); // Для синхронизации при добавлении в индекс

    public PathResolver(List<string> searchPaths, string projectFilePath)
    {
        _searchPaths = searchPaths ?? new List<string>();
        _pathCache = new ConcurrentDictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        _fileIndex = new ConcurrentDictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        // Определяем корень проекта (директория .dproj файла)
        _projectRoot = Path.GetDirectoryName(Path.GetFullPath(projectFilePath)) ?? string.Empty;

        // Загружаем конфигурацию внешних юнитов
        _externalUnitsConfig = ExternalUnitsConfig.Load(_projectRoot);

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

                // Потокобезопасное добавление в индекс
                lock (_indexLock)
                {
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
    /// Выбирает лучшее совпадение из нескольких файлов с одинаковым именем.
    /// Приоритизация:
    /// 1. Файлы из search paths, которые идут первыми в списке
    /// 2. Файлы ближе к корню проекта
    /// 3. Файлы с меньшей глубиной вложенности
    /// </summary>
    private string SelectBestMatch(List<string> candidates)
    {
        if (candidates.Count == 1)
        {
            return candidates[0];
        }

        // Приоритет 1: Сортируем по порядку search paths
        var candidatesWithPriority = candidates
            .Select(path => new
            {
                Path = path,
                SearchPathIndex = GetSearchPathIndex(path),
                DistanceToProject = GetDistanceToProject(path),
                Depth = GetPathDepth(path)
            })
            .OrderBy(x => x.SearchPathIndex)           // Первый критерий: порядок в search paths
            .ThenBy(x => x.DistanceToProject)          // Второй: близость к проекту
            .ThenBy(x => x.Depth)                      // Третий: меньшая глубина вложенности
            .ToList();

        return candidatesWithPriority.First().Path;
    }

    /// <summary>
    /// Получает индекс search path, которому принадлежит файл
    /// </summary>
    private int GetSearchPathIndex(string filePath)
    {
        for (int i = 0; i < _searchPaths.Count; i++)
        {
            var fullSearchPath = Path.GetFullPath(_searchPaths[i]);
            if (filePath.StartsWith(fullSearchPath, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        // Если не найден ни в одном search path, даём низкий приоритет
        return int.MaxValue;
    }

    /// <summary>
    /// Вычисляет "расстояние" между файлом и корнем проекта (количество общих сегментов пути)
    /// </summary>
    private int GetDistanceToProject(string filePath)
    {
        var projectSegments = _projectRoot.Split(new[] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
        var fileSegments = filePath.Split(new[] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

        // Считаем количество общих сегментов с начала
        int commonSegments = 0;
        for (int i = 0; i < Math.Min(projectSegments.Length, fileSegments.Length); i++)
        {
            if (string.Equals(projectSegments[i], fileSegments[i], StringComparison.OrdinalIgnoreCase))
            {
                commonSegments++;
            }
            else
            {
                break;
            }
        }

        // Чем больше общих сегментов, тем меньше "расстояние"
        return Math.Abs(projectSegments.Length - commonSegments) + Math.Abs(fileSegments.Length - commonSegments);
    }

    /// <summary>
    /// Получает глубину вложенности пути (количество сегментов)
    /// </summary>
    private int GetPathDepth(string filePath)
    {
        return filePath.Split(new[] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries).Length;
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
            // Если один вариант - возвращаем сразу
            if (indexedPaths.Count == 1)
            {
                var result = indexedPaths[0];
                _pathCache[unitName] = result;
                return result;
            }

            // Если несколько вариантов (коллизия имён в монорепозитории),
            // используем приоритизацию по search paths
            var prioritizedResult = SelectBestMatch(indexedPaths);
            _pathCache[unitName] = prioritizedResult;
            return prioritizedResult;
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
    /// Проверяет, является ли юнит внешним (системным, библиотечным)
    /// </summary>
    public bool IsExternalUnit(string unitName)
    {
        return _externalUnitsConfig.IsExternalUnit(unitName);
    }
}
