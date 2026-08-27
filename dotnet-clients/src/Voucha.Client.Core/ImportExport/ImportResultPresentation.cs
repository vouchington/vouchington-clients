using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.ImportExport;

public sealed record ImportResultPresentation(
    ImportResult Result,
    IUiLocalization Localization)
{
  public string UserInput => Result.Input;

  public string LocalizedDisplayStatus => Localization.Resolve(StatusText());

  public string? ExternalContentError => Result.Error;

  private UiText StatusText() => Result.Status switch
  {
    ImportResultStatus.Pending when !string.IsNullOrWhiteSpace(Result.Error) =>
        UiText.Localized(
            UiMessageKey.NativeSwiftImportExportRetrying,
            ("error", UiText.ExternalContent(Result.Error))),
    ImportResultStatus.Pending =>
        UiText.Localized(UiMessageKey.NativeSwiftImportExportPending),
    ImportResultStatus.Followed =>
        UiText.Localized(UiMessageKey.NativeSwiftImportExportFollowed),
    ImportResultStatus.Imported =>
        UiText.Localized(UiMessageKey.NativeSwiftImportExportImported),
    ImportResultStatus.SourceCreated =>
        UiText.Localized(UiMessageKey.NativeSwiftImportExportSourceCreated),
    ImportResultStatus.RecommendationCreated =>
        UiText.Localized(UiMessageKey.NativeSwiftImportExportRecommendationCreated),
    ImportResultStatus.AlreadyFollowing =>
        UiText.Localized(UiMessageKey.NativeSwiftImportExportAlreadyFollowing),
    _ => UiText.Localized(
        UiMessageKey.NativeSwiftImportExportFailed,
        ("error", UiText.ExternalContent(Result.Error))),
  };
}
