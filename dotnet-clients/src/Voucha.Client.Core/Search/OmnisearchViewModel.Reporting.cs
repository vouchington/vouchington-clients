using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Search;

public readonly record struct HostnameReportTarget(string HostnameId);

public enum OmnisearchReportSubmissionState
{
  Idle,
  Submitting,
  Submitted,
}

public sealed partial class OmnisearchViewModel
{
  private HostnameReportTarget? selectedReportTarget;
  private OmnisearchReportSubmissionState reportSubmissionState;
  private int reportOperationGeneration;

  public bool HasSelectedReportTarget => selectedReportTarget is not null && ViewerIsAuthenticated;

  public bool CanReportSelectedHostname =>
      HasSelectedReportTarget && ReportSubmissionState == OmnisearchReportSubmissionState.Idle;

  public OmnisearchReportSubmissionState ReportSubmissionState
  {
    get => reportSubmissionState;
    private set
    {
      if (reportSubmissionState == value) return;
      reportSubmissionState = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(CanReportSelectedHostname));
      OnPropertyChanged(nameof(ReportButtonText));
    }
  }

  public UiText ReportButtonText => UiText.Localized(ReportSubmissionState switch
  {
    OmnisearchReportSubmissionState.Submitting =>
        UiMessageKey.NativeSwiftModerationReportsReporting,
    OmnisearchReportSubmissionState.Submitted =>
        UiMessageKey.NativeSwiftModerationReportsReported,
    _ => UiMessageKey.NativeSwiftModerationReportsReport,
  });

  public async Task<bool> ReportSelectedHostnameAsync(
      string reason,
      string? note,
      string turnstileToken,
      CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(reason);
    ArgumentException.ThrowIfNullOrWhiteSpace(turnstileToken);
    if (!CanReportSelectedHostname || selectedReportTarget is not { } target) return false;

    var operationGeneration = ++reportOperationGeneration;
    ReportSubmissionState = OmnisearchReportSubmissionState.Submitting;
    try
    {
      await client.ReportAsync(
          new ReportBody("url_hostname", target.HostnameId, reason, TrimmedOrNull(note), turnstileToken),
          cancellationToken).ConfigureAwait(true);
      if (IsCurrentReportOperation(operationGeneration, target))
      {
        ReportSubmissionState = OmnisearchReportSubmissionState.Submitted;
        return true;
      }

      return false;
    }
    catch
    {
      if (IsCurrentReportOperation(operationGeneration, target))
      {
        ReportSubmissionState = OmnisearchReportSubmissionState.Idle;
      }
      throw;
    }
  }

  private void SetReportTarget(Hostname? hostname) =>
      selectedReportTarget = hostname is { Blocked: not true }
          ? new HostnameReportTarget(hostname.Id)
          : null;

  private void ResetReportState()
  {
    reportOperationGeneration++;
    selectedReportTarget = null;
    ReportSubmissionState = OmnisearchReportSubmissionState.Idle;
  }

  private bool IsCurrentReportOperation(int operationGeneration, HostnameReportTarget target) =>
      reportOperationGeneration == operationGeneration && selectedReportTarget == target;

  private static string? TrimmedOrNull(string? value) =>
      string.IsNullOrWhiteSpace(value) ? null : value.Trim();

  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "Reporting enrichment must not prevent crawl content from loading.")]
  private async Task<Hostname?> FetchReportHostnameAsync(
      string urlId,
      CancellationToken cancellationToken)
  {
    try
    {
      var response = await client.FetchUrlAsync(urlId, cancellationToken).ConfigureAwait(true);
      return response.Url?.Hostname;
    }
    catch (Exception) when (!cancellationToken.IsCancellationRequested)
    {
      return null;
    }
  }
}
