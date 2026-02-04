using System.Diagnostics;

namespace DelphiDepsAnalyzer.Core;

/// <summary>
/// Собирает метрики производительности анализа
/// </summary>
public class PerformanceMetrics
{
    private readonly Stopwatch _totalStopwatch = new();
    private readonly Dictionary<string, TimeSpan> _operationTimes = new();
    private readonly Dictionary<string, int> _operationCounts = new();

    public void Start()
    {
        _totalStopwatch.Start();
    }

    public void Stop()
    {
        _totalStopwatch.Stop();
    }

    public TimeSpan TotalTime => _totalStopwatch.Elapsed;

    /// <summary>
    /// Измеряет время выполнения операции
    /// </summary>
    public T MeasureOperation<T>(string operationName, Func<T> operation)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            return operation();
        }
        finally
        {
            sw.Stop();
            RecordOperation(operationName, sw.Elapsed);
        }
    }

    /// <summary>
    /// Измеряет время выполнения операции без возвращаемого значения
    /// </summary>
    public void MeasureOperation(string operationName, Action operation)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            operation();
        }
        finally
        {
            sw.Stop();
            RecordOperation(operationName, sw.Elapsed);
        }
    }

    private void RecordOperation(string operationName, TimeSpan elapsed)
    {
        if (_operationTimes.ContainsKey(operationName))
        {
            _operationTimes[operationName] += elapsed;
            _operationCounts[operationName]++;
        }
        else
        {
            _operationTimes[operationName] = elapsed;
            _operationCounts[operationName] = 1;
        }
    }

    /// <summary>
    /// Выводит метрики производительности
    /// </summary>
    public void PrintReport()
    {
        Console.WriteLine("\n" + new string('=', 60));
        Console.WriteLine("МЕТРИКИ ПРОИЗВОДИТЕЛЬНОСТИ");
        Console.WriteLine(new string('=', 60));
        Console.WriteLine($"Общее время:      {TotalTime.TotalSeconds:F2}с");
        Console.WriteLine();

        if (_operationTimes.Any())
        {
            Console.WriteLine("Операции:");
            foreach (var op in _operationTimes.OrderByDescending(x => x.Value))
            {
                var count = _operationCounts[op.Key];
                var avg = op.Value.TotalMilliseconds / count;
                var percent = (op.Value.TotalMilliseconds / TotalTime.TotalMilliseconds) * 100;

                Console.WriteLine($"  {op.Key,-35} {op.Value.TotalMilliseconds,8:F1}мс  ({count,3}x, avg {avg,6:F1}мс, {percent,5:F1}%)");
            }
        }

        Console.WriteLine(new string('=', 60));
    }

    /// <summary>
    /// Получает метрики в виде словаря для JSON
    /// </summary>
    public Dictionary<string, object> GetMetricsData()
    {
        return new Dictionary<string, object>
        {
            ["totalTimeMs"] = TotalTime.TotalMilliseconds,
            ["operations"] = _operationTimes.Select(op => new
            {
                name = op.Key,
                totalMs = op.Value.TotalMilliseconds,
                count = _operationCounts[op.Key],
                avgMs = op.Value.TotalMilliseconds / _operationCounts[op.Key]
            }).ToList()
        };
    }
}
