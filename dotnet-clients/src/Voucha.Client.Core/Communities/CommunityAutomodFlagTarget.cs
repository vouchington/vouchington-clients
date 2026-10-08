using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Communities;

public static class CommunityAutomodFlagTarget
{
  public static string? PostId(CommunityModerationQueueEntry entry)
  {
    ArgumentNullException.ThrowIfNull(entry);
    if (!string.Equals(entry.QueueSource, "automod_flag", StringComparison.Ordinal) ||
        !string.Equals(entry.EntityType, "post", StringComparison.Ordinal)) return null;
    return !string.IsNullOrWhiteSpace(entry.PostId) ? entry.PostId :
        !string.IsNullOrWhiteSpace(entry.EntityId) ? entry.EntityId : null;
  }
}
