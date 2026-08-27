using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Settings;

public sealed partial class SettingsViewModel
{
  private const string DeleteAccountConfirmationPhrase = "delete my account";

  private string deleteConfirmation = string.Empty;

  private Uri? dataRequestDownloadUrl;

  private bool canDownloadDataRequest;

  public string DeleteConfirmation
  {
    get => deleteConfirmation;
    set
    {
      if (SetProperty(ref deleteConfirmation, value ?? string.Empty))
      {
        OnPropertyChanged(nameof(CanDeleteAccount));
      }
    }
  }

  public bool CanDeleteAccount =>
      string.Equals(DeleteConfirmation.Trim(), DeleteAccountConfirmationPhrase, StringComparison.OrdinalIgnoreCase);

  public Uri? DataRequestDownloadUrl
  {
    get => dataRequestDownloadUrl;
    private set => SetProperty(ref dataRequestDownloadUrl, value);
  }

  public bool CanDownloadDataRequest
  {
    get => canDownloadDataRequest;
    private set => SetProperty(ref canDownloadDataRequest, value);
  }

  public void ReportDataRequestDownloadError(Exception exception)
  {
    ArgumentNullException.ThrowIfNull(exception);
    ErrorMessage = localization.Format(
        UiMessageKey.NativeDotnetCsharpFailedToOpenDownload,
        ("error", exception.Message));
  }

  private void UpdateDataRequestState()
  {
    if (DataRequest is null)
    {
      DataRequestDownloadUrl = null;
      CanDownloadDataRequest = false;
      LocalizedDataRequestStatus = localization.Localize(UiMessageKey.NativeDotnetSettingsNoExportRequest);
      return;
    }

    var isDownloadAvailable = string.Equals(DataRequest.Status, "ready", StringComparison.OrdinalIgnoreCase)
        && DataRequest.ExpiresAt is { } expiresAt
        && expiresAt > DateTimeOffset.UtcNow;
    DataRequestDownloadUrl = isDownloadAvailable ? DataRequest.DownloadUrl : null;
    CanDownloadDataRequest = DataRequestDownloadUrl is not null;
    LocalizedDataRequestStatus = localization.Format(
        UiMessageKey.NativeDotnetSettingsExportStatus,
        ("status", localization.Resolve(UiTaxonomy.DataRequestStatus(DataRequest.Status))),
        ("createdAt", localization.FormatDateTime(DataRequest.CreatedAt, TimeZoneInfo.Local)));
  }
}
