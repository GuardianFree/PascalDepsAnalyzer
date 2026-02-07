using DelphiDepsAnalyzer.Core;
using Xunit;

namespace DelphiDepsAnalyzer.Tests;

/// <summary>
/// Тесты для ConditionalCompilationEvaluator
/// </summary>
public class ConditionalCompilationEvaluatorTests
{
    #region Базовые директивы (IFDEF, IFNDEF, ELSE, ENDIF)

    [Fact]
    public void ProcessConditionalDirectives_IfdefDefined_IncludesContent()
    {
        // Arrange
        var defines = new HashSet<string> { "DEBUG" };
        var evaluator = new ConditionalCompilationEvaluator(defines);
        var content = @"
{$IFDEF DEBUG}
DebugUnit,
{$ENDIF}
CommonUnit";

        // Act
        var result = evaluator.ProcessConditionalDirectives(content);

        // Assert
        Assert.Contains("DebugUnit", result);
        Assert.Contains("CommonUnit", result);
    }

    [Fact]
    public void ProcessConditionalDirectives_IfdefNotDefined_ExcludesContent()
    {
        // Arrange
        var defines = new HashSet<string> { "RELEASE" };
        var evaluator = new ConditionalCompilationEvaluator(defines);
        var content = @"
{$IFDEF DEBUG}
DebugUnit,
{$ENDIF}
CommonUnit";

        // Act
        var result = evaluator.ProcessConditionalDirectives(content);

        // Assert
        Assert.DoesNotContain("DebugUnit", result);
        Assert.Contains("CommonUnit", result);
    }

    [Fact]
    public void ProcessConditionalDirectives_IfndefNotDefined_IncludesContent()
    {
        // Arrange
        var defines = new HashSet<string> { "RELEASE" };
        var evaluator = new ConditionalCompilationEvaluator(defines);
        var content = @"
{$IFNDEF DEBUG}
ProductionUnit,
{$ENDIF}
CommonUnit";

        // Act
        var result = evaluator.ProcessConditionalDirectives(content);

        // Assert
        Assert.Contains("ProductionUnit", result);
        Assert.Contains("CommonUnit", result);
    }

    [Fact]
    public void ProcessConditionalDirectives_IfndefDefined_ExcludesContent()
    {
        // Arrange
        var defines = new HashSet<string> { "DEBUG" };
        var evaluator = new ConditionalCompilationEvaluator(defines);
        var content = @"
{$IFNDEF DEBUG}
ProductionUnit,
{$ENDIF}
CommonUnit";

        // Act
        var result = evaluator.ProcessConditionalDirectives(content);

        // Assert
        Assert.DoesNotContain("ProductionUnit", result);
        Assert.Contains("CommonUnit", result);
    }

    [Fact]
    public void ProcessConditionalDirectives_ElseBlock_WorksCorrectly()
    {
        // Arrange
        var defines = new HashSet<string> { "DEBUG" };
        var evaluator = new ConditionalCompilationEvaluator(defines);
        var content = @"
{$IFDEF DEBUG}
DebugUnit,
{$ELSE}
ReleaseUnit,
{$ENDIF}
CommonUnit";

        // Act
        var result = evaluator.ProcessConditionalDirectives(content);

        // Assert
        Assert.Contains("DebugUnit", result);
        Assert.DoesNotContain("ReleaseUnit", result);
        Assert.Contains("CommonUnit", result);
    }

    [Fact]
    public void ProcessConditionalDirectives_ElseBlock_WhenConditionFalse_IncludesElse()
    {
        // Arrange
        var defines = new HashSet<string> { "RELEASE" };
        var evaluator = new ConditionalCompilationEvaluator(defines);
        var content = @"
{$IFDEF DEBUG}
DebugUnit,
{$ELSE}
ReleaseUnit,
{$ENDIF}
CommonUnit";

        // Act
        var result = evaluator.ProcessConditionalDirectives(content);

        // Assert
        Assert.DoesNotContain("DebugUnit", result);
        Assert.Contains("ReleaseUnit", result);
        Assert.Contains("CommonUnit", result);
    }

