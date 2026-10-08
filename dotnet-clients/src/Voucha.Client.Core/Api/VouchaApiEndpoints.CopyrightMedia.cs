namespace Voucha.Client.Core.Api;

public static partial class VouchaApiEndpoints
{
  public static ApiRequest PostImages(string postIdOrSlug) =>
      Get($"/api/v1/posts/{Path(postIdOrSlug)}/images");

  public static ApiRequest CopyrightImageSimilarityCandidates(string noticeId, string targetId) =>
      Get($"/api/v1/copyright-notices/{Path(noticeId)}/targets/{Path(targetId)}/image-similarity-candidates");
}
