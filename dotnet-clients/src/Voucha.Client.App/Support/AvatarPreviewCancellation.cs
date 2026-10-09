namespace Voucha.Client.App.Support;

internal sealed class AvatarPreviewCancellation
{
  private CancellationTokenSource? current;

  public void Replace(CancellationTokenSource next)
  {
    CancelCurrent();
    current = next;
  }

  public void CancelCurrent()
  {
    var previous = current;
    current = null;
    previous?.Cancel();
  }

  public void Complete(CancellationTokenSource completed)
  {
    if (ReferenceEquals(current, completed)) current = null;
  }
}
