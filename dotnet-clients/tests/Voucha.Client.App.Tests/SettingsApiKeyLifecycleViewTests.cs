using System.Collections;
using System.Reflection;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Settings;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class SettingsApiKeyLifecycleViewTests
{
  [Fact]
  public async Task RenderedLifecycleControlsFollowOwnerRoleAndServerKeyState()
  {
    PrepareMaui();
    var service = DispatchProxy.Create<ISettingsService, LifecycleSettingsService>();
    var fake = (LifecycleSettingsService)service;
    fake.Roles = ["administrator"];
    fake.Keys = [Key("admin-old", expiresAt: null)];
    using var model = new SettingsViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    var view = new SettingsApiKeyLifecycleView { BindingContext = model };
    _ = new ContentPage { Content = view };

    var lifetime = Find<Picker>(view, "api-key-lifetime");
    Assert.Equal(["30", "90"], lifetime.ItemsSource.Cast<UiProtocolOption>().Select(option => option.ProtocolValue));
    Assert.Equal("30", Assert.IsType<UiProtocolOption>(lifetime.SelectedItem).ProtocolValue);
    var rows = Find<CollectionView>(view, "api-key-lifecycle-rows");
    var adminRow = RenderRow(rows, Assert.Single(model.LocalizedApiKeys));
    Assert.Contains("invalid", Find<Label>(adminRow, "api-key-status").Text, StringComparison.OrdinalIgnoreCase);
    Assert.True(Find<Button>(adminRow, "api-key-rotate").IsVisible);

    fake.Roles = ["member"];
    fake.Keys = [Key("ordinary-expired", DateTimeOffset.UtcNow.AddDays(-1))];
    await model.LoadAsync(TestContext.Current.CancellationToken);
    Assert.Equal(["30", "90", "365", "none"], lifetime.ItemsSource.Cast<UiProtocolOption>().Select(option => option.ProtocolValue));
    Assert.Equal("90", Assert.IsType<UiProtocolOption>(lifetime.SelectedItem).ProtocolValue);
    var expiredRow = RenderRow(rows, Assert.Single(model.LocalizedApiKeys));
    Assert.Contains("expired", Find<Label>(expiredRow, "api-key-status").Text, StringComparison.OrdinalIgnoreCase);
    Assert.False(Find<Button>(expiredRow, "api-key-rotate").IsVisible);
    Assert.False(string.IsNullOrWhiteSpace(Find<Label>(expiredRow, "api-key-expiry").Text));

    fake.Keys = [Key("ordinary-active", DateTimeOffset.UtcNow.AddDays(30))];
    await model.LoadAsync(TestContext.Current.CancellationToken);
    var activeRow = RenderRow(rows, Assert.Single(model.LocalizedApiKeys));
    var rotate = Find<Button>(activeRow, "api-key-rotate");
    Assert.True(rotate.IsVisible);
    rotate.SendClicked();
    Assert.Equal("rotated-secret", model.ApiKeySecret);
    var dismiss = Find<Button>(view, "api-key-secret-dismiss");
    Assert.True(dismiss.IsVisible);
    Assert.False(string.IsNullOrWhiteSpace(Find<Label>(view, "api-key-secret").Text));
    dismiss.SendClicked();
    Assert.False(dismiss.IsVisible);
    Assert.Null(model.ApiKeySecret);
  }

  private static ApiKey Key(string id, DateTimeOffset? expiresAt) => new(
      id, "user-1", "rk_key", "rss", "Reader", ["rss:read"],
      DateTimeOffset.UtcNow.AddDays(-1), null, null, DateTimeOffset.UtcNow.AddDays(-1),
      ExpiresAt: expiresAt);

  private static Element RenderRow(CollectionView rows, SettingsApiKeyRow row)
  {
    var element = Assert.IsAssignableFrom<Element>(rows.ItemTemplate.CreateContent());
    element.BindingContext = row;
    return element;
  }

  private static void PrepareMaui()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    var application = new Application
    {
      Resources =
      {
        ["Headline"] = new Style(typeof(Label)),
        ["Body"] = new Style(typeof(Label)),
        ["Metadata"] = new Style(typeof(Label)),
      },
    };
    foreach (var key in UiMessageKey.All)
      application.Resources[key.Value] = UiLocalization.English.Localize(key);
  }

  private static T Find<T>(Element root, string automationId) where T : Element =>
      Assert.Single(Descendants<T>(root), element => element.AutomationId == automationId);

  private static IEnumerable<T> Descendants<T>(Element root) where T : Element
  {
    foreach (var child in ((IVisualTreeElement)root).GetVisualChildren().OfType<Element>())
    {
      if (child is T match) yield return match;
      foreach (var descendant in Descendants<T>(child)) yield return descendant;
    }
  }

  public class LifecycleSettingsService : DispatchProxy
  {
    public IReadOnlyList<string> Roles { get; set; } = ["member"];
    public IReadOnlyList<ApiKey> Keys { get; set; } = [];

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod?.Name switch
    {
      nameof(ISettingsService.FetchMyIdentityAsync) => Task.FromResult(new MyIdentityResponse(new User("user-1", "alice"))),
      nameof(ISettingsService.FetchUserAsync) => Task.FromResult(new UserResponse(new User("user-1", "alice", Roles: Roles))),
      nameof(ISettingsService.FetchMyProfileAsync) => Task.FromResult(new MyProfileResponse(new MyProfile("profile-1", ""))),
      nameof(ISettingsService.FetchProfileLinksAsync) => Task.FromResult(new ProfileLinkListResponse([], new PageInfo(null, false, null))),
      nameof(ISettingsService.FetchApiKeysAsync) => Task.FromResult(new ApiKeyListResponse(Keys, new PageInfo(null, false, null))),
      nameof(ISettingsService.FetchAuthSessionsAsync) => Task.FromResult(new AuthSessionListResponse([], new PageInfo(null, false, null))),
      nameof(ISettingsService.FetchMembershipPlansAsync) => Task.FromResult(new MembershipPlansResponse([])),
      nameof(ISettingsService.FetchMembershipAsync) => Task.FromResult<MembershipResponse?>(null),
      nameof(ISettingsService.FetchPushSubscriptionsAsync) => Task.FromResult(new WebPushSubscriptionListResponse([], new PageInfo(null, false, null))),
      nameof(ISettingsService.FetchUserDataRequestAsync) => Task.FromResult<UserDataRequestResponse?>(null),
      nameof(ISettingsService.FetchScopeCatalogAsync) => Task.FromResult(new ScopeCatalogResponse([])),
      nameof(ISettingsService.FetchOAuthGrantsAsync) => Task.FromResult(new OAuthGrantListResponse([], new PageInfo(null, false, null))),
      nameof(ISettingsService.RotateApiKeyAsync) => Rotate(),
      _ => throw new NotSupportedException(targetMethod?.Name),
    };

    private Task<ApiKeyCreationResponse> Rotate()
    {
      var replacement = Keys[0] with { Id = "replacement" };
      Keys = [replacement];
      return Task.FromResult(new ApiKeyCreationResponse(replacement, "rotated-secret"));
    }
  }

  private sealed class ImmediateDispatcherProvider : IDispatcherProvider
  {
    public IDispatcher GetForCurrentThread() => ImmediateDispatcher.Instance;
  }

  private sealed class ImmediateDispatcher : IDispatcher
  {
    public static ImmediateDispatcher Instance { get; } = new();
    public bool IsDispatchRequired => false;
    public bool Dispatch(Action action) { action(); return true; }
    public bool DispatchDelayed(TimeSpan delay, Action action) { action(); return true; }
    public IDispatcherTimer CreateTimer() => throw new NotSupportedException();
  }
}
