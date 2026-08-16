using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace UnityAssetsPatcher.LocalizationGenerator.Tests;

public sealed class LocalizedStringsGeneratorTests
{
    [Fact]
    public void Generate_WhenLocalesAreValid_GeneratesCatalogAndTypedMembers()
    {
        const string primary =
            """
            {
              "Greeting": "Hello",
              "Welcome": "Welcome, {name}!",
              "Range": "{start} to {end}",
              "LiteralBraces": "Use {{braces}}",
              "Quoted": "Quote: \"{value}\""
            }
            """;

        const string secondary =
            """
            {
              "Greeting": "你好",
              "Welcome": "欢迎，{name}！",
              "Range": "{end} 到 {start}",
              "LiteralBraces": "使用 {{大括号}}",
              "Quoted": "引用：\"{value}\""
            }
            """;

        GeneratorTestResult result = Run(("en-US.json", primary), ("zh-CN.json", secondary));

        GeneratorRunResult generatorResult = Assert.Single(result.RunResult.Results);
        Assert.Empty(generatorResult.Diagnostics);
        GeneratedSourceResult generatedSource = Assert.Single(generatorResult.GeneratedSources);
        string source = generatedSource.SourceText.ToString();

        Assert.Contains("internal string Greeting =>", source);

        Assert.Contains("internal string Welcome(object? name)", source);

        Assert.Contains("\"Welcome, {0}!\"", source);

        Assert.Contains("\"{1} 到 {0}\"", source);

        Assert.Contains("\"Use {braces}\"", source);

        Assert.Contains("internal string Quoted(object? value)", source);

        Assert.Contains("\\\"{0}\\\"", source);

        Assert.DoesNotContain(
            result.OutputCompilation.GetDiagnostics(TestContext.Current.CancellationToken),
            diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void Generate_WhenJsonHasTrailingContent_DoesNotValidateJsonSyntax()
    {
        GeneratorTestResult result = Run(("en-US.json", "{ \"Greeting\": \"Hello\" } trailing"));

        GeneratorRunResult generatorResult = Assert.Single(result.RunResult.Results);

        Assert.Empty(generatorResult.Diagnostics);

        Assert.Single(generatorResult.GeneratedSources);
    }

    [Fact]
    public void Generate_WhenLocaleKeysDiffer_ReportsMissingAndExtraKeys()
    {
        GeneratorTestResult result = Run(
            ("en-US.json", "{ \"First\": \"First\" }"),
            ("zh-CN.json", "{ \"Second\": \"第二\" }"));

        var diagnostics = Assert.Single(result.RunResult.Results).Diagnostics;

        Assert.Equal(["LOC004", "LOC005"], diagnostics.Select(diagnostic => diagnostic.Id).Order().ToArray());

        Assert.All(diagnostics, diagnostic => Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity));
    }

    [Fact]
    public void Generate_WhenLocaleContainsDuplicateKey_ReportsDuplicateKey()
    {
        GeneratorTestResult result = Run(("en-US.json", "{ \"Greeting\": \"One\", \"Greeting\": \"Two\" }"));

        var diagnostics = Assert.Single(result.RunResult.Results).Diagnostics;

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "LOC006");
    }

    [Fact]
    public void Generate_WhenPlaceholdersDiffer_ReportsPlaceholderMismatch()
    {
        GeneratorTestResult result = Run(
            ("en-US.json", "{ \"Welcome\": \"Welcome, {name}!\" }"),
            ("zh-CN.json", "{ \"Welcome\": \"欢迎，{user}！\" }"));

        Diagnostic diagnostic = Assert.Single(Assert.Single(result.RunResult.Results).Diagnostics);

        Assert.Equal("LOC012", diagnostic.Id);
    }

