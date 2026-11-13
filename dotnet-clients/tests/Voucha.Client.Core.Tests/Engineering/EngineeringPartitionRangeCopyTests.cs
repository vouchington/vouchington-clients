using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Engineering;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Engineering;

public sealed class EngineeringPartitionRangeCopyTests
{
  private static readonly IUiLocalization English = UiLocalization.English;

  [Theory]
  [InlineData(false, false)]
  [InlineData(false, true)]
  [InlineData(true, false)]
  [InlineData(true, true)]
  public void ConfirmationCopyIsDistinctPerFamilyAndActionAndCarriesFrozenBounds(bool requestFamily, bool retire)
  {
    using var vm = Create(out _);
    var range = new PsqlClassifierRange("2020-02-01", "2020-02-02", 5);
    var body = new PsqlJobBody(
        retire ? "cleanupPartitions" : "createPartitions",
        ClassifierRange: requestFamily ? null : range,
        RequestRange: requestFamily ? range : null);
    var key = (requestFamily, retire) switch
    {
      (false, false) => UiMessageKey.EngineeringClassifierRangeConfirmCreate,
      (false, true) => UiMessageKey.EngineeringClassifierRangeConfirmRetire,
      (true, false) => UiMessageKey.EngineeringRequestRangeConfirmCreate,
      (true, true) => UiMessageKey.EngineeringRequestRangeConfirmRetire,
    };
    var title = (requestFamily, retire) switch
    {
      (false, false) => UiMessageKey.EngineeringClassifierRangeCreate,
      (false, true) => UiMessageKey.EngineeringClassifierRangeRetire,
      (true, false) => UiMessageKey.EngineeringRequestRangeCreate,
      (true, true) => UiMessageKey.EngineeringRequestRangeRetire,
    };

    var message = vm.RangeConfirmationMessage(body);
    Assert.Equal(English.Format(key, ("fromDate", "2020-02-01"), ("toDate", "2020-02-02")), message);
    Assert.Contains("2020-02-01", message, StringComparison.Ordinal);
    Assert.Contains("2020-02-02", message, StringComparison.Ordinal);
    Assert.Equal(English.Localize(title), vm.RangeConfirmationTitle(body));
  }

  [Fact]
  public void AllFourConfirmationMessagesDiffer()
  {
    using var vm = Create(out _);
    var range = new PsqlClassifierRange("2020-02-01", "2020-02-02");
    var messages = new[]
    {
      vm.RangeConfirmationMessage(new("createPartitions", ClassifierRange: range)),
      vm.RangeConfirmationMessage(new("cleanupPartitions", ClassifierRange: range)),
      vm.RangeConfirmationMessage(new("createPartitions", RequestRange: range)),
      vm.RangeConfirmationMessage(new("cleanupPartitions", RequestRange: range)),
    };
    Assert.Equal(4, messages.Distinct(StringComparer.Ordinal).Count());
  }

