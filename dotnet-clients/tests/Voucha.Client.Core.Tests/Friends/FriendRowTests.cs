using Voucha.Client.Core.Friends;
using Xunit;

namespace Voucha.Client.Core.Tests.Friends;

public sealed class FriendRowTests
{
  [Fact]
  public void ComputedPropertiesPreferDisplayNameAndReflectFollowState()
  {
    var row = new FriendRow(
        "friend-1",
        "Friendly User",
        "friend-title",
        "friend",
        "image-1",
        IsFollowing: true,
        IsSelf: false);

    Assert.True(row.CanToggleFollow);
    Assert.Equal("Friendly User", row.PrimaryLabel);
    Assert.Equal("@friend", row.SecondaryLabel);
    Assert.Equal("F", row.AvatarInitial);
    Assert.Equal("Unfollow", row.FollowActionLabel);
  }

  [Fact]
  public void ComputedPropertiesFallBackAndSuppressSelfToggle()
  {
    var row = new FriendRow(
        "friend-2",
        DisplayName: null,
        Title: null,
        Username: null,
        ProfileImageId: null,
        IsFollowing: false,
        IsSelf: true);

    Assert.False(row.CanToggleFollow);
    Assert.Equal("friend-2", row.PrimaryLabel);
    Assert.Equal(string.Empty, row.SecondaryLabel);
    Assert.Equal("F", row.AvatarInitial);
    Assert.Equal("Follow", row.FollowActionLabel);
  }

  [Fact]
  public void AvatarInitialIsSafeWhenPrimaryLabelIsEmpty()
  {
    var row = new FriendRow(
        string.Empty,
        DisplayName: string.Empty,
        Title: null,
        Username: null,
        ProfileImageId: null,
        IsFollowing: false,
        IsSelf: false);

    Assert.Equal(string.Empty, row.PrimaryLabel);
    Assert.Equal(string.Empty, row.AvatarInitial);
  }

  [Fact]
  public void ComputedPropertiesIgnoreWhitespaceLabels()
  {
    var row = new FriendRow(
        "friend-4",
        DisplayName: "   ",
        Title: "  Friend Title  ",
        Username: "  friend  ",
        ProfileImageId: null,
        IsFollowing: false,
        IsSelf: false);

    Assert.Equal("Friend Title", row.PrimaryLabel);
    Assert.Equal("@friend", row.SecondaryLabel);
    Assert.Equal("F", row.AvatarInitial);
  }
}
