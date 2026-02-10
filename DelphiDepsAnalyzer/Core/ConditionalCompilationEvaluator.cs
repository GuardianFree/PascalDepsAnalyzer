using System.Text;
using System.Text.RegularExpressions;

namespace DelphiDepsAnalyzer.Core;

/// <summary>
/// Вычисляет условные директивы компиляции Delphi
/// Поддерживает: {$IFDEF}, {$IFNDEF}, {$IF DEFINED()}, {$ELSE}, {$ELSEIF}, {$ENDIF}
/// </summary>
public class ConditionalCompilationEvaluator
{
    private readonly HashSet<string> _defines;
    private readonly Dictionary<string, double> _variables;
    private const int MaxNestingLevel = 200;

    public ConditionalCompilationEvaluator(HashSet<string> defines, Dictionary<string, double>? variables = null)
    {
        _defines = new HashSet<string>(defines, StringComparer.OrdinalIgnoreCase);
        _variables = variables != null
            ? new Dictionary<string, double>(variables, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Создаёт копию evaluator с теми же дефайнами (для потокобезопасности)
    /// </summary>
    public ConditionalCompilationEvaluator Clone()
    {
        return new ConditionalCompilationEvaluator(_defines, _variables);
    }

    /// <summary>
    /// Добавляет новый символ компиляции
    /// </summary>
    public void Define(string symbol)
    {
        _defines.Add(symbol);
    }

    /// <summary>
    /// Удаляет символ компиляции
    /// </summary>
    public void Undefine(string symbol)
    {
        _defines.Remove(symbol);
    }

    /// <summary>
    /// Проверяет, определён ли символ
    /// </summary>
    public bool IsDefined(string symbol)
    {
        return _defines.Contains(symbol);
    }

    /// <summary>
    /// Нормализует текст: выносит каждую директиву {$...} на отдельную строку.
    /// Это позволяет корректно обрабатывать inline директивы вроде:
    /// SysUtils, {$IFDEF DEBUG} DebugUtils, {$ENDIF} Classes
    /// </summary>
    private static string NormalizeDirectives(string content)
    {
        return Regex.Replace(content, @"(\{\s*\$(?:IFDEF|IFNDEF|IFEND|IFOPT|IF|ELSE|ELSEIF|ENDIF|DEFINE|UNDEF)\b[^}]*\})", "\n$1\n", RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Обрабатывает условные директивы в тексте uses секции.
    /// Возвращает только активные части кода.
    /// </summary>
    public string ProcessConditionalDirectives(string content)
    {
        content = NormalizeDirectives(content);
        var result = new StringBuilder();
        var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.None);
        var stack = new Stack<ConditionalBlock>();
        var currentBlock = new ConditionalBlock { IsActive = true, ParentActive = true };
        var nestingLevel = 0;
        var nestingWarningShown = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // {$IFDEF symbol}
            var ifdefMatch = Regex.Match(trimmed, @"^\{\s*\$IFDEF\s+(\w+)\s*\}", RegexOptions.IgnoreCase);
            if (ifdefMatch.Success)
            {
                nestingLevel++;
                if (nestingLevel > MaxNestingLevel)
                {
                    if (!nestingWarningShown)
                    {
                        Console.WriteLine($"  [WARNING] Слишком глубокая вложенность условных директив (>{MaxNestingLevel})");
                        nestingWarningShown = true;
                    }
                }

                var symbol = ifdefMatch.Groups[1].Value;
                stack.Push(currentBlock);

                var conditionMet = currentBlock.IsActive && IsDefined(symbol);
                currentBlock = new ConditionalBlock
                {
                    IsActive = conditionMet,
                    ParentActive = currentBlock.IsActive,
                    ElseAllowed = true,
                    AnyBranchWasActive = conditionMet
                };
                continue;
            }

            // {$IFNDEF symbol}
            var ifndefMatch = Regex.Match(trimmed, @"^\{\s*\$IFNDEF\s+(\w+)\s*\}", RegexOptions.IgnoreCase);
            if (ifndefMatch.Success)
            {
                nestingLevel++;
                if (nestingLevel > MaxNestingLevel)
                {
                    if (!nestingWarningShown)
                    {
                        Console.WriteLine($"  [WARNING] Слишком глубокая вложенность условных директив (>{MaxNestingLevel})");
                        nestingWarningShown = true;
                    }
                }

                var symbol = ifndefMatch.Groups[1].Value;
                stack.Push(currentBlock);

                var conditionMet = currentBlock.IsActive && !IsDefined(symbol);
                currentBlock = new ConditionalBlock
                {
                    IsActive = conditionMet,
                    ParentActive = currentBlock.IsActive,
                    ElseAllowed = true,
                    AnyBranchWasActive = conditionMet
                };
                continue;
            }

            // {$IFEND} - альтернативная закрывающая директива для {$IF} (аналог {$ENDIF})
            // ВАЖНО: проверяем ПЕРЕД {$IF}, иначе {$IFEND} матчится как {$IF END}
            if (Regex.IsMatch(trimmed, @"^\{\s*\$IFEND\b[^}]*\}", RegexOptions.IgnoreCase))
            {
                nestingLevel--;
                if (stack.Count > 0)
                {
                    currentBlock = stack.Pop();
                }
                continue;
            }

            // {$IF expression} - поддержка сложных выражений
            var ifMatch = Regex.Match(trimmed, @"^\{\s*\$IF\s+(.+?)\s*\}", RegexOptions.IgnoreCase);
            if (ifMatch.Success)
            {
                nestingLevel++;
                if (nestingLevel > MaxNestingLevel)
                {
                    if (!nestingWarningShown)
                    {
                        Console.WriteLine($"  [WARNING] Слишком глубокая вложенность условных директив (>{MaxNestingLevel})");
                        nestingWarningShown = true;
                    }
                }

                var expression = ifMatch.Groups[1].Value;
                stack.Push(currentBlock);

                var conditionMet = currentBlock.IsActive && EvaluateExpression(expression);
                currentBlock = new ConditionalBlock
                {
                    IsActive = conditionMet,
                    ParentActive = currentBlock.IsActive,
                    ElseAllowed = true,
                    AnyBranchWasActive = conditionMet
                };
                continue;
            }

            // {$ELSE}
            if (Regex.IsMatch(trimmed, @"^\{\s*\$ELSE\s*\}", RegexOptions.IgnoreCase))
            {
                if (currentBlock.ElseAllowed)
                {
                    currentBlock.IsActive = currentBlock.ParentActive && !currentBlock.AnyBranchWasActive;
                    currentBlock.ElseAllowed = false; // ELSE может быть только один раз
                }
                else
                {
                    Console.WriteLine($"  [WARNING] Множественные {{$ELSE}} в одном блоке игнорируются");
                }
                continue;
            }

            // {$ELSEIF expression} - поддержка сложных выражений
            var elseifMatch = Regex.Match(trimmed, @"^\{\s*\$ELSEIF\s+(.+?)\s*\}", RegexOptions.IgnoreCase);
            if (elseifMatch.Success)
            {
                var expression = elseifMatch.Groups[1].Value;
                if (currentBlock.ElseAllowed)
                {
                    if (currentBlock.AnyBranchWasActive)
                    {
                        // Уже была активная ветка — все последующие неактивны
                        currentBlock.IsActive = false;
                    }
                    else
                    {
                        // Проверяем новое условие
                        var conditionMet = currentBlock.ParentActive && EvaluateExpression(expression);
                        currentBlock.IsActive = conditionMet;
                        if (conditionMet)
                            currentBlock.AnyBranchWasActive = true;
                    }
                }
                continue;
            }

            // {$ENDIF} или {$ENDIF SYMBOL} — допускаем опциональный комментарий/символ после ENDIF
            if (Regex.IsMatch(trimmed, @"^\{\s*\$ENDIF\b[^}]*\}", RegexOptions.IgnoreCase))
            {
                nestingLevel--;
                if (stack.Count > 0)
                {
                    currentBlock = stack.Pop();
                }
                else
                {
                    Console.WriteLine($"  [WARNING] Незакрытые {{$ENDIF}} игнорируются");
                }
                continue;
            }

            // {$IFOPT X+} - опции компилятора, пока пропускаем (будущее расширение)
            if (Regex.IsMatch(trimmed, @"^\{\s*\$IFOPT\s+", RegexOptions.IgnoreCase))
            {
                // Для упрощения, считаем что IFOPT всегда true
                nestingLevel++;
                stack.Push(currentBlock);
                currentBlock = new ConditionalBlock
                {
                    IsActive = currentBlock.IsActive,
                    ParentActive = currentBlock.IsActive,
                    ElseAllowed = true,
                    AnyBranchWasActive = currentBlock.IsActive // true = считаем активной
                };
                continue;
            }

            // Добавляем строку только если текущий блок активен
            if (currentBlock.IsActive)
            {
                result.AppendLine(line);
            }
        }

        // Проверяем незакрытые блоки
        if (stack.Count > 0)
        {
            Console.WriteLine($"  [WARNING] Обнаружено {stack.Count} незакрытых блоков условной компиляции");
        }

        return result.ToString();
    }

    /// <summary>
    /// Извлекает {$DEFINE} и {$UNDEF} директивы из текста и обновляет внутреннее состояние.
    /// Учитывает условные блоки — {$DEFINE} внутри неактивного {$IFDEF} игнорируется.
    /// ВАЖНО: Оставляет {$IFDEF}/{$IFNDEF}/{$IF}/{$ELSE}/{$ENDIF} для последующей обработки в uses секции.
    /// Возвращает текст без директив {$DEFINE}/{$UNDEF}, но С условными директивами.
    /// </summary>
    public string ProcessDefineDirectives(string content)
    {
        content = NormalizeDirectives(content);
        var result = new StringBuilder();
        var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.None);
        var stack = new Stack<ConditionalBlock>();
        var currentBlock = new ConditionalBlock { IsActive = true, ParentActive = true };

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // --- Обработка условных директив (для отслеживания активности блоков) ---
            // ВАЖНО: эти директивы нужно ОСТАВИТЬ в коде для последующей обработки!

            // {$IFDEF symbol}
            var ifdefMatch = Regex.Match(trimmed, @"^\{\s*\$IFDEF\s+(\w+)\s*\}", RegexOptions.IgnoreCase);
            if (ifdefMatch.Success)
            {
                var symbol = ifdefMatch.Groups[1].Value;
                stack.Push(currentBlock);
                var conditionMet = currentBlock.IsActive && IsDefined(symbol);
                currentBlock = new ConditionalBlock
                {
                    IsActive = conditionMet,
                    ParentActive = currentBlock.IsActive,
                    ElseAllowed = true,
                    AnyBranchWasActive = conditionMet
                };
                // ОСТАВЛЯЕМ директиву в коде!
                result.AppendLine(line);
                continue;
            }

            // {$IFNDEF symbol}
            var ifndefMatch = Regex.Match(trimmed, @"^\{\s*\$IFNDEF\s+(\w+)\s*\}", RegexOptions.IgnoreCase);
            if (ifndefMatch.Success)
            {
                var symbol = ifndefMatch.Groups[1].Value;
                stack.Push(currentBlock);
                var conditionMet = currentBlock.IsActive && !IsDefined(symbol);
                currentBlock = new ConditionalBlock
                {
                    IsActive = conditionMet,
                    ParentActive = currentBlock.IsActive,
                    ElseAllowed = true,
                    AnyBranchWasActive = conditionMet
                };
                // ОСТАВЛЯЕМ директиву в коде!
                result.AppendLine(line);
                continue;
            }

            // {$IFEND} - альтернативная закрывающая директива для {$IF}
            // ВАЖНО: проверяем ПЕРЕД {$IF}, иначе {$IFEND} матчится как {$IF END}
            if (Regex.IsMatch(trimmed, @"^\{\s*\$IFEND\b[^}]*\}", RegexOptions.IgnoreCase))
            {
                if (stack.Count > 0)
                    currentBlock = stack.Pop();
                // ОСТАВЛЯЕМ директиву в коде (как ENDIF)!
                result.AppendLine(line);
                continue;
            }

            // {$IF expression}
            var ifMatch = Regex.Match(trimmed, @"^\{\s*\$IF\s+(.+?)\s*\}", RegexOptions.IgnoreCase);
            if (ifMatch.Success)
            {
                var expression = ifMatch.Groups[1].Value;
                stack.Push(currentBlock);
                var conditionMet = currentBlock.IsActive && EvaluateExpression(expression);
                currentBlock = new ConditionalBlock
                {
                    IsActive = conditionMet,
                    ParentActive = currentBlock.IsActive,
                    ElseAllowed = true,
                    AnyBranchWasActive = conditionMet
                };
                // ОСТАВЛЯЕМ директиву в коде!
                result.AppendLine(line);
                continue;
            }

            // {$ELSE}
            if (Regex.IsMatch(trimmed, @"^\{\s*\$ELSE\s*\}", RegexOptions.IgnoreCase))
            {
                if (currentBlock.ElseAllowed)
                {
                    currentBlock.IsActive = currentBlock.ParentActive && !currentBlock.AnyBranchWasActive;
                    currentBlock.ElseAllowed = false;
                }
                // ОСТАВЛЯЕМ директиву в коде!
                result.AppendLine(line);
                continue;
            }

            // {$ELSEIF expression}
            var elseifMatch = Regex.Match(trimmed, @"^\{\s*\$ELSEIF\s+(.+?)\s*\}", RegexOptions.IgnoreCase);
            if (elseifMatch.Success)
            {
                var expression = elseifMatch.Groups[1].Value;
                if (currentBlock.ElseAllowed)
                {
                    if (currentBlock.AnyBranchWasActive)
                    {
                        currentBlock.IsActive = false;
                    }
                    else
                    {
                        var conditionMet = currentBlock.ParentActive && EvaluateExpression(expression);
                        currentBlock.IsActive = conditionMet;
                        if (conditionMet)
                            currentBlock.AnyBranchWasActive = true;
                    }
                }
                // ОСТАВЛЯЕМ директиву в коде!
                result.AppendLine(line);
                continue;
            }

            // {$ENDIF} или {$ENDIF SYMBOL}
            if (Regex.IsMatch(trimmed, @"^\{\s*\$ENDIF\b[^}]*\}", RegexOptions.IgnoreCase))
            {
                if (stack.Count > 0)
                    currentBlock = stack.Pop();
                // ОСТАВЛЯЕМ директиву в коде!
                result.AppendLine(line);
                continue;
            }

            // {$IFOPT} — пропускаем, считаем true
            if (Regex.IsMatch(trimmed, @"^\{\s*\$IFOPT\s+", RegexOptions.IgnoreCase))
            {
                stack.Push(currentBlock);
                currentBlock = new ConditionalBlock
                {
                    IsActive = currentBlock.IsActive,
                    ParentActive = currentBlock.IsActive,
                    ElseAllowed = true,
                    AnyBranchWasActive = currentBlock.IsActive
                };
                // ОСТАВЛЯЕМ директиву в коде!
                result.AppendLine(line);
                continue;
            }

            // --- Обработка DEFINE/UNDEF только в активных блоках ---

            // {$DEFINE symbol}
            var defineMatch = Regex.Match(trimmed, @"^\{\s*\$DEFINE\s+(\w+)\s*\}", RegexOptions.IgnoreCase);
            if (defineMatch.Success)
            {
                if (currentBlock.IsActive)
                    Define(defineMatch.Groups[1].Value);
                continue;
            }

            // {$UNDEF symbol}
            var undefMatch = Regex.Match(trimmed, @"^\{\s*\$UNDEF\s+(\w+)\s*\}", RegexOptions.IgnoreCase);
            if (undefMatch.Success)
            {
                if (currentBlock.IsActive)
                    Undefine(undefMatch.Groups[1].Value);
                continue;
            }

            // Обычные строки — добавляем в результат (без фильтрации по IsActive,
            // т.к. фильтрация uses выполняется отдельно в ProcessConditionalDirectives)
            result.AppendLine(line);
        }

        return result.ToString();
    }

