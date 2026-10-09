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
public sealed class SettingsOAuthGrantsViewTests
{
  [Fact]
  public async Task RenderedGrantRevokeWaitsForConfirmationAndCancelPreservesGrant()
  {
    PrepareMaui();
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
    var service = DispatchProxy.Create<ISettingsService, GrantSettingsService>();
    var fake = (GrantSettingsService)service;
    var grant = new OAuthGrant("grant-1", new OAuthGrantClient("client-1", "app-1", "Trusted reader", true),
        "rss", ["rss:read"], DateTimeOffset.UtcNow.AddDays(-1), null);
    var other = grant with { Id = "grant-2", Client = grant.Client with { ClientName = "Another app" } };
    fake.Grants = [grant, other];
    using var model = new SettingsViewModel(service);
    await model.LoadAsync(cancellation.Token);
    var view = new SettingsOAuthGrantsView { BindingContext = model };
    _ = new ContentPage { Content = view };
    var rowHost = Assert.Single(Descendants<Layout>(view), layout =>
        BindableLayout.GetItemsSource(layout)?.Cast<object>().Any(item => item is SettingsOAuthGrantRow) == true);
    var row = rowHost.Children.OfType<Element>().FirstOrDefault(child =>
        child.BindingContext is SettingsOAuthGrantRow item && item.ProtocolValue.Id == grant.Id);
    if (row is null)
    {
      row = Assert.IsAssignableFrom<View>(BindableLayout.GetItemTemplate(rowHost)!.CreateContent());
      row.BindingContext = Assert.Single(model.LocalizedOAuthGrants, item => item.ProtocolValue.Id == grant.Id);
      rowHost.Children.Add((View)row);
    }
    var revoke = Find<Button>(row, "oauth-grant-revoke");
    var notice = Find<Label>(view, "oauth-grant-notice");
    Assert.Equal("Trusted reader", Assert.Single(Descendants<Label>(row), label =>
        label.Text == "Trusted reader").Text);

    (string Title, string Message, string Accept, string Cancel)? canceledPrompt = null;
    view.ConfirmRevocationAsync = (title, message, accept, cancel) =>
    {
      canceledPrompt = (title, message, accept, cancel);
      return Task.FromResult(false);
    };
    revoke.SendClicked();
    Assert.NotNull(canceledPrompt);
    Assert.Equal(ExpectedPrompt("Trusted reader"), canceledPrompt.Value);
    Assert.Empty(fake.RevokedIds);
    Assert.Equal(2, model.LocalizedOAuthGrants.Count);

    var dialog = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    var prompted = new TaskCompletionSource<(string Title, string Message, string Accept, string Cancel)>(
        TaskCreationOptions.RunContinuationsAsynchronously);
    view.ConfirmRevocationAsync = (title, message, accept, cancel) =>
    {
      prompted.TrySetResult((title, message, accept, cancel));
      return dialog.Task;
    };
    revoke.SendClicked();
    try
    {
      Assert.Equal(ExpectedPrompt("Trusted reader"), await prompted.Task.WaitAsync(cancellation.Token));
      Assert.Empty(fake.RevokedIds);
      dialog.SetResult(true);
      var success = UiLocalization.English.Localize(UiMessageKey.NativeCredentialsGrantRevoked);
      await WaitUntilAsync(() => fake.RevokedIds.Count == 1 && model.LocalizedOAuthGrants.Count == 1 &&
          notice.Text == success,
          cancellation.Token);
    }
    finally { dialog.TrySetResult(false); }
    Assert.Equal(grant.Id, Assert.Single(fake.RevokedIds));
    Assert.Equal(other.Id, Assert.Single(model.LocalizedOAuthGrants).ProtocolValue.Id);
    Assert.Equal(UiLocalization.English.Localize(UiMessageKey.NativeCredentialsGrantRevoked),
        notice.Text);
  }

  private static (string Title, string Message, string Accept, string Cancel) ExpectedPrompt(string clientName)
  {
    var localization = UiLocalization.English;
    var revoke = localization.Localize(UiMessageKey.NativeSwiftSettingsRevoke);
    return (revoke,
        localization.Format(UiMessageKey.NativeCredentialsConfirmRevokeGrant, ("app", clientName)),
        revoke, localization.Localize(UiMessageKey.CommonCancel));
  }

  private static async Task WaitUntilAsync(Func<bool> condition, CancellationToken cancellationToken)
  {
    while (!condition())
    {
      cancellationToken.ThrowIfCancellationRequested();
      await Task.Yield();
    }
  }

  private static void PrepareMaui()
  {
    UiCopy.UseLocalization(UiLocalization.English);
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
    foreach (var child in ((IVisualTreeElement)root).GetVisualChildren().OfType<Element>().ToArray())
    {
      if (child is T match) yield return match;
      foreach (var descendant in Descendants<T>(child)) yield return descendant;
    }
  }

  public class GrantSettingsService : DispatchProxy
  {
    public IReadOnlyList<OAuthGrant> Grants { get; set; } = [];
    public List<string> RevokedIds { get; } = [];

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod?.Name switch
    {
      nameof(ISettingsService.FetchMyIdentityAsync) => Task.FromResult(new MyIdentityResponse(new User("user-1", "alice"))),
      nameof(ISettingsService.FetchUserAsync) => Task.FromResult(new UserResponse(new User("user-1", "alice", Roles: ["member"]))),
      nameof(ISettingsService.FetchMyProfileAsync) => Task.FromResult(new MyProfileResponse(new MyProfile("profile-1", ""))),
      nameof(ISettingsService.FetchProfileLinksAsync) => Task.FromResult(new ProfileLinkListResponse([], new PageInfo(null, false, null))),
      nameof(ISettingsService.FetchApiKeysAsync) => Task.FromResult(new ApiKeyListResponse([], new PageInfo(null, false, null))),
      nameof(ISettingsService.FetchAuthSessionsAsync) => Task.FromResult(new AuthSessionListResponse([], new PageInfo(null, false, null))),
      nameof(ISettingsService.FetchMembershipPlansAsync) => Task.FromResult(new MembershipPlansResponse([])),
      nameof(ISettingsService.FetchMembershipAsync) => Task.FromResult<MembershipResponse?>(null),
      nameof(ISettingsService.FetchPushSubscriptionsAsync) => Task.FromResult(new WebPushSubscriptionListResponse([], new PageInfo(null, false, null))),
      nameof(ISettingsService.FetchUserDataRequestAsync) => Task.FromResult<UserDataRequestResponse?>(null),
      nameof(ISettingsService.FetchScopeCatalogAsync) => Task.FromResult(new ScopeCatalogResponse([])),
      nameof(ISettingsService.FetchOAuthGrantsAsync) => Task.FromResult(new OAuthGrantListResponse(Grants, new PageInfo(null, false, null))),
      nameof(ISettingsService.RevokeOAuthGrantAsync) => Revoke((string)args![0]!),
      _ => throw new NotSupportedException(targetMethod?.Name),
    };

    private Task Revoke(string id)
    {
      RevokedIds.Add(id);
      return Task.CompletedTask;
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
