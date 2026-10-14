using Microsoft.Maui.Controls;
using Voucha.Client.Core;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Topics;

namespace Voucha.Client.App.Pages;

public sealed record TopicPageRow(TopicRow Topic, ImageSource? LogoImageSource)
{
  public string Id => Topic.Id;

  public string Name => Topic.Name;

  public string Slug => Topic.Slug;

  public string LocalizedTopicType => Topic.LocalizedTopicType;

  public string? LocalizedProvenanceLabel => Topic.LocalizedProvenanceLabel;

  public bool HasProvenance => Topic.HasProvenance;

  public string? StaffChannelLabel => Topic.StaffChannelLabel;

  public bool HasStaffChannel => Topic.HasStaffChannel;

  public string? Description => Topic.Description;

  public int? VoteCountUp => Topic.VoteCountUp;

  public int? VoteCountDown => Topic.VoteCountDown;

  public ElectionVoteChoice? CurrentVoteChoice => Topic.CurrentVoteChoice;

  public bool HasVoteCounts => Topic.HasVoteCounts;

  public bool HasLogo => LogoImageSource is not null;

  public static TopicPageRow Create(TopicRow topic, AppConfig config)
  {
    var placement = topic.LogoImagePlacement;
    var url = placement is null
        ? null
        : config.ImageUrlForPlacement(placement.PlacementId, placement.PlacementRevision, placement.ImageId, 96);
    return new TopicPageRow(topic, url is null ? null : ImageSource.FromUri(url));
  }
}
