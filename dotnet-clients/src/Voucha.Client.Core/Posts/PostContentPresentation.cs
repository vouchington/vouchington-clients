using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Posts;

public enum PostContentPresentation
{
  Content,
  RemovedForAuthor,
  UnderReviewForStaff,
  Unavailable,
}

public static class PostContentPolicy
{
  public static bool CanShowMedia(string? clearanceStatus, bool isStaffViewer) =>
      !string.Equals(clearanceStatus, "rejected", StringComparison.Ordinal) || isStaffViewer;

  // Clearance is a coarse display state; server-owned context decides public eligibility.
  public static PostContentPresentation For(Post post, string? viewerId, bool isStaffViewer = false)
  {
    ArgumentNullException.ThrowIfNull(post);
    return For(post.ClearanceStatus, post.CreatedById, viewerId, isStaffViewer);
  }

  public static PostContentPresentation For(
      string? clearanceStatus, string? createdById, string? viewerId, bool isStaffViewer = false)
  {
    if (!string.Equals(clearanceStatus, "rejected", StringComparison.Ordinal))
      return PostContentPresentation.Content;
    if (!string.IsNullOrEmpty(viewerId) && string.Equals(viewerId, createdById, StringComparison.Ordinal))
      return PostContentPresentation.RemovedForAuthor;
    return isStaffViewer ? PostContentPresentation.UnderReviewForStaff : PostContentPresentation.Unavailable;
  }
}
