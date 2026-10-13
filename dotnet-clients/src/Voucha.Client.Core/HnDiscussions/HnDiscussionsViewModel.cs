using System.Collections.ObjectModel;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.HnDiscussions;

public sealed class HnDiscussionRow
{
  public required string Title { get; init; }
  public required string LocalizedMetadata { get; init; }
  public required Uri ItemUrl { get; init; }
}

public sealed class HnDiscussionsViewModel : ObservableObject
{
  private readonly IHnDiscussionsSettings settings;
  private readonly HnDiscussionsClient client;
  private readonly IUiLocalization localization;
  private bool hasThreads;
  private int loadRevision;

  public HnDiscussionsViewModel(
      IHnDiscussionsSettings settings,
      HnDiscussionsClient client,
      IUiLocalization? localization = null)
  {
    this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
    this.client = client ?? throw new ArgumentNullException(nameof(client));
    this.localization = localization ?? UiLocalization.English;
  }

  public ObservableCollection<HnDiscussionRow> Threads { get; } = [];

  public bool HasThreads
  {
    get => hasThreads;
    private set => SetProperty(ref hasThreads, value);
  }

  public string LocalizedHeading =>
      localization.Localize(UiMessageKey.ExtractedAsidesHnDiscussionsAsideHackerNews619f304a);

  public async Task LoadAsync(IEnumerable<string?> urls, CancellationToken cancellationToken = default)
  {
    var revision = ++loadRevision;
    Threads.Clear();
    HasThreads = false;
    if (!settings.Enabled)
    {
      return;
    }
    var threads = await client.SearchAsync(urls, cancellationToken).ConfigureAwait(true);
    if (revision != loadRevision) return;
    foreach (var thread in threads)
    {
      Threads.Add(new HnDiscussionRow
      {
        Title = thread.Title,
        LocalizedMetadata = $"{Score(thread.Score)} · {Comments(thread.CommentCount)}",
        ItemUrl = thread.ItemUrl,
      });
    }
    HasThreads = Threads.Count > 0;
  }

  public Task LoadAsync(Post? post, CancellationToken cancellationToken = default)
  {
    var urls = new List<string?>();
    if (post?.Url is not null) urls.Add(post.Url.ToString());
    urls.AddRange(HnDiscussionUrlCollector.ExtractFromMarkdown(post?.Markdown));
    return LoadAsync(urls, cancellationToken);
  }

  private string Score(int points) =>
      localization.Format(UiMessageKey.ExtractedAsidesHnDiscussionsAsidePointsPoints29c78cd8, ("points", points));

  private string Comments(int comments) =>
      localization.Format(
          UiMessageKey.ExtractedAsidesHnDiscussionsAsideCommentsComments29834540,
          ("comments", comments));
}
