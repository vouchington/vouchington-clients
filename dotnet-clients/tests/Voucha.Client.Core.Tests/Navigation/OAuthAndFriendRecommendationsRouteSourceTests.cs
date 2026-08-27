using System.Runtime.CompilerServices;
using Xunit;

namespace Voucha.Client.Core.Tests.Navigation;

public sealed class OAuthAndFriendRecommendationsRouteSourceTests
{
  [Fact]
  public void AppShellOwnsExactOAuthCallbackAndRecommendationRoutes()
  {
    var appLinks = Source("AppShell.AppLinks.cs");
    var authOAuth = Source("Pages", "AuthPage.OAuth.cs");
    var authXaml = Source("Pages", "AuthPage.xaml");
    var dispatcher = Source("AppLinkDispatcher.cs");
    var friends = Source("AppShell.Friends.cs");
    var notifications = Source("Pages", "NotificationsPage.xaml.cs");
    var page = Source("Pages", "FriendRecommendationsPage.xaml");
    var persistence = Source("MauiNativeOAuthAuthorizationPersistence.cs");
    var settingsOAuth = Source("Pages", "SettingsPage.OAuth.cs");
    var settingsXaml = Source("Pages", "SettingsPage.xaml");
    var usersBrowse = Source("AppShell.UsersBrowse.cs");

    Assert.Contains("url.AbsolutePath == \"/oauth/callback\"", appLinks, StringComparison.Ordinal);
    Assert.Contains("HandleOAuthCallbackAsync(url)", appLinks, StringComparison.Ordinal);
    Assert.Contains(
        "var outcome = await coordinator.HandleCallbackAsync(url)",
        appLinks,
        StringComparison.Ordinal);
    Assert.Contains(
        "outcome.Purpose == OAuthAuthorizationPurpose.Connect",
        appLinks,
        StringComparison.Ordinal);
    Assert.DoesNotContain(
        "coordinator.Result?.Purpose ?? coordinator.Pending?.Purpose",
        appLinks,
        StringComparison.Ordinal);
    Assert.Contains(
        "GetRequiredService<NativeOAuthAuthorizationCoordinator>()",
        appLinks,
        StringComparison.Ordinal);
    Assert.Contains("DispatchFireAndForget", dispatcher, StringComparison.Ordinal);
    Assert.Contains("ObserveDispatchAsync", dispatcher, StringComparison.Ordinal);
    Assert.Contains("catch (Exception ex)", dispatcher, StringComparison.Ordinal);
    Assert.Contains("Debug.WriteLine(ex)", dispatcher, StringComparison.Ordinal);
    Assert.Contains("await AppLinkDispatcher.DispatchAsync", notifications, StringComparison.Ordinal);
    Assert.Contains("CanCancelPendingAuthorization", authOAuth, StringComparison.Ordinal);
    Assert.Contains(
        "result.Kind == NativeOAuthAuthorizationResultKind.Expired",
        authOAuth,
        StringComparison.Ordinal);
    Assert.Contains("viewModel.ClearMfaChallenge()", authOAuth, StringComparison.Ordinal);
    Assert.Contains(
        "oauthCoordinator.State == NativeOAuthAuthorizationState.Cancelled",
        authOAuth,
        StringComparison.Ordinal);
    AssertCapabilityRetrySurface(authOAuth, authXaml, "RetryOAuthCapabilitiesButton");
    Assert.Contains(
        "new NativeOAuthAuthorizationSnapshot(snapshot?.Pending, snapshot?.Result)",
        persistence,
        StringComparison.Ordinal);
    Assert.DoesNotContain("ReadPendingAsync", persistence, StringComparison.Ordinal);
    Assert.DoesNotContain("ReadResultAsync", persistence, StringComparison.Ordinal);
    Assert.Contains("CanCancelPendingAuthorization", settingsOAuth, StringComparison.Ordinal);
    Assert.Contains(
        "result.Kind == NativeOAuthAuthorizationResultKind.Expired",
        settingsOAuth,
        StringComparison.Ordinal);
    Assert.Contains(
        "NativeOAuthAuthorizationState.Disconnecting",
        settingsOAuth,
        StringComparison.Ordinal);
    AssertCapabilityRetrySurface(
        settingsOAuth,
        settingsXaml,
        "RetryOAuthAccountCapabilitiesButton");
    AssertOAuthStatusCopyIsProviderNeutral(authOAuth);
    AssertOAuthStatusCopyIsProviderNeutral(settingsOAuth);
    Assert.Contains(
        "resolution.Match?.Path == \"/my/friend-recommendations\"",
        friends,
        StringComparison.Ordinal);
    Assert.Contains(
        "GetRequiredService<FriendRecommendationsPage>()",
        friends,
        StringComparison.Ordinal);
    Assert.DoesNotContain(
        "path is \"/users\" or \"/my/friend-recommendations\"",
        friends,
        StringComparison.Ordinal);
    Assert.DoesNotContain("IsGenericFriendsPath", friends, StringComparison.Ordinal);
    Assert.Contains(
        "GetRequiredService<UsersBrowsePage>()",
        usersBrowse,
        StringComparison.Ordinal);
    Assert.Contains("controls:HybridPaginationControl", page, StringComparison.Ordinal);
    Assert.Contains("Text=\"{Binding ProviderPresentation}\"", page, StringComparison.Ordinal);
    Assert.Contains("Clicked=\"OnConnectAccountsClicked\"", page, StringComparison.Ordinal);
    Assert.Contains("Clicked=\"OnFollowClicked\"", page, StringComparison.Ordinal);
    Assert.Contains("Clicked=\"OnDismissClicked\"", page, StringComparison.Ordinal);
    Assert.Contains(
        "Text=\"{DynamicResource native.dotnet.bookmarks.dismissedFriendRecommendations}\"",
        page,
        StringComparison.Ordinal);
    Assert.Contains("Grid.Row=\"2\"", page, StringComparison.Ordinal);
    Assert.Contains("Clicked=\"OnDismissedRecommendationsClicked\"", page, StringComparison.Ordinal);
    var pageCodeBehind = Source("Pages", "FriendRecommendationsPage.xaml.cs");
    Assert.Contains(
        "OpenNativePathAsync(\"/my/friend-recommendations/dismissed\")",
        pageCodeBehind,
        StringComparison.Ordinal);
  }

