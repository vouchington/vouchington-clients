using System.Collections;
using System.Reflection;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App;
using Voucha.Client.App.Pages;
using Voucha.Client.Core;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Content;
using Voucha.Client.Core.Images;
using Voucha.Client.Core.LandingPages;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Posts;
using Voucha.Client.Core.Profiles;
using Voucha.Client.Core.Settings;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class AccountTypeAuthorRenderingTests
{
  [Theory]
  [InlineData(AccountType.Official, "Official")]
  [InlineData(AccountType.System, "System")]
  [InlineData(AccountType.AiAgent, "AI Agent")]
  [InlineData(null, null)]
  public void PostsPageCardRendersAndClearsTheAuthorClassification(AccountType? type, string? expected)
  {
    InstallPageResources();
    var page = new PostsPage();
    var collection = Assert.Single(Descendants<CollectionView>(page));
    var card = Assert.IsAssignableFrom<View>(collection.ItemTemplate.CreateContent());
    var official = Row(AccountType.Official);
    card.BindingContext = official;
    var badge = Assert.Single(Descendants<Label>(card), label => label.Text == "Official");

    card.BindingContext = Row(type);

    Assert.Equal(expected, badge.Text);
  }

  [Theory]
  [InlineData(AccountType.Official, "Official")]
  [InlineData(AccountType.System, "System")]
  [InlineData(AccountType.AiAgent, "AI Agent")]
  [InlineData(null, null)]
  public void PostDetailRootAndCommentTemplatesRenderClassification(AccountType? type, string? expected)
  {
    InstallPageResources();
    var page = new PostDetailPage();
    var binding = new PostDetailPageBinding(
        new CommentThreadViewModel(Proxy<ICommentThreadService>(), "post"),
        new AnonymousSessionStore());
    var root = DetailRow(AccountType.Official);
    var rootProperty = typeof(PostDetailPageBinding).GetProperty(nameof(PostDetailPageBinding.RootRow))!;
    rootProperty.SetValue(binding, root);
    page.BindingContext = binding;
    var rootBadge = Assert.Single(Descendants<Label>(page), label => label.Text == "Official");
    rootProperty.SetValue(binding, DetailRow(type));
    Assert.Equal(expected, rootBadge.Text);

    var comments = Assert.Single(Descendants<CollectionView>(page), view =>
        ReferenceEquals(view.ItemsSource, binding.CommentRows));
    var card = Assert.IsAssignableFrom<View>(comments.ItemTemplate.CreateContent());
    card.BindingContext = root;
    var commentBadge = Assert.Single(Descendants<Label>(card), label => label.Text == "Official");
    card.BindingContext = DetailRow(type);

    Assert.Equal(expected, commentBadge.Text);
  }

  [Theory]
  [InlineData(AccountType.Official, "Official")]
  [InlineData(AccountType.System, "System")]
  [InlineData(AccountType.AiAgent, "AI Agent")]
  [InlineData(null, null)]
  public void ProfileHeaderRendersAndClearsClassification(AccountType? type, string? expected)
  {
    InstallPageResources();
    using var model = new ProfileViewModel(
        Proxy<ISettingsService>(), Proxy<ILandingPagesService>(), Proxy<IPostsService>(),
        Proxy<IImageUploadService>(), new AppConfig(new Uri("https://api.test")));
    typeof(ProfileViewModel).GetProperty(nameof(ProfileViewModel.User))!
        .SetValue(model, Author(AccountType.Official));
    var page = new ProfilePage { BindingContext = model };
    var badge = Assert.Single(Descendants<Label>(page), label => label.Text == "Official");

    typeof(ProfileViewModel).GetProperty(nameof(ProfileViewModel.User))!
        .SetValue(model, Author(type));

    Assert.Equal(expected, badge.Text);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public void RejectedFeedAndProfileCardsRenderViewerSpecificNoticeAndControls(bool profile)
  {
    InstallPageResources();
    var page = profile ? (ContentPage)new ProfilePage() : new PostsPage();
    var collection = Named<CollectionView>(page, "authored-post-collection");
    var card = Assert.IsAssignableFrom<View>(collection.ItemTemplate.CreateContent());
    var notice = Named<Label>(card, "authored-post-notice");
    var appeals = Named<Button>(card, "authored-post-appeals");
    var body = Named<VisualElement>(card, "authored-post-body");
    var embed = Named<VisualElement>(card, "authored-post-embed");
    var thumbnail = Assert.Single(Descendants<Image>(embed));

    foreach (var (viewer, staff, showAppeals, showBody, showEmbed) in new[]
    {
      ("user-1", false, true, false, false),
      ("other", false, false, false, false),
      ("user-1", true, true, false, true),
      ("other", true, false, true, true),
    })
    {
      card.BindingContext = RejectedRow(viewer, staff);
      Assert.Equal(showAppeals, appeals.IsVisible);
      Assert.Equal(showBody, body.IsVisible);
      Assert.Equal(showEmbed, embed.IsVisible);
      Assert.Equal(showEmbed, thumbnail.Source is not null);
      Assert.Equal(!staff || viewer == "user-1", notice.IsVisible);
      Assert.Equal(!staff || viewer == "user-1", notice.Text is not null);
    }
  }

  [Fact]
  public void RejectedDetailRootRendersAuthorNoticeAndHidesBodyAndEmbed()
  {
    InstallPageResources();
    var page = new PostDetailPage();
    var binding = new PostDetailPageBinding(
        new CommentThreadViewModel(Proxy<ICommentThreadService>(), "post"),
        new AnonymousSessionStore());
    var rootProperty = typeof(PostDetailPageBinding).GetProperty(nameof(PostDetailPageBinding.RootRow))!;
    var rejectedRow = RejectedDetailRow("user-1", false) with { CanReply = true };
    rootProperty.SetValue(binding, rejectedRow with
    {
      Post = rejectedRow.Post with
      {
        CreatedBy = Author(null) with
        {
          ProfileImagePlacement = new TopicImagePlacement("avatar-1", "author-placement", 2),
        },
      },
    });
    page.BindingContext = binding;
    var notice = Named<Label>(page, "authored-post-notice");
    var appeals = Named<Button>(page, "authored-post-appeals");
    var body = Named<VisualElement>(page, "authored-post-body");
    var embed = Named<VisualElement>(page, "authored-post-embed");
    var thumbnail = Named<Image>(page, "authored-post-thumbnail");
    var authorAvatar = Named<Image>(page, "post-author-avatar-root");
    var quote = Named<Button>(page, "authored-post-quote");
    Assert.True(notice.IsVisible);
    Assert.True(appeals.IsVisible);
    Assert.False(body.IsVisible);
    Assert.False(embed.IsVisible);
    Assert.Null(thumbnail.Source);
    Assert.True(authorAvatar.IsVisible);
    Assert.NotNull(authorAvatar.Source);
    Assert.False(quote.IsVisible);

    rootProperty.SetValue(binding, RejectedDetailRow("other", true) with { CanReply = true });
    Assert.False(notice.IsVisible);
    Assert.False(appeals.IsVisible);
    Assert.True(body.IsVisible);
    Assert.True(embed.IsVisible);
    Assert.NotNull(thumbnail.Source);
    Assert.True(quote.IsVisible);

    var comments = Assert.Single(Descendants<CollectionView>(page), view =>
        ReferenceEquals(view.ItemsSource, binding.CommentRows));
    var card = Assert.IsAssignableFrom<View>(comments.ItemTemplate.CreateContent());
    var commentQuote = Named<Button>(card, "authored-comment-quote");
    var commentThumbnail = Named<Image>(card, "authored-post-thumbnail");
    var commentAvatar = Named<Image>(card, "post-author-avatar-comment");
    var rejectedCommentRow = RejectedDetailRow("other", false) with { CanReply = true };
    card.BindingContext = rejectedCommentRow with
    {
      Post = rejectedCommentRow.Post with
      {
        CreatedBy = Author(null) with
        {
          ProfileImagePlacement = new TopicImagePlacement("avatar-1", "author-placement", 2),
        },
      },
    };
    Assert.False(commentQuote.IsVisible);
    Assert.Null(commentThumbnail.Source);
    Assert.True(commentAvatar.IsVisible);
    Assert.NotNull(commentAvatar.Source);
    card.BindingContext = RejectedDetailRow("other", true) with { CanReply = true };
    Assert.True(commentQuote.IsVisible);
    Assert.NotNull(commentThumbnail.Source);

    var ancestors = Assert.Single(Descendants<CollectionView>(page), view =>
        ReferenceEquals(view.ItemsSource, binding.AncestorRows));
    var ancestorCard = Assert.IsAssignableFrom<View>(ancestors.ItemTemplate.CreateContent());
    var ancestorThumbnail = Named<Image>(ancestorCard, "authored-post-thumbnail");
    ancestorCard.BindingContext = RejectedDetailRow("other", false);
    Assert.Null(ancestorThumbnail.Source);
    ancestorCard.BindingContext = RejectedDetailRow("other", true);
    Assert.NotNull(ancestorThumbnail.Source);
  }

  [Fact]
  public async Task FeedSessionChangeHidesRejectedBodyBeforeHeldRefreshFinishes()
  {
    InstallPageResources();
    var service = DispatchProxy.Create<IPostsService, FeedPostsProxy>();
    ((FeedPostsProxy)(object)service).Response = new PostsFeedResponse(
        [new EntityReference(null, null, "post", null, null, null, null, null, null, null, null)],
        new PageInfo(null, false, null),
        new Dictionary<string, Post>
        {
          ["post"] = Post(null) with { ClearanceStatus = "rejected" },
        },
        new Dictionary<string, User>(),
        new Dictionary<string, Community>());
    using var model = new PostsListViewModel(service);
    await model.LoadFeedAsync(cancellationToken: TestContext.Current.CancellationToken);
    var session = new MutableSessionStore(new SessionSnapshot(new User(
        "staff", "staff", Roles: ["moderator"])));
    var heldRefresh = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var binding = new PostsPageBinding(model, session, () => heldRefresh.Task);
    var page = new PostsPage { BindingContext = binding };
    var collection = Named<CollectionView>(page, "authored-post-collection");
    Assert.True(Assert.Single(binding.Items).ShowBodyContent);
    var itemsChanged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    binding.PropertyChanged += (_, args) =>
    {
      if (args.PropertyName == nameof(binding.Items)) itemsChanged.TrySetResult();
    };
    session.SetCurrent(SessionSnapshot.Anonymous);
    var refreshTask = binding.RefreshAfterSessionChangedAsync();
    await itemsChanged.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

    Assert.False(heldRefresh.Task.IsCompleted);
    var row = Assert.Single(Assert.IsAssignableFrom<IEnumerable>(collection.ItemsSource).Cast<PostRow>());
    var card = Assert.IsAssignableFrom<View>(collection.ItemTemplate.CreateContent());
    card.BindingContext = row;
    Assert.False(Named<VisualElement>(card, "authored-post-body").IsVisible);
    Assert.True(Named<Label>(card, "authored-post-notice").IsVisible);
    heldRefresh.SetResult();
    await refreshTask;
  }

  [Fact]
  public void StaffProvenanceRendersChannelOnCardsAndClientOnlyOnStaffDetail()
  {
    InstallPageResources();
    var facts = new StaffContentProvenance("mcp",
        new StaffProvenanceOAuthClient("internal-client-42", "Fixture Agent", null, true));
    var post = Post(null) with { StaffProvenance = facts };
    var page = new PostsPage();
    var collection = Named<CollectionView>(page, "authored-post-collection");
    var card = Assert.IsAssignableFrom<View>(collection.ItemTemplate.CreateContent());
    card.BindingContext = PostRows.From(post, null, null, null) with { IsStaffViewer = true };
    var channel = Assert.Single(Descendants<Label>(card), label =>
        label.Text?.Contains("mcp", StringComparison.Ordinal) == true);
    Assert.True(channel.IsVisible);
    Assert.DoesNotContain(Descendants<Label>(card), label =>
        label.Text?.Contains("internal-client-42", StringComparison.Ordinal) == true);

    card.BindingContext = PostRows.From(post, null, null, null);
    Assert.False(channel.IsVisible);
    Assert.Null(channel.Text);

    var detail = new PostDetailPage();
    var binding = new PostDetailPageBinding(
        new CommentThreadViewModel(Proxy<ICommentThreadService>(), "post"),
        new AnonymousSessionStore());
    var root = typeof(PostDetailPageBinding).GetProperty(nameof(PostDetailPageBinding.RootRow))!;
    root.SetValue(binding, DetailRow(null) with { Post = post, IsStaffViewer = true });
    detail.BindingContext = binding;
    var client = Assert.Single(Descendants<Label>(detail), label =>
        label.Text?.Contains("internal-client-42", StringComparison.Ordinal) == true);
    Assert.True(client.IsVisible);
    root.SetValue(binding, DetailRow(null) with { Post = post, IsStaffViewer = false });
    Assert.False(client.IsVisible);
    Assert.Null(client.Text);
  }

  private static readonly UrlEmbedPreview Preview = new("preview", null, null,
      new Uri("https://cdn.example.test/private-preview.png"), null, null);

  private static PostRow RejectedRow(string viewer, bool staff) =>
      PostRows.From(Post(null) with { ClearanceStatus = "rejected", ClearanceReason = "Public reason" },
          null, null, null) with
      {
        ViewerId = viewer,
        IsStaffViewer = staff,
        EmbedPreview = Preview,
      };

  private static PostDetailPageRow RejectedDetailRow(string viewer, bool staff) =>
      DetailRow(null) with
      {
        Post = Post(null) with { ClearanceStatus = "rejected", ClearanceReason = "Public reason" },
        ViewerId = viewer,
        IsStaffViewer = staff,
        EmbedPreview = Preview,
      };

  private static T Named<T>(Element root, string id) where T : VisualElement =>
      Assert.Single(Descendants<T>(root), element => element.AutomationId == id);

  private static User Author(AccountType? type) =>
      new("user-1", "alice", AccountType: type);

  private static Post Post(AccountType? type) =>
      new("post", "discussion", "A discussion", "Body", "user-1", CreatedBy: Author(type));

  private static PostRow Row(AccountType? type) =>
      PostRows.From(Post(type), null, null, null);

  private static PostDetailPageRow DetailRow(AccountType? type) =>
      new(Post(type), "discussion", "post", 1, false, false, false,
          false, false, false, false, false, false, false, false, false, false,
          null, null, null, null, UiLocalization.English);

  private static T Proxy<T>() where T : class => DispatchProxy.Create<T, ThrowingProxy>();

  public class ThrowingProxy : DispatchProxy
  {
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
        throw new NotSupportedException(targetMethod?.Name);
  }

  public class FeedPostsProxy : DispatchProxy
  {
    public PostsFeedResponse Response { get; set; } = null!;
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
        targetMethod?.Name == nameof(IPostsService.FetchFeedAsync)
            ? Task.FromResult(Response)
            : throw new NotSupportedException(targetMethod?.Name);
  }

  private sealed class MutableSessionStore(SessionSnapshot initial) : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged { add { } remove { } }
    public SessionSnapshot Current { get; private set; } = initial;
    public void SetCurrent(SessionSnapshot next) => Current = next;
    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }

  private static IEnumerable<T> Descendants<T>(Element root) where T : Element =>
      root.GetVisualTreeDescendants().OfType<T>();

  private static void InstallPageResources()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    var app = new Application();
    foreach (var key in new[] { "Body", "Eyebrow", "Headline", "Metadata" })
      app.Resources[key] = new Style(typeof(Label));
    app.Resources["ProfilePlacementImageSource96"] = new PlacementImageSourceConverter(
        new AppConfig(new Uri("https://api.test"), ImageBaseUrl: new Uri("https://images.test")), 96);
    app.Resources["UiLocalizedValue"] = new UiLocalizedValueConverter(UiLocalization.English);
    app.Resources["UiLocaleVersion"] = new UiLocaleVersion(new UiLocaleController(new DeviceLanguage()));
  }

  private sealed class DeviceLanguage : IDeviceLanguageProvider
  {
    public IReadOnlyList<string> PreferredLanguages => ["en"];
  }

  private sealed class ImmediateDispatcherProvider : IDispatcherProvider
  {
    public IDispatcher GetForCurrentThread() => new ImmediateDispatcher();
  }

  private sealed class ImmediateDispatcher : IDispatcher
  {
    public bool IsDispatchRequired => false;
    public bool Dispatch(Action action) { action(); return true; }
    public bool DispatchDelayed(TimeSpan delay, Action action) { action(); return true; }
    public IDispatcherTimer CreateTimer() => throw new NotSupportedException();
  }

  private sealed class AnonymousSessionStore : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged { add { } remove { } }
    public SessionSnapshot Current => SessionSnapshot.Anonymous;
    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }
}