    #endregion

    #region Вложенные блоки

    [Fact]
    public void ProcessConditionalDirectives_NestedTwoLevels_WorksCorrectly()
    {
        // Arrange
        var defines = new HashSet<string> { "DEBUG", "LOGGING" };
        var evaluator = new ConditionalCompilationEvaluator(defines);
        var content = @"
{$IFDEF DEBUG}
DebugUnit,
{$IFDEF LOGGING}
VerboseUnit,
{$ELSE}
QuietUnit,
{$ENDIF}
{$ENDIF}
CommonUnit";

        // Act
        var result = evaluator.ProcessConditionalDirectives(content);

        // Assert
        Assert.Contains("DebugUnit", result);
        Assert.Contains("VerboseUnit", result);
        Assert.DoesNotContain("QuietUnit", result);
        Assert.Contains("CommonUnit", result);
    }

    [Fact]
    public void ProcessConditionalDirectives_NestedTwoLevels_InnerFalse()
    {
        // Arrange
        var defines = new HashSet<string> { "DEBUG" }; // LOGGING не определен
        var evaluator = new ConditionalCompilationEvaluator(defines);
        var content = @"
{$IFDEF DEBUG}
DebugUnit,
{$IFDEF LOGGING}
VerboseUnit,
{$ELSE}
QuietUnit,
{$ENDIF}
{$ENDIF}
CommonUnit";

        // Act
        var result = evaluator.ProcessConditionalDirectives(content);

        // Assert
        Assert.Contains("DebugUnit", result);
        Assert.DoesNotContain("VerboseUnit", result);
        Assert.Contains("QuietUnit", result);
        Assert.Contains("CommonUnit", result);
    }

    [Fact]
    public void ProcessConditionalDirectives_NestedFiveLevels_WorksCorrectly()
    {
        // Arrange
        var defines = new HashSet<string> { "L1", "L2", "L3", "L4", "L5" };
        var evaluator = new ConditionalCompilationEvaluator(defines);
        var content = @"
{$IFDEF L1}
{$IFDEF L2}
{$IFDEF L3}
{$IFDEF L4}
{$IFDEF L5}
DeepUnit,
{$ENDIF}
{$ENDIF}
{$ENDIF}
{$ENDIF}
{$ENDIF}
CommonUnit";

        // Act
        var result = evaluator.ProcessConditionalDirectives(content);

        // Assert
        Assert.Contains("DeepUnit", result);
        Assert.Contains("CommonUnit", result);
    }

    [Fact]
    public void ProcessConditionalDirectives_NestedFiveLevels_MissingOneLevel()
    {
        // Arrange
        var defines = new HashSet<string> { "L1", "L2", "L3", "L4" }; // L5 отсутствует
        var evaluator = new ConditionalCompilationEvaluator(defines);
        var content = @"
{$IFDEF L1}
{$IFDEF L2}
{$IFDEF L3}
{$IFDEF L4}
{$IFDEF L5}
DeepUnit,
{$ENDIF}
{$ENDIF}
{$ENDIF}
{$ENDIF}
{$ENDIF}
CommonUnit";

        // Act
        var result = evaluator.ProcessConditionalDirectives(content);

        // Assert
        Assert.DoesNotContain("DeepUnit", result);
        Assert.Contains("CommonUnit", result);
    }

    #endregion

    #region Сложные выражения (AND, OR, NOT)

