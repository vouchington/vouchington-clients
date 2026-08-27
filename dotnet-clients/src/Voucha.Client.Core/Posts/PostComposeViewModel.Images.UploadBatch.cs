using System.Threading;

namespace Voucha.Client.Core.Posts;

public sealed partial class PostComposeViewModel
{
  private int imageUploadBatchInProgress;

  public IDisposable BeginImageUploadBatch()
  {
    Interlocked.Increment(ref imageUploadBatchInProgress);
    OnImageUploadInProgressChanged();
    return new ImageUploadBatchScope(this);
  }

  public bool CanUploadMoreImagesInBatch => imageUploadService is not null && Images.Count < MaxImages;

  private void EndImageUploadBatch()
  {
    Interlocked.Decrement(ref imageUploadBatchInProgress);
    OnImageUploadInProgressChanged();
  }

  private sealed class ImageUploadBatchScope : IDisposable
  {
    private PostComposeViewModel? owner;

    public ImageUploadBatchScope(PostComposeViewModel owner)
    {
      this.owner = owner;
    }

    public void Dispose()
    {
      Interlocked.Exchange(ref owner, null)?.EndImageUploadBatch();
    }
  }
}
