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

  [Fact]
  public async Task MountedCreateAndOtherRotateButtonsStayDisabledThroughOneTimeSecretDisclosure()
  {
    PrepareMaui();
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
    var service = DispatchProxy.Create<ISettingsService, LifecycleSettingsService>();
    var fake = (LifecycleSettingsService)service;
    var firstKey = Key("key-a", DateTimeOffset.UtcNow.AddDays(30));
    var secondKey = Key("key-b", DateTimeOffset.UtcNow.AddDays(30));
    fake.Keys = [firstKey, secondKey];
    var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    var response = new TaskCompletionSource<ApiKeyCreationResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    fake.RotateOverride = id =>
    {
      if (id != firstKey.Id) throw new InvalidOperationException("Second key dispatched while the first secret is pending.");
      started.TrySetResult(true);
      return response.Task;
    };
    using var model = new SettingsViewModel(service) { ApiKeyLabel = "Reader" };
    await model.LoadAsync(cancellation.Token);
    model.SetApiKeyScopeSelected("rss:read", true);
    var view = new SettingsApiKeyLifecycleView { BindingContext = model };
    _ = new ContentPage { Content = view };
    var create = Find<Button>(view, "api-key-create");
    var rows = Find<CollectionView>(view, "api-key-lifecycle-rows");
    Assert.True(create.IsEnabled);
    Assert.True(Find<Button>(RenderRow(rows, model.LocalizedApiKeys[1]), "api-key-rotate").IsEnabled);

    var first = model.RotateApiKeyAsync(firstKey, cancellation.Token);
    try
    {
      await started.Task.WaitAsync(cancellation.Token);
      Assert.False(create.IsEnabled);
      Assert.False(Find<Button>(RenderRow(rows, model.LocalizedApiKeys[1]), "api-key-rotate").IsEnabled);
      Assert.Equal(1, fake.RotateCount);
    }
    finally
    {
      response.TrySetResult(new ApiKeyCreationResponse(firstKey, "raw-a"));
      await first;
    }

    Assert.Equal("raw-a", model.ApiKeySecret);
    Assert.False(create.IsEnabled);
    Assert.False(Find<Button>(RenderRow(rows, model.LocalizedApiKeys[1]), "api-key-rotate").IsEnabled);
    Find<Button>(view, "api-key-secret-dismiss").SendClicked();
    Assert.True(create.IsEnabled);
    Assert.True(Find<Button>(RenderRow(rows, model.LocalizedApiKeys[1]), "api-key-rotate").IsEnabled);
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
    public Func<string, Task<ApiKeyCreationResponse>>? RotateOverride { get; set; }
    public int RotateCount { get; private set; }

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
      nameof(ISettingsService.FetchScopeCatalogAsync) => Task.FromResult(new ScopeCatalogResponse([
          new ScopeCatalogEntry("rss:read", "api", "rss", "read", ["api-key"], null, null),
      ])),
      nameof(ISettingsService.FetchOAuthGrantsAsync) => Task.FromResult(new OAuthGrantListResponse([], new PageInfo(null, false, null))),
      nameof(ISettingsService.RotateApiKeyAsync) => Rotate((string)args![0]!),
      _ => throw new NotSupportedException(targetMethod?.Name),
    };

    private Task<ApiKeyCreationResponse> Rotate(string id)
    {
      RotateCount++;
      if (RotateOverride is { } rotate) return rotate(id);
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
