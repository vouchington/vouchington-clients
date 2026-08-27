using Microsoft.Extensions.Logging;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.FeatureFlags;
using Voucha.Client.Core.Navigation;

namespace Voucha.Client.App;

public static partial class MauiProgram
{
  private static MutableNavigationViewerProvider CreateNavigationViewerProvider(IServiceProvider sp)
  {
    var provider = new MutableNavigationViewerProvider();
    var sessionStore = sp.GetRequiredService<ISessionStore>();
    var apiClient = sp.GetRequiredService<VouchaApiClient>();
    var logger = sp.GetRequiredService<ILogger<MutableNavigationViewerProvider>>();
    var featureFlags = sp.GetRequiredService<FeatureFlagState>();
    CancellationTokenSource? featureFlagRefreshCts = null;
    var refreshLock = new object();
    void QueueFeatureFlagRefresh()
    {
      User? currentIdentity;
      CancellationTokenSource? oldCts;
      var refreshCts = new CancellationTokenSource();
      lock (refreshLock)
      {
        currentIdentity = sessionStore.Current.Identity;
        oldCts = featureFlagRefreshCts;
        featureFlagRefreshCts = refreshCts;
      }
      CancelNavigationViewerFeatureFlagRefresh(oldCts);
      _ = RefreshNavigationViewerFeatureFlagsAsync(
          provider,
          sessionStore,
          apiClient,
          featureFlags,
          logger,
          refreshCts,
          currentIdentity,
          () =>
          {
            lock (refreshLock)
            {
              if (ReferenceEquals(featureFlagRefreshCts, refreshCts))
              {
                featureFlagRefreshCts = null;
              }
            }
          });
    }

    _ = new FeatureFlagNavigationBinding(featureFlags, sessionStore, provider);
    provider.SetViewer(NavigationCatalog.FromIdentity(sessionStore.Current.Identity));
    _ = featureFlags.InitializeAsync();
    QueueFeatureFlagRefresh();
    sessionStore.SessionChanged += (_, args) =>
    {
      provider.SetViewer(NavigationCatalog.FromIdentity(args.Snapshot.Identity, provider.CurrentViewer.FeatureFlags));
      QueueFeatureFlagRefresh();
    };
    return provider;
  }

  private static async Task RefreshNavigationViewerFeatureFlagsAsync(
      MutableNavigationViewerProvider provider,
      ISessionStore sessionStore,
      VouchaApiClient apiClient,
      FeatureFlagState featureFlags,
      ILogger logger,
      CancellationTokenSource refreshCts,
      User? identityAtQueueTime,
      Action clearCurrentRefresh)
  {
    var cancellationToken = refreshCts.Token;
    try
    {
      var retryDelay = TimeSpan.FromSeconds(5);
      while (!cancellationToken.IsCancellationRequested)
      {
        try
        {
          var ticket = await featureFlags.BeginRemoteReadAsync(cancellationToken).ConfigureAwait(false);
          var response = await apiClient.FetchFeatureFlagsAsync(cancellationToken).ConfigureAwait(false);
          if (ReferenceEquals(sessionStore.Current.Identity, identityAtQueueTime))
          {
            await featureFlags.ApplyRemoteReadAsync(ticket, response.Flags, cancellationToken).ConfigureAwait(false);
          }
          return;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
          return;
        }
        catch (Exception ex)
        {
          LogNavigationFeatureFlagRefreshFailed(logger, ex);
        }

        try
        {
          await Task.Delay(retryDelay, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
          return;
        }
        retryDelay = TimeSpan.FromSeconds(Math.Min(retryDelay.TotalSeconds * 2, 60));
      }
    }
    finally
    {
      clearCurrentRefresh();
      refreshCts.Dispose();
    }
  }

  private static void CancelNavigationViewerFeatureFlagRefresh(CancellationTokenSource? refreshCts)
  {
    try
    {
      refreshCts?.Cancel();
    }
    catch (ObjectDisposedException)
    {
    }
  }

  [LoggerMessage(
      EventId = 2,
      Level = LogLevel.Warning,
      Message = "Navigation feature flag refresh failed.")]
  private static partial void LogNavigationFeatureFlagRefreshFailed(
      ILogger logger,
      Exception exception);
}
