using Xunit;

namespace Voucha.Client.WindowsLanguageModelSurface.Tests;

public sealed class WindowsLanguageModelSurfaceProbeTests
{
  private readonly ITestOutputHelper output;

  public WindowsLanguageModelSurfaceProbeTests(ITestOutputHelper output) => this.output = output;

  [Fact]
  public void PinnedSurfaceAdmitsPromptLengthWithoutAnAnswerTokenApi()
  {
    var assets = Path.Combine(AppContext.BaseDirectory, "windows-language-model-surface.assets.json");
    var report = WindowsLanguageModelSurfaceCatalog.Read(assets);
    var json = report.ToJson();
    output.WriteLine(json);
    Console.WriteLine(json);
    var summary = Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY");
    if (!string.IsNullOrEmpty(summary))
      File.AppendAllText(summary, "### Windows language-model surface\n\n```json\n" + json + "\n```\n");

    Assert.Equal("1.8.260710003", report.WindowsAppSdkVersion);
    Assert.Equal("1.8.79", report.AiVersion);
    Assert.Equal("[1.8.79]", report.AiDependencyVersion);
    Assert.Equal(LanguageModelSignatures, report.LanguageModelSignatures);
    Assert.Equal(OptionProperties, report.OptionProperties);
    Assert.Equal(ResponseStatuses, report.ResponseStatuses);
    Assert.Equal(ContentFilterProperties, report.ContentFilterProperties);
    Assert.Equal(["(context, prompt)", "(prompt)"], report.UsablePromptLengthParameters);
    Assert.Equal(["()", "(systemPrompt)", "(systemPrompt, contentFilterOptions)"], report.CreateContextParameters);
    Assert.Equal(
        ["(messages, options, cutoffPosition)", "(text, cutoffPosition)"],
        report.SummarizerCutoffParameters);
    Assert.Empty(report.ForbiddenMembers);
    Assert.Equal(0, WindowsLanguageModelProjectionBindings.InvocationCount);
  }

  [Fact]
  public void CompiledProjectionIsNotInvoked()
  {
    Assert.Null(Environment.GetEnvironmentVariable("VOUCHA_PROBE_INVOKE_LANGUAGE_MODEL"));
    if (string.Equals(
        Environment.GetEnvironmentVariable("VOUCHA_PROBE_INVOKE_LANGUAGE_MODEL"),
        "1",
        StringComparison.Ordinal))
      WindowsLanguageModelProjectionBindings.Bind();
    Assert.Equal(0, WindowsLanguageModelProjectionBindings.InvocationCount);
  }

  private static readonly string[] LanguageModelSignatures =
  [
    "CreateAsync() -> IAsyncOperation<LanguageModel>",
    "CreateContext() -> LanguageModelContext",
    "CreateContext(String) -> LanguageModelContext",
    "CreateContext(String, ContentFilterOptions) -> LanguageModelContext",
    "Dispose() -> Void",
    "EnsureReadyAsync() -> IAsyncOperationWithProgress<AIFeatureReadyResult, Double>",
    "GenerateEmbeddingVectors(String) -> LanguageModelEmbeddingVectorResult",
    "GenerateEmbeddingVectors(String, ContentFilterOptions) -> LanguageModelEmbeddingVectorResult",
    "GenerateResponseAsync(LanguageModelContext, String, LanguageModelOptions) -> IAsyncOperationWithProgress<LanguageModelResponseResult, String>",
    "GenerateResponseAsync(String) -> IAsyncOperationWithProgress<LanguageModelResponseResult, String>",
    "GenerateResponseAsync(String, LanguageModelOptions) -> IAsyncOperationWithProgress<LanguageModelResponseResult, String>",
    "GenerateResponseFromEmbeddingsAsync(IEnumerable<EmbeddingVector>) -> IAsyncOperationWithProgress<LanguageModelResponseResult, String>",
    "GenerateResponseFromEmbeddingsAsync(IEnumerable<EmbeddingVector>, LanguageModelOptions) -> IAsyncOperationWithProgress<LanguageModelResponseResult, String>",
    "GenerateResponseFromEmbeddingsAsync(LanguageModelContext, IEnumerable<EmbeddingVector>, LanguageModelOptions) -> IAsyncOperationWithProgress<LanguageModelResponseResult, String>",
    "GetReadyState() -> AIFeatureReadyState",
    "GetUsablePromptLength(LanguageModelContext, String) -> UInt64",
    "GetUsablePromptLength(String) -> UInt64",
    "GetVectorSpaceId() -> Guid",
  ];

  private static readonly string[] OptionProperties =
  [
    "ContentFilterOptions -> ContentFilterOptions",
    "Temperature -> Single",
    "TopK -> UInt32",
    "TopP -> Single",
  ];

  private static readonly string[] ResponseStatuses =
  [
    "BlockedByPolicy",
    "Complete",
    "Error",
    "InProgress",
    "PromptBlockedByContentModeration",
    "PromptLargerThanContext",
    "ResponseBlockedByContentModeration",
  ];

  private static readonly string[] ContentFilterProperties =
  [
    "ImageMaxAllowedSeverityLevel -> ImageContentFilterSeverity",
    "PromptMaxAllowedSeverityLevel -> TextContentFilterSeverity",
    "ResponseMaxAllowedSeverityLevel -> TextContentFilterSeverity",
  ];
}
