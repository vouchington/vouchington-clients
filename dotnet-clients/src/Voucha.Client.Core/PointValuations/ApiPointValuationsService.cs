using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.PointValuations;

public sealed class ApiPointValuationsService : IPointValuationsService
{
  private static readonly PageInfo TerminalPage = new(null, false, null);
  private readonly VouchaApiClient client;

  public ApiPointValuationsService(VouchaApiClient client) =>
      this.client = client ?? throw new ArgumentNullException(nameof(client));

  public async Task<PointValuationPage> FetchAsync(
      string? after = null, int limit = 25, CancellationToken cancellationToken = default)
  {
    var response = await client.FetchPointValuationsAsync(after, limit, cancellationToken).ConfigureAwait(false);
    return new(response.Results.Select(PointValuation.FromWire).ToArray(), response.PageInfo ?? TerminalPage);
  }

  public async Task<PointValuation> CreateAsync(
      CreatePointValuationBody body, CancellationToken cancellationToken = default) =>
      PointValuation.FromWire((await client.CreatePointValuationAsync(body, cancellationToken)
          .ConfigureAwait(false)).PointValuation);

  public async Task<PointValuation> UpdateAsync(
      string id, UpdatePointValuationBody body, CancellationToken cancellationToken = default) =>
      PointValuation.FromWire((await client.UpdatePointValuationAsync(id, body, cancellationToken)
          .ConfigureAwait(false)).PointValuation);

  public Task DeleteAsync(string id, CancellationToken cancellationToken = default) =>
      client.DeletePointValuationAsync(id, cancellationToken);

  public async Task<IReadOnlyList<RewardsProgramOption>> SearchAsync(
      string query, CancellationToken cancellationToken = default)
  {
    var response = await client.SearchRewardsProgramTopicsAsync(query, cancellationToken: cancellationToken)
        .ConfigureAwait(false);
    return response.Results
        .Where(item => item.Id is not null && item.Name is not null && item.Slug is not null &&
            item.TopicType == "rewards_program")
        .Select(item => new RewardsProgramOption(item.Id!, item.Name!, item.Slug!))
        .ToArray();
  }
}
