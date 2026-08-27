using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Search;

public sealed partial class OmnisearchViewModel
{
  public Task MuteSelectedHostnameAsync(CancellationToken cancellationToken = default) =>
      selectedHostnameId is { } id && CanUseSelectedHostnameActions
          ? client.MuteHostnameAsync(id, true, cancellationToken)
          : Task.CompletedTask;

  public Task BlockSelectedHostnameAsync(CancellationToken cancellationToken = default) =>
      selectedHostnameId is { } id && CanUseSelectedHostnameActions
          ? client.BlockHostnameAsync(id, true, cancellationToken)
          : Task.CompletedTask;

  public Task TriggerSelectedUrlCrawlAsync(CancellationToken cancellationToken = default) =>
      selectedUrlId is { } id && CanTriggerSelectedUrlCrawl
          ? client.TriggerUrlCrawlAsync(id, cancellationToken)
          : Task.CompletedTask;

  private Task RunSelectedPrimaryActionAsync(string? primaryAction, CancellationToken cancellationToken) =>
      primaryAction switch
      {
        "sentiment.like" => VoteSelectedHostnameAsync(ElectionVoteChoice.Like, cancellationToken),
        "sentiment.dislike" => VoteSelectedHostnameAsync(ElectionVoteChoice.Dislike, cancellationToken),
        "Mute" => MuteSelectedHostnameAsync(cancellationToken),
        "Block" => BlockSelectedHostnameAsync(cancellationToken),
        "Trigger crawl" => TriggerSelectedUrlCrawlAsync(cancellationToken),
        _ => Task.CompletedTask,
      };
}
