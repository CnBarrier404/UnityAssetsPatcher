using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using UnityAssetsPatcher.LocalizationGenerator;
using Xunit;

namespace UnityAssetsPatcher.Tests.LocalizationGenerator;

public sealed class LocalizedStringsGeneratorTests
{
    [Fact]
    public void Generate_FlatLocale_GeneratesProperties()
    {
        const string json =
            """
            {
              "Greeting_Title": "Hello",
              "Prompt_MessageFormat": "Welcome, {0}!"
            }
            """;

        GeneratorDriverRunResult result = Run(("en-US.json", json));

        GeneratorRunResult generatorResult = Assert.Single(result.Results);
        Assert.Empty(generatorResult.Diagnostics);
        GeneratedSourceResult source = Assert.Single(generatorResult.GeneratedSources);
        Assert.Contains("internal static string Greeting_Title", source.SourceText.ToString());
        Assert.Contains("internal static string Prompt_MessageFormat", source.SourceText.ToString());
    }

    [Fact]
    public void Generate_TrailingInvalidJson_DoesNotReportSyntaxDiagnostic()
    {
        const string json =
            """
            {
              "Greeting_Title": "Hello"
            } invalid trailing content
            """;

        GeneratorDriverRunResult result = Run(("en-US.json", json));

        GeneratorRunResult generatorResult = Assert.Single(result.Results);
        Assert.Empty(generatorResult.Diagnostics);
        Assert.Single(generatorResult.GeneratedSources);
    }

    [Fact]
    public void Generate_LocaleKeyMismatch_ReportsSemanticWarnings()
    {
        GeneratorDriverRunResult result = Run(
            ("en-US.json", "{\n  \"First_Key\": \"First\"\n}"),
            ("zh-CN.json", "{\n  \"Second_Key\": \"Second\"\n}"));

        ImmutableArray<Diagnostic> diagnostics = Assert.Single(result.Results).Diagnostics;
        Assert.Equal(["LOC004", "LOC005"], diagnostics.Select(diagnostic => diagnostic.Id).Order().ToArray());
        Assert.Single(Assert.Single(result.Results).GeneratedSources);
    }

    private static GeneratorDriverRunResult Run(params (string Path, string Content)[] files)
    {
        ImmutableArray<AdditionalText> additionalTexts = files
            .Select(file => (AdditionalText)new InMemoryAdditionalText(
                $"C:/Localization/JSON/{file.Path}",
                file.Content))
            .ToImmutableArray();
        CSharpCompilation compilation = CSharpCompilation.Create("GeneratorTests");
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [new LocalizedStringsGenerator().AsSourceGenerator()],
            additionalTexts: additionalTexts);

        driver = driver.RunGenerators(compilation);
        return driver.GetRunResult();
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

        public override SourceText GetText(CancellationToken cancellationToken = default) => _text;
    }
}
