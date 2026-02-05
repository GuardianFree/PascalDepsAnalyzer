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
    /// Загружает конфигурацию из файла или создаёт дефолтную
    /// </summary>
    public static ExternalUnitsConfig Load(string projectDir)
    {
        var configPath = Path.Combine(projectDir, ".external-units.json");

        // Если файл существует, загружаем из него
        if (File.Exists(configPath))
        {
            try
            {
                var json = File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<ExternalUnitsConfig>(json);
                if (config != null)
                {
                    Console.WriteLine($"Загружена конфигурация внешних юнитов: {configPath}");
                    Console.WriteLine($"  Префиксов: {config.Prefixes.Count}, точных имён: {config.ExactNames.Count}");
                    return config;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка загрузки конфигурации {configPath}: {ex.Message}");
                Console.WriteLine("Используется дефолтная конфигурация.");
            }
        }

        // Создаём дефолтную конфигурацию
        var defaultConfig = CreateDefault();

        // Сохраняем для будущего использования
        try
        {
            var json = JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(configPath, json);
            Console.WriteLine($"Создан файл конфигурации: {configPath}");
            Console.WriteLine("Вы можете добавить в него префиксы и имена ваших внешних библиотек.");
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
