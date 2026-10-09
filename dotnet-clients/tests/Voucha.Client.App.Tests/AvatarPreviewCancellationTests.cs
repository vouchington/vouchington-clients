using Voucha.Client.App.Support;
using Xunit;

namespace Voucha.Client.App.Tests;

public sealed class AvatarPreviewCancellationTests
{
  [Fact]
  public void RemovalAndDisappearanceAllowASecondUploadAfterFirstHandlerDisposes()
  {
    var previews = new AvatarPreviewCancellation();
    using var first = new CancellationTokenSource();
    previews.Replace(first);

    previews.CancelCurrent(); // Remove avatar while its picker or upload is pending.
    Assert.True(first.IsCancellationRequested);
    first.Dispose(); // The cancelled async handler exits and its using scope ends.
    previews.CancelCurrent(); // The page disappears after the handler has exited.

    using var second = new CancellationTokenSource();
    previews.Replace(second); // Reappear, then upload again.
    previews.Complete(first); // A stale completion must not discard the new upload.
    previews.CancelCurrent();
    Assert.True(second.IsCancellationRequested);
  }

  [Fact]
  public void ReplacingPendingUploadCancelsOnlyItsPredecessor()
  {
    var previews = new AvatarPreviewCancellation();
    using var first = new CancellationTokenSource();
    using var second = new CancellationTokenSource();
    previews.Replace(first);

    previews.Replace(second);
    previews.Complete(first);

    Assert.True(first.IsCancellationRequested);
    Assert.False(second.IsCancellationRequested);
    previews.CancelCurrent();
    Assert.True(second.IsCancellationRequested);
  }
}
