#if WINDOWS
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Storage;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System.Diagnostics;
using System.Collections.Concurrent;

namespace Voucha.Client.App.Controls;

/// <summary>Uses a unique, disposable WebView2 profile when no supported private-profile API exists.</summary>
public sealed class ProviderEmbedWebViewHandler : WebViewHandler
{
  private const string ProfileDirectoryName = "provider-embed";
  private static readonly ConcurrentDictionary<string, byte> PendingCleanup = new(StringComparer.Ordinal);
  private static readonly ConcurrentDictionary<string, byte> ActiveProfiles = new(StringComparer.Ordinal);
  private EmbedProfile? profile;

  protected override WebView2 CreatePlatformView() => new();

  protected override void ConnectHandler(WebView2 platformView)
  {
    base.ConnectHandler(platformView);
    SweepOwnedProfiles();
    var next = new EmbedProfile();
    next.UserDataFolder = CreateProfileDirectory();
    profile = next;
    next.Initialization = InitializeAsync(platformView, next);
  }

  protected override void DisconnectHandler(WebView2 platformView)
  {
    var active = Interlocked.Exchange(ref profile, null);
    active?.Cancellation.Cancel();
    platformView.Close();
    base.DisconnectHandler(platformView);
    if (active is not null) _ = DisposeProfileAsync(active);
  }

  private static async Task InitializeAsync(WebView2 platformView, EmbedProfile active)
  {
    try
    {
      if (active.UserDataFolder is null) return;
      var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: active.UserDataFolder).ConfigureAwait(true);
      active.Cancellation.Token.ThrowIfCancellationRequested();
      await platformView.EnsureCoreWebView2Async(environment).ConfigureAwait(true);
      active.Cancellation.Token.ThrowIfCancellationRequested();
    }
    catch (OperationCanceledException) when (active.Cancellation.IsCancellationRequested) { }
    catch (Exception exception) { Debug.WriteLine(exception); }
  }

  private static async Task DisposeProfileAsync(EmbedProfile active)
  {
    await active.Initialization.ConfigureAwait(false);
    if (active.UserDataFolder is not { } folder) return;
    ActiveProfiles.TryRemove(folder, out _);
    ScheduleCleanup(folder);
  }

  private static string ProfileRoot => Path.Combine(FileSystem.CacheDirectory, ProfileDirectoryName);

  private static void SweepOwnedProfiles()
  {
    try
    {
      if (!Directory.Exists(ProfileRoot)) return;
      foreach (var folder in Directory.EnumerateDirectories(ProfileRoot))
        if (!ActiveProfiles.ContainsKey(folder)) ScheduleCleanup(folder);
    }
    catch (Exception exception) { Debug.WriteLine(exception); }
  }

  private static void ScheduleCleanup(string folder)
  {
    if (!IsOwnedProfileDirectory(folder) || ActiveProfiles.ContainsKey(folder) || !PendingCleanup.TryAdd(folder, 0)) return;
    _ = RetryCleanupAsync(folder);
  }

  private static string? CreateProfileDirectory()
  {
    var folder = Path.Combine(ProfileRoot, Guid.NewGuid().ToString("N"));
    try
    {
      ActiveProfiles.TryAdd(folder, 0);
      Directory.CreateDirectory(folder);
      return folder;
    }
    catch (Exception exception)
    {
      ActiveProfiles.TryRemove(folder, out _);
      Debug.WriteLine(exception);
      return null;
    }
  }

  private static async Task RetryCleanupAsync(string folder)
  {
    var delay = TimeSpan.FromMilliseconds(100);
    while (true)
    {
      try
      {
        if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        PendingCleanup.TryRemove(folder, out _);
        return;
      }
      catch (Exception exception)
      {
        Debug.WriteLine(exception);
        await Task.Delay(delay).ConfigureAwait(false);
        delay = TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds * 2, 5000));
      }
    }
  }

  private static bool IsOwnedProfileDirectory(string folder) =>
      string.Equals(Path.GetDirectoryName(Path.GetFullPath(folder)), Path.GetFullPath(ProfileRoot), StringComparison.Ordinal) &&
      Guid.TryParseExact(Path.GetFileName(folder), "N", out _);

  private sealed class EmbedProfile
  {
    public CancellationTokenSource Cancellation { get; } = new();
    public string? UserDataFolder { get; set; }
    public Task Initialization { get; set; } = Task.CompletedTask;
  }
}
#endif