    [Fact]
    public void ProcessConditionalDirectives_AndExpression_BothDefined()
    {
        // Arrange
        var defines = new HashSet<string> { "DEBUG", "LOGGING" };
        var evaluator = new ConditionalCompilationEvaluator(defines);
        var content = @"
{$IF DEFINED(DEBUG) AND DEFINED(LOGGING)}
VerboseDebugUnit,
{$ENDIF}
CommonUnit";

        // Act
        var result = evaluator.ProcessConditionalDirectives(content);

        // Assert
        Assert.Contains("VerboseDebugUnit", result);
        Assert.Contains("CommonUnit", result);
    }

    [Fact]
    public void ProcessConditionalDirectives_AndExpression_OnlyOneDefined()
    {
        // Arrange
        var defines = new HashSet<string> { "DEBUG" }; // LOGGING не определен
        var evaluator = new ConditionalCompilationEvaluator(defines);
        var content = @"
{$IF DEFINED(DEBUG) AND DEFINED(LOGGING)}
VerboseDebugUnit,
{$ENDIF}
CommonUnit";

        // Act
        var result = evaluator.ProcessConditionalDirectives(content);

        // Assert
        Assert.DoesNotContain("VerboseDebugUnit", result);
        Assert.Contains("CommonUnit", result);
    }

    [Fact]
    public void ProcessConditionalDirectives_OrExpression_OneDefined()
    {
        // Arrange
        var defines = new HashSet<string> { "RELEASE" }; // BETA не определен
        var evaluator = new ConditionalCompilationEvaluator(defines);
        var content = @"
{$IF DEFINED(RELEASE) OR DEFINED(BETA)}
ProductionUnit,
{$ENDIF}
CommonUnit";

        // Act
        var result = evaluator.ProcessConditionalDirectives(content);

        // Assert
        Assert.Contains("ProductionUnit", result);
        Assert.Contains("CommonUnit", result);
    }

    [Fact]
    public void ProcessConditionalDirectives_OrExpression_NeitherDefined()
    {
        // Arrange
        var defines = new HashSet<string> { "DEBUG" };
        var evaluator = new ConditionalCompilationEvaluator(defines);
        var content = @"
{$IF DEFINED(RELEASE) OR DEFINED(BETA)}
ProductionUnit,
{$ENDIF}
CommonUnit";

        // Act
        var result = evaluator.ProcessConditionalDirectives(content);

        // Assert
        Assert.DoesNotContain("ProductionUnit", result);
        Assert.Contains("CommonUnit", result);
    }

    [Fact]
    public void ProcessConditionalDirectives_NotExpression_NotDefined()
    {
        // Arrange
        var defines = new HashSet<string> { "DEBUG" }; // LOGGING не определен
        var evaluator = new ConditionalCompilationEvaluator(defines);
        var content = @"
{$IF NOT DEFINED(LOGGING)}
SilentUnit,
{$ENDIF}
CommonUnit";

        // Act
        var result = evaluator.ProcessConditionalDirectives(content);

        // Assert
        Assert.Contains("SilentUnit", result);
        Assert.Contains("CommonUnit", result);
    }

    [Fact]
    public void ProcessConditionalDirectives_NotExpression_IsDefined()
    {
        // Arrange
        var defines = new HashSet<string> { "DEBUG", "LOGGING" };
        var evaluator = new ConditionalCompilationEvaluator(defines);
        var content = @"
{$IF NOT DEFINED(LOGGING)}
SilentUnit,
{$ENDIF}
CommonUnit";

        // Act
        var result = evaluator.ProcessConditionalDirectives(content);

        // Assert
        Assert.DoesNotContain("SilentUnit", result);
        Assert.Contains("CommonUnit", result);
    }

    [Fact]
    public void ProcessConditionalDirectives_ComplexExpression_MultipleConditions()
    {
        // Arrange
        var defines = new HashSet<string> { "WIN32", "DEBUG" };
        var evaluator = new ConditionalCompilationEvaluator(defines);
        var content = @"
{$IF DEFINED(WIN32) AND DEFINED(DEBUG)}
Win32DebugUnit,
{$ENDIF}
CommonUnit";

        // Act
        var result = evaluator.ProcessConditionalDirectives(content);

        // Assert
        Assert.Contains("Win32DebugUnit", result);
        Assert.Contains("CommonUnit", result);
    }

