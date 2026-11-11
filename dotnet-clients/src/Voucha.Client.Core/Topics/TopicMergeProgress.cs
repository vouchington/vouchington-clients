using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Topics;

/// <summary>
/// Maps the producer's asynchronous topic-merge status to presentation copy. A merge is only
/// complete once the producer reports <c>completed</c>; acceptance (202) is always "queued".
/// </summary>
public static class TopicMergeProgress
{
  public static UiMessageKey MessageFor(TopicMergeDetails operation)
  {
    ArgumentNullException.ThrowIfNull(operation);
    return operation.Status switch
    {
      "completed" => UiMessageKey.SettingsTopicsMergeCompleted,
      "failed" => UiMessageKey.SettingsTopicsMergeFailed,
      _ => UiMessageKey.SettingsTopicsMergeQueued,
    };
  }

  /// <summary>Queued or running operations can still be checked; terminal ones cannot.</summary>
  public static bool CanCheckStatus(TopicMergeDetails operation)
  {
    ArgumentNullException.ThrowIfNull(operation);
    return operation.Status is not ("completed" or "failed");
  }
}
