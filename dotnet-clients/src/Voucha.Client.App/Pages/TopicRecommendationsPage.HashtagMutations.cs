using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;

namespace Voucha.Client.App.Pages;

public sealed partial class TopicRecommendationsPage
{
  private async Task LinkAsync(TopHashtag? hashtag, string? topicId)
  {
    if (hashtag is null || !sessionStore.Current.CanManageTopics() || string.IsNullOrWhiteSpace(topicId)) return;
    await RunHashtagMutationAsync(() => hashtags.LinkAsync(hashtag, topicId.Trim())).ConfigureAwait(true);
  }

  private async Task UnlinkAsync(TopHashtag? hashtag)
  {
    if (hashtag is null || !sessionStore.Current.CanManageTopics()) return;
    await RunHashtagMutationAsync(() => hashtags.UnlinkAsync(hashtag)).ConfigureAwait(true);
  }

  private async Task CreateAsync(TopHashtag? hashtag, string? name)
  {
    if (hashtag is null || !sessionStore.Current.CanManageTopics() || string.IsNullOrWhiteSpace(name)) return;
    await RunHashtagMutationAsync(() => hashtags.CreateTopicAsync(hashtag, name)).ConfigureAwait(true);
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "MAUI actions display failures inline.")]
  private async Task RunHashtagMutationAsync(Func<Task> mutation)
  {
    var requestGeneration = Volatile.Read(ref tabLoadGeneration);
    try
    {
      await mutation().ConfigureAwait(true);
      if (!IsCurrentTab(requestGeneration, true)) return;
      RebindHashtagRows();
      ShowHashtagMutationError();
    }
    catch (Exception exception)
    {
      if (IsCurrentTab(requestGeneration, true)) error.Text = exception.Message;
    }
  }

  private void ShowHashtagMutationError() => error.Text = hashtags.ErrorMessage ?? string.Empty;

  private void RebindHashtagRows()
  {
    rows.ItemsSource = hashtags.Items;
    more.IsVisible = hashtags.HasMore;
  }
}