  private static void AssertOAuthStatusCopyIsProviderNeutral(string source)
  {
    Assert.Contains("NativeSwiftSettingsOauthFinalizing", source, StringComparison.Ordinal);
    Assert.Contains("NativeSwiftSettingsOauthExpired", source, StringComparison.Ordinal);
    Assert.Contains("NativeSwiftSettingsOauthFailed", source, StringComparison.Ordinal);
    Assert.DoesNotContain("NativeSwiftSettingsBlueskyFinalizing", source, StringComparison.Ordinal);
    Assert.DoesNotContain("NativeSwiftSettingsBlueskyExpired", source, StringComparison.Ordinal);
    Assert.DoesNotContain("NativeSwiftSettingsBlueskyLinkFailed", source, StringComparison.Ordinal);
  }

  private static void AssertCapabilityRetrySurface(
      string codeBehind,
      string xaml,
      string buttonName)
  {
    Assert.Contains($"x:Name=\"{buttonName}\"", xaml, StringComparison.Ordinal);
    Assert.Contains(
        "Text=\"{DynamicResource native.common.retry}\"",
        xaml,
        StringComparison.Ordinal);
    Assert.Contains("Clicked=\"OnRetryOAuthCapabilitiesClicked\"", xaml, StringComparison.Ordinal);
    Assert.Contains($"{buttonName}.IsVisible", codeBehind, StringComparison.Ordinal);
    Assert.Contains("oauthCoordinator.CanRetryCapabilityLoading", codeBehind, StringComparison.Ordinal);
    Assert.Contains("OnRetryOAuthCapabilitiesClicked", codeBehind, StringComparison.Ordinal);
    Assert.Contains("oauthCoordinator.LoadCapabilitiesAsync()", codeBehind, StringComparison.Ordinal);
  }

  private static string Source(params string[] parts)
  {
    var path = Path.Combine(
        new[] { RepoRoot(), "dotnet-clients", "src", "Voucha.Client.App" }
            .Concat(parts)
            .ToArray());
    return File.ReadAllText(path);
  }

  private static string RepoRoot([CallerFilePath] string sourcePath = "")
  {
    var directory = new DirectoryInfo(Path.GetDirectoryName(sourcePath)!);
    while (directory is not null)
    {
      if (Directory.Exists(Path.Combine(directory.FullName, "dotnet-clients")))
      {
        return directory.FullName;
      }
      directory = directory.Parent;
    }
    throw new DirectoryNotFoundException("Could not find repository root.");
  }
}
