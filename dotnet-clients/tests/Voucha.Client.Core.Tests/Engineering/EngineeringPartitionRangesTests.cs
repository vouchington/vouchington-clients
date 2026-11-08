using System.Net;
using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Engineering;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Engineering;

public sealed class EngineeringPartitionRangesTests
{
  [Theory]
  [InlineData(false, false, "classifierRange", "createPartitions")]
  [InlineData(false, true, "classifierRange", "cleanupPartitions")]
  [InlineData(true, false, "requestRange", "createPartitions")]
  [InlineData(true, true, "requestRange", "cleanupPartitions")]
  public async Task ConfirmationCapturesExactBoundsAndEnqueuesOnlySelectedFamily(
      bool requestFamily, bool retire, string family, string type)
  {
    var (vm, handler, _) = Create();
    using (vm)
    {
      PsqlJobBody? confirmed = null;
      await vm.ConfirmRangeAsync(requestFamily, retire, body =>
      {
        confirmed = body;
        vm.RangeFromDate = "2025-01-01";
        return Task.FromResult(true);
      }, TestContext.Current.CancellationToken);
      Assert.NotNull(confirmed);
      var request = Assert.Single(handler.Requests);
      Assert.Equal(HttpMethod.Post, request.Method);
      Assert.Equal("/api/v1/psql/jobs", request.PathAndQuery);
      using var json = JsonDocument.Parse(request.Body!);
      Assert.Equal(type, json.RootElement.GetProperty("type").GetString());
      var range = json.RootElement.GetProperty(family);
      Assert.Equal("2020-02-01", range.GetProperty("fromDate").GetString());
      Assert.Equal("2020-02-02", range.GetProperty("toDate").GetString());
      Assert.Equal(!retire, range.TryGetProperty("maxDefaultRows", out _));
      Assert.False(json.RootElement.TryGetProperty(requestFamily ? "classifierRange" : "requestRange", out _));
      Assert.True(vm.RangeQueued);
      Assert.False(vm.IsLoading);
    }
  }

  [Fact]
  public async Task CancelAndInvalidInputNeverSend()
  {
    var (vm, handler, _) = Create();
    using (vm)
    {
      await vm.ConfirmRangeAsync(false, false, _ => Task.FromResult(false), TestContext.Current.CancellationToken);
      Assert.Empty(handler.Requests);
      vm.RangeFromDate = "2026-02-30";
      var confirmations = 0;
      await vm.ConfirmRangeAsync(false, false, _ => { confirmations++; return Task.FromResult(true); }, TestContext.Current.CancellationToken);
      Assert.Equal(0, confirmations);
      Assert.Equal(PsqlPartitionRangeError.InvalidDate, vm.RangeError);
      Assert.Empty(handler.Requests);
      Assert.False(vm.RangeQueued);
    }
  }

  [Theory]
  [InlineData("developer")]
  [InlineData("moderator")]
  [InlineData("Administrator")]
  [InlineData(null)]
  public async Task ExactAdministratorRoleRequired(string? role)
  {
    var (vm, handler, session) = Create();
    using (vm)
    {
      session.Set(role);
      var confirmations = 0;
      await vm.ConfirmRangeAsync(true, true, _ => { confirmations++; return Task.FromResult(true); }, TestContext.Current.CancellationToken);
      Assert.False(vm.IsRangeAuthorized);
      Assert.Equal(0, confirmations);
      Assert.Empty(handler.Requests);
    }
  }

  [Fact]
  public async Task SessionChangeAndPageCancellationInvalidatePendingConfirmation()
  {
    var (vm, handler, session) = Create();
    using (vm)
    {
      await vm.ConfirmRangeAsync(false, false, _ =>
      {
        session.Set(null);
        session.Set("administrator");
        return Task.FromResult(true);
      }, TestContext.Current.CancellationToken);
      Assert.Empty(handler.Requests);
      await vm.ConfirmRangeAsync(true, true, _ =>
      {
        vm.CancelRangeOperation();
        return Task.FromResult(true);
      }, TestContext.Current.CancellationToken);
      Assert.Empty(handler.Requests);
      Assert.False(vm.RangeQueued);
      Assert.Null(vm.ErrorMessage);
    }
  }

