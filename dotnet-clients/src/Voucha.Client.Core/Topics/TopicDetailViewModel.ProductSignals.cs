namespace Voucha.Client.Core.Topics;

public sealed partial class TopicDetailViewModel
{
  private bool isActive = true;
  private int activationVersion;
  private int loadVersion;
  private string? activeTopicIdOrSlug;
  private string? recordedRssFeedId;

  public void Activate()
  {
    if (isActive) return;
    isActive = true;
    activationVersion++;
    recordedRssFeedId = null;
  }

  public void Deactivate()
  {
    isActive = false;
    activationVersion++;
    recordedRssFeedId = null;
  }

  private bool IsCurrentLoad(int activation, int load, CancellationToken cancellationToken) =>
      isActive && activation == activationVersion && load == loadVersion && !cancellationToken.IsCancellationRequested;

  private void RecordRssFeedViewOnce()
  {
    if (productSignals is null || SourceRssFeedId is not { Length: > 0 } feedId) return;
    if (recordedRssFeedId == feedId) return;
    recordedRssFeedId = feedId;
    productSignals.RecordRssFeedView(feedId);
  }
}
