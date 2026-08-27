using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Voucha.Client.App;

internal static class AppLinkDispatcher
{
  private static readonly object Gate = new();
  private static readonly Queue<Uri> PendingUrls = new();
  private static Func<Uri, Task>? handler;

  public static void Attach(Func<Uri, Task> urlHandler)
  {
    List<Uri> pending;

    lock (Gate)
    {
      handler = urlHandler;
      pending = PendingUrls.ToArray().ToList();
      PendingUrls.Clear();
    }

    foreach (var url in pending)
    {
      DispatchFireAndForget(url);
    }
  }

  public static Task DispatchAsync(Uri url)
  {
    Func<Uri, Task>? currentHandler;

    lock (Gate)
    {
      currentHandler = handler;
      if (currentHandler is null)
      {
        PendingUrls.Enqueue(url);
        return Task.CompletedTask;
      }
    }

    return currentHandler(url);
  }

  public static void DispatchFireAndForget(Uri url) =>
      _ = ObserveDispatchAsync(url);

  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "Platform app-link entry points discard dispatch tasks, so failures must be observed here.")]
  private static async Task ObserveDispatchAsync(Uri url)
  {
    try
    {
      await DispatchAsync(url).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
      Debug.WriteLine(ex);
    }
  }
}
