using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record ResolveModerationReportBody(
    [property: JsonPropertyName("status")] ModerationReportResolution Status);

public sealed record ResolveCommunityModerationReportBody(
    [property: JsonPropertyName("status")] string Status);

public sealed record IssueAdminWarningRequest(
    [property: JsonPropertyName("userId")] string UserId,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("publicMessage")] string? PublicMessage,
    [property: JsonPropertyName("reportId")] string ReportId,
    [property: JsonPropertyName("resolveReport")] bool ResolveReport = true)
{
  public void Validate()
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(UserId);
    ArgumentException.ThrowIfNullOrWhiteSpace(Reason);
    ArgumentException.ThrowIfNullOrWhiteSpace(ReportId);
    if (Reason.Length > 1_000)
    {
      throw new ArgumentOutOfRangeException(nameof(Reason), "Warning reason must be 1,000 characters or fewer.");
    }
    if (PublicMessage?.Length > 2_000)
    {
      throw new ArgumentOutOfRangeException(nameof(PublicMessage), "Public message must be 2,000 characters or fewer.");
    }
  }
}