    /// <summary>
    /// Вычисляет сложное выражение условной компиляции
    /// Поддерживает: DEFINED(symbol), AND, OR, NOT, скобки
    /// Примеры: "DEFINED(A) AND DEFINED(B)", "DEFINED(A) OR DEFINED(B)", "NOT DEFINED(A)"
    /// </summary>
    public bool EvaluateExpression(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return false;

        expression = expression.Trim();

        // Обработка NOT оператора (префикс) — "NOT expr" или "NOT(expr)"
        if (expression.StartsWith("NOT ", StringComparison.OrdinalIgnoreCase))
        {
            var subExpression = expression.Substring(4).Trim();
            return !EvaluateExpression(subExpression);
        }
        if (expression.StartsWith("NOT(", StringComparison.OrdinalIgnoreCase))
        {
            var subExpression = expression.Substring(3).Trim();
            return !EvaluateExpression(subExpression);
        }

        // Снятие внешних скобок, если всё выражение обёрнуто в matching парные скобки
        // Например: (DEFINED(A) OR DEFINED(B)) → DEFINED(A) OR DEFINED(B)
        if (expression.StartsWith("(") && expression.EndsWith(")"))
        {
            int depth = 0;
            bool outerMatch = true;
            for (int i = 0; i < expression.Length; i++)
            {
                if (expression[i] == '(') depth++;
                else if (expression[i] == ')') depth--;
                if (depth == 0 && i < expression.Length - 1)
                {
                    outerMatch = false;
                    break;
                }
            }
            if (outerMatch)
            {
                return EvaluateExpression(expression.Substring(1, expression.Length - 2));
            }
        }

        // Обработка OR оператора (самый низкий приоритет)
        var orIndex = FindOperator(expression, "OR");
        if (orIndex >= 0)
        {
            var left = expression.Substring(0, orIndex).Trim();
            var right = expression.Substring(orIndex + 2).Trim();
            return EvaluateExpression(left) || EvaluateExpression(right);
        }

        // Обработка AND оператора
        var andIndex = FindOperator(expression, "AND");
        if (andIndex >= 0)
        {
            var left = expression.Substring(0, andIndex).Trim();
            var right = expression.Substring(andIndex + 3).Trim();
            return EvaluateExpression(left) && EvaluateExpression(right);
        }

        // Обработка DEFINED(symbol)
        var definedMatch = Regex.Match(expression, @"^DEFINED\s*\(\s*(\w+)\s*\)$", RegexOptions.IgnoreCase);
        if (definedMatch.Success)
        {
            var symbol = definedMatch.Groups[1].Value;
            return IsDefined(symbol);
        }

        // Обработка Declared(identifier) — проверяет видимость идентификатора.
        // В Delphi 12 (Unicode) большинство RTL-идентификаторов объявлены, поэтому возвращаем true.
        var declaredMatch = Regex.Match(expression, @"^DECLARED\s*\(\s*(\w+)\s*\)$", RegexOptions.IgnoreCase);
        if (declaredMatch.Success)
        {
            return true;
        }

        // Обработка литералов TRUE/FALSE (результат вычисления скобок)
        if (expression.Equals("TRUE", StringComparison.OrdinalIgnoreCase))
            return true;
        if (expression.Equals("FALSE", StringComparison.OrdinalIgnoreCase))
            return false;

        // Обработка числовых сравнений: CompilerVersion >= 31, RTLVersion >= 14.2
        var comparisonResult = EvaluateComparison(expression);
        if (comparisonResult.HasValue)
            return comparisonResult.Value;

        // Неизвестное выражение - предупреждение
        Console.WriteLine($"  [WARNING] Не удалось распарсить выражение: {expression}");
        return false;
    }