  [Fact]
  public void ConfirmationCopyRejectsMissingBodyOrRange()
  {
    using var vm = Create(out _);
    Assert.Throws<ArgumentNullException>(() => vm.RangeConfirmationMessage(null!));
    Assert.Throws<ArgumentNullException>(() => vm.RangeConfirmationTitle(null!));
    Assert.Throws<ArgumentException>(() => vm.RangeConfirmationMessage(new("createPartitions")));
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task InvalidRangeShowsOnlyTheAttemptedFamilysMessageAndClearsOnRetry(bool requestFamily)
  {
    using var vm = Create(out var handler);
    var changed = new List<string?>();
    vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
    if (requestFamily) vm.RequestRangeToDate = "2020-02-01";
    else vm.RangeToDate = "2020-02-01";

    await vm.ConfirmRangeAsync(requestFamily, false, _ => throw new InvalidOperationException(), TestContext.Current.CancellationToken);

    Assert.Equal(PsqlPartitionRangeError.InvalidOrder, vm.RangeError);
    Assert.Equal(requestFamily ? null : English.Localize(UiMessageKey.EngineeringClassifierRangeInvalidRange), vm.ClassifierRangeErrorMessage);
    Assert.Equal(requestFamily ? English.Localize(UiMessageKey.EngineeringRequestRangeInvalidRange) : null, vm.RequestRangeErrorMessage);
    Assert.Contains(nameof(vm.ClassifierRangeErrorMessage), changed);
    Assert.Contains(nameof(vm.RequestRangeErrorMessage), changed);
    Assert.Empty(handler.Requests);

    if (requestFamily) vm.RequestRangeToDate = "2020-02-02";
    else vm.RangeToDate = "2020-02-02";
    await vm.ConfirmRangeAsync(requestFamily, false, _ => Task.FromResult(true), TestContext.Current.CancellationToken);
    Assert.Null(vm.ClassifierRangeErrorMessage);
    Assert.Null(vm.RequestRangeErrorMessage);
    Assert.True(vm.RangeQueued);
  }

  [Fact]
  public async Task InvalidRowTextIsScopedToTheRequestFamilyAndLocaleChangeRefreshesMessages()
  {
    using var vm = Create(out _);
    vm.RequestRangeMaxDefaultRows = "abc";
    await vm.ConfirmRangeAsync(true, false, _ => throw new InvalidOperationException(), TestContext.Current.CancellationToken);
    Assert.Equal(PsqlPartitionRangeError.InvalidRowLimit, vm.RangeError);
    Assert.NotNull(vm.RequestRangeErrorMessage);
    Assert.Null(vm.ClassifierRangeErrorMessage);

    var changed = new List<string?>();
    vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
    vm.OnUiLocaleChanged();
    Assert.Contains(nameof(vm.RequestRangeErrorMessage), changed);
    Assert.Contains(nameof(vm.ClassifierRangeErrorMessage), changed);
  }

  [Fact]
  public async Task FamiliesKeepSeparateDraftsAndRetireOmitsTheRowBudget()
  {
    using var vm = Create(out var handler);
    vm.RangeFromDate = "garbage";
    vm.RangeMaxDefaultRows = "garbage";
    vm.RequestRangeMaxDefaultRows = "75";

    await vm.ConfirmRangeAsync(true, false, _ => Task.FromResult(true), TestContext.Current.CancellationToken);
    await vm.ConfirmRangeAsync(true, true, _ => Task.FromResult(true), TestContext.Current.CancellationToken);

    Assert.Equal(2, handler.Requests.Count);
    using var create = JsonDocument.Parse(handler.Requests[0].Body!);
    using var retire = JsonDocument.Parse(handler.Requests[1].Body!);
    Assert.False(create.RootElement.TryGetProperty("classifierRange", out _));
    var createRange = create.RootElement.GetProperty("requestRange");
    Assert.Equal("2020-02-01", createRange.GetProperty("fromDate").GetString());
    Assert.Equal(75, createRange.GetProperty("maxDefaultRows").GetInt32());
    Assert.False(retire.RootElement.GetProperty("requestRange").TryGetProperty("maxDefaultRows", out _));
  }

  [Fact]
  public async Task SessionChangeClearsScopedRangeErrors()
  {
    var session = new SessionStore();
    using var vm = Create(out _, session);
    vm.RangeToDate = "2020-02-01";
    await vm.ConfirmRangeAsync(false, false, _ => throw new InvalidOperationException(), TestContext.Current.CancellationToken);
    Assert.NotNull(vm.ClassifierRangeErrorMessage);
    session.Raise();
    Assert.Null(vm.ClassifierRangeErrorMessage);
  }

  private static EngineeringPostgreSqlViewModel Create(out RecordingHandler handler, SessionStore? session = null)
  {
    handler = new RecordingHandler("{\"success\":true}", System.Net.HttpStatusCode.OK);
    var service = new ApiEngineeringService(new VouchaApiClient(new HttpClient(handler) { BaseAddress = new("https://api.test") }));
    return new EngineeringPostgreSqlViewModel(service, sessionStore: session ?? new SessionStore())
    {
      RangeFromDate = "2020-02-01",
      RangeToDate = "2020-02-02",
      RequestRangeFromDate = "2020-02-01",
      RequestRangeToDate = "2020-02-02",
    };
  }

  private sealed class SessionStore : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged;
    public SessionSnapshot Current { get; } = new(new User("admin", "admin", Roles: ["administrator"]));
    public void Raise() => SessionChanged?.Invoke(this, new(Current));
    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }
}
