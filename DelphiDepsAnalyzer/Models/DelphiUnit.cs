namespace DelphiDepsAnalyzer.Models;

/// <summary>
/// Представляет Delphi юнит (.pas или .dpr файл)
/// </summary>
public class DelphiUnit
{
    /// <summary>
    /// Полный путь к файлу юнита
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Имя юнита (без расширения)
    /// </summary>
    public string UnitName { get; set; } = string.Empty;

    /// <summary>
    /// Зависимости из секции interface uses
    /// </summary>
    public List<string> InterfaceUses { get; set; } = new();

    /// <summary>
    /// Зависимости из секции implementation uses
    /// </summary>
    public List<string> ImplementationUses { get; set; } = new();

    /// <summary>
    /// Include файлы ({$I} директивы)
    /// </summary>
    public List<string> IncludeFiles { get; set; } = new();

    /// <summary>
    /// Все зависимости (interface + implementation uses)
    /// </summary>
    public IEnumerable<string> AllDependencies =>
        InterfaceUses.Concat(ImplementationUses).Distinct();
}