  [Fact]
  public async Task BusyGuardSpansConfirmationAndRejectsOtherJobs()
  {
    var (vm, handler, _) = Create();
    using (vm)
    {
      var confirmation = new TaskCompletionSource<bool>();
      var first = vm.ConfirmRangeAsync(false, false, _ => confirmation.Task, TestContext.Current.CancellationToken);
      Assert.True(vm.IsLoading);
      await vm.CreatePartitionsAsync(TestContext.Current.CancellationToken);
      await vm.ConfirmRangeAsync(true, false, _ => throw new InvalidOperationException(), TestContext.Current.CancellationToken);
      Assert.Empty(handler.Requests);
      confirmation.SetResult(true);
      await first;
      Assert.Single(handler.Requests);
    }
  }

  [Theory]
  [InlineData(HttpStatusCode.Forbidden)]
  [InlineData(HttpStatusCode.UnprocessableEntity)]
  public async Task ServerAuthorizationAndValidationFailuresStayErrors(HttpStatusCode status)
  {
    var (vm, handler, _) = Create(status);
    using (vm)
    {
      await vm.ConfirmRangeAsync(true, true, _ => Task.FromResult(true), TestContext.Current.CancellationToken);
      Assert.Single(handler.Requests);
      Assert.True(vm.HasError);
      Assert.Contains(((int)status).ToString(System.Globalization.CultureInfo.InvariantCulture), vm.ErrorMessage);
      Assert.False(vm.RangeQueued);
      Assert.False(vm.IsLoading);
    }
  }

  [Fact]
  public async Task UnexpectedConfirmationFailureReleasesOperationBeforeSessionChanges()
  {
    var (vm, handler, session) = Create();
    using (vm)
    {
      await Assert.ThrowsAsync<ArgumentException>(() => vm.ConfirmRangeAsync(false, false,
          _ => throw new ArgumentException("confirmation failed"), TestContext.Current.CancellationToken));
      Assert.False(vm.IsLoading);
      session.Set(null);
      session.Set("administrator");
      vm.CancelRangeOperation();
      await vm.ConfirmRangeAsync(true, false, _ => Task.FromResult(true), TestContext.Current.CancellationToken);
      Assert.Single(handler.Requests);
      Assert.True(vm.RangeQueued);
    }
  }

  [Fact]
  public async Task DisposingDuringConfirmationCancelsWithoutSendingAndIsIdempotent()
  {
    var (vm, handler, session) = Create();
    var confirmation = new TaskCompletionSource<bool>();
    var pending = vm.ConfirmRangeAsync(false, false, _ => confirmation.Task, TestContext.Current.CancellationToken);
    Assert.True(vm.IsLoading);
    vm.Dispose();
    vm.Dispose();
    session.Set(null);
    confirmation.SetResult(true);
    await pending;
    Assert.Empty(handler.Requests);
    Assert.False(vm.IsLoading);
    Assert.False(vm.RangeQueued);
    Assert.Null(vm.ErrorMessage);
    await vm.ConfirmRangeAsync(true, true, _ => throw new ArgumentException(), TestContext.Current.CancellationToken);
    Assert.False(vm.IsRangeAuthorized);
    Assert.Empty(handler.Requests);
  }

  [Theory]
  [InlineData("1.5")]
  [InlineData("abc")]
  [InlineData("9999999999999999999999")]
  public async Task InvalidRowTextNeverOpensConfirmation(string value)
  {
    var (vm, handler, _) = Create();
    using (vm)
    {
      vm.RangeMaxDefaultRows = value;
      await vm.ConfirmRangeAsync(false, false, _ => throw new ArgumentException(), TestContext.Current.CancellationToken);
      Assert.Equal(PsqlPartitionRangeError.InvalidRowLimit, vm.RangeError);
      Assert.Empty(handler.Requests);
    }
  }