    [Fact]
    public void Generate_WhenFormatIsInvalid_ReportsInvalidFormat()
    {
        GeneratorTestResult result = Run(("en-US.json", "{ \"Welcome\": \"Welcome, {name!\" }"));

        var diagnostics = Assert.Single(result.RunResult.Results).Diagnostics;

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "LOC009");
    }

    [Fact]
    public void Generate_WhenPlaceholderIsKeyword_ReportsInvalidFormat()
    {
        GeneratorTestResult result = Run(("en-US.json", "{ \"Message\": \"Value: {class}\" }"));

        Diagnostic diagnostic = Assert.Single(Assert.Single(result.RunResult.Results).Diagnostics);

        Assert.Equal("LOC009", diagnostic.Id);
    }

    [Fact]
    public void Generate_WhenCultureIsDefinedTwice_ReportsDuplicateLocale()
    {
        GeneratorTestResult result = Run(
            ("en-US.json", "{ \"Greeting\": \"Hello\" }"),
            ("en-us.json", "{ \"Greeting\": \"Hello again\" }"));

        var diagnostics = Assert.Single(result.RunResult.Results).Diagnostics;

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "LOC010");
    }

    [Fact]
    public void Generate_WhenPrimaryLocaleIsMissing_ReportsMissingPrimaryLocale()
    {
        GeneratorTestResult result = Run(("zh-CN.json", "{ \"Greeting\": \"你好\" }"));

        Diagnostic diagnostic = Assert.Single(Assert.Single(result.RunResult.Results).Diagnostics);

        Assert.Equal("LOC001", diagnostic.Id);
    }

    [Fact]
    public void Generate_WhenPrimaryLocaleIsEmpty_ReportsEmptyPrimaryLocale()
    {
        GeneratorTestResult result = Run(("en-US.json", "{}"));

        Diagnostic diagnostic = Assert.Single(Assert.Single(result.RunResult.Results).Diagnostics);

        Assert.Equal("LOC002", diagnostic.Id);
    }

    [Fact]
    public void Generate_WhenKeyIsNotValidIdentifier_ReportsInvalidKey()
    {
        GeneratorTestResult result = Run(("en-US.json", "{ \"invalid-key\": \"Value\" }"));

        Diagnostic diagnostic = Assert.Single(Assert.Single(result.RunResult.Results).Diagnostics);

        Assert.Equal("LOC007", diagnostic.Id);
    }

    [Fact]
    public void Generate_WhenLocaleFilenameIsNotCulture_ReportsInvalidCulture()
    {
        GeneratorTestResult result = Run(
            ("en-US.json", "{ \"Greeting\": \"Hello\" }"),
            ("not_a_culture.json", "{ \"Greeting\": \"Value\" }"));

        var diagnostics = Assert.Single(result.RunResult.Results).Diagnostics;

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "LOC014");
    }

    [Fact]
    public void Generate_WhenPrimaryLocaleCannotBeRead_ReportsUnreadableLocale()
    {
        ImmutableArray<AdditionalText> additionalTexts =
            [(AdditionalText)new UnreadableAdditionalText("C:/Localization/en-US.json")];

        GeneratorTestResult result = Run(additionalTexts);

        Diagnostic diagnostic = Assert.Single(Assert.Single(result.RunResult.Results).Diagnostics);

        Assert.Equal("LOC013", diagnostic.Id);
    }

    [Fact]
    public void GeneratedCatalog_WhenCultureChanges_UsesLocaleAndPrimaryFallback()
    {
        const string primary = "{ \"Greeting\": \"Hello\", \"Range\": \"{start} to {end}\" }";
        const string secondary = "{ \"Greeting\": \"你好\", \"Range\": \"{end} 到 {start}\" }";
        GeneratorTestResult result = Run(("en-US.json", primary), ("zh-CN.json", secondary));
        using MemoryStream assemblyStream = new();

        EmitResult emitResult = result.OutputCompilation.Emit(
            assemblyStream,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(emitResult.Success, string.Join(Environment.NewLine, emitResult.Diagnostics));

        Assembly assembly = Assembly.Load(assemblyStream.ToArray());
        Type localizedStringsType = assembly.GetRequiredType("UnityAssetsPatcher.Localization.LocalizedStrings");
        Assert.Null(assembly.GetType("UnityAssetsPatcher.Localization.LegacyLocalizedStrings"));
        object chineseStrings = CreateLocalizedStrings(localizedStringsType, "zh-CN");
        object fallbackStrings = CreateLocalizedStrings(localizedStringsType, "fr-FR");
        PropertyInfo greeting = localizedStringsType.GetRequiredProperty("Greeting");
        MethodInfo range = localizedStringsType.GetRequiredMethod("Range");

        Assert.Equal("你好", greeting.GetValue(chineseStrings));

        Assert.Equal("Hello", greeting.GetValue(fallbackStrings));

        Assert.Equal("结束 到 开始", range.Invoke(chineseStrings, ["开始", "结束"]));
    }

    private static GeneratorTestResult Run(params (string Path, string Content)[] files)
    {
        var additionalTexts = files
            .Select(file => (AdditionalText)new InMemoryAdditionalText(
                $"C:/Localization/{file.Path}",
                file.Content))
            .ToImmutableArray();

        return Run(additionalTexts);
    }

    private static GeneratorTestResult Run(ImmutableArray<AdditionalText> additionalTexts)
    {
        var compilation = CSharpCompilation.Create(
            $"GeneratorTests_{Guid.NewGuid():N}",
            references: GetFrameworkReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new LocalizedStringsGenerator().AsSourceGenerator()],
            additionalTexts,
            CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp14));

        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out Compilation outputCompilation,
            out _);

        GeneratorDriverRunResult runResult = driver.GetRunResult();

        return new GeneratorTestResult(runResult, outputCompilation);
    }

    private static MetadataReference[] GetFrameworkReferences()
    {
        string trustedPlatformAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ??
                                           throw new InvalidOperationException(
                                               "Trusted platform assemblies are unavailable.");

        return trustedPlatformAssemblies
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path))
            .ToArray();
    }

    private static object CreateLocalizedStrings(Type type, string cultureName)
    {
        object? instance = Activator.CreateInstance(
            type,
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            [CultureInfo.GetCultureInfo(cultureName)],
            null);

        return instance ?? throw new InvalidOperationException("Could not create generated LocalizedStrings instance.");
    }

    private sealed class InMemoryAdditionalText : AdditionalText
    {
        private readonly SourceText _text;

        public InMemoryAdditionalText(string path, string content)
        {
            Path = path;
            _text = SourceText.From(content);
        }

        public override string Path { get; }

        public override SourceText GetText(CancellationToken cancellationToken = default)
        {
            return _text;
        }
    }

    private sealed class UnreadableAdditionalText : AdditionalText
    {
        public UnreadableAdditionalText(string path)
        {
            Path = path;
        }

        public override string Path { get; }

        public override SourceText? GetText(CancellationToken cancellationToken = default)
        {
            return null;
        }
    }

    private sealed record GeneratorTestResult(GeneratorDriverRunResult RunResult, Compilation OutputCompilation);
}

internal static class ReflectionExtensions
{
    public static Type GetRequiredType(this Assembly assembly, string name)
    {
        return assembly.GetType(name) ?? throw new InvalidOperationException($"Type '{name}' was not found.");
    }

    public static PropertyInfo GetRequiredProperty(this Type type, string name)
    {
        return type.GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic) ??
               throw new InvalidOperationException($"Property '{name}' was not found.");
    }

    public static MethodInfo GetRequiredMethod(this Type type, string name)
    {
        return type.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic) ??
               throw new InvalidOperationException($"Method '{name}' was not found.");
    }
}