    #endregion

    #region ELSEIF

    [Fact]
    public void ProcessConditionalDirectives_ElseIf_FirstConditionTrue()
    {
        // Arrange
        var defines = new HashSet<string> { "DEBUG" };
        var evaluator = new ConditionalCompilationEvaluator(defines);
        var content = @"
{$IF DEFINED(DEBUG)}
ElseIfFirstUnit,
{$ELSEIF DEFINED(RELEASE)}
ElseIfSecondUnit,
{$ELSE}
ElseIfDefaultUnit,
{$ENDIF}
CommonUnit";

        // Act
        var result = evaluator.ProcessConditionalDirectives(content);

        // Assert
        Assert.Contains("ElseIfFirstUnit", result);
        Assert.DoesNotContain("ElseIfSecondUnit", result);
        Assert.DoesNotContain("ElseIfDefaultUnit", result);
        Assert.Contains("CommonUnit", result);
    }

    [Fact]
    public void ProcessConditionalDirectives_ElseIf_SecondConditionTrue()
    {
        // Arrange
        var defines = new HashSet<string> { "RELEASE" };
        var evaluator = new ConditionalCompilationEvaluator(defines);
        var content = @"
{$IF DEFINED(DEBUG)}
ElseIfFirstUnit,
{$ELSEIF DEFINED(RELEASE)}
ElseIfSecondUnit,
{$ELSE}
ElseIfDefaultUnit,
{$ENDIF}
CommonUnit";

        // Act
        var result = evaluator.ProcessConditionalDirectives(content);

        // Assert
        Assert.DoesNotContain("ElseIfFirstUnit", result);
        Assert.Contains("ElseIfSecondUnit", result);
        Assert.DoesNotContain("ElseIfDefaultUnit", result);
        Assert.Contains("CommonUnit", result);
    }

    [Fact]
    public void ProcessConditionalDirectives_ElseIf_NoneTrue_FallsToElse()
    {
        // Arrange
        var defines = new HashSet<string> { "BETA" };
        var evaluator = new ConditionalCompilationEvaluator(defines);
        var content = @"
{$IF DEFINED(DEBUG)}
ElseIfFirstUnit,
{$ELSEIF DEFINED(RELEASE)}
ElseIfSecondUnit,
{$ELSE}
ElseIfDefaultUnit,
{$ENDIF}
CommonUnit";

        // Act
        var result = evaluator.ProcessConditionalDirectives(content);

        // Assert
        Assert.DoesNotContain("ElseIfFirstUnit", result);
        Assert.DoesNotContain("ElseIfSecondUnit", result);
        Assert.Contains("ElseIfDefaultUnit", result);
        Assert.Contains("CommonUnit", result);
    }

    #endregion

    #region DEFINE/UNDEF

    [Fact]
    public void ProcessDefineDirectives_DefinesSymbol()
    {
        // Arrange
        var defines = new HashSet<string>();
        var evaluator = new ConditionalCompilationEvaluator(defines);
        var content = @"
{$DEFINE TEST}
{$IFDEF TEST}
TestUnit,
{$ENDIF}
CommonUnit";

        // Act
        var processed = evaluator.ProcessDefineDirectives(content);
        var result = evaluator.ProcessConditionalDirectives(processed);

        // Assert
        Assert.Contains("TestUnit", result);
        Assert.Contains("CommonUnit", result);
    }

    [Fact]
    public void ProcessDefineDirectives_UndefinesSymbol()
    {
        // Arrange
        var defines = new HashSet<string> { "TEST" };
        var evaluator = new ConditionalCompilationEvaluator(defines);
        var content = @"
{$UNDEF TEST}
{$IFDEF TEST}
TestUnit,
{$ENDIF}
CommonUnit";

        // Act
        var processed = evaluator.ProcessDefineDirectives(content);
        var result = evaluator.ProcessConditionalDirectives(processed);

        // Assert
        Assert.DoesNotContain("TestUnit", result);
        Assert.Contains("CommonUnit", result);
    }

