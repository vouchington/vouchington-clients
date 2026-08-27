using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using HttpStatusCode = System.Net.HttpStatusCode;

namespace Voucha.Client.Core.ReferralLinks;

public sealed partial class ReferralLinksViewModel
{
  public Task LoadAnalyticsAsync(CancellationToken cancellationToken = default) =>
      LoadAnalyticsPageAsync(after: null, append: false, cancellationToken);

  public Task LoadMoreAnalyticsAsync(CancellationToken cancellationToken = default) =>
      !IsLoading && HasMoreAnalytics && AnalyticsEndCursor is not null
          ? LoadAnalyticsPageAsync(AnalyticsEndCursor, append: true, cancellationToken)
          : Task.CompletedTask;

  private async Task LoadAnalyticsPageAsync(
      string? after,
      bool append,
      CancellationToken cancellationToken)
  {
    var currentRequest = BeginLoad();
    try
    {
      var response = await referralLinksService
          .FetchClicksAsync(new FetchReferralClickLogsRequest(after), cancellationToken)
          .ConfigureAwait(true);
      var rows = response.Results.Select(result =>
          response.Clicks is not null && response.Clicks.TryGetValue(result.Id, out var click)
              ? RowFromClick(click, SignupStatusFromClick(click, response.Users))
              : null)
          .OfType<ReferralLinkRow>()
          .ToArray();
      CompleteAnalyticsRows(currentRequest, rows, response.PageInfo, append);
    }
    catch (OperationCanceledException)
    {
      CompleteCanceled(currentRequest);
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      if (append)
      {
        CompleteErrorPreservingRows(currentRequest, ex.Message);
      }
      else
      {
        CompleteError(currentRequest, ex.Message);
      }
    }
  }

  public async Task<IReadOnlyList<ReferralProgramChoice>> SearchReferralProgramsAsync(
      string query,
      CancellationToken cancellationToken = default)
  {
    var response = await referralLinksService
        .SearchReferralProgramsAsync(
            new SearchTopicsRequest(query, TopicTypes: "referral_program", Limit: 10),
            cancellationToken)
        .ConfigureAwait(true);

    return response.Results
        .Select(result => result.Id ?? result.EntityId)
        .Where(id => id is not null)
        .Select(id => response.Topics is not null && response.Topics.TryGetValue(id!, out var topic) ? topic : null)
        .OfType<Topic>()
        .Select(topic => new ReferralProgramChoice(topic.Id, topic.Name, topic.Slug))
        .ToArray();
  }

  public async Task<ReferralProgramValidationInfo> FetchValidationInfoAsync(
      string referralProgramId,
      CancellationToken cancellationToken = default)
  {
    try
    {
      var response = await referralLinksService
          .FetchValidationInfoAsync(referralProgramId, cancellationToken)
          .ConfigureAwait(true);
      return response.ValidationInfo;
    }
    catch (VouchaApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
    {
      return new ReferralProgramValidationInfo(null, []);
    }
  }

  public Task CreateAsync(
      string referralProgramId,
      Uri url,
      string? label,
      CancellationToken cancellationToken = default) =>
      RunMutationAsync(
          token => referralLinksService.CreateAsync(
              new CreateReferralLinkBody(referralProgramId, url, string.IsNullOrWhiteSpace(label) ? null : label),
              token),
          cancellationToken);

  public Task RenameAsync(string referralLinkId, string? label, CancellationToken cancellationToken = default) =>
      RunMutationAsync(
          token => referralLinksService.UpdateAsync(referralLinkId, new UpdateReferralLinkBody(label), token),
          cancellationToken);

  public Task DeleteAsync(string referralLinkId, CancellationToken cancellationToken = default) =>
      RunMutationAsync(token => referralLinksService.DeleteAsync(referralLinkId, token), cancellationToken);

  public Task SetActiveAsync(
      string referralLinkId,
      bool active,
      CancellationToken cancellationToken = default) =>
      RunMutationAsync(
          token => active
              ? referralLinksService.ActivateAsync(referralLinkId, token)
              : referralLinksService.DeactivateAsync(referralLinkId, token),
          cancellationToken);

  private async Task RunMutationAsync(
      Func<CancellationToken, Task> operation,
      CancellationToken cancellationToken)
  {
    var currentRequest = BeginLoad();
    try
    {
      await operation(cancellationToken).ConfigureAwait(true);
      var response = await referralLinksService
          .FetchMineAsync(new FetchReferralLinksRequest(), cancellationToken)
          .ConfigureAwait(true);
      CompleteManagedReferralRows(
          currentRequest,
          response.Results.Select(RowFromMine).ToArray(),
          response.PageInfo,
          append: false);
    }
    catch (OperationCanceledException)
    {
      CompleteCanceled(currentRequest);
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      CompleteErrorPreservingRows(currentRequest, MutationErrorMessage(ex));
    }
  }

  private ReferralLinkRow RowFromClick(ReferralClickLogEntry click, UiText signupStatus) =>
      new(
          click.Id,
          UiText.Verbatim(click.LandingUrl),
          ParseUri(click.LandingUrl),
          UiText.Verbatim(string.Empty),
          IsAnalytics: true,
          SignupStatusText: signupStatus,
          DetailInstant: click.CreatedAt,
          Localization: localization);

  private static UiText SignupStatusFromClick(
      ReferralClickLogEntry click,
      IReadOnlyDictionary<string, User>? users)
  {
    if (click.SignedUpAt is null)
    {
      return UiText.Localized(UiMessageKey.NativeDotnetReferralLinksReferralClick);
    }

    return click.UserId is not null &&
        users is not null &&
        users.TryGetValue(click.UserId, out var user) &&
        !string.IsNullOrWhiteSpace(user.Username)
        ? UiText.Localized(
            UiMessageKey.NativeDotnetReferralLinksSignupUsername,
            ("username", user.Username))
        : UiText.Localized(UiMessageKey.NativeDotnetReferralLinksSignupAnonymous);
  }

}

public sealed record ReferralProgramChoice(string Id, string Name, string Slug);
