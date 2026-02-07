using DelphiDepsAnalyzer.Parsers;
using Xunit;

namespace DelphiDepsAnalyzer.Tests;

/// <summary>
/// Интеграционные тесты для DelphiProjectParser
/// </summary>
public class DelphiProjectParserTests
{
    [Fact]
    public void Parse_WithDebugConfiguration_ParsesDefinesCorrectly()
    {
        // Arrange
        var parser = new DelphiProjectParser();
        var dprojPath = Path.Combine("TestData", "TestProject.dproj");
        
        // Создаем временный тестовый файл
        Directory.CreateDirectory("TestData");
        File.WriteAllText(dprojPath, @"<?xml version=""1.0"" encoding=""utf-8""?>
<Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
    <PropertyGroup>
        <ProjectGuid>{12345678-1234-1234-1234-123456789ABC}</ProjectGuid>
        <MainSource>TestProject.dpr</MainSource>
        <Config Condition=""'$(Config)'==''\\u"">Debug</Config>
    </PropertyGroup>
    <PropertyGroup Condition=""'$(Config)'=='Debug' or '$(Cfg_1)'!=''\\u"">
        <Cfg_1>true</Cfg_1>
        <DCC_Define>DEBUG;LOGGING;$(DCC_Define)</DCC_Define>
    </PropertyGroup>
    <PropertyGroup Condition=""'$(Config)'=='Release' or '$(Cfg_2)'!=''\\u"">
        <Cfg_2>true</Cfg_2>
        <DCC_Define>RELEASE;$(DCC_Define)</DCC_Define>
    </PropertyGroup>
    <PropertyGroup Condition=""'$(Base)'!=''\\u"">
        <DCC_UnitSearchPath>.\;$(DCC_UnitSearchPath)</DCC_UnitSearchPath>
    </PropertyGroup>
</Project>");

        try
        {
            // Act
            var project = parser.Parse(dprojPath, "Debug", "Win32");

            // Assert
            Assert.NotNull(project);
            Assert.Equal("Debug", project.Configuration);
            Assert.Equal("Win32", project.Platform);
            Assert.Contains("DEBUG", project.CompilationDefines);
            Assert.Contains("LOGGING", project.CompilationDefines);
            Assert.Contains("MSWINDOWS", project.CompilationDefines); // предопределенный для Win32
            Assert.Contains("WIN32", project.CompilationDefines); // предопределенный для Win32
            Assert.Contains("CONDITIONALEXPRESSIONS", project.CompilationDefines); // всегда
            Assert.DoesNotContain("RELEASE", project.CompilationDefines);
        }
        finally
        {
            // Cleanup
            if (File.Exists(dprojPath))
                File.Delete(dprojPath);
            if (Directory.Exists("TestData"))
                Directory.Delete("TestData", true);
        }
    }

    [Fact]
    public void Parse_WithReleaseConfiguration_ParsesDefinesCorrectly()
    {
        // Arrange
        var parser = new DelphiProjectParser();
        var dprojPath = Path.Combine("TestData", "TestProject.dproj");
        
        Directory.CreateDirectory("TestData");
        File.WriteAllText(dprojPath, @"<?xml version=""1.0"" encoding=""utf-8""?>
<Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
    <PropertyGroup>
        <ProjectGuid>{12345678-1234-1234-1234-123456789ABC}</ProjectGuid>
        <MainSource>TestProject.dpr</MainSource>
        <Config Condition=""'$(Config)'==''\\u"">Debug</Config>
    </PropertyGroup>
    <PropertyGroup Condition=""'$(Config)'=='Debug' or '$(Cfg_1)'!=''\\u"">
        <Cfg_1>true</Cfg_1>
        <DCC_Define>DEBUG;LOGGING;$(DCC_Define)</DCC_Define>
    </PropertyGroup>
    <PropertyGroup Condition=""'$(Config)'=='Release' or '$(Cfg_2)'!=''\\u"">
        <Cfg_2>true</Cfg_2>
        <DCC_Define>RELEASE;OPTIMIZE;$(DCC_Define)</DCC_Define>
    </PropertyGroup>
    <PropertyGroup Condition=""'$(Base)'!=''\\u"">
        <DCC_UnitSearchPath>.\;$(DCC_UnitSearchPath)</DCC_UnitSearchPath>
    </PropertyGroup>
</Project>");

        try
        {
            // Act
            var project = parser.Parse(dprojPath, "Release", "Win32");

            // Assert
            Assert.NotNull(project);
            Assert.Equal("Release", project.Configuration);
            Assert.Equal("Win32", project.Platform);
            Assert.Contains("RELEASE", project.CompilationDefines);
            Assert.Contains("OPTIMIZE", project.CompilationDefines);
            Assert.Contains("MSWINDOWS", project.CompilationDefines);
            Assert.Contains("WIN32", project.CompilationDefines);
            Assert.DoesNotContain("DEBUG", project.CompilationDefines);
            Assert.DoesNotContain("LOGGING", project.CompilationDefines);
        }
        finally
        {
            if (File.Exists(dprojPath))
                File.Delete(dprojPath);
            if (Directory.Exists("TestData"))
                Directory.Delete("TestData", true);
        }
    }

    [Fact]
    public void Parse_WithWin64Platform_IncludesWin64Defines()
    {
        // Arrange
        var parser = new DelphiProjectParser();
        var dprojPath = Path.Combine("TestData", "TestProject.dproj");
        
        Directory.CreateDirectory("TestData");
        File.WriteAllText(dprojPath, @"<?xml version=""1.0"" encoding=""utf-8""?>
<Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
    <PropertyGroup>
        <ProjectGuid>{12345678-1234-1234-1234-123456789ABC}</ProjectGuid>
        <MainSource>TestProject.dpr</MainSource>
    </PropertyGroup>
    <PropertyGroup Condition=""'$(Base)'!=''"">
        <DCC_UnitSearchPath>.\;$(DCC_UnitSearchPath)</DCC_UnitSearchPath>
    </PropertyGroup>
</Project>");

        try
        {
            // Act
            var project = parser.Parse(dprojPath, "Debug", "Win64");

            // Assert
            Assert.Contains("MSWINDOWS", project.CompilationDefines);
            Assert.Contains("WIN64", project.CompilationDefines);
            Assert.Contains("CPU64BITS", project.CompilationDefines);
            Assert.DoesNotContain("WIN32", project.CompilationDefines);
        }
        finally
        {
            if (File.Exists(dprojPath))
                File.Delete(dprojPath);
            if (Directory.Exists("TestData"))
                Directory.Delete("TestData", true);
        }
    }

    [Fact]
    public void Parse_WithOSXPlatform_IncludesMacOSDefines()
    {
        // Arrange
        var parser = new DelphiProjectParser();
        var dprojPath = Path.Combine("TestData", "TestProject.dproj");
        
        Directory.CreateDirectory("TestData");
        File.WriteAllText(dprojPath, @"<?xml version=""1.0"" encoding=""utf-8""?>
<Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
    <PropertyGroup>
        <ProjectGuid>{12345678-1234-1234-1234-123456789ABC}</ProjectGuid>
        <MainSource>TestProject.dpr</MainSource>
    </PropertyGroup>
</Project>");

        try
        {
            // Act
            var project = parser.Parse(dprojPath, "Debug", "OSX32");

            // Assert
            Assert.Contains("MACOS", project.CompilationDefines);
            Assert.Contains("POSIX", project.CompilationDefines);
            Assert.DoesNotContain("MSWINDOWS", project.CompilationDefines);
        }
        finally
        {
            if (File.Exists(dprojPath))
                File.Delete(dprojPath);
            if (Directory.Exists("TestData"))
                Directory.Delete("TestData", true);
        }
    }

    [Fact]
    public void Parse_RealTestProject_ParsesCorrectly()
    {
        // Arrange
        var parser = new DelphiProjectParser();
        var dprojPath = Path.Combine("..", "..", "..", "..", "TestConditionalProject", "ConditionalTest.dproj");

        // Проверяем, что тестовый проект существует
        if (!File.Exists(dprojPath))
        {
            // Skip если тестовый проект не найден
            return;
        }

        // Act
        var projectDebug = parser.Parse(dprojPath, "Debug", "Win32");
        var projectRelease = parser.Parse(dprojPath, "Release", "Win32");

        // Assert - Debug
        Assert.NotNull(projectDebug);
        Assert.Contains("DEBUG", projectDebug.CompilationDefines);
        Assert.Contains("LOGGING", projectDebug.CompilationDefines);
        Assert.DoesNotContain("RELEASE", projectDebug.CompilationDefines);

        // Assert - Release
        Assert.NotNull(projectRelease);
        Assert.Contains("RELEASE", projectRelease.CompilationDefines);
        Assert.DoesNotContain("DEBUG", projectRelease.CompilationDefines);
        Assert.DoesNotContain("LOGGING", projectRelease.CompilationDefines);
    }

    [Fact]
    public void Parse_CaseInsensitiveDefines_WorksCorrectly()
    {
        // Arrange
        var parser = new DelphiProjectParser();
        var dprojPath = Path.Combine("TestData", "TestProject.dproj");
        
        Directory.CreateDirectory("TestData");
        File.WriteAllText(dprojPath, @"<?xml version=""1.0"" encoding=""utf-8""?>
<Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
    <PropertyGroup>
        <ProjectGuid>{12345678-1234-1234-1234-123456789ABC}</ProjectGuid>
        <MainSource>TestProject.dpr</MainSource>
    </PropertyGroup>
    <PropertyGroup Condition=""'$(Config)'=='Debug'"">
        <DCC_Define>debug;Logging;$(DCC_Define)</DCC_Define>
    </PropertyGroup>
</Project>");

        try
        {
            // Act
            var project = parser.Parse(dprojPath, "Debug", "Win32");

            // Assert
            Assert.True(project.CompilationDefines.Contains("debug") || 
                       project.CompilationDefines.Contains("DEBUG"));
            Assert.True(project.CompilationDefines.Contains("Logging") || 
                       project.CompilationDefines.Contains("LOGGING"));
        }
        finally
        {
            if (File.Exists(dprojPath))
                File.Delete(dprojPath);
            if (Directory.Exists("TestData"))
                Directory.Delete("TestData", true);
        }
    }
}