    [Fact]
    public void ProcessDefineDirectives_DefineInsideInactiveBlock_DoesNotDefine()
    {
        // Arrange
        var defines = new HashSet<string>(); // DEBUG не определен
        var evaluator = new ConditionalCompilationEvaluator(defines);
        var content = @"
{$IFDEF DEBUG}
{$DEFINE TEST}
{$ENDIF}
{$IFDEF TEST}
TestUnit,
{$ENDIF}
CommonUnit";

        // Act
        var processed = evaluator.ProcessDefineDirectives(content);
        var result = evaluator.ProcessConditionalDirectives(processed);

        // Assert
        Assert.DoesNotContain("TestUnit", result);
        Assert.Contains("CommonUnit", result);
    }

    [Fact]
    public void ProcessDefineDirectives_DefineInsideActiveBlock_Defines()
    {
        // Arrange
        var defines = new HashSet<string> { "DEBUG" };
        var evaluator = new ConditionalCompilationEvaluator(defines);
        var content = @"
{$IFDEF DEBUG}
{$DEFINE TEST}
{$ENDIF}
{$IFDEF TEST}
TestUnit,
{$ENDIF}
CommonUnit";

        // Act
        var processed = evaluator.ProcessDefineDirectives(content);
        var result = evaluator.ProcessConditionalDirectives(processed);

        // Assert
        Assert.Contains("TestUnit", result);
        Assert.Contains("CommonUnit", result);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ProcessConditionalDirectives_CaseInsensitive()
    {
        // Arrange
        var defines = new HashSet<string> { "debug" }; // lowercase
        var evaluator = new ConditionalCompilationEvaluator(defines);
        var content = @"
{$IFDEF DEBUG}
DebugUnit,
{$ENDIF}
CommonUnit";

        // Act
        var result = evaluator.ProcessConditionalDirectives(content);

        // Assert
        Assert.Contains("DebugUnit", result);
        Assert.Contains("CommonUnit", result);
    }

    [Fact]
    public void ProcessConditionalDirectives_InlineDirectives_ProcessedCorrectly()
    {
        // Arrange
        var defines = new HashSet<string> { "DEBUG" };
        var evaluator = new ConditionalCompilationEvaluator(defines);
        var content = "InlineUnitA, {$IFDEF DEBUG} InlineDebugUnit, {$ENDIF} InlineUnitB";

        // Act
        var result = evaluator.ProcessConditionalDirectives(content);

        // Assert
        Assert.Contains("InlineUnitA", result);
        Assert.Contains("InlineDebugUnit", result);
        Assert.Contains("InlineUnitB", result);
    }

    [Fact]
    public void ProcessConditionalDirectives_EmptyDefines_ExcludesAllConditionalContent()
    {
        // Arrange
        var defines = new HashSet<string>(); // пусто
        var evaluator = new ConditionalCompilationEvaluator(defines);
        var content = @"
{$IFDEF DEBUG}
DebugUnit,
{$ENDIF}
{$IFDEF RELEASE}
ReleaseUnit,
{$ENDIF}
CommonUnit";

        // Act
        var result = evaluator.ProcessConditionalDirectives(content);

        // Assert
        Assert.DoesNotContain("DebugUnit", result);
        Assert.DoesNotContain("ReleaseUnit", result);
        Assert.Contains("CommonUnit", result);
    }

    [Fact]
    public void IsDefined_ChecksCorrectly()
    {
        // Arrange
        var defines = new HashSet<string> { "DEBUG", "LOGGING" };
        var evaluator = new ConditionalCompilationEvaluator(defines);

        // Act & Assert
        Assert.True(evaluator.IsDefined("DEBUG"));
        Assert.True(evaluator.IsDefined("LOGGING"));
        Assert.False(evaluator.IsDefined("RELEASE"));
    }

    [Fact]
    public void Define_AddsSymbol()
    {
        // Arrange
        var defines = new HashSet<string>();
        var evaluator = new ConditionalCompilationEvaluator(defines);

        // Act
        evaluator.Define("TEST");

        // Assert
        Assert.True(evaluator.IsDefined("TEST"));
    }

    [Fact]
    public void Undefine_RemovesSymbol()
    {
        // Arrange
        var defines = new HashSet<string> { "TEST" };
        var evaluator = new ConditionalCompilationEvaluator(defines);

        // Act
        evaluator.Undefine("TEST");

        // Assert
        Assert.False(evaluator.IsDefined("TEST"));
    }

    [Fact]
    public void Clone_CreatesIndependentCopy()
    {
        // Arrange
        var defines = new HashSet<string> { "DEBUG" };
        var evaluator1 = new ConditionalCompilationEvaluator(defines);

        // Act
        var evaluator2 = evaluator1.Clone();
        evaluator2.Define("LOGGING");

        // Assert
        Assert.True(evaluator2.IsDefined("LOGGING"));
        Assert.False(evaluator1.IsDefined("LOGGING")); // оригинал не изменился
    }

    #endregion

    #region EvaluateExpression

    [Fact]
    public void EvaluateExpression_SimpleDefined_ReturnsTrue()
    {
        // Arrange
        var defines = new HashSet<string> { "DEBUG" };
        var evaluator = new ConditionalCompilationEvaluator(defines);

        // Act
        var result = evaluator.EvaluateExpression("DEFINED(DEBUG)");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void EvaluateExpression_SimpleDefined_ReturnsFalse()
    {
        // Arrange
        var defines = new HashSet<string>();
        var evaluator = new ConditionalCompilationEvaluator(defines);

        // Act
        var result = evaluator.EvaluateExpression("DEFINED(DEBUG)");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void EvaluateExpression_And_BothTrue()
    {
        // Arrange
        var defines = new HashSet<string> { "A", "B" };
        var evaluator = new ConditionalCompilationEvaluator(defines);

        // Act
        var result = evaluator.EvaluateExpression("DEFINED(A) AND DEFINED(B)");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void EvaluateExpression_And_OneFalse()
    {
        // Arrange
        var defines = new HashSet<string> { "A" };
        var evaluator = new ConditionalCompilationEvaluator(defines);

        // Act
        var result = evaluator.EvaluateExpression("DEFINED(A) AND DEFINED(B)");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void EvaluateExpression_Or_OneTrue()
    {
        // Arrange
        var defines = new HashSet<string> { "A" };
        var evaluator = new ConditionalCompilationEvaluator(defines);

        // Act
        var result = evaluator.EvaluateExpression("DEFINED(A) OR DEFINED(B)");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void EvaluateExpression_Or_BothFalse()
    {
        // Arrange
        var defines = new HashSet<string>();
        var evaluator = new ConditionalCompilationEvaluator(defines);

        // Act
        var result = evaluator.EvaluateExpression("DEFINED(A) OR DEFINED(B)");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void EvaluateExpression_Not_True()
    {
        // Arrange
        var defines = new HashSet<string>();
        var evaluator = new ConditionalCompilationEvaluator(defines);

        // Act
        var result = evaluator.EvaluateExpression("NOT DEFINED(A)");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void EvaluateExpression_Not_False()
    {
        // Arrange
        var defines = new HashSet<string> { "A" };
        var evaluator = new ConditionalCompilationEvaluator(defines);

        // Act
        var result = evaluator.EvaluateExpression("NOT DEFINED(A)");

        // Assert
        Assert.False(result);
    }

    #endregion
}
