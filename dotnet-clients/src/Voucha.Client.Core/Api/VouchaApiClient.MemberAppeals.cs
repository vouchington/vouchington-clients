namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public Task<ModerationAppealSubmissionResponse> SubmitAppealAsync(
      ModerationAppealSubmissionRequest request,
      CancellationToken cancellationToken = default) =>
      SendAsync<ModerationAppealSubmissionResponse>(
          VouchaApiEndpoints.SubmitAppeal(request),
          cancellationToken);

  public Task<MemberWarningNoticesResponse> FetchPersonalWarningsAsync(
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default) =>
      SendAsync<MemberWarningNoticesResponse>(
          VouchaApiEndpoints.PersonalWarnings(limit, after),
          cancellationToken);
}
