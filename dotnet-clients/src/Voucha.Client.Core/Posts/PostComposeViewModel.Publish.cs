using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Posts;

public sealed partial class PostComposeViewModel
{
  public async Task<bool> PublishAsync(CancellationToken cancellationToken = default)
  {
    if (IsPublishing) return false;

    var validation = Validation;
    if (!validation.CanPublish)
    {
      ErrorMessage = validation.Message;
      State = LoadState.Error;
      return false;
    }

    State = LoadState.Loading;
    ErrorMessage = null;
    try
    {
      var response = await EmailVerificationGate.RunAsync<PostMutationResponse?>(
          async () => IsCommunityPost
              ? await postsService.CreateCommunityPostAsync(
                  CommunitySlug.Trim(),
                  BuildBody(),
                  cancellationToken).ConfigureAwait(true)
              : await postsService.CreatePostAsync(BuildBody(), cancellationToken).ConfigureAwait(true),
          ex =>
          {
            TurnstileToken = "";
            CompleteError(ex.Message);
            return null;
          }).ConfigureAwait(true);
      if (response is null) return false;
      LastResponse = response;
      OnPropertyChanged(nameof(LastResponse));
      OnPropertyChanged(nameof(HasPublished));
      State = LoadState.Loaded;
      try
      {
        await CreateRelationsAsync(response.Post.Id, cancellationToken).ConfigureAwait(true);
      }
      catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
      {
        System.Diagnostics.Debug.WriteLine($"Failed to create post relations: {ex.Message}");
      }

      return true;
    }
    catch (OperationCanceledException)
    {
      State = LoadState.Idle;
      return false;
    }
    catch (VouchaApiException ex)
    {
      TurnstileToken = "";
      CompleteError(ex.Message);
      return false;
    }
    catch (HttpRequestException ex)
    {
      TurnstileToken = "";
      CompleteError(ex.Message);
      return false;
    }
    catch (InvalidOperationException ex)
    {
      TurnstileToken = "";
      CompleteError(ex.Message);
      return false;
    }
  }

  private CreatePostBody BuildBody()
  {
    TryParseStructuredData(out var structuredData);
    return new CreatePostBody(
        PostType.Trim(),
        Title.Trim(),
        Markdown,
        EmptyToNull(TurnstileToken),
        CanEditSlug ? EmptyToNull(Slug) : null,
        EmptyToNull(Broadcast),
        EmptyToNull(Privacy),
        IsAnonymous,
        PostType == PostComposeTypes.Link ? ParseUri(LinkAddress) : null,
        PostType == PostComposeTypes.Link ? EmptyToNull(LinkIdentifier) : null,
        ReviewTopicRatings: PostType == PostComposeTypes.Review
            ? EffectiveReviewTopicRatings().Select(x => new CreatePostReviewTopicRatingInput(
                x.TopicId,
                x.Rating)).ToArray()
            : null,
        Images: EffectiveImages().Select(x => new CreatePostImageInput(
            x.ImageId,
            x.OrderIndex,
            EmptyToNull(x.Caption))).ToArray(),
        DataPointVertical: PostType == PostComposeTypes.DataPoint ? EmptyToNull(DataPointVertical) : null,
        StructuredData: PostType == PostComposeTypes.DataPoint ? structuredData : null,
        DeclaredLanguage: EmptyToNull(DeclaredLanguage),
        HpWebsite: EmptyToNull(HpWebsite),
        HpPhone: EmptyToNull(HpPhone));
  }

  private async Task CreateRelationsAsync(string postId, CancellationToken cancellationToken)
  {
    if (relationsService is null) return;
    foreach (var url in EffectiveRelatedUrls())
    {
      await relationsService.CreateAsync(
          new CreateEntityRelationRequest("post", postId, "related", "url", url.Identifier),
          cancellationToken).ConfigureAwait(true);
    }
    foreach (var topic in EffectiveDiscussionCategoryTopics())
    {
      await relationsService.CreateAsync(
          new CreateEntityRelationRequest("post", postId, "category", "topic", topic.TopicId),
          cancellationToken).ConfigureAwait(true);
    }
  }

  private IReadOnlyList<PostComposeTopicRatingDraft> EffectiveReviewTopicRatings()
  {
    if (ReviewTopicRatings.Count > 0) return ReviewTopicRatings;
    return int.TryParse(ReviewRating, out var rating) && !string.IsNullOrWhiteSpace(ReviewTopicId)
        ? [new PostComposeTopicRatingDraft(ReviewTopicId.Trim(), rating)]
        : [];
  }

  private IReadOnlyList<PostComposeTopicDraft> EffectiveDiscussionCategoryTopics() =>
      DiscussionCategoryTopics.Count > 0 || string.IsNullOrWhiteSpace(DiscussionCategoryTopicId)
          ? DiscussionCategoryTopics
          : [new PostComposeTopicDraft(DiscussionCategoryTopicId.Trim())];

  private IReadOnlyList<PostComposeRelatedUrlDraft> EffectiveRelatedUrls() =>
      RelatedUrls.Count > 0
          ? RelatedUrls
          : IdentifierList(RelatedLinkIdentifier).Select(id => new PostComposeRelatedUrlDraft(id)).ToArray();

  private PostComposeImageDraft[] EffectiveImages()
  {
    var readyImages = Images.Where(image => image.IsReady).ToArray();
    if (readyImages.Length > 0) return readyImages;
    return string.IsNullOrWhiteSpace(ImageId)
        ? []
        : [new PostComposeImageDraft(ImageId.Trim(), 0, EmptyToNull(ImageCaption))];
  }

  private void CompleteError(string message)
  {
    ErrorMessage = message;
    State = LoadState.Error;
  }

  private static string? EmptyToNull(string? value) =>
      string.IsNullOrWhiteSpace(value) ? null : value.Trim();

  private static Uri? ParseUri(string value) =>
      Uri.TryCreate(EmptyToNull(value), UriKind.Absolute, out var uri) ? uri : null;

  private static IEnumerable<string> IdentifierList(string? value) =>
      (value ?? "")
          .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
          .Take(20);
}
