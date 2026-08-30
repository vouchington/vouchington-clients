using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Posts;

public sealed partial class PostComposeViewModel
{
  public string PostType
  {
    get => postType;
    set
    {
      if (!SetDraftProperty(ref postType, value)) return;
      OnPropertyChanged(nameof(IsLinkType));
      OnPropertyChanged(nameof(IsReviewType));
      OnPropertyChanged(nameof(IsDataPointType));
      OnPropertyChanged(nameof(IsDiscussionType));
    }
  }

  public string Title { get => title; set => SetDraftProperty(ref title, value); }

  public string Slug { get => slug; set => SetDraftProperty(ref slug, value); }

  public string Markdown { get => markdown; set => SetDraftProperty(ref markdown, value); }

  public string CommunitySlug
  {
    get => communitySlug;
    set
    {
      if (SetDraftProperty(ref communitySlug, value))
      {
        OnPropertyChanged(nameof(IsCommunityPost));
        OnPropertyChanged(nameof(AvailablePostTypes));
        if (IsCommunityPost && !PostComposeTypes.CommunityTypes.Contains(PostType, StringComparer.Ordinal))
        {
          PostType = PostComposeTypes.Discussion;
        }
      }
    }
  }

  public string LinkAddress { get => linkAddress; set => SetDraftProperty(ref linkAddress, value); }

  public string LinkIdentifier { get => linkIdentifier; set => SetDraftProperty(ref linkIdentifier, value); }

  public string Broadcast { get => broadcast; set => SetDraftProperty(ref broadcast, value); }

  public string Privacy { get => privacy; set => SetDraftProperty(ref privacy, value); }

  public bool IsAnonymous { get => isAnonymous; set => SetDraftProperty(ref isAnonymous, value); }

  public string DeclaredLanguage { get => declaredLanguage; set => SetDraftProperty(ref declaredLanguage, value); }

  public string TurnstileToken { get => turnstileToken; set => SetDraftProperty(ref turnstileToken, value); }

  public string DataPointVertical { get => dataPointVertical; set => SetDraftProperty(ref dataPointVertical, value); }

  public string StructuredDataJson { get => structuredDataJson; set => SetDraftProperty(ref structuredDataJson, value); }

  public string HpWebsite { get => hpWebsite; set => SetDraftProperty(ref hpWebsite, value); }

  public string HpPhone { get => hpPhone; set => SetDraftProperty(ref hpPhone, value); }

  public string ReviewTopicId { get => reviewTopicId; set => SetDraftProperty(ref reviewTopicId, value); }

  public string ReviewRating { get => reviewRating; set => SetDraftProperty(ref reviewRating, value); }

  public string DiscussionCategoryTopicId
  {
    get => discussionCategoryTopicId;
    set => SetDraftProperty(ref discussionCategoryTopicId, value);
  }

  public string DiscussionCategoryHashtag
  {
    get => discussionCategoryHashtag;
    set => SetDraftProperty(ref discussionCategoryHashtag, value);
  }

  public string RelatedLinkIdentifier
  {
    get => relatedLinkIdentifier;
    set => SetDraftProperty(ref relatedLinkIdentifier, value);
  }

  public string ImageId { get => imageId; set => SetDraftProperty(ref imageId, value); }

  public string ImageCaption { get => imageCaption; set => SetDraftProperty(ref imageCaption, NormalizeCaption(value) ?? string.Empty); }

  public LoadState State
  {
    get => state;
    private set
    {
      if (SetProperty(ref state, value))
      {
        OnPropertyChanged(nameof(IsPublishing));
        OnPropertyChanged(nameof(HasError));
        OnValidationChanged();
      }
    }
  }

  public string? ErrorMessage
  {
    get => errorMessage;
    private set => SetProperty(ref errorMessage, value);
  }

  private bool SetDraftProperty<T>(ref T field, T value)
  {
    if (!SetProperty(ref field, value)) return false;
    OnValidationChanged();
    return true;
  }
}
