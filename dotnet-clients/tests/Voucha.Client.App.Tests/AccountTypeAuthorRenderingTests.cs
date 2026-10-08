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

  private static IEnumerable<T> Descendants<T>(Element root) where T : Element =>
      root.GetVisualTreeDescendants().OfType<T>();

  private static void InstallPageResources()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    var app = new Application();
    foreach (var key in new[] { "Body", "Eyebrow", "Headline", "Metadata" })
      app.Resources[key] = new Style(typeof(Label));
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
