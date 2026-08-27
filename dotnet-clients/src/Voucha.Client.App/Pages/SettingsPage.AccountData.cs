using System.Globalization;
using Microsoft.Maui.ApplicationModel;

namespace Voucha.Client.App.Pages;

public partial class SettingsPage
{
  private CancellationTokenSource? dataRequestPollingCts;

  private string? dataRequestPollingKey;

  private Task? dataRequestPollingTask;

  private bool isPageVisible;

  protected override async void OnAppearing()
  {
    base.OnAppearing();
    isPageVisible = true;
    AttachBlueskyObservers();
    AttachOAuthObservers();
    await SettingsPageInitialLoad.RunAsync(
        LoadNotificationPreferencesForFocusedRouteAsync,
        () => Task.WhenAll(viewModel.LoadAsync(), emailAddressViewModel.LoadAsync())).ConfigureAwait(true);
    await RefreshBlueskyAsync();
    await RefreshOAuthAccountsAsync();
    EnsureDataRequestPolling();
  }

  protected override void OnDisappearing()
  {
    isPageVisible = false;
    notificationRouteEpoch.Invalidate();
    notificationAccessibility.Deactivate();
    DetachBlueskyObservers();
    DetachOAuthObservers();
    StopDataRequestPolling();
    base.OnDisappearing();
  }

  private async void OnCreateDataRequestClicked(object? sender, EventArgs e)
  {
    await viewModel.CreateDataRequestAsync();
    EnsureDataRequestPolling();
  }

  private async void OnRefreshDataRequestClicked(object? sender, EventArgs e)
  {
    await viewModel.RefreshDataRequestAsync();
    EnsureDataRequestPolling();
  }

  private async void OnDownloadDataRequestClicked(object? sender, EventArgs e)
  {
    if (viewModel.DataRequestDownloadUrl is { } downloadUrl)
    {
      try
      {
        await Launcher.OpenAsync(downloadUrl);
      }
      catch (Exception ex)
      {
        viewModel.ReportDataRequestDownloadError(ex);
      }
    }
  }

  private async void OnDeleteAccountClicked(object? sender, EventArgs e)
  {
    if (await viewModel.DeleteAccountAsync())
    {
      await SignOutAndNavigateAsync();
    }
  }

  private void EnsureDataRequestPolling()
  {
    if (!isPageVisible)
    {
      return;
    }

    if (!ShouldWatchDataRequest())
    {
      StopDataRequestPolling();
      return;
    }

    var pollingKey = GetDataRequestPollingKey();
    if (dataRequestPollingCts is { IsCancellationRequested: false })
    {
      if (string.Equals(dataRequestPollingKey, pollingKey, StringComparison.Ordinal))
      {
        return;
      }

      StopDataRequestPolling();
    }

    dataRequestPollingKey = pollingKey;
    dataRequestPollingCts = new CancellationTokenSource();
    dataRequestPollingTask = PollDataRequestAsync(dataRequestPollingCts);
  }

  private void StopDataRequestPolling()
  {
    dataRequestPollingCts?.Cancel();
  }

  private async Task PollDataRequestAsync(CancellationTokenSource pollingCts)
  {
    var cancellationToken = pollingCts.Token;
    try
    {
      while (!cancellationToken.IsCancellationRequested && ShouldWatchDataRequest())
      {
        var delay = GetDataRequestWatchDelay();
        if (delay > TimeSpan.Zero)
        {
          await Task.Delay(delay, cancellationToken);
        }

        if (cancellationToken.IsCancellationRequested)
        {
          break;
        }

        await viewModel.RefreshDataRequestAsync(cancellationToken);
      }
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
    }
    finally
    {
      pollingCts.Dispose();
      if (ReferenceEquals(dataRequestPollingCts, pollingCts))
      {
        dataRequestPollingCts = null;
        dataRequestPollingKey = null;
        dataRequestPollingTask = null;
      }
    }
  }

  private string? GetDataRequestPollingKey()
  {
    var dataRequest = viewModel.DataRequest;
    if (dataRequest is null)
    {
      return null;
    }

    return string.Join(
        ':',
        dataRequest.Id,
        dataRequest.Status,
        dataRequest.ExpiresAt?.UtcTicks.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
        dataRequest.DownloadUrl is null ? "no-url" : "url");
  }

  private bool ShouldWatchDataRequest()
  {
    var dataRequest = viewModel.DataRequest;
    if (dataRequest is null)
    {
      return false;
    }

    return string.Equals(dataRequest.Status, "pending", StringComparison.OrdinalIgnoreCase)
        || string.Equals(dataRequest.Status, "processing", StringComparison.OrdinalIgnoreCase)
        || (string.Equals(dataRequest.Status, "ready", StringComparison.OrdinalIgnoreCase)
            && (dataRequest.DownloadUrl is null
                || (dataRequest.ExpiresAt is { } expiresAt && expiresAt > DateTimeOffset.UtcNow)));
  }

  private TimeSpan GetDataRequestWatchDelay()
  {
    var dataRequest = viewModel.DataRequest;
    if (dataRequest is null || !string.Equals(dataRequest.Status, "ready", StringComparison.OrdinalIgnoreCase))
    {
      return TimeSpan.FromSeconds(5);
    }

    if (dataRequest.DownloadUrl is null)
    {
      return TimeSpan.FromSeconds(5);
    }

    if (dataRequest.ExpiresAt is not { } expiresAt)
    {
      return TimeSpan.Zero;
    }

    var delay = expiresAt - DateTimeOffset.UtcNow;
    return delay > TimeSpan.Zero ? delay : TimeSpan.Zero;
  }
}