    /// <summary>
    /// Вычисляет выражение сравнения вида "CompilerVersion >= 31" или "RTLVersion >= 14.2"
    /// Возвращает null если выражение не является сравнением
    /// </summary>
    private bool? EvaluateComparison(string expression)
    {
        // Ищем оператор сравнения (>=, <=, <>, >, <, =)
        // Порядок важен: сначала двухсимвольные, потом односимвольные
        string[] operators = [">=", "<=", "<>", ">", "<", "="];
        string? foundOp = null;
        int opIndex = -1;

        foreach (var op in operators)
        {
            var idx = expression.IndexOf(op, StringComparison.Ordinal);
            if (idx > 0)
            {
                foundOp = op;
                opIndex = idx;
                break;
            }
        }

        if (foundOp == null)
            return null;

        var leftStr = expression.Substring(0, opIndex).Trim();
        var rightStr = expression.Substring(opIndex + foundOp.Length).Trim();

        // Разрешаем значения: переменная или числовой литерал
        if (!TryResolveNumericValue(leftStr, out var leftVal) || !TryResolveNumericValue(rightStr, out var rightVal))
            return null;

        return foundOp switch
        {
            ">=" => leftVal >= rightVal,
            "<=" => leftVal <= rightVal,
            "<>" => Math.Abs(leftVal - rightVal) > 0.001,
            ">" => leftVal > rightVal,
            "<" => leftVal < rightVal,
            "=" => Math.Abs(leftVal - rightVal) < 0.001,
            _ => null
        };
    }

