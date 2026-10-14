using System.Reflection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
using Voucha.Client.Core;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Posts;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class PostAuthorAvatarRenderingTests
{
  private static readonly TopicImagePlacement Placement =
      new("00000000-0000-7000-8000-000000000201", "00000000-0000-7000-8000-000000000301", 2);

  private static readonly Uri ExpectedUrl = new(
      "https://images.test/images/placements/00000000-0000-7000-8000-000000000301/2/00000000-0000-7000-8000-000000000201?w=96");

  [Fact]
  public void RootAuthorImageUsesPlacementAndHidesForLegacyAnonymousAndDeletedAuthors()
  {
    InstallPageResources();
    var page = new PostDetailPage();
    var binding = Binding();
    var rootProperty = typeof(PostDetailPageBinding).GetProperty(nameof(PostDetailPageBinding.RootRow))!;
    page.BindingContext = binding;
    var image = Named<Image>(page, "post-author-avatar-root");

    rootProperty.SetValue(binding, Row(Post(Author(Placement))));
    Assert.Equal(ExpectedUrl, Assert.IsType<UriImageSource>(image.Source).Uri);
    Assert.True(image.IsVisible);
    AssertDecorative(image);
    Assert.Contains(Descendants<Label>(page), label => label.Text == "Alice");

    foreach (var post in new[]
    {
      Post(Author(null, legacyImageId: "legacy-image")),
      Post(Author(Placement)) with { IsAnonymous = true },
      Post(Author(Placement)) with { DeletedAt = DateTimeOffset.UnixEpoch },
    })
    {
      rootProperty.SetValue(binding, Row(post));
      Assert.Null(image.Source);
      Assert.False(image.IsVisible);
    }
  }

  [Fact]
  public void AncestorAndCommentTemplatesRenderPlacementAndClearItForRestrictedAuthors()
  {
    InstallPageResources();
    var page = new PostDetailPage();
    var binding = Binding();
    page.BindingContext = binding;

    var ancestorCollection = Assert.Single(Descendants<CollectionView>(page), view =>
        ReferenceEquals(view.ItemsSource, binding.AncestorRows));
    var ancestor = Assert.IsAssignableFrom<View>(ancestorCollection.ItemTemplate.CreateContent());
    var ancestorImage = Named<Image>(ancestor, "post-author-avatar-ancestor");
    AssertAvatar(ancestor, ancestorImage);
    ancestor.BindingContext = Row(Post(Author(null, legacyImageId: "legacy-image")));
    AssertNoAvatar(ancestorImage);

    var commentCollection = Assert.Single(Descendants<CollectionView>(page), view =>
        ReferenceEquals(view.ItemsSource, binding.CommentRows));
    var comment = Assert.IsAssignableFrom<View>(commentCollection.ItemTemplate.CreateContent());
    var commentImage = Named<Image>(comment, "post-author-avatar-comment");
    AssertAvatar(comment, commentImage);
    comment.BindingContext = Row(Post(Author(Placement)) with { IsAnonymous = true });
    AssertNoAvatar(commentImage);
    comment.BindingContext = Row(Post(Author(Placement)) with { DeletedAt = DateTimeOffset.UnixEpoch });
    AssertNoAvatar(commentImage);
  }

  private static void AssertAvatar(View template, Image image)
  {
    template.BindingContext = Row(Post(Author(Placement)));
    Assert.Equal(ExpectedUrl, Assert.IsType<UriImageSource>(image.Source).Uri);
    Assert.True(image.IsVisible);
    AssertDecorative(image);
    Assert.Contains(Descendants<Label>(template), label => label.Text == "Alice");
  }

  private static void AssertDecorative(Image image)
  {
    Assert.False(AutomationProperties.GetIsInAccessibleTree(image));
    Assert.Null(SemanticProperties.GetDescription(image));
  }

  private static void AssertNoAvatar(Image image)
  {
    Assert.Null(image.Source);
    Assert.False(image.IsVisible);
  }

  private static PostDetailPageBinding Binding() => new(
      new CommentThreadViewModel(Proxy<ICommentThreadService>(), "root"), new AnonymousSessionStore());

  private static PostDetailPageRow Row(Post post) => new(
      post, "discussion", "root", 1, false, false, false, false, false, false, false,
      false, false, false, false, false, false, null, null, null, null, UiLocalization.English);

  private static Post Post(User author) => new(
      "post", "discussion", "Post title", "Body", author.Id, CreatedBy: author);

  private static User Author(TopicImagePlacement? placement, string? legacyImageId = null) =>
      new("author", "Alice", ProfileImageId: legacyImageId, ProfileImagePlacement: placement);

  private static T Proxy<T>() where T : class => DispatchProxy.Create<T, UnusedProxy>();

  private static IEnumerable<T> Descendants<T>(Element root) where T : Element =>
      root.GetVisualTreeDescendants().OfType<T>();

  private static T Named<T>(Element root, string automationId) where T : Element =>
      Assert.Single(Descendants<T>(root), element => element.AutomationId == automationId);

  private static void InstallPageResources()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    var app = new Application();
    foreach (var key in new[] { "Body", "Eyebrow", "Headline", "Metadata" })
      app.Resources[key] = new Style(typeof(Label));
    app.Resources["UiLocalizedValue"] = new UiLocalizedValueConverter(UiLocalization.English);
    app.Resources["UiLocaleVersion"] = new UiLocaleVersion(new UiLocaleController(new DeviceLanguage()));
    app.Resources["ProfilePlacementImageSource96"] = new PlacementImageSourceConverter(
        new AppConfig(new Uri("https://api.test"), ImageBaseUrl: new Uri("https://images.test")), 96);
  }

  public class UnusedProxy : DispatchProxy
  {
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
        throw new NotSupportedException(targetMethod?.Name);
  }

  private sealed class AnonymousSessionStore : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged { add { } remove { } }
    public SessionSnapshot Current => SessionSnapshot.Anonymous;
    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
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
}
