using System.Net;
using System.Text.Json;

namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public async Task<RssFeedDeletionOutcome> DeleteRssFeedAsync(
      string rssFeedId, CancellationToken cancellationToken = default)
  {
    using var response = await SendMutationAsync(
        VouchaApiEndpoints.DeleteRssFeed(rssFeedId), cancellationToken).ConfigureAwait(false);
    if (!response.IsSuccessStatusCode)
    {
      var body = await ReadErrorBodyAsync(response.Content, cancellationToken).ConfigureAwait(false);
      var error = new VouchaApiException(
          response.StatusCode, string.IsNullOrEmpty(body) ? null : body, response.Headers.RetryAfter?.Delta);
      if (error.StatusCode == HttpStatusCode.Conflict && error.ErrorCode == "RSS_FEED_ADMIN_IMPORT_REFERENCED")
        return RssFeedDeletionOutcome.BlockedByAdminImports();
      throw error;
    }
    if (response.StatusCode != HttpStatusCode.Accepted)
      throw DecodeFailure(response, "unexpected RSS feed deletion status");
    using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
    RssFeedDeletionAcknowledgement? acknowledgement;
    try
    {
      acknowledgement = await JsonSerializer.DeserializeAsync<RssFeedDeletionAcknowledgement>(
          stream, VouchaApiJson.Options, cancellationToken).ConfigureAwait(false);
    }
    catch (JsonException error)
    {
      throw DecodeFailure(response, "invalid RSS feed deletion acknowledgement", error);
    }
    if (acknowledgement is null || string.IsNullOrEmpty(acknowledgement.RssFeedId) || acknowledgement.Status != "deleting")
      throw DecodeFailure(response, "invalid RSS feed deletion acknowledgement");
    return RssFeedDeletionOutcome.AcceptedPending(acknowledgement.RssFeedId);
  }
}
