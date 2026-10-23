using Sentry;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Xunit;

namespace Voucha.Client.App.Tests;

public sealed class SentrySdkBoundaryTests
{
  [Fact]
  public void EventDropsNestedExtrasThatCanCarrySecrets()
  {
    var sentryEvent = new SentryEvent();
    sentryEvent.SetExtra(
        "profile",
        new Dictionary<string, object?> { ["access_token"] = "scrub-me", ["request_body"] = "private-body" });
    sentryEvent.SetExtra("screen", "settings");
    sentryEvent.SetExtra("request_url", "https://voucha.ai/search?q=private");

    var scrubbed = SentryEventProjection.Scrub(sentryEvent);

    Assert.False(scrubbed.Extra.ContainsKey("profile"));
    Assert.Equal("settings", scrubbed.Extra["screen"]);
    Assert.Equal("https://voucha.ai/search", scrubbed.Extra["request_url"]);
  }

  [Fact]
  public void TransactionKeepsOnlyTheAccountMarker()
  {
    SentryAccountSync.ResetBoundAccount();
    var transaction = new SentryTransaction("GET /search", "http");
    transaction.User.Other["email"] = "person@example.com";
    transaction.User.Other["access_token"] = "secret";
    transaction.User.Other[SentryAccountSync.AccountMarker] = "account-1";
    transaction.SetData("profile", new Dictionary<string, object?> { ["access_token"] = "scrub-me" });

    SentryTransactionProjection.Scrub(transaction);

    Assert.Equal("account-1", transaction.User.Id);
    Assert.Null(transaction.User.Email);
    Assert.Equal("account-1", transaction.User.Other[SentryAccountSync.AccountMarker]);
    Assert.False(transaction.User.Other.ContainsKey("email"));
    Assert.False(transaction.User.Other.ContainsKey("access_token"));
    Assert.False(transaction.Data.ContainsKey("profile"));
  }

  [Fact]
  public void TransactionWithoutAMarkerDropsIdentityFields()
  {
    SentryAccountSync.ResetBoundAccount();
    var transaction = new SentryTransaction("GET /search", "http");
    transaction.User.Id = "installation";
    transaction.User.Email = "person@example.com";
    transaction.User.Other["name"] = "Ada";

    SentryTransactionProjection.Scrub(transaction);

    Assert.Null(transaction.User.Id);
    Assert.Null(transaction.User.Email);
    Assert.Empty(transaction.User.Other);
  }

  [Fact]
  public void LateSessionNotificationFollowsTheCurrentAccount()
  {
    SentryAccountSync.ResetBoundAccount();
    var store = new RecordingSessionStore();
    var applied = new List<string?>();
    store.SessionChanged += (_, _) =>
        SentryAccountSync.Reconcile(store, decision => applied.Add(decision.AccountId));

    store.Current = new SessionSnapshot(new User("account-1", "ada"));
    store.Raise(store.Current);
    store.Current = SessionSnapshot.Anonymous;
    store.Raise(new SessionSnapshot(new User("account-1", "ada")));
    store.Current = new SessionSnapshot(new User("account-2", "ada"));
    store.Raise(new SessionSnapshot(new User("account-1", "ada")));

    Assert.Equal(new string?[] { "account-1", null, "account-2" }, applied);
  }

  private sealed class RecordingSessionStore : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged;

    public SessionSnapshot Current { get; set; } = SessionSnapshot.Anonymous;

    public void Raise(SessionSnapshot snapshot) =>
        SessionChanged?.Invoke(this, new SessionChangedEventArgs(snapshot));

    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }
}
