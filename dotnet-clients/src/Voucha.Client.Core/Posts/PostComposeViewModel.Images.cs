namespace Voucha.Client.Core.Posts;

public sealed partial class PostComposeViewModel
{
  private const int MaxImages = 20;
  private const int MaxImageCaptionLength = 1000;

  public bool TryAddImageDraft(PostComposeImageDraft image)
  {
    ArgumentNullException.ThrowIfNull(image);
    if (Images.Count >= MaxImages || string.IsNullOrWhiteSpace(image.ImageId))
    {
      return false;
    }

    SetImages([.. Images, NormalizeImage(image, Images.Count)]);
    return true;
  }

  public void RemoveImage(string imageId)
  {
    var index = IndexOfImage(imageId);
    if (index < 0) return;
    SetImages(Images.Where((_, candidate) => candidate != index).ToArray());
  }

  public void MoveImage(int fromIndex, int toIndex)
  {
    if (fromIndex == toIndex || fromIndex < 0 || fromIndex >= Images.Count) return;
    if (toIndex < 0 || toIndex >= Images.Count) return;

    var items = Images.ToList();
    var image = items[fromIndex];
    items.RemoveAt(fromIndex);
    items.Insert(toIndex, image);
    SetImages(items);
  }

  public void SetImageCaption(string imageId, string? caption) =>
      UpdateImage(imageId, image => image with { Caption = NormalizeCaption(caption) });

  private bool UpdateImage(string imageId, Func<PostComposeImageDraft, PostComposeImageDraft> update)
  {
    var index = IndexOfImage(imageId);
    if (index < 0) return false;

    var items = Images.ToList();
    items[index] = NormalizeImage(update(items[index]), index);
    SetImages(items);
    return true;
  }

  private int IndexOfImage(string imageId)
  {
    for (var index = 0; index < Images.Count; index++)
    {
      if (string.Equals(Images[index].ImageId, imageId, StringComparison.Ordinal))
      {
        return index;
      }
    }
    return -1;
  }

  private PostComposeImageDraft[] NormalizeImages(IReadOnlyList<PostComposeImageDraft> images) =>
      images
          .Take(MaxImages)
          .Select((image, index) => NormalizeImage(image, index))
          .ToArray();

  private PostComposeImageDraft NormalizeImage(PostComposeImageDraft image, int orderIndex) =>
      image with
      {
        OrderIndex = orderIndex,
        Caption = NormalizeCaption(image.Caption),
        Localization = image.Localization ?? localization,
      };

  private static string? NormalizeCaption(string? caption)
  {
    var normalized = EmptyToNull(caption);
    if (normalized is null || normalized.Length <= MaxImageCaptionLength)
    {
      return normalized;
    }

    return normalized[..MaxImageCaptionLength];
  }
}
