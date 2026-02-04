using System.Security.Cryptography;
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

    public DependencyCache(string projectDir)
    {
        _cacheDir = Path.Combine(projectDir, ".deps-cache");
        _cache = new Dictionary<string, CachedUnit>(StringComparer.OrdinalIgnoreCase);
        LoadCache();
    }

    /// <summary>
    /// Проверяет, есть ли актуальная запись в кэше
    /// </summary>
    public bool TryGetCachedUnit(string filePath, out DelphiUnit? unit)
    {
        unit = null;

        if (!File.Exists(filePath))
        {
            return false;
        }

        var fileHash = ComputeFileHash(filePath);
        var cacheKey = Path.GetFullPath(filePath);

        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            // Проверяем актуальность по хэшу
            if (cached.FileHash == fileHash)
            {
                unit = cached.Unit;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Добавляет юнит в кэш
    /// </summary>
    public void CacheUnit(string filePath, DelphiUnit unit)
    {
        if (!File.Exists(filePath))
        {
            return;
        }

        var fileHash = ComputeFileHash(filePath);
        var cacheKey = Path.GetFullPath(filePath);

        _cache[cacheKey] = new CachedUnit
        {
            FilePath = cacheKey,
            FileHash = fileHash,
            Unit = unit,
            CachedAt = DateTime.UtcNow
        };
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
