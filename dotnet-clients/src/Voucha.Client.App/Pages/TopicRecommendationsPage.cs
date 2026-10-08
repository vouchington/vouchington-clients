using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.TopicRecommendations;
namespace Voucha.Client.App.Pages;
public sealed partial class TopicRecommendationsPage : ContentPage
{
  private readonly TopicRecommendationsListViewModel recommendations;
  private readonly TopHashtagsViewModel hashtags;
  private readonly ISessionStore sessionStore;
  private readonly CollectionView rows = new();
  private readonly Entry query = UiCopy.Bind(new Entry(), Entry.PlaceholderProperty, UiMessageKey.NativeDotnetTopHashtagsSearchHashtags);
  private readonly Button mapping = UiCopy.Bind(new Button(), Button.TextProperty, UiMessageKey.NativeDotnetTopHashtagsAll);
  private readonly Button search = UiCopy.Bind(new Button(), Button.TextProperty, UiMessageKey.NativeDotnetCsharpSearch);
  private readonly Button more = UiCopy.Bind(new Button(), Button.TextProperty, UiMessageKey.NativeDotnetTopHashtagsLoadMore);
  private readonly Label error = new() { TextColor = Colors.IndianRed };
  private bool showingHashtags;
  private int tabLoadGeneration;
  public TopicRecommendationsPage(
      VouchaApiClient client,
      ITopHashtagsService hashtagsService,
      ISessionStore sessionStore)
  {
    recommendations = new(client);
    hashtags = new(hashtagsService);
    this.sessionStore = sessionStore;
    SetDynamicResource(TitleProperty, UiMessageKey.NativeTaxonomyPostsTopicRecommendation.Value);
    rows.ItemTemplate = new DataTemplate(BuildRow);
    var recommendationsTab = UiCopy.Bind(new Button(), Button.TextProperty, UiMessageKey.NativeDotnetTopHashtagsRecommendations);
    recommendationsTab.Clicked += async (_, _) => await ShowRecommendationsAsync().ConfigureAwait(true);
    var hashtagsTab = UiCopy.Bind(new Button(), Button.TextProperty, UiMessageKey.NativeDotnetTopHashtagsTopHashtags);
    hashtagsTab.Clicked += async (_, _) => await ShowHashtagsAsync().ConfigureAwait(true);
    mapping.Clicked += async (_, _) =>
    {
      hashtags.Mapping = hashtags.Mapping switch
      {
        TopHashtagMapping.All => TopHashtagMapping.Linked,
        TopHashtagMapping.Linked => TopHashtagMapping.Unlinked,
        _ => TopHashtagMapping.All,
      };
      mapping.SetDynamicResource(Button.TextProperty, MappingKey(hashtags.Mapping).Value);
      await SearchHashtagsAsync().ConfigureAwait(true);
    };
    search.Clicked += async (_, _) => await SearchHashtagsAsync().ConfigureAwait(true);
    more.Clicked += async (_, _) => await LoadMoreAsync().ConfigureAwait(true);
    Content = new ScrollView
    {
      Content = new VerticalStackLayout
      {
        Padding = 20,
        Spacing = 12,
        Children =
        {
          new HorizontalStackLayout { Spacing = 8, Children = { recommendationsTab, hashtagsTab } },
          mapping,
          query,
          search,
          error,
          rows,
          more,
        },
      },
    };
  }
  protected override async void OnAppearing()
  {
    base.OnAppearing();
    await ShowRecommendationsAsync().ConfigureAwait(true);
  }
  private async Task ShowRecommendationsAsync()
  {
    var requestGeneration = Interlocked.Increment(ref tabLoadGeneration);
    showingHashtags = false;
    query.IsVisible = false;
    mapping.IsVisible = false;
    search.IsVisible = false;
    await recommendations.LoadAsync().ConfigureAwait(true);
    if (!IsCurrentTab(requestGeneration, false)) return;
    error.Text = recommendations.ErrorMessage;
    rows.ItemsSource = recommendations.Items;
    more.IsVisible = recommendations.HasMore;
  }
  private async Task ShowHashtagsAsync()
  {
    var requestGeneration = Interlocked.Increment(ref tabLoadGeneration);
    showingHashtags = true;
    var signedIn = sessionStore.Current.IsAuthenticated;
    query.IsVisible = signedIn;
    mapping.IsVisible = signedIn;
    search.IsVisible = signedIn;
    if (!signedIn) { rows.ItemsSource = Array.Empty<object>(); more.IsVisible = false; return; }
    await hashtags.LoadAsync().ConfigureAwait(true);
    if (!IsCurrentTab(requestGeneration, true)) return;
    error.Text = hashtags.ErrorMessage;
    RebindHashtagRows();
  }
  private async Task SearchHashtagsAsync()
  {
    if (!showingHashtags) return;
    var requestGeneration = Volatile.Read(ref tabLoadGeneration);
    hashtags.Query = query.Text ?? string.Empty;
    await hashtags.LoadAsync().ConfigureAwait(true);
    if (!IsCurrentTab(requestGeneration, true)) return;
    error.Text = hashtags.ErrorMessage;
    RebindHashtagRows();
  }
  private async Task LoadMoreAsync()
  {
    var loadingHashtags = showingHashtags; var requestGeneration = Volatile.Read(ref tabLoadGeneration);
    if (loadingHashtags) await hashtags.LoadMoreAsync().ConfigureAwait(true);
    else await recommendations.LoadMoreAsync().ConfigureAwait(true);
    if (!IsCurrentTab(requestGeneration, loadingHashtags)) return;
    if (loadingHashtags) { error.Text = hashtags.ErrorMessage; RebindHashtagRows(); }
    else { error.Text = recommendations.ErrorMessage; rows.ItemsSource = recommendations.Items; more.IsVisible = recommendations.HasMore; }
  }
  private View BuildRow()
  {
    var label = new Label { FontAttributes = FontAttributes.Bold };
    label.BindingContextChanged += (_, _) => label.Text = label.BindingContext switch
    {
      TopHashtag hashtag => HashtagLabel(hashtag),
      Post post => post.Title,
      _ => string.Empty,
    };
    var topicId = UiCopy.Bind(new Entry(), Entry.PlaceholderProperty, UiMessageKey.NativeDotnetTopHashtagsTopic);
    var link = UiCopy.Bind(new Button(), Button.TextProperty, UiMessageKey.NativeDotnetTopHashtagsLinkTopic);
    link.Clicked += async (_, _) => await LinkAsync(link.BindingContext as TopHashtag, topicId.Text).ConfigureAwait(true);
    var unlink = UiCopy.Bind(new Button(), Button.TextProperty, UiMessageKey.NativeDotnetTopHashtagsUnlink);
    unlink.Clicked += async (_, _) => await UnlinkAsync(unlink.BindingContext as TopHashtag).ConfigureAwait(true);
    var name = UiCopy.Bind(new Entry(), Entry.PlaceholderProperty, UiMessageKey.NativeDotnetTopHashtagsTopicName);
    var create = UiCopy.Bind(new Button(), Button.TextProperty, UiMessageKey.NativeDotnetTopHashtagsCreateTopic);
    create.Clicked += async (_, _) => await CreateAsync(create.BindingContext as TopHashtag, name.Text).ConfigureAwait(true);
    var actions = new HorizontalStackLayout { Spacing = 6, Children = { topicId, link, unlink, name, create } };
    actions.SetBinding(IsEnabledProperty, new Binding(nameof(TopHashtagsViewModel.CanMutate), source: hashtags));
    actions.IsVisible = false;
    actions.BindingContextChanged += (_, _) =>
    {
      var hashtag = actions.BindingContext as TopHashtag;
      topicId.Text = string.Empty;
      name.Text = string.Empty;
      actions.IsVisible = hashtag is not null && sessionStore.Current.CanManageTopics();
      var isLinked = hashtag?.TopicId is not null;
      unlink.IsVisible = hashtag is not null && hashtags.CanUnlink(hashtag);
      topicId.IsVisible = link.IsVisible = name.IsVisible = create.IsVisible = !isLinked;
    };
    return new VerticalStackLayout { Spacing = 6, Children = { label, actions } };
  }
  private bool IsCurrentTab(int requestGeneration, bool shouldShowHashtags) =>
      requestGeneration == Volatile.Read(ref tabLoadGeneration) && showingHashtags == shouldShowHashtags;

  private string HashtagLabel(TopHashtag hashtag)
  {
    var counts = UiCopy.Format(
        UiMessageKey.NativeDotnetTopHashtagsItemsContributors,
        ("items", hashtag.ItemCount),
        ("contributors", hashtag.ContributorCount));
    var topic = hashtag.TopicId is { } topicId && hashtags.Topics.TryGetValue(topicId, out var mapped)
        ? Environment.NewLine + mapped.Name
        : string.Empty;
    return $"{AuthoredHashtagToken(hashtag.Hashtag)}{Environment.NewLine}{counts}{topic}";
  }

  private static string AuthoredHashtagToken(string hashtag) => hashtag.StartsWith('#') ? hashtag : $"#{hashtag}";

  private static UiMessageKey MappingKey(TopHashtagMapping value) => value switch
  {
    TopHashtagMapping.Linked => UiMessageKey.NativeDotnetTopHashtagsLinked,
    TopHashtagMapping.Unlinked => UiMessageKey.NativeDotnetTopHashtagsUnlinked,
    _ => UiMessageKey.NativeDotnetTopHashtagsAll,
  };
}
