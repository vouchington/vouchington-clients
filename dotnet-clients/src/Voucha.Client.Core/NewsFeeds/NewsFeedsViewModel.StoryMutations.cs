using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.NewsFeeds;

public sealed partial class NewsFeedsViewModel
{
  private StoryRelatedArticles? FindStoryPeer(string itemId) =>
      Items.Select(item => item.StoryArticles).FirstOrDefault(group => group?.Items.Any(peer => peer.Id == itemId) == true);

  [SuppressMessage("Design", "CA1031:Do not catch general exception types",
      Justification = "Related article mutations retain their preview and expose recoverable native errors.")]
  private async Task MutateStoryPeerAsync(StoryRelatedArticles group, NewsFeedItem original,
      Func<NewsFeedItem, NewsFeedItem> project, bool remove, Func<Task> submit, bool verifyEmail = false)
  {
    if (!togglingArticleIds.Add(original.Id)) return;
    var generation = loadRequestId;
    var index = group.Items.IndexOf(original);
    if (index < 0)
    {
      togglingArticleIds.Remove(original.Id);
      return;
    }
    var updated = project(original);
    var mainIndex = Items.ToList().FindIndex(item => item.Id == original.Id);
    var mainOriginal = mainIndex >= 0 ? Items[mainIndex] : null;
    var mainUpdated = mainOriginal is null ? null : project(mainOriginal);
    if (mainOriginal is not null)
      Items = Items.Where(item => !remove || item.Id != original.Id)
          .Select(item => item.Id == original.Id ? mainUpdated! : item).ToArray();
    if (remove)
    {
      group.SuppressHiddenPeer(original.Id);
      group.InvalidatePendingPage();
      group.Items.RemoveAt(index);
    }
    else group.Items[index] = updated;
    group.Notify();
    ErrorMessage = null;
    try
    {
      if (verifyEmail)
        await EmailVerificationGate.RunAsync(submit, ex => Restore(ex.Message)).ConfigureAwait(true);
      else
        await submit().ConfigureAwait(true);
    }
    catch (OperationCanceledException) { Restore(null); }
    catch (Exception ex) { Restore(ex.Message); }
    finally { togglingArticleIds.Remove(original.Id); }

    void Restore(string? message)
    {
      if (generation != loadRequestId) return;
      if (remove) group.RestoreHiddenPeer(original.Id);
      if (remove && !group.Items.Any(item => item.Id == original.Id))
        group.Items.Insert(Math.Min(index, group.Items.Count), original);
      else if (!remove && group.Items.IndexOf(updated) is var current && current >= 0)
        group.Items[current] = original;
      if (mainOriginal is not null && Items.Any(item => ReferenceEquals(item.StoryArticles, group)))
      {
        if (remove && !Items.Any(item => item.Id == original.Id))
        {
          var restored = Items.ToList();
          restored.Insert(Math.Min(mainIndex, restored.Count), mainOriginal);
          Items = restored;
        }
        else if (!remove)
          Items = Items.Select(item => ReferenceEquals(item, mainUpdated) ? mainOriginal : item).ToArray();
      }
      group.Notify();
      ErrorMessage = message;
    }
  }
}
