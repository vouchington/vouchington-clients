using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Communities;

public sealed partial class CommunityActionViewModel
{
  private int inviteSubmissionInProgress;

  public async Task<bool> SubmitAsync(CancellationToken cancellationToken = default)
  {
    if (kind == CommunityActionKind.Invite &&
        Interlocked.CompareExchange(ref inviteSubmissionInProgress, 1, 0) != 0) return false;
    if (kind != CommunityActionKind.Invite) DismissInviteCode();
    try
    {
      if (kind == CommunityActionKind.Invite && CreatedInviteCode is not null) return false;
      return await SubmitAdmittedAsync(cancellationToken).ConfigureAwait(true);
    }
    finally
    {
      if (kind == CommunityActionKind.Invite) Volatile.Write(ref inviteSubmissionInProgress, 0);
    }
  }

  private async Task<bool> SubmitAdmittedAsync(CancellationToken cancellationToken)
  {
    var revision = submissionRevision;
    if (Validation is { IsValid: false } validation)
    {
      ErrorMessage = validation.ErrorMessage;
      State = LoadState.Error;
      return false;
    }

    State = LoadState.Loading;
    ErrorMessage = null;
    try
    {
      await SubmitValidatedAsync(revision, cancellationToken).ConfigureAwait(true);
      if (revision != submissionRevision)
      {
        return false;
      }
      State = LoadState.Loaded;
      return true;
    }
    catch (OperationCanceledException)
    {
      if (revision != submissionRevision)
      {
        return false;
      }
      State = LoadState.Idle;
      return false;
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      if (revision != submissionRevision)
      {
        return false;
      }
      HandleLoadException(ex);
      return false;
    }
  }

  private async Task SubmitValidatedAsync(long revision, CancellationToken cancellationToken)
  {
    switch (kind)
    {
      case CommunityActionKind.Create:
        await service.CreateAsync(
            new CreateCommunityRequest(
                Name.Trim(),
                EmptyToNull(Slug),
                Markdown,
                TurnstileToken: EmptyToNull(TurnstileToken)),
            cancellationToken).ConfigureAwait(true);
        break;
      case CommunityActionKind.Apply:
        await service.ApplyAsync(
            CommunitySlug,
            new ApplyToCommunityRequest(ApplicationAnswers, Message),
            cancellationToken).ConfigureAwait(true);
        break;
      case CommunityActionKind.Invite:
        var created = await service.SendInviteAsync(
            CommunitySlug,
            new SendCommunityInviteRequest(Email, Username),
            cancellationToken).ConfigureAwait(true);
        if (revision != submissionRevision)
        {
          return;
        }
        CreatedInviteCode = created.CommunityInvite.Code;
        break;
      case CommunityActionKind.RedeemInvite:
        await service.RedeemInviteAsync(
            new RedeemCommunityInviteRequest(Code ?? string.Empty),
            cancellationToken).ConfigureAwait(true);
        break;
    }
  }

  private static string? EmptyToNull(string? value) =>
      string.IsNullOrWhiteSpace(value) ? null : value.Trim();

  private bool HandleLoadException(Exception ex)
  {
    if (ex is not (VouchaApiException or HttpRequestException or InvalidOperationException))
    {
      return false;
    }

    if (kind == CommunityActionKind.Create)
    {
      TurnstileToken = "";
    }

    ErrorMessage = ex.Message;
    State = LoadState.Error;
    return true;
  }
}