  [Fact]
  public async Task MissingSessionStoreDeniesNewRangeOperations()
  {
    var handler = new RecordingHandler("{\"success\":true}");
    var service = new ApiEngineeringService(new VouchaApiClient(new HttpClient(handler) { BaseAddress = new("https://api.test") }));
    using var vm = new EngineeringPostgreSqlViewModel(service)
    {
      RangeFromDate = "2020-02-01",
      RangeToDate = "2020-02-02",
    };
    await vm.ConfirmRangeAsync(false, false, _ => throw new ArgumentException(), TestContext.Current.CancellationToken);
    Assert.False(vm.IsRangeAuthorized);
    Assert.Empty(handler.Requests);
  }

  [Theory]
  [InlineData("operation")]
  [InlineData("caller")]
  [InlineData("dispose")]
  public async Task CancellationReleasesBusyStateWithoutWaitingForConfirmation(string cancellationKind)
  {
    var (vm, handler, _) = Create();
    using (vm)
    using (var callerCancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken))
    {
      var confirmation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
      var pending = vm.ConfirmRangeAsync(false, false, _ => confirmation.Task, callerCancellation.Token);
      Assert.True(vm.IsLoading);
      Assert.False(confirmation.Task.IsCompleted);
      switch (cancellationKind)
      {
        case "operation": vm.CancelRangeOperation(); break;
        case "caller": callerCancellation.Cancel(); break;
        case "dispose": vm.Dispose(); break;
      }
      await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
      Assert.False(confirmation.Task.IsCompleted);
      Assert.False(vm.IsLoading);
      Assert.False(vm.RangeQueued);
      Assert.Null(vm.ErrorMessage);
      Assert.Empty(handler.Requests);
      confirmation.SetResult(true);
      await confirmation.Task;
      Assert.Empty(handler.Requests);
      Assert.False(vm.RangeQueued);
    }
  }

  [Fact]
  public async Task AlreadyCanceledCallerDoesNotOpenConfirmation()
  {
    var (vm, handler, _) = Create();
    using (vm)
    using (var cancellation = new CancellationTokenSource())
    {
      cancellation.Cancel();
      var confirmations = 0;
      await vm.ConfirmRangeAsync(false, false, _ =>
      {
        confirmations++;
        return Task.FromResult(true);
      }, cancellation.Token);
      Assert.Equal(0, confirmations);
      Assert.Empty(handler.Requests);
      Assert.False(vm.IsLoading);
      Assert.Null(vm.ErrorMessage);
    }
  }

  private static (EngineeringPostgreSqlViewModel Vm, RecordingHandler Handler, SessionStore Session) Create(
      HttpStatusCode status = HttpStatusCode.OK)
  {
    var handler = new RecordingHandler("{\"success\":true}", status);
    var service = new ApiEngineeringService(new VouchaApiClient(new HttpClient(handler) { BaseAddress = new("https://api.test") }));
    var session = new SessionStore();
    var vm = new EngineeringPostgreSqlViewModel(service, sessionStore: session)
    {
      RangeFromDate = "2020-02-01",
      RangeToDate = "2020-02-02",
      RangeMaxDefaultRows = "100",
    };
    return (vm, handler, session);
  }

  private sealed class SessionStore : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged;
    public SessionSnapshot Current { get; private set; } = new(new User("admin", "admin", Roles: ["administrator"]));
    public void Set(string? role)
    {
      Current = role is null ? SessionSnapshot.Anonymous : new(new User("admin", "admin", Roles: [role]));
      SessionChanged?.Invoke(this, new(Current));
    }
    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SignOutAsync(CancellationToken cancellationToken = default) { Set(null); return Task.CompletedTask; }
  }
}
