using System.Diagnostics;
using System.Text;

namespace DelphiDepsAnalyzer.Core;

/// <summary>
/// Анализатор изменений в git репозитории между коммитами
/// </summary>
public class GitChangesAnalyzer
{
    private readonly string _repoRoot;

    public GitChangesAnalyzer(string repoRoot)
    {
        _repoRoot = Path.GetFullPath(repoRoot);
        ValidateGitRepository();
    }

    /// <summary>
    /// Получить нормализованные пути измененных файлов между коммитами
    /// </summary>
    public HashSet<string> GetChangedFiles(string fromCommit, string toCommit)
    {
        ValidateCommit(fromCommit);
        ValidateCommit(toCommit);

        var changedFiles = ExecuteGitDiff(fromCommit, toCommit);

        // Нормализуем и добавляем в HashSet для быстрого поиска
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in changedFiles)
        {
            var normalized = NormalizePath(file);
            result.Add(normalized);
        }

        return result;
    }

    /// <summary>
    /// Выполнить git diff через Process
    /// </summary>
    private List<string> ExecuteGitDiff(string fromCommit, string toCommit)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = $"diff --name-only {fromCommit}..{toCommit}",
            WorkingDirectory = _repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var output = new List<string>();
        while (!process.StandardOutput.EndOfStream)
        {
            var line = process.StandardOutput.ReadLine();
            if (!string.IsNullOrWhiteSpace(line))
            {
                output.Add(line);
            }
        }

        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Git diff завершился с ошибкой (код {process.ExitCode}): {error}");
        }

        return output;
    }

    /// <summary>
    /// Нормализовать путь: lowercase, forward slashes, относительный от корня репо
    /// </summary>
    private string NormalizePath(string path)
    {
        // Git возвращает пути с forward slashes относительно корня репо
        // Нормализуем для case-insensitive сравнения
        return path.Replace('\\', '/').ToLowerInvariant();
    }

    /// <summary>
    /// Проверить, что директория является git репозиторием
    /// </summary>
    private void ValidateGitRepository()
    {
        var gitDir = Path.Combine(_repoRoot, ".git");
        if (!Directory.Exists(gitDir) && !File.Exists(gitDir))
        {
            throw new InvalidOperationException(
                $"Директория не является git-репозиторием: {_repoRoot}");
        }
    }

    /// <summary>
    /// Проверить валидность хэша коммита
    /// </summary>
    private void ValidateCommit(string commit)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = $"rev-parse --verify {commit}",
            WorkingDirectory = _repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            var error = process.StandardError.ReadToEnd();
            throw new ArgumentException(
                $"Неверный хэш коммита '{commit}': {error.Trim()}");
        }
    }
}
