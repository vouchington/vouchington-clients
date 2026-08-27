using System.Diagnostics;

namespace Voucha.Client.App.Pages;

public sealed partial class MemberAppealsPage
{
  public Task EnsureLoadedAsync()
  {
    EnsureLifetime();
    if (isLoaded) return Task.CompletedTask;
    return initialLoad is { IsCompleted: false } active
        ? active
        : StartReload();
  }

  public async Task ReloadAsync()
  {
    EnsureLifetime();
    if (initialLoad is { IsCompleted: false } active)
    {
      await active.ConfigureAwait(true);
      return;
    }
    await StartReload().ConfigureAwait(true);
  }

  private Task StartReload()
  {
    var previous = loadSettlement;
    var load = ReloadCoreAsync(previous, lifetimeCancellation.Token);
    loadSettlement = load;
    initialLoad = load;
    return load;
  }

  private async Task ReloadCoreAsync(
      Task previous,
      CancellationToken lifetimeToken)
  {
    try
    {
      await previous.ConfigureAwait(true);
      lifetimeToken.ThrowIfCancellationRequested();
    }
    catch (OperationCanceledException)
    {
      return;
    }

    var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetimeToken);
    loadCancellation = cancellation;
    pageLoadFailed = false;
    var load = viewModel.ReloadAsync(cancellation.Token);
    Content = BuildContent();
    try
    {
      await load.ConfigureAwait(true);
    }
    catch (OperationCanceledException)
    {
    }
    catch (Exception ex)
    {
      Debug.WriteLine(ex);
      pageLoadFailed = true;
    }
    finally
    {
      if (ReferenceEquals(loadCancellation, cancellation))
      {
        loadCancellation = null;
        isLoaded = !cancellation.IsCancellationRequested;
        if (isLoaded) Content = BuildContent();
      }
      cancellation.Dispose();
    }
  }
}
