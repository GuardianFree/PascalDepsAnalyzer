using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DelphiDepsAnalyzer.Models;
using DelphiDepsAnalyzer.Output;

namespace DelphiDepsAnalyzer.Core;

/// <summary>
/// Кэш результатов анализа зависимостей
/// </summary>
public class DependencyCache
{
    private readonly string _cacheDir;
    private readonly Dictionary<string, CachedUnit> _cache;
    private readonly Dictionary<string, string> _hashCache; // Кэш хэшей в памяти для избежания повторного вычисления

    public DependencyCache(string projectDir)
    {
        _cacheDir = Path.Combine(projectDir, ".deps-cache");
        _cache = new Dictionary<string, CachedUnit>(StringComparer.OrdinalIgnoreCase);
        _hashCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        LoadCache();
    }

    /// <summary>
    /// Вычисляет стабильный хэш из набора defines (для ключа кэша)
    /// </summary>
    public static string ComputeDefinesHash(HashSet<string>? defines)
    {
        if (defines == null || defines.Count == 0)
            return "NO_CONDITIONALS";

        var sorted = defines.OrderBy(d => d, StringComparer.OrdinalIgnoreCase);
        var combined = string.Join(";", sorted);

        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(combined));
        return Convert.ToHexString(hashBytes);
    }

    /// <summary>
    /// Формирует составной ключ кэша: путь + хэш defines
    /// </summary>
    private static string MakeCacheKey(string filePath, HashSet<string>? activeDefines)
    {
        var definesHash = ComputeDefinesHash(activeDefines);
        return $"{Path.GetFullPath(filePath)}|{definesHash}";
    }

    /// <summary>
    /// Проверяет, есть ли актуальная запись в кэше
    /// </summary>
    public bool TryGetCachedUnit(string filePath, HashSet<string>? activeDefines, out DelphiUnit? unit)
    {
        unit = null;

        if (!File.Exists(filePath))
        {
            return false;
        }

        var cacheKey = MakeCacheKey(filePath, activeDefines);

        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            // Быстрая проверка: сначала время модификации + размер файла
            var fileInfo = new FileInfo(filePath);
            if (cached.LastModified == fileInfo.LastWriteTimeUtc && cached.FileSize == fileInfo.Length)
            {
                // Файл не изменился, используем кэш без вычисления хэша
                unit = cached.Unit;
                return true;
            }

            // Если время/размер изменились, делаем полную проверку по хэшу
            var fileHash = GetOrComputeFileHash(filePath);
            if (cached.FileHash == fileHash)
            {
                // Файл не изменился (совпадение по хэшу), обновляем метаданные
                cached.LastModified = fileInfo.LastWriteTimeUtc;
                cached.FileSize = fileInfo.Length;
                unit = cached.Unit;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Добавляет юнит в кэш
    /// </summary>
    public void CacheUnit(string filePath, HashSet<string>? activeDefines, DelphiUnit unit)
    {
        if (!File.Exists(filePath))
        {
            return;
        }

        var cacheKey = MakeCacheKey(filePath, activeDefines);
        var fileInfo = new FileInfo(filePath);

        // Используем уже вычисленный хэш из памяти или вычисляем новый
        var fileHash = GetOrComputeFileHash(filePath);

        _cache[cacheKey] = new CachedUnit
        {
            FilePath = Path.GetFullPath(filePath),
            FileHash = fileHash,
            DefinesHash = ComputeDefinesHash(activeDefines),
            LastModified = fileInfo.LastWriteTimeUtc,
            FileSize = fileInfo.Length,
            Unit = unit,
            CachedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Получает хэш из памяти или вычисляет его (с кэшированием)
    /// </summary>
    private string GetOrComputeFileHash(string filePath)
    {
        var cacheKey = Path.GetFullPath(filePath);

        if (_hashCache.TryGetValue(cacheKey, out var cachedHash))
        {
            return cachedHash;
        }

        var hash = ComputeFileHash(filePath);
        _hashCache[cacheKey] = hash;
        return hash;
    }

    /// <summary>
    /// Сохраняет кэш на диск
    /// </summary>
    public void SaveCache()
    {
        try
        {
            Directory.CreateDirectory(_cacheDir);
            var cacheFile = Path.Combine(_cacheDir, "cache.json");

            var json = JsonSerializer.Serialize(_cache,
                AnalysisOutputContext.Default.DictionaryStringCachedUnit);

            File.WriteAllText(cacheFile, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Предупреждение: Не удалось сохранить кэш: {ex.Message}");
        }
    }

    /// <summary>
    /// Загружает кэш с диска
    /// </summary>
    private void LoadCache()
    {
        try
        {
            var cacheFile = Path.Combine(_cacheDir, "cache.json");
            if (!File.Exists(cacheFile))
            {
                return;
            }

            var json = File.ReadAllText(cacheFile);
            var loaded = JsonSerializer.Deserialize(json,
                AnalysisOutputContext.Default.DictionaryStringCachedUnit);

            if (loaded != null)
            {
                foreach (var kvp in loaded)
                {
                    _cache[kvp.Key] = kvp.Value;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Предупреждение: Не удалось загрузить кэш: {ex.Message}");
        }
    }

    /// <summary>
    /// Вычисляет SHA256 хэш файла
    /// </summary>
    private static string ComputeFileHash(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(stream);
        return Convert.ToHexString(hashBytes);
    }

    /// <summary>
    /// Получает статистику кэша
    /// </summary>
    public CacheStatistics GetStatistics()
    {
        return new CacheStatistics
        {
            TotalEntries = _cache.Count,
            CacheHits = _cacheHits,
            CacheMisses = _cacheMisses
        };
    }

    private int _cacheHits;
    private int _cacheMisses;

    public void RecordHit() => _cacheHits++;
    public void RecordMiss() => _cacheMisses++;
}

/// <summary>
/// Запись в кэше
/// </summary>
public class CachedUnit
{
    public string FilePath { get; set; } = string.Empty;
    public string FileHash { get; set; } = string.Empty;
    public string DefinesHash { get; set; } = string.Empty;
    public DateTime LastModified { get; set; } // Быстрая проверка изменений
    public long FileSize { get; set; } // Быстрая проверка изменений
    public DelphiUnit Unit { get; set; } = new();
    public DateTime CachedAt { get; set; }
}

/// <summary>
/// Статистика кэша
/// </summary>
public class CacheStatistics
{
    public int TotalEntries { get; set; }
    public int CacheHits { get; set; }
    public int CacheMisses { get; set; }
    public double HitRate => CacheHits + CacheMisses > 0
        ? (double)CacheHits / (CacheHits + CacheMisses) * 100
        : 0;
}
