namespace Voucha.Client.Core.Chat;

public sealed partial class ChatConversationViewModel
{
  public async Task StopStreamingAsync()
  {
    var cts = Interlocked.Exchange(ref streamingCts, null);
    if (cts is null) return;

    try
    {
      await cts.CancelAsync().ConfigureAwait(true);
    }
    finally
    {
      cts.Dispose();
      IsStreaming = false;
    }
  }
}
