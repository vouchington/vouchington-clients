using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Communities;

public sealed partial class CommunityActionViewModel
{
  public async Task<bool> SubmitAsync(CancellationToken cancellationToken = default)
  {
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
      await SubmitValidatedAsync(cancellationToken).ConfigureAwait(true);
      State = LoadState.Loaded;
      return true;
    }
    catch (OperationCanceledException)
    {
      State = LoadState.Idle;
      return false;
    }
    catch (Exception ex) when (HandleLoadException(ex))
    {
      return false;
    }
  }

  private async Task SubmitValidatedAsync(CancellationToken cancellationToken)
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
        await service.SendInviteAsync(
            CommunitySlug,
            new SendCommunityInviteRequest(Email, Username),
            cancellationToken).ConfigureAwait(true);
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
