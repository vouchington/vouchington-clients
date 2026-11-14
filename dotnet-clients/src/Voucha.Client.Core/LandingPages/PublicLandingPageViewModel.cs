using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Analytics;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.LandingPages;

public sealed class PublicLandingPageViewModel : ObservableObject, IDisposable
{
  private readonly Func<CancellationToken, Task<PublicLandingPageResponse>> fetch;
  private readonly ISessionStore session;
  private readonly IProductSurfaceAnalytics analytics;
  private string? account;
  private int generation;
  private bool active;
  private bool visited;
  private bool disposed;
  private CancellationTokenSource? loading;
  public LandingPage? Page { get; private set; }
  public PublicLandingPageUser? Owner { get; private set; }
  public UiText? Error { get; private set; }
  public bool IsLoading { get; private set; }
  public int ActivationVersion => generation;

  public PublicLandingPageViewModel(
      Func<CancellationToken, Task<PublicLandingPageResponse>> fetch,
      ISessionStore session, IProductSurfaceAnalytics analytics)
  {
    ArgumentNullException.ThrowIfNull(fetch);
    ArgumentNullException.ThrowIfNull(session);
    ArgumentNullException.ThrowIfNull(analytics);
    this.fetch = fetch;
    this.session = session;
    this.analytics = analytics;
    account = session.Current.Identity?.Id;
    session.SessionChanged += OnSessionChanged;
  }

  public Task ActivateAsync(CancellationToken cancellationToken = default)
  {
    if (active || disposed) return Task.CompletedTask;
    active = true;
    visited = false;
    return ReloadAsync(cancellationToken);
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Public content load failures are presented inline.")]
  public async Task ReloadAsync(CancellationToken cancellationToken = default)
  {
    if (!active || disposed || IsLoading) return;
    var request = generation;
    using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    var token = cancellation.Token;
    loading = cancellation;
    IsLoading = true;
    Error = null;
    OnPropertyChanged(string.Empty);
    try
    {
      var result = await fetch(token).ConfigureAwait(true);
      if (!IsCurrent(request) || token.IsCancellationRequested) return;
      Owner = result.User;
      Page = result.LandingPage;
      if (!visited)
      {
        visited = true;
        if (account is not null) analytics.RecordLandingPageVisit(Page.Id);
      }
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      if (IsCurrent(request)) Error = UiText.Verbatim(ex.Message);
    }
    catch (OperationCanceledException) { }
    finally
    {
      if (ReferenceEquals(loading, cancellation)) loading = null;
      if (request == generation)
      {
        IsLoading = false;
        OnPropertyChanged(string.Empty);
      }
    }
  }

  public PublicLandingPageTarget? ActivateItem(string itemId, string? memberId = null, int? activationVersion = null)
  {
    if (!active || !IsCurrent(generation) ||
        (activationVersion is not null && activationVersion != generation) || Page is not { } page) return null;
    var parent = page.Items?.FirstOrDefault(item => item.Id == itemId);
    var selected = memberId is null ? parent : (parent as LandingPageTopicGroupItem)?.Entries
        .FirstOrDefault(item => PublicLandingPageTarget.MemberId(item) == memberId);
    var target = PublicLandingPageTarget.For(selected);
    if (target is null) return null;
    if (account is not null) analytics.RecordLandingPageClick(page.Id, new(itemId, memberId));
    return target;
  }

  public void Deactivate()
  {
    active = false;
    generation++;
    loading?.Cancel();
    IsLoading = false;
  }

  private bool IsCurrent(int request) => active && request == generation && account == session.Current.Identity?.Id;

  private void OnSessionChanged(object? sender, SessionChangedEventArgs args)
  {
    if (disposed || account == args.Snapshot.Identity?.Id) return;
    account = args.Snapshot.Identity?.Id;
    generation++;
    loading?.Cancel();
    Page = null;
    Owner = null;
    Error = null;
    IsLoading = false;
    visited = false;
    OnPropertyChanged(string.Empty);
    if (active) _ = ReloadAsync();
  }

  public void Dispose()
  {
    if (disposed) return;
    disposed = true;
    Deactivate();
    session.SessionChanged -= OnSessionChanged;
    loading?.Dispose();
    loading = null;
  }
}
