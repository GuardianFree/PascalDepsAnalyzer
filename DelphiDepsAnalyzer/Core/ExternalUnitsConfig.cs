using System.Text.Json;
using System.Text.Json.Serialization;

namespace DelphiDepsAnalyzer.Core;

/// <summary>
/// Конфигурация внешних юнитов (системных, компонентных библиотек и т.д.)
/// которые находятся вне репозитория и не требуют анализа
/// </summary>
public class ExternalUnitsConfig
{
    /// <summary>
    /// Префиксы внешних юнитов (например, "System.", "Vcl.", "DevExpress.")
    /// </summary>
    [JsonPropertyName("prefixes")]
    public List<string> Prefixes { get; set; } = new();

    /// <summary>
    /// Точные имена внешних юнитов (например, "madExcept", "JclBase")
    /// </summary>
    [JsonPropertyName("exactNames")]
    public List<string> ExactNames { get; set; } = new();

    /// <summary>
    /// Загружает конфигурацию из файла или создаёт дефолтную.
    /// Приоритет поиска:
    /// 1. Директория исполняемого файла (единый конфиг для всех проектов)
    /// 2. Директория проекта (для обратной совместимости)
    /// </summary>
    public static ExternalUnitsConfig Load(string projectDir)
    {
        // Путь к директории исполняемого файла (работает для single-file приложений)
        var exeDir = AppContext.BaseDirectory;
        var exeConfigPath = Path.Combine(exeDir, ".external-units.json");

        // Путь к конфигу в директории проекта (для обратной совместимости)
        var projectConfigPath = Path.Combine(projectDir, ".external-units.json");

        // Приоритет 1: Ищем в директории исполняемого файла
        if (File.Exists(exeConfigPath))
        {
            try
            {
                var json = File.ReadAllText(exeConfigPath);
                var config = JsonSerializer.Deserialize<ExternalUnitsConfig>(json);
                if (config != null)
                {
                    Console.WriteLine($"Загружена конфигурация внешних юнитов: {exeConfigPath}");
                    Console.WriteLine($"  Префиксов: {config.Prefixes.Count}, точных имён: {config.ExactNames.Count}");
                    return config;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка загрузки конфигурации {exeConfigPath}: {ex.Message}");
            }
        }

        // Приоритет 2: Ищем в директории проекта (обратная совместимость)
        if (File.Exists(projectConfigPath))
        {
            try
            {
                var json = File.ReadAllText(projectConfigPath);
                var config = JsonSerializer.Deserialize<ExternalUnitsConfig>(json);
                if (config != null)
                {
                    Console.WriteLine($"Загружена конфигурация внешних юнитов: {projectConfigPath}");
                    Console.WriteLine($"  Префиксов: {config.Prefixes.Count}, точных имён: {config.ExactNames.Count}");
                    Console.WriteLine($"  [INFO] Рекомендуется переместить конфиг в директорию анализатора: {exeDir}");
                    return config;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка загрузки конфигурации {projectConfigPath}: {ex.Message}");
            }
        }

        // Создаём дефолтную конфигурацию
        var defaultConfig = CreateDefault();

        // Сохраняем в директории исполняемого файла (единый конфиг для всех проектов)
        try
        {
            var json = JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(exeConfigPath, json);
            Console.WriteLine($"Создан файл конфигурации: {exeConfigPath}");
            Console.WriteLine("Вы можете добавить в него префиксы и имена ваших внешних библиотек.");
            Console.WriteLine("Этот конфиг будет использоваться для всех проектов.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Не удалось создать файл конфигурации: {ex.Message}");
        }

        return defaultConfig;
    }

    /// <summary>
    /// Создаёт дефолтную конфигурацию со стандартными RTL/VCL/FMX префиксами
    /// </summary>
    private static ExternalUnitsConfig CreateDefault()
    {
        return new ExternalUnitsConfig
        {
            Prefixes = new List<string>
            {
                // Delphi RTL/VCL/FMX
                "System.",
                "Vcl.",
                "FMX.",
                "Data.",
                "Xml.",
                "Soap.",
                "Web.",
                "REST.",
                "Datasnap.",
                "WinApi.",
                "Posix.",
                "Androidapi.",
                "iOSapi.",
                "Macapi.",

                // Популярные сторонние библиотеки (можно расширить)
                "DevExpress.",
                "dxCore.",
                "cxGrid.",
                "Spring.",
                "DUnitX.",
            },
            ExactNames = new List<string>
            {
                // Примеры точных имён (пользователь может добавить свои)
                "madExcept",
                "JclBase",
                "JclStrings",
            }
        };
    }

    /// <summary>
    /// Проверяет, является ли юнит внешним (системным или из библиотек)
    /// </summary>
    public bool IsExternalUnit(string unitName)
    {
        // Проверяем точные совпадения
        if (ExactNames.Any(name => string.Equals(name, unitName, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        // Проверяем префиксы
        if (Prefixes.Any(prefix => unitName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
    }
}
