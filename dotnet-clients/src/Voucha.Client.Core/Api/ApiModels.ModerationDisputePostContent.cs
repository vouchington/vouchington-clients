using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record ModerationDisputePostContent
{
  [JsonConstructor]
  public ModerationDisputePostContent(
      string text,
      string? declaredLanguage = null,
      string? linguaRsDetectedLanguage = null)
  {
    ArgumentNullException.ThrowIfNull(text);
    Text = text;
    DeclaredLanguage = declaredLanguage;
    LinguaRsDetectedLanguage = linguaRsDetectedLanguage;
  }

  [JsonPropertyName("text")]
  public string Text { get; }

  [JsonPropertyName("declared_language")]
  public string? DeclaredLanguage { get; }

  [JsonPropertyName("lingua_rs_detected_language")]
  public string? LinguaRsDetectedLanguage { get; }
}
