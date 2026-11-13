using Voucha.Client.Core.Auth;

namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  private readonly IAccountChangeConfirmation accountChangeConfirmation;

  public VouchaApiClient(HttpClient httpClient, IAccountChangeConfirmation accountChangeConfirmation)
      : this(httpClient, null, accountChangeConfirmation)
  {
  }

  private async Task<ApiRequest> ConfirmAccountChangeAsync(ApiRequest request, CancellationToken cancellationToken)
  {
    if (request.ConfirmationFactory is not { } createRequest) return request;
    var proof = await accountChangeConfirmation.ConfirmAsync(cancellationToken).ConfigureAwait(false);
    cancellationToken.ThrowIfCancellationRequested();
    return createRequest(proof.Consume());
  }
}