    /// <summary>
    /// Разрешает имя переменной или числовой литерал в double значение
    /// </summary>
    private bool TryResolveNumericValue(string token, out double value)
    {
        // Hex-литерал Delphi: $0100, $FF и т.д.
        if (token.StartsWith('$') && int.TryParse(token.Substring(1),
            System.Globalization.NumberStyles.HexNumber,
            System.Globalization.CultureInfo.InvariantCulture, out var hexVal))
        {
            value = hexVal;
            return true;
        }

        // Числовой литерал (31, 14.2 и т.д.)
        if (double.TryParse(token, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        // SizeOf(Type) — размеры типов Delphi
        var sizeOfMatch = Regex.Match(token, @"^SizeOf\s*\(\s*(\w+)\s*\)$", RegexOptions.IgnoreCase);
        if (sizeOfMatch.Success)
        {
            var typeName = sizeOfMatch.Groups[1].Value;
            if (TryGetSizeOf(typeName, out value))
                return true;
        }

        // Затем как переменную компилятора
        if (_variables.TryGetValue(token, out value))
        {
            return true;
        }

        value = 0;
        return false;
    }

    /// <summary>
    /// Возвращает SizeOf для типов Delphi (Unicode, Delphi 2009+).
    /// Платформозависимые типы (Pointer, NativeInt) определяются по активным символам WIN64/WIN32.
    /// </summary>
    private bool TryGetSizeOf(string typeName, out double value)
    {
        bool is64Bit = IsDefined("WIN64") || IsDefined("CPUX64");
        int pointerSize = is64Bit ? 8 : 4;

        value = typeName.ToUpperInvariant() switch
        {
            "CHAR" or "WIDECHAR" => 2,
            "ANSICHAR" => 1,
            "BYTE" or "SHORTINT" or "BOOLEAN" or "BYTEBOOL" or "ANSISTRING" => 1,
            "WORD" or "SMALLINT" or "WORDBOOL" => 2,
            "INTEGER" or "CARDINAL" or "LONGINT" or "LONGWORD" or "LONGBOOL" or "SINGLE" => 4,
            "INT64" or "UINT64" or "DOUBLE" or "COMP" or "CURRENCY" => 8,
            "EXTENDED" => is64Bit ? 8 : 10,
            "POINTER" or "NATIVEINT" or "NATIVEUINT" => pointerSize,
            "STRING" or "UNICODESTRING" or "WIDESTRING" or "INTERFACE" or "TOBJECT" => pointerSize,
            _ => -1
        };

        return value >= 0;
    }

    /// <summary>
    /// Находит оператор (AND/OR) вне скобок и вне DEFINED()
    /// </summary>
    private int FindOperator(string expression, string op)
    {
        var parenDepth = 0;
        var i = 0;

        while (i < expression.Length)
        {
            if (expression[i] == '(')
            {
                parenDepth++;
            }
            else if (expression[i] == ')')
            {
                parenDepth--;
            }
            else if (parenDepth == 0)
            {
                // Проверяем оператор только вне скобок
                if (i + op.Length <= expression.Length)
                {
                    var substr = expression.Substring(i, op.Length);
                    if (substr.Equals(op, StringComparison.OrdinalIgnoreCase))
                    {
                        // Проверяем что это отдельное слово (не часть идентификатора)
                        var beforeOk = (i == 0 || !char.IsLetterOrDigit(expression[i - 1]));
                        var afterOk = (i + op.Length >= expression.Length || !char.IsLetterOrDigit(expression[i + op.Length]));

                        if (beforeOk && afterOk)
                        {
                            return i;
                        }
                    }
                }
            }

            i++;
        }

        return -1;
    }

    /// <summary>
    /// Вспомогательный класс для отслеживания вложенных условных блоков
    /// </summary>
    private class ConditionalBlock
    {
        /// <summary>
        /// Активен ли текущий блок (нужно ли включать код)
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Активен ли родительский блок (нужно для корректной работы ELSE)
        /// </summary>
        public bool ParentActive { get; set; }

        /// <summary>
        /// Разрешён ли ELSE для этого блока (предотвращает множественные ELSE)
        /// </summary>
        public bool ElseAllowed { get; set; }

        /// <summary>
        /// Была ли уже активна какая-либо ветка в цепочке IF/ELSEIF/ELSE.
        /// Предотвращает активацию нескольких веток в одной цепочке.
        /// </summary>
        public bool AnyBranchWasActive { get; set; }
    }
}
