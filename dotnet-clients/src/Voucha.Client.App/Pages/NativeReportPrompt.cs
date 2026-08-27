using Voucha.Client.App;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

internal static class NativeReportPrompt
{
  private static readonly IReadOnlyList<UiProtocolOptionDefinition> ReportReasons =
  [
    new("spam", UiMessageKey.NativeDotnetCsharpDialogsSpam),
    new("harassment", UiMessageKey.NativeDotnetCsharpDialogsHarassment),
    new("illegal_content", UiMessageKey.NativeDotnetCsharpDialogsIllegalContent),
    new("misinformation", UiMessageKey.NativeDotnetCsharpDialogsMisinformation),
    new("other", UiMessageKey.NativeDotnetCsharpDialogsOther),
  ];

  public static async Task ShowAsync(
      Page page,
      UiText reportTitle,
      ITurnstileTokenProvider turnstileTokenProvider,
      Func<string, string?, string, Task<NativeReportSubmissionResult>> submit)
  {
    var reason = await SelectReasonAsync(page, reportTitle).ConfigureAwait(true);
    if (reason is null) return;

    var note = await page.DisplayPromptAsync(
        UiCopy.Resolve(reportTitle),
        UiCopy.Localize(UiMessageKey.NativeDotnetCsharpDialogsAddContext),
        accept: UiCopy.Localize(UiMessageKey.NativeDotnetCsharpDialogsContinue),
        cancel: UiCopy.Localize(UiMessageKey.CommonCancel),
        placeholder: UiCopy.Localize(UiMessageKey.NativeDotnetCsharpDialogsOptionalNote),
        maxLength: 1000).ConfigureAwait(true);
    if (note is null) return;
    note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

    while (true)
    {
      string token;
      try
      {
        token = await turnstileTokenProvider.GetTokenAsync().ConfigureAwait(true);
      }
      catch (OperationCanceledException)
      {
        return;
      }
      catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or VouchaApiException)
      {
        if (await ShouldRetryAsync(page, UiText.ExternalContent(ex.Message)).ConfigureAwait(true))
        {
          continue;
        }
        return;
      }

      NativeReportSubmissionResult result;
      try
      {
        result = await submit(reason, note, token).ConfigureAwait(true);
      }
      catch (OperationCanceledException)
      {
        return;
      }
      catch (Exception ex) when (ex is HttpRequestException or VouchaApiException)
      {
        if (await ShouldRetryAsync(page, UiText.ExternalContent(ex.Message)).ConfigureAwait(true))
        {
          continue;
        }
        return;
      }

      if (result is NativeReportSubmissionResult.Failed failure)
      {
        if (await ShouldRetryAsync(page, failure.Message).ConfigureAwait(true)) continue;
        return;
      }

      if (result is NativeReportSubmissionResult.Unavailable unavailable)
      {
        await page.DisplayAlertAsync(
            UiCopy.Localize(UiMessageKey.NativeDotnetCsharpActionFailed),
            UiCopy.Resolve(unavailable.Message),
            UiCopy.Localize(UiMessageKey.NativeDotnetCsharpOk)).ConfigureAwait(true);
        return;
      }

      await page.DisplayAlertAsync(
          UiCopy.Localize(UiMessageKey.NativeSwiftModerationReportsReportSubmitted),
          UiCopy.Localize(UiMessageKey.NativeSwiftModerationReportsReportSubmittedMessage),
          UiCopy.Localize(UiMessageKey.NativeDotnetCsharpOk)).ConfigureAwait(true);
      return;
    }
  }

  private static Task<bool> ShouldRetryAsync(Page page, UiText message) =>
      page.DisplayAlertAsync(
          UiCopy.Localize(UiMessageKey.NativeDotnetCsharpActionFailed),
          UiCopy.Resolve(message),
          UiCopy.Localize(UiMessageKey.NativeCommonRetry),
          UiCopy.Localize(UiMessageKey.CommonCancel));

  private static async Task<string?> SelectReasonAsync(Page page, UiText reportTitle)
  {
    var reasons = ReportReasons
        .Select(definition => UiProtocolOption.From(definition, UiCopy.CurrentLocalization))
        .ToArray();
    var action = await page.DisplayActionSheetAsync(
        UiCopy.Resolve(reportTitle),
        UiCopy.Localize(UiMessageKey.CommonCancel),
        null,
        reasons.Select(reason => reason.DisplayLabel).ToArray()).ConfigureAwait(true);
    return reasons.FirstOrDefault(reason => reason.DisplayLabel == action)?.ProtocolValue;
  }
}

internal abstract class NativeReportSubmissionResult
{
  private NativeReportSubmissionResult() { }

  public static NativeReportSubmissionResult Success { get; } = new Successful();

  public static NativeReportSubmissionResult Failure(UiText message)
  {
    return new Failed(message);
  }

  public static NativeReportSubmissionResult NotSubmitted(UiText message)
  {
    return new Unavailable(message);
  }

  private sealed class Successful : NativeReportSubmissionResult { }

  internal sealed class Failed(UiText message) : NativeReportSubmissionResult
  {
    public UiText Message { get; } = message;
  }

  internal sealed class Unavailable(UiText message) : NativeReportSubmissionResult
  {
    public UiText Message { get; } = message;
  }
}
